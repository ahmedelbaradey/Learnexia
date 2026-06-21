using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;

namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// Pure static calibration-rule engine for P5-07.
///
/// CONTRACT:
/// - Pure static — no database, no DI, no I/O. The caller (<c>CalibrationService</c>)
///   pre-fetches all inputs and passes them here.
/// - ONE plain static class. No Strategy / Visitor / Pipeline / State patterns (CLAUDE rule 8).
/// - Thread-safe: no shared mutable state.
/// - Deterministic: same inputs always produce the same output.
///
/// RULES (P5-07 Execution Plan — LOCKED):
///   BE-2 — Difficulty-divergence proposals:
///     p-value → empirical band:
///       p >= EasyThreshold → Easy
///       p &lt;= HardThreshold → Hard
///       else → Medium
///     Divergence = empirical band != authored Difficulty AND SampleSize >= MinSamples.
///     On divergence: produce a <see cref="ProposalDecision"/> carrying the proposed band + evidence.
///     NEVER mutate QuizQuestion.Difficulty here.
///
///   BE-3 — AI-question flagging:
///     GeneratedBy != Curated (i.e. AI) AND SampleSize >= MinSamples
///     AND (p >= AiFlagHighPValue OR p &lt;= AiFlagLowPValue)
///     → produce a <see cref="FlagDecision"/> carrying the stable reason code.
///     Reversible by admin Clear-flag (BE-5). Does NOT touch Difficulty/QuestionText/Options/CorrectAnswer.
///
/// PATTERN CAUTION (rule 8):
///   All logic lives as static methods in this one class. Do NOT refactor into strategy classes
///   or separate abstractions without lead approval.
/// </summary>
public static class CalibrationEngine
{
    // ── Stable reason codes (OQ-6 decision: store code, localize at read) ─────────────────────────
    // These codes are persisted in the DB and mapped to localized strings in BE-5 DTOs.
    // Any change to these codes is a breaking change for existing rows — only append, never rename.

    /// <summary>Empirical difficulty band diverges from the authored band (standard proposal reason).</summary>
    public const string ReasonDifficultyDiverged = "DIFFICULTY_DIVERGENCE";

    /// <summary>AI-generated question has an extremely high p-value (trivially easy — nearly everyone correct).</summary>
    public const string ReasonAiPValueExtremeHigh = "AI_PVALUE_EXTREME_HIGH";

    /// <summary>AI-generated question has an extremely low p-value (suspiciously hard — very few correct).</summary>
    public const string ReasonAiPValueExtremeLow = "AI_PVALUE_EXTREME_LOW";

    // ── BE-2: Difficulty-divergence proposal ──────────────────────────────────────────────────────

    /// <summary>
    /// Maps a p-value to an empirical <see cref="DifficultyLevel"/> using the config-driven thresholds.
    /// </summary>
    /// <param name="pValue">Fraction correct (0.0–1.0).</param>
    /// <param name="options">Calibration config (EasyThreshold / HardThreshold).</param>
    /// <returns>The empirical difficulty band.</returns>
    public static DifficultyLevel ComputeEmpiricalBand(double pValue, CalibrationOptions options)
    {
        if (pValue >= options.EasyThreshold)
            return DifficultyLevel.Easy;

        if (pValue <= options.HardThreshold)
            return DifficultyLevel.Hard;

        return DifficultyLevel.Medium;
    }

    /// <summary>
    /// Evaluates whether a divergence proposal should be raised for the given question stats.
    ///
    /// Returns a <see cref="ProposalDecision"/> describing whether to upsert/insert a Pending proposal.
    /// Never returns a decision that mutates <c>QuizQuestion.Difficulty</c>.
    /// </summary>
    /// <param name="stats">The aggregated stats row for this question.</param>
    /// <param name="authoredDifficulty">The current authored band on the question.</param>
    /// <param name="options">Calibration config.</param>
    /// <returns>
    /// A decision: <c>ShouldPropose = true</c> when SampleSize >= MinSamples and empirical band != authored.
    /// </returns>
    public static ProposalDecision EvaluateDivergence(
        QuestionDifficultyStats stats,
        DifficultyLevel authoredDifficulty,
        CalibrationOptions options)
    {
        if (stats.SampleSize < options.MinSamples)
            return new ProposalDecision(ShouldPropose: false, EmpiricalBand: authoredDifficulty, ReasonCode: string.Empty);

        var empiricalBand = ComputeEmpiricalBand(stats.PValue, options);

        var diverges = empiricalBand != authoredDifficulty;

        return new ProposalDecision(
            ShouldPropose: diverges,
            EmpiricalBand: empiricalBand,
            ReasonCode: diverges ? ReasonDifficultyDiverged : string.Empty);
    }

    // ── BE-3: AI-question quality flag ────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates whether an AI-generated question should be flagged for review based on extreme p-value.
    ///
    /// Only flags questions where <c>GeneratedBy != Curated</c> (i.e. AI-generated).
    /// Does NOT flag curated questions regardless of p-value.
    /// Reversible via the admin Clear-flag endpoint (BE-5).
    /// </summary>
    /// <param name="stats">The aggregated stats row for this question.</param>
    /// <param name="generatedBy">Provenance of the question.</param>
    /// <param name="options">Calibration config (AiFlagHighPValue / AiFlagLowPValue / MinSamples).</param>
    /// <returns>
    /// A decision: <c>ShouldFlag = true</c> when GeneratedBy == AI, SampleSize >= MinSamples,
    /// and p-value is outside the [AiFlagLowPValue, AiFlagHighPValue] bounds.
    /// </returns>
    public static FlagDecision EvaluateAiFlag(
        QuestionDifficultyStats stats,
        GeneratedBy generatedBy,
        CalibrationOptions options)
    {
        // Only flag AI-generated questions (Curated questions are reviewed by human authors).
        if (generatedBy == GeneratedBy.Curated)
            return new FlagDecision(ShouldFlag: false, ReasonCode: string.Empty);

        // Not enough data yet — gate on MinSamples.
        if (stats.SampleSize < options.MinSamples)
            return new FlagDecision(ShouldFlag: false, ReasonCode: string.Empty);

        if (stats.PValue >= options.AiFlagHighPValue)
            return new FlagDecision(ShouldFlag: true, ReasonCode: ReasonAiPValueExtremeHigh);

        if (stats.PValue <= options.AiFlagLowPValue)
            return new FlagDecision(ShouldFlag: true, ReasonCode: ReasonAiPValueExtremeLow);

        return new FlagDecision(ShouldFlag: false, ReasonCode: string.Empty);
    }
}

/// <summary>
/// Result of <see cref="CalibrationEngine.EvaluateDivergence"/>.
/// </summary>
/// <param name="ShouldPropose">Whether a Pending proposal should be upserted.</param>
/// <param name="EmpiricalBand">The empirical band the engine computed (valid when ShouldPropose = true).</param>
/// <param name="ReasonCode">Stable reason code to persist (empty string when ShouldPropose = false).</param>
public sealed record ProposalDecision(bool ShouldPropose, DifficultyLevel EmpiricalBand, string ReasonCode);

/// <summary>
/// Result of <see cref="CalibrationEngine.EvaluateAiFlag"/>.
/// </summary>
/// <param name="ShouldFlag">Whether the question should be flagged for review.</param>
/// <param name="ReasonCode">Stable reason code to persist on the question (empty string when ShouldFlag = false).</param>
public sealed record FlagDecision(bool ShouldFlag, string ReasonCode);

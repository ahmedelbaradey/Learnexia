using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static domain service for deriving the four AC1 behavioral attributes from pre-fetched
/// learning signals (P3-13).
///
/// CONTRACT:
/// - Pure static — no database, no DI, no side effects. The caller (StudentProfileService)
///   pre-fetches all signals into <see cref="StudentSignals"/> and resolves options from DI
///   before calling <see cref="Derive"/>.
/// - ONE plain static class with FOUR private derivation methods (plan rule 8 — highest risk
///   in the cluster). No Strategy / Visitor / Pipeline / State patterns. Each derivation is a
///   private method called sequentially from Derive.
/// - Thread-safe: no shared mutable state.
/// - Deterministic and reproducible: same inputs always produce the same output (AC2).
/// - Rule-based and explainable: every attribute can be traced back to its inputs (FR-AI-6).
///
/// COLD-START:
///   When <c>signals.TotalAnswers &lt; options.ColdStartDataPointThreshold</c>, returns neutral
///   defaults: empty affinity/clusters, null attention span, Standard style, DataPointCount=0.
///
/// PATTERN CAUTION (rule 8):
///   The four derivations live as four private methods in this one class. If a future maintainer
///   feels the urge to extract them into separate classes or introduce an abstraction pattern,
///   they MUST stop and ask the lead before doing so.
/// </summary>
public static class StudentProfileEngine
{
    /// <summary>
    /// Derives the behavioral profile for a student from pre-aggregated signals.
    /// </summary>
    /// <param name="signals">Pre-fetched signal aggregates (assembled by StudentProfileService).</param>
    /// <param name="options">Tunable thresholds (resolved from IOptions&lt;StudentProfileOptions&gt;).</param>
    /// <returns>
    ///     A <see cref="DerivedProfile"/> with four behavioral attributes. On cold-start
    ///     (TotalAnswers &lt; ColdStartDataPointThreshold) returns neutral defaults.
    /// </returns>
    public static DerivedProfile Derive(StudentSignals signals, StudentProfileOptions options)
    {
        // ── Cold-start guard ─────────────────────────────────────────────────────────────────────────
        // Insufficient data → return neutral defaults; do not adapt yet. DataPointCount = 0 signals
        // to consumers (P3-03, P3-08) that this profile is not yet reliable.
        if (signals.TotalAnswers < options.ColdStartDataPointThreshold)
        {
            return new DerivedProfile(
                QuestionTypeAffinity:     new Dictionary<string, double>(),
                RecurringErrorSkillIds:   Array.Empty<int>(),
                AttentionSpanMinutes:     null,
                PreferredExplanationStyle: ExplanationStyle.Standard,
                DataPointCount:           0);
        }

        // ── Four derivations (sequential — order matters for ExplanationStyle which uses affinity) ──
        var affinity      = DeriveQuestionTypeAffinity(signals, options);
        var errorClusters = DeriveRecurringErrorClusters(signals, options);
        var attentionSpan = DeriveAttentionSpan(signals, options);
        var style         = DeriveExplanationStyle(signals, affinity, options);

        return new DerivedProfile(
            QuestionTypeAffinity:     affinity,
            RecurringErrorSkillIds:   errorClusters,
            AttentionSpanMinutes:     attentionSpan,
            PreferredExplanationStyle: style,
            DataPointCount:           signals.TotalAnswers);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // PRIVATE DERIVATION METHODS — four separate derivations, one plain method each.
    // Do NOT refactor these into strategy classes, plugins, or registries without lead approval.
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Derives per-QuestionType affinity from answer aggregates.
    ///
    /// RULE: For each QuestionType, compute success rate = correct / total.
    /// A type is included in the affinity map only when:
    ///   (a) it has at least <c>options.MinSamplePerQuestionType</c> answers (min-sample guard), AND
    ///   (b) its success rate exceeds the student's overall accuracy
    ///       (affinity = meaningfully above average, not just above zero).
    ///
    /// OUTPUT: Dictionary with QuestionType.ToString() keys and [0.0..1.0] success-rate values,
    /// restricted to "above-overall" types that passed the min-sample guard. Empty when no types
    /// qualify.
    /// </summary>
    private static IReadOnlyDictionary<string, double> DeriveQuestionTypeAffinity(
        StudentSignals signals,
        StudentProfileOptions options)
    {
        var affinity = new Dictionary<string, double>();
        var overall  = signals.OverallAccuracy;

        foreach (var (type, correct, total) in signals.AnswersByType)
        {
            // Min-sample guard: ignore types with too few answers (unreliable rate).
            if (total < options.MinSamplePerQuestionType)
                continue;

            var successRate = (double)correct / total;

            // Affinity = above-overall-rate with min-sample satisfied.
            if (successRate > overall)
                affinity[type.ToString()] = Math.Round(successRate, 4);
        }

        return affinity;
    }

    /// <summary>
    /// Identifies skills with recurring wrong-answer patterns.
    ///
    /// RULE: A skill is a "recurring error cluster" when the student has recorded
    /// <c>wrongCount &gt;= options.RecurringErrorThreshold</c> incorrect answers for that skill
    /// across all attempts. The threshold is configurable — default 3.
    ///
    /// OUTPUT: List of SkillIds that qualify. Empty when no skills meet the threshold.
    /// </summary>
    private static IReadOnlyList<int> DeriveRecurringErrorClusters(
        StudentSignals signals,
        StudentProfileOptions options)
    {
        var clusters = new List<int>();

        foreach (var (skillId, wrongCount) in signals.SkillErrorCounts)
        {
            if (wrongCount >= options.RecurringErrorThreshold)
                clusters.Add(skillId);
        }

        return clusters;
    }

    /// <summary>
    /// Derives the attention-span proxy from accuracy-over-time buckets.
    ///
    /// RULE: Scan the <c>SessionAccuracyBuckets</c> (ordered by MinuteBucket ascending) for the
    /// first bucket where accuracy drops more than <c>options.AccuracyDropThreshold</c> below
    /// the session-start accuracy (bucket 0 or the first available bucket). If such a bucket is
    /// found, return its MinuteBucket as <c>AttentionSpanMinutes</c>; otherwise return null.
    ///
    /// NULL = insufficient data (fewer than 2 buckets) or no drop detected within the window.
    ///
    /// v1 proxy from Attempt/StudentAnswer timestamps; enrich from P5-03 (not yet built) when
    /// available. P5-03 (session signals) will provide truer cross-attempt session boundaries;
    /// for now this uses the within-attempt elapsed-time proxy assembled by StudentProfileService.
    /// </summary>
    private static int? DeriveAttentionSpan(
        StudentSignals signals,
        StudentProfileOptions options)
    {
        var buckets = signals.SessionAccuracyBuckets;

        // Need at least 2 buckets to detect a drop (a baseline and a later point).
        if (buckets.Count < 2)
            return null;

        var baselineAccuracy = buckets[0].Accuracy;

        for (var i = 1; i < buckets.Count; i++)
        {
            var drop = baselineAccuracy - buckets[i].Accuracy;

            if (drop >= options.AccuracyDropThreshold)
                return buckets[i].MinuteBucket;
        }

        return null;
    }

    /// <summary>
    /// Derives the preferred explanation style from question-type affinity and hint behavior.
    ///
    /// Speculative derivation — confirm ExplanationStyle enum values match P3-03 prompt builder
    /// before enabling this feature in production. The enum values (Standard/Simplified/Visual/
    /// StepByStep) are provisional pending P3-03 implementation (AI module, not yet built).
    ///
    /// RULE (conservative heuristic — not a measurement):
    ///   - High affinity for Matching + high hint rate on any type → Visual
    ///     (student succeeds at pattern-matching / visual formats and relies on hints for text).
    ///   - High affinity for FillInBlank + low hint rate → StepByStep
    ///     (student succeeds at fill-in-the-blank with minimal prompts → follows structured reasoning).
    ///   - High hint rate across the board with low overall accuracy → Simplified
    ///     (student frequently seeks hints and struggles → simpler explanations may help).
    ///   - All other cases → Standard (default; also returned when affinity map is empty).
    ///
    /// The thresholds used here are hardcoded heuristics (0.5 for "high hint rate",
    /// presence of Matching/FillInBlank in the affinity map). They are intentionally simple
    /// because ExplanationStyle is the weakest / most speculative attribute (brief §Q1).
    /// When P3-03 is built, the consuming prompt builder should treat this value as a hint,
    /// not a hard directive.
    /// </summary>
    private static ExplanationStyle DeriveExplanationStyle(
        StudentSignals signals,
        IReadOnlyDictionary<string, double> affinity,
        StudentProfileOptions options)
    {
        // On empty affinity (insufficient differentiated data), default to Standard.
        if (affinity.Count == 0)
            return ExplanationStyle.Standard;

        // Compute overall hint rate across all types for the "Simplified" signal.
        var totalAnswers = signals.TotalAnswers;
        var totalHints   = signals.HintAnswerCountByType.Sum(h => h.HintCount);
        var overallHintRate = totalAnswers == 0 ? 0.0 : (double)totalHints / totalAnswers;

        // Check per-type hint rates for the stronger heuristics.
        var hintRateByType = signals.HintAnswerCountByType
            .Where(h => h.Total > 0)
            .ToDictionary(h => h.Type.ToString(), h => (double)h.HintCount / h.Total);

        var hasMatchingAffinity    = affinity.ContainsKey(QuestionType.Matching.ToString());
        var hasFillInBlankAffinity = affinity.ContainsKey(QuestionType.FillInBlank.ToString());

        // Heuristic A — Visual: strong Matching affinity + high overall hint rate.
        // Matching/visual format + hint reliance → visual explanation preferred.
        if (hasMatchingAffinity && overallHintRate >= 0.5)
            return ExplanationStyle.Visual;

        // Heuristic B — StepByStep: FillInBlank affinity + low hint rate.
        // Succeeds at fill-in (procedural) with few hints → follows step-by-step reasoning.
        hintRateByType.TryGetValue(QuestionType.FillInBlank.ToString(), out var fillHintRate);
        if (hasFillInBlankAffinity && fillHintRate < 0.3)
            return ExplanationStyle.StepByStep;

        // Heuristic C — Simplified: high overall hint rate + below-average overall accuracy.
        // Frequent hints + struggles → simplify explanations.
        if (overallHintRate >= 0.5 && signals.OverallAccuracy < 0.5)
            return ExplanationStyle.Simplified;

        // Default — Standard: no strong pattern detected.
        return ExplanationStyle.Standard;
    }
}

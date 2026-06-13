using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static domain service for computing a per-skill target difficulty (AC1–AC3, P3-08).
///
/// CONTRACT:
/// - Pure static — no database, no DI. Caller pre-fetches all signals into <see cref="AdaptivitySignals"/>
///   and resolves options from DI before calling this method.
/// - <b>No Strategy/State pattern.</b> A plain pure function + options + enum (CLAUDE rule 8).
/// - Thread-safe: no shared mutable state.
/// - Deterministic and reproducible: same inputs always produce the same output (AC2).
///
/// ALGORITHM (Q1 — approved weighted-score band):
///   <c>score = w_acc·acc + w_time·timeNorm + w_hint·(1−hintRate) + w_retry·(1−retryNorm)</c>
///   Band: score ≥ HighBand → Hard; score ≤ LowBand → Easy; else Medium.
///   Monotonic by construction: increasing accuracy/speed/fewer-hints/fewer-retries ↑ score (AC3).
///
/// TIME NORMALIZATION (Q3):
///   When <c>signals.AvgTimeSeconds ≤ 0</c>, the time component is zeroed (graceful degradation).
///   A reference baseline of 30 s is used: faster answers yield a higher time score, capped at 1.
///
/// COLD-START (Q4):
///   When <c>signals.IsDefault == true</c> (no answers recorded), returns (Medium, IsDefault=true, Score=0).
///
/// P3-13 OPTIONAL FATIGUE HOOK:
///   <c>AdaptivityOptions.FatigueSignalWeight</c> defaults 0. When non-zero it is subtracted from the
///   score before banding. This slot is reserved for P3-13 — the engine does not hard-depend on it.
/// </summary>
public static class AdaptivityEngine
{
    /// <summary>Expected answer time in seconds used as the time-normalization baseline (Q3).</summary>
    private const double DefaultExpectedTimeSeconds = 30.0;

    /// <summary>
    /// Computes the target difficulty for a student on a skill from four pre-fetched signals.
    /// </summary>
    /// <param name="signals">Pre-aggregated input vector (fetched by <c>AdaptivityService</c>).</param>
    /// <param name="options">Tunable weights and bands (resolved from <c>IOptions&lt;AdaptivityOptions&gt;</c>).</param>
    /// <returns>
    ///     An <see cref="AdaptivityDecision"/> with the recommended <see cref="DifficultyLevel"/>,
    ///     an <c>IsDefault</c> flag (true = cold-start, not computed), and the raw <c>Score</c>.
    /// </returns>
    public static AdaptivityDecision DecideDifficulty(AdaptivitySignals signals, AdaptivityOptions options)
    {
        // ── Cold-start guard (Q4) ─────────────────────────────────────────────────────────────────────
        // No answers recorded → return Medium as the neutral starting difficulty.
        if (signals.IsDefault)
            return new AdaptivityDecision(DifficultyLevel.Medium, IsDefault: true, Score: 0.0);

        // ── Signal normalization ──────────────────────────────────────────────────────────────────────

        // Accuracy component: signals.AccuracyPct is already in [0,1]. Clamp defensively.
        var acc = Math.Clamp(signals.AccuracyPct, 0.0, 1.0);

        // Time component (Q3): normalize against expected baseline (faster = higher score).
        // AvgTimeSeconds <= 0 means unavailable → degrade gracefully by contributing 0.
        double timeNorm;
        if (signals.AvgTimeSeconds <= 0.0)
        {
            timeNorm = 0.0;
        }
        else
        {
            // Faster than expected → timeNorm > 1 before clamping; slower → timeNorm < 1.
            // Cap at [0, 1] so an outlier fast answer doesn't dominate.
            timeNorm = Math.Clamp(DefaultExpectedTimeSeconds / signals.AvgTimeSeconds, 0.0, 1.0);
        }

        // Hint component: fewer hints = higher score. HintRate is in [0,1].
        var hintComponent = 1.0 - Math.Clamp(signals.HintRate, 0.0, 1.0);

        // Retry component: fewer retries = higher score.
        // retryNorm = RetryCount / ExpectedAttempts, capped at 1 so extra retries degrade to 0 contribution.
        var expectedAttempts = Math.Max(options.ExpectedAttempts, 1);
        var retryNorm = Math.Clamp((double)signals.RetryCount / expectedAttempts, 0.0, 1.0);
        var retryComponent = 1.0 - retryNorm;

        // ── Weighted score ────────────────────────────────────────────────────────────────────────────
        var score = options.WeightAccuracy * acc
                  + options.WeightTime    * timeNorm
                  + options.WeightHint    * hintComponent
                  + options.WeightRetry   * retryComponent;

        // ── P3-13 optional fatigue hook ───────────────────────────────────────────────────────────────
        // FatigueSignalWeight defaults 0 — no effect until P3-13 feeds in a real value.
        // When non-zero it subtracts from the score (fatigue lowers the performance band).
        if (options.FatigueSignalWeight > 0.0)
            score -= Math.Clamp(options.FatigueSignalWeight, 0.0, 1.0);

        // Clamp final score to [0, 1] to absorb any floating-point edge cases.
        score = Math.Clamp(score, 0.0, 1.0);

        // ── Band decision ─────────────────────────────────────────────────────────────────────────────
        var difficulty = score >= options.HighBand
            ? DifficultyLevel.Hard
            : score <= options.LowBand
                ? DifficultyLevel.Easy
                : DifficultyLevel.Medium;

        return new AdaptivityDecision(difficulty, IsDefault: false, Score: score);
    }
}

using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Pure static domain service for the spaced-repetition scheduler (P3-10, AC1, AC4).
///
/// CONTRACT:
/// - Pure static — no database, no DI. Caller pre-fetches all required data.
/// - <b>No SM-2 / EaseFactor.</b> Fixed expanding ladder only. SM-2 is explicitly deferred;
///   do NOT add EaseFactor or a pluggable algorithm without lead approval (CLAUDE rule 8).
/// - <b>No Strategy pattern</b> — plain static functions + options (CLAUDE rule 8).
/// - Thread-safe: no shared mutable state.
/// - Deterministic and reproducible: same inputs always produce the same output.
/// - All <see cref="DateTime"/> arguments must be UTC-kind. Store in <c>timestamptz</c>.
///
/// Mirrors <see cref="AdaptivityEngine"/> in its pure-static shape.
/// </summary>
public static class SpacedRepetitionEngine
{
    /// <summary>
    /// Computes the next review schedule after a completed review attempt.
    ///
    /// Algorithm (Q2 approved — fixed expanding ladder):
    /// <list type="bullet">
    ///   <item>If <paramref name="improved"/> is <c>true</c>: advance the ladder index by 1,
    ///         clamped at the last index; return the corresponding <c>IntervalLadderDays</c> value.</item>
    ///   <item>If <paramref name="improved"/> is <c>false</c>: reset to index 0 (shortest interval).</item>
    /// </list>
    ///
    /// The caller is responsible for computing <c>NextReviewDueAt = UtcNow + TimeSpan.FromDays(nextIntervalDays)</c>.
    /// </summary>
    /// <param name="repetitionNumber">
    ///     Current repetition counter (0-based index into <paramref name="options"/>.IntervalLadderDays).
    ///     Must be ≥ 0; values beyond the ladder length are treated as the last index.
    /// </param>
    /// <param name="improved">
    ///     <c>true</c> when the student's mastery did NOT regress to <c>NeedsReview</c>;
    ///     <c>false</c> when mastery dropped (failure — reset the ladder).
    /// </param>
    /// <param name="options">Engine configuration (ladder, cron). Resolved from DI by the caller.</param>
    /// <returns>
    ///     A tuple of <c>(nextIntervalDays, nextRepetitionNumber)</c>:
    ///     <c>nextIntervalDays</c> is the spacing to add to UtcNow;
    ///     <c>nextRepetitionNumber</c> is the new 0-based ladder index.
    /// </returns>
    public static (int nextIntervalDays, int nextRepetitionNumber) ComputeNextReview(
        int repetitionNumber,
        bool improved,
        SpacedRepetitionOptions options)
    {
        var ladder = options.IntervalLadderDays;

        if (ladder is null || ladder.Length == 0)
        {
            // Defensive: degenerate config — return no-op (1 day, same repetition).
            return (1, repetitionNumber);
        }

        if (!improved)
        {
            // Failure — reset to the first (shortest) interval.
            return (ladder[0], 0);
        }

        // Success — advance one step, clamped at the last index.
        var currentIndex = Math.Clamp(repetitionNumber, 0, ladder.Length - 1);
        var nextIndex = Math.Min(currentIndex + 1, ladder.Length - 1);
        return (ladder[nextIndex], nextIndex);
    }

    /// <summary>
    /// Determines whether a skill is due for spaced-repetition review at the given UTC instant.
    ///
    /// Due rule (AC1, Q2 approved — mastered-but-stale half ensures forgetting is detected):
    /// <list type="bullet">
    ///   <item><c>NeedsReview</c> — ALWAYS due (skill needs immediate remediation).</item>
    ///   <item><c>Mastered</c> AND <paramref name="nextReviewDueAt"/> has a value AND
    ///         <paramref name="utcNow"/> ≥ <paramref name="nextReviewDueAt"/> — due (forgetting check).</item>
    ///   <item>All other states — not due.</item>
    /// </list>
    ///
    /// All <see cref="DateTime"/> arguments MUST be UTC (Kind = Utc). The method does NOT
    /// normalise Kind — callers are responsible for UTC discipline (HANDOFF P2-08 quirk).
    /// </summary>
    /// <param name="status">Current mastery status of the skill.</param>
    /// <param name="nextReviewDueAt">
    ///     Scheduled due date/time (UTC). <c>null</c> = not yet scheduled
    ///     (mastered but never reviewed with SR → not due until the sweep job sets the first schedule).
    /// </param>
    /// <param name="utcNow">Current UTC time (inject via <c>DateTime.UtcNow</c> or a test stub).</param>
    /// <returns><c>true</c> if the skill should be included in the due-review list.</returns>
    public static bool IsDue(MasteryStatus status, DateTime? nextReviewDueAt, DateTime utcNow)
    {
        if (status == MasteryStatus.NeedsReview)
            return true;

        if (status == MasteryStatus.Mastered
            && nextReviewDueAt.HasValue
            && utcNow >= nextReviewDueAt.Value)
            return true;

        return false;
    }
}

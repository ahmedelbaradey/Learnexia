namespace Learnexia.Modules.Learning.Domain.Services;

/// <summary>
/// Configuration for the Spaced-Repetition Engine (P3-10). Bound from
/// <c>appsettings.json:SpacedRepetition:Engine</c>.
///
/// Placed in the Domain layer (alongside the pure engine it configures) so that
/// <see cref="SpacedRepetitionEngine"/> can reference it without violating the
/// Application → Domain dependency direction. Mirrors <see cref="AdaptivityOptions"/>.
///
/// Algorithm choice (Q2, P3-10 brief — approved): deterministic fixed expanding ladder.
/// Intervals (days): RepetitionNumber-indexed array. On a successful review advance to the
/// next index (clamped at max); on failure reset to index 0. Idempotent, explainable, tunable.
///
/// SM-2 ease factor model is explicitly deferred — do NOT add EaseFactor without lead approval.
/// </summary>
public sealed class SpacedRepetitionOptions
{
    /// <summary>Config section key — <c>"SpacedRepetition:Engine"</c>.</summary>
    public const string SectionName = "SpacedRepetition:Engine";

    /// <summary>
    /// The expanding interval ladder in days. RepetitionNumber indexes into this array.
    /// Default: <c>[1, 3, 7, 14, 30]</c>.
    ///
    /// Advance one index on success (clamped at max); reset to index 0 on failure.
    /// </summary>
    public int[] IntervalLadderDays { get; set; } = [1, 3, 7, 14, 30];

    /// <summary>
    /// Interval (days) used by the sweep job when a skill is in <c>NeedsReview</c> status
    /// and no review has yet been scheduled. Default: 1 (surface immediately).
    /// </summary>
    public int NeedsReviewIntervalDays { get; set; } = 1;

    /// <summary>
    /// Hangfire cron expression for the daily sweep job. Default: <c>"0 0 * * *"</c> (midnight UTC).
    /// </summary>
    public string JobCronExpression { get; set; } = "0 0 * * *";
}

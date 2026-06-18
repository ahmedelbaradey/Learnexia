namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeeklyKpis;

/// <summary>
/// E3 response DTO — "Overview this week" KPI strip with week-over-week deltas.
/// Covers all fields from <c>OverviewKpiStub</c> in the FE gold contract.
///
/// Windows:
///   This week  = rolling 7 days ending now (UTC).
///   Prior week = the 7-day window immediately before that (days 8–14 ago).
///   An empty first week returns all zeros — never an error (AC3).
/// </summary>
public sealed class WeeklyKpisDto
{
    // ── This-week values ──

    /// <summary>Minutes of active learning this week (Σ DurationSeconds / 60).</summary>
    public int TimeLearningMinutes { get; init; }

    /// <summary>XP earned this week.</summary>
    public int XpEarned { get; init; }

    /// <summary>Distinct lessons completed this week.</summary>
    public int LessonsDone { get; init; }

    /// <summary>Current streak at the time of the request.</summary>
    public int StreakDays { get; init; }

    // ── Week-over-week deltas ──

    /// <summary>
    /// Change in time-learning minutes vs the prior 7-day window.
    /// Positive = improved, negative = declined, zero = same or first week.
    /// </summary>
    public int TimeLearningDeltaMinutes { get; init; }

    /// <summary>
    /// XP earned this week minus XP earned the prior week.
    /// </summary>
    public int XpDelta { get; init; }

    /// <summary>
    /// Lessons completed this week minus lessons completed the prior week.
    /// </summary>
    public int LessonsDelta { get; init; }

    /// <summary>
    /// Streak change (current streak minus streak value at end of prior window).
    /// Since streak is a live value, we compute delta as currentStreak − priorWeekStreak
    /// where priorWeekStreak ≈ XpTimeSeries profile snapshot from 7 days ago (best effort).
    /// For MVP: delta = 0 (streak delta is informational, not blocking AC6).
    /// </summary>
    public int StreakDelta { get; init; }
}

namespace Learnexia.Shared.Contracts.Analytics;

/// <summary>
/// Cross-module platform-aggregate read seam: Analytics implements, façade consumers inject.
/// Returns a cohesive set of platform-wide session and retention statistics over a UTC time window.
///
/// <para>Registered in <c>AddAnalyticsInfrastructure()</c> as Scoped.</para>
///
/// <para>P5-03 / P7-10 analytics dashboard. Data is real going-forward: the
/// <c>analytics.ActivityEvents</c> table starts empty and fills as integration events fire.
/// The KPI dashboard will show zeroed session/retention values until events accrue — this is
/// the correct, honest v1 behaviour (not an N/A placeholder).</para>
///
/// <para>Module isolation: the Identity façade injects this interface from <c>Shared.Contracts</c>
/// — it never references <c>Analytics.*</c> projects directly. The adapter implementing this
/// interface lives in <c>Analytics.Infrastructure</c>.</para>
///
/// <para>All results are sentinel-safe: an empty window or an empty events table returns a zeroed
/// <see cref="PlatformAnalyticsStats"/> — never <c>null</c>, never throws.</para>
/// </summary>
public interface IPlatformAnalyticsQuery
{
    /// <summary>
    /// Returns platform-wide session and retention statistics over
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PlatformAnalyticsStats"/> value — never <c>null</c>.
    /// Returns a zeroed instance when no events exist in the window.
    /// </returns>
    Task<PlatformAnalyticsStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Platform-wide session and retention statistics over a UTC time window.
/// Produced by <see cref="IPlatformAnalyticsQuery"/>.
///
/// <para>Data source: <c>analytics.ActivityEvents</c> (filled going-forward as integration
/// events fire). All fields are zeroed when no events exist in the window — sentinel-safe.</para>
/// </summary>
/// <param name="DistinctActiveStudents">
/// Count of distinct <c>StudentId</c> values with at least one event in the window.
/// True activity-based DAU/WAU/MAU (Analytics-derived). Distinct from
/// <c>PlatformLearningStats.DistinctActiveStudents</c> which is an attempt-completion proxy.
/// </param>
/// <param name="TotalSessions">
/// Total session count across all active students in the window. A session is a contiguous
/// run of a student's events where consecutive gaps are &lt; the configured
/// <c>SessionGapMinutes</c> (default 30). Zero when no events exist.
/// </param>
/// <param name="AvgSessionDurationSeconds">
/// Mean session duration in seconds across all sessions in the window.
/// A single-event session has duration = 0. Zero when no sessions exist (sentinel-safe).
/// </param>
/// <param name="AvgActiveDaysPerStudent">
/// Average count of distinct UTC calendar days on which each active student had at least one
/// event. Foundation for D1/D7/D30 cohort retention curves. Zero when no active students.
/// </param>
/// <param name="ReturningStudentRate">
/// Fraction of distinct active students who were active on ≥ 2 distinct UTC calendar days.
/// Range [0.0, 1.0]. Zero when no active students exist (sentinel-safe).
/// </param>
public record PlatformAnalyticsStats(
    int    DistinctActiveStudents,
    int    TotalSessions,
    double AvgSessionDurationSeconds,
    double AvgActiveDaysPerStudent,
    double ReturningStudentRate);

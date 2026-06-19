namespace Learnexia.Modules.Analytics.Application.Abstractions;

/// <summary>
/// Read-time derivation service over the <c>analytics.ActivityEvents</c> table.
/// Computes session/retention/DAU metrics for a UTC time window without materialising a
/// persisted <c>Session</c> table (v1 inactivity-window model).
///
/// <para>Option C: the implementation lives in <c>Analytics.Infrastructure</c> and has direct
/// access to <c>AnalyticsDbContext</c>; the Application layer only references this interface.</para>
///
/// <para>All results are sentinel-safe: an empty window or a table with no rows in the window
/// returns a zeroed <see cref="ActivitySessionStats"/> — never <c>null</c>, never throws.</para>
///
/// <para>v1 scalability note: derivation is read-time (in-memory over a windowed
/// <c>AsNoTracking</c> slice). Acceptable for admin-only low-traffic dashboards at current
/// event volumes. Follow-up: materialise daily session/active-day rollups if event volume
/// makes read-time derivation slow (documented follow-up, same as the Learning adapter).</para>
/// </summary>
public interface IActivitySessionService
{
    /// <summary>
    /// Computes activity and session/retention statistics over
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="ActivitySessionStats"/> value — never <c>null</c>.
    /// Returns a zeroed instance when no events exist in the window.
    /// </returns>
    Task<ActivitySessionStats> GetStatsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Session and retention statistics derived read-time from <c>analytics.ActivityEvents</c>
/// over a UTC time window. Produced by <see cref="IActivitySessionService"/>.
/// </summary>
/// <param name="DistinctActiveStudents">
/// Count of distinct <c>StudentId</c> values with at least one event in the window.
/// This is the true DAU/WAU/MAU metric anchored on real activity events
/// (not the attempt-completion proxy from Learning).
/// </param>
/// <param name="TotalSessions">
/// Total session count across all active students. A session is a contiguous run of a
/// student's events where consecutive gaps are &lt; <c>SessionGapMinutes</c> config.
/// A student with a single event in the window contributes one session.
/// </param>
/// <param name="AvgSessionDurationSeconds">
/// Mean session duration in seconds across all sessions in the window.
/// A single-event session has duration = 0.
/// Zero when no sessions exist (sentinel-safe).
/// </param>
/// <param name="SessionsPerActiveStudent">
/// Average number of sessions per distinct active student.
/// Zero when no active students exist (sentinel-safe).
/// </param>
/// <param name="AvgActiveDaysPerStudent">
/// Average count of distinct UTC calendar days on which each active student had at least one
/// event. Foundation for D1/D7/D30 cohort retention curves (further analytics layers).
/// </param>
/// <param name="ReturningStudentCount">
/// Count of students who were active on two or more distinct UTC calendar days in the window.
/// </param>
/// <param name="ReturningStudentRate">
/// Fraction of distinct active students who were active on &gt;= 2 distinct days.
/// Zero when no active students exist (sentinel-safe). Range [0.0, 1.0].
/// </param>
public record ActivitySessionStats(
    int    DistinctActiveStudents,
    int    TotalSessions,
    double AvgSessionDurationSeconds,
    double SessionsPerActiveStudent,
    double AvgActiveDaysPerStudent,
    int    ReturningStudentCount,
    double ReturningStudentRate);

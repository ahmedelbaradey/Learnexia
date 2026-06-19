using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Application.Options;
using Learnexia.Modules.Analytics.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Analytics.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IActivitySessionService"/>.
/// Queries <see cref="AnalyticsDbContext"/> with <c>AsNoTracking</c> + a date window,
/// then derives sessions and retention metrics in-memory.
///
/// <para>Option C: all EF access is here, in Infrastructure. The Application layer
/// only references <see cref="IActivitySessionService"/>.</para>
///
/// <para>Session model (inactivity-window): for each student, events are ordered by
/// <c>OccurredAtUtc</c>. A new session starts whenever the gap between two consecutive
/// events is ≥ <c>SessionGapMinutes</c> (from <see cref="AnalyticsOptions"/>).
/// Session duration = <c>last_event_in_run − first_event_in_run</c> (wall-clock span).
/// A single-event session has duration = 0.</para>
///
/// <para>Retention: distinct active UTC calendar days per student (foundation for D1/D7/D30
/// cohort curves). <c>ReturningStudentCount</c> = students active on ≥ 2 distinct days.</para>
///
/// <para>v1 scalability note: derivation is read-time over the windowed event slice.
/// Acceptable for admin-only low-traffic dashboards. Follow-up: materialise daily
/// session/active-day rollups if event volume makes this slow.</para>
///
/// <para>Sentinel-safe: returns a zeroed <see cref="ActivitySessionStats"/> on an empty
/// window — never null, never throws.</para>
/// </summary>
internal sealed class ActivitySessionService : IActivitySessionService
{
    private readonly AnalyticsDbContext      _db;
    private readonly AnalyticsOptions        _options;
    private readonly ILoggerManager          _logger;

    public ActivitySessionService(
        AnalyticsDbContext             db,
        IOptions<AnalyticsOptions>     options,
        ILoggerManager                 logger)
    {
        _db      = db;
        _options = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public async Task<ActivitySessionStats> GetStatsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Pull the windowed event slice (lightweight projection) ─────────────────────
            // AsNoTracking: read-only. Composite index (StudentId, OccurredAtUtc) drives this.
            var events = await _db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc)
                .Select(e => new { e.StudentId, e.OccurredAtUtc })
                .ToListAsync(ct);

            if (events.Count == 0)
            {
                return new ActivitySessionStats(
                    DistinctActiveStudents:  0,
                    TotalSessions:           0,
                    AvgSessionDurationSeconds: 0.0,
                    SessionsPerActiveStudent: 0.0,
                    AvgActiveDaysPerStudent:  0.0,
                    ReturningStudentCount:   0,
                    ReturningStudentRate:    0.0);
            }

            // ── 2. Group by student, derive sessions + retention ──────────────────────────────
            var gapThreshold = TimeSpan.FromMinutes(_options.SessionGapMinutes);

            var byStudent = events
                .GroupBy(e => e.StudentId)
                .ToList();

            int distinctActiveStudents = byStudent.Count;
            int totalSessions          = 0;
            double totalDurationSeconds = 0.0;
            double totalActiveDays      = 0.0;
            int returningCount          = 0;

            foreach (var studentGroup in byStudent)
            {
                var orderedTimes = studentGroup
                    .Select(e => e.OccurredAtUtc)
                    .OrderBy(t => t)
                    .ToList();

                // ── Session gap-split ─────────────────────────────────────────────────────────
                // Walk the sorted timeline; start a new session when gap >= threshold.
                int    studentSessions   = 1;
                double studentDuration   = 0.0;
                var    sessionStart      = orderedTimes[0];
                var    prevTime          = orderedTimes[0];

                for (int i = 1; i < orderedTimes.Count; i++)
                {
                    var gap = orderedTimes[i] - prevTime;
                    if (gap >= gapThreshold)
                    {
                        // Close the current session
                        studentDuration += (prevTime - sessionStart).TotalSeconds;
                        studentSessions++;
                        sessionStart = orderedTimes[i];
                    }
                    prevTime = orderedTimes[i];
                }
                // Close the last (or only) session
                studentDuration += (prevTime - sessionStart).TotalSeconds;

                totalSessions      += studentSessions;
                totalDurationSeconds += studentDuration;

                // ── Retention: distinct active UTC calendar days ──────────────────────────────
                int activeDays = orderedTimes
                    .Select(t => t.Date)  // UTC date (DateTime.Date strips time, Kind is UTC from DB)
                    .Distinct()
                    .Count();

                totalActiveDays += activeDays;

                if (activeDays >= 2)
                    returningCount++;
            }

            double avgSessionDuration    = totalSessions > 0
                ? totalDurationSeconds / totalSessions
                : 0.0;

            double sessionsPerStudent    = distinctActiveStudents > 0
                ? (double)totalSessions / distinctActiveStudents
                : 0.0;

            double avgActiveDaysPerStudent = distinctActiveStudents > 0
                ? totalActiveDays / distinctActiveStudents
                : 0.0;

            double returningRate = distinctActiveStudents > 0
                ? (double)returningCount / distinctActiveStudents
                : 0.0;

            _logger.LogInfo(
                $"Analytics: ActivitySessionService.GetStatsAsync [{fromUtc:u}, {toUtc:u}) — " +
                $"students={distinctActiveStudents}, sessions={totalSessions}, " +
                $"avgDuration={avgSessionDuration:F1}s, returning={returningCount}.");

            return new ActivitySessionStats(
                DistinctActiveStudents:    distinctActiveStudents,
                TotalSessions:             totalSessions,
                AvgSessionDurationSeconds: avgSessionDuration,
                SessionsPerActiveStudent:  sessionsPerStudent,
                AvgActiveDaysPerStudent:   avgActiveDaysPerStudent,
                ReturningStudentCount:     returningCount,
                ReturningStudentRate:      returningRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Analytics: ActivitySessionService error for window [{fromUtc:u}, {toUtc:u}).");
            // Sentinel-safe: return zeroed on error rather than propagating.
            return new ActivitySessionStats(
                DistinctActiveStudents:    0,
                TotalSessions:             0,
                AvgSessionDurationSeconds: 0.0,
                SessionsPerActiveStudent:  0.0,
                AvgActiveDaysPerStudent:   0.0,
                ReturningStudentCount:     0,
                ReturningStudentRate:      0.0);
        }
    }
}

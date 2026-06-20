using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily at 18:00 UTC (<c>"0 18 * * *"</c>) (P4-09, AC1 + AC2;
/// P9-06 Pass 3).
///
/// <para><b>Purpose:</b> three-pass daily evaluation for re-engagement nudges:
/// <list type="number">
///   <item><b>Streak-at-risk pass:</b> scans <c>StudentXpProfile</c> rows where
///         <c>CurrentStreak &gt; 0</c> and <c>LastActivityDateUtc &lt; today_utc</c> (active streak
///         but no activity yet today). Publishes one <see cref="StreakAtRiskIntegrationEvent"/>
///         per match.</item>
///   <item><b>Daily-mission-reminder pass:</b> scans <c>StudentMission</c> rows where
///         <c>MissionType = Daily</c>, <c>Status IN (NotStarted, InProgress)</c>, and
///         <c>PeriodEndUtc</c> falls within the next <c>MissionReminderLookaheadHours</c>.
///         Publishes one <see cref="DailyMissionReminderIntegrationEvent"/> per distinct student
///         (even if multiple missions are outstanding).</item>
///   <item><b>Weekly-mission-reminder pass (P9-06):</b> scans <c>StudentMission</c> rows where
///         <c>MissionType = Weekly</c>, <c>Status IN (NotStarted, InProgress)</c>, and
///         <c>PeriodEndUtc</c> falls within the next <c>WeeklyMissionReminderLookaheadHours</c>
///         (default 48h). Publishes one <see cref="WeeklyMissionReminderIntegrationEvent"/> per
///         distinct student — most-urgent (earliest <c>PeriodEndUtc</c>) mission wins when a
///         student has multiple weekly missions in window. Note: this daily job intentionally drives
///         a weekly nudge — the consumer-side period-scoped Redis dedupe
///         (<c>TryAcquireTierAsync</c>, key <c>nudge-tier:{studentId}:WEEKLY_CHALLENGE:{periodKey}</c>,
///         TTL ≈ lookahead + margin) ensures at most one weekly reminder per student per weekly
///         period regardless of how many daily runs fall within the 48-hour window.</item>
/// </list></para>
///
/// <para><b>Pagination note:</b> all passes use <c>Take(500)</c> as a page guard for MVP scale.
/// If the count reaches 500, a WARN is logged for operator attention. A cursor-based page loop
/// can replace this when the student count grows past the threshold.</para>
///
/// <para><b>Fail-soft:</b> each individual publish is try/caught. A publish failure does NOT
/// block remaining publishes (ADR 0002 §3).</para>
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; wired in <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class StreakAtRiskJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly IOptions<ReengagementOptions> _options;
    private readonly IOptions<StreakOptions> _streakOptions;
    private readonly ILoggerManager _logger;

    public StreakAtRiskJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock clock,
        IOptions<ReengagementOptions> options,
        IOptions<StreakOptions> streakOptions,
        ILoggerManager logger)
    {
        _scopeFactory  = scopeFactory;
        _clock         = clock;
        _options       = options;
        _streakOptions = streakOptions;
        _logger        = logger;
    }

    /// <summary>
    /// Executes all three passes (streak-at-risk + daily-mission-reminder + weekly-mission-reminder).
    /// Creates a fresh DI scope (Hangfire worker has no HTTP request scope — mandatory).
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var nowUtc  = _clock.UtcNow.ToUniversalTime();
        var todayUtc = StreakDayCalculator.Today(_streakOptions.Value.TimeZoneId, _clock);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db        = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        // ── Pass 1: Streak-at-risk ────────────────────────────────────────────────────────────
        // Students with an active streak that have not recorded any activity today yet.
        var atRiskStudents = await db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.CurrentStreak > 0
                     && (p.LastActivityDateUtc == null || p.LastActivityDateUtc < todayUtc))
            .Select(p => new { p.StudentId, p.CurrentStreak, p.LastActivityDateUtc })
            .Take(500)
            .ToListAsync(ct);

        if (atRiskStudents.Count == 500)
        {
            _logger.LogInfo(
                "P4-09: StreakAtRiskJob — streak-at-risk page guard hit (count=500). " +
                "Consider cursor-based pagination when student count grows.");
        }

        int streakPublished = 0;
        int streakFailed = 0;

        foreach (var s in atRiskStudents)
        {
            try
            {
                await publisher.Publish(new StreakAtRiskIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: nowUtc,
                    StudentId: s.StudentId,
                    CurrentStreak: s.CurrentStreak,
                    LastActivityDateUtc: s.LastActivityDateUtc), ct);

                streakPublished++;
            }
            catch (Exception ex)
            {
                streakFailed++;
                _logger.LogError(ex,
                    $"P4-09: StreakAtRiskJob — streak-at-risk publish failed for studentId={s.StudentId}.");
            }
        }

        // ── Pass 2: Daily-mission-reminder ────────────────────────────────────────────────────
        // Active daily missions expiring within the configured lookahead window.
        var lookaheadEnd = nowUtc.AddHours(_options.Value.MissionReminderLookaheadHours);

        var reminderStudentIds = await db.StudentMissions
            .AsNoTracking()
            .Where(m => m.MissionType == MissionType.Daily
                     && (m.Status == MissionStatus.NotStarted || m.Status == MissionStatus.InProgress)
                     && m.PeriodEndUtc > nowUtc
                     && m.PeriodEndUtc <= lookaheadEnd)
            .Select(m => m.StudentXpProfile.StudentId)
            .Distinct()
            .Take(500)
            .ToListAsync(ct);

        if (reminderStudentIds.Count == 500)
        {
            _logger.LogInfo(
                "P4-09: StreakAtRiskJob — mission-reminder page guard hit (count=500). " +
                "Consider cursor-based pagination when student count grows.");
        }

        int reminderPublished = 0;
        int reminderFailed = 0;

        foreach (var studentId in reminderStudentIds)
        {
            var minutesRemaining = (int)(lookaheadEnd - nowUtc).TotalMinutes;

            try
            {
                await publisher.Publish(new DailyMissionReminderIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: nowUtc,
                    StudentId: studentId,
                    MinutesRemaining: minutesRemaining), ct);

                reminderPublished++;
            }
            catch (Exception ex)
            {
                reminderFailed++;
                _logger.LogError(ex,
                    $"P4-09: StreakAtRiskJob — mission-reminder publish failed for studentId={studentId}.");
            }
        }

        // ── Pass 3: Weekly-mission-reminder (P9-06) ───────────────────────────────────────────
        // Active weekly missions expiring within the configured lookahead window (default 48h).
        // Per student, the most-urgent (earliest PeriodEndUtc) in-window mission is selected so
        // each student receives at most one WeeklyMissionReminderIntegrationEvent per run.
        // Consumer-side period-scoped dedupe (TryAcquireTierAsync keyed on PeriodKey) ensures
        // at most one weekly reminder per student per weekly period across multiple daily runs.
        var weeklyLookaheadEnd = nowUtc.AddHours(_options.Value.WeeklyMissionReminderLookaheadHours);

        // Project per-student the most-urgent (min PeriodEndUtc) in-window weekly mission.
        var weeklyReminderStudents = await db.StudentMissions
            .AsNoTracking()
            .Where(m => m.MissionType == MissionType.Weekly
                     && (m.Status == MissionStatus.NotStarted || m.Status == MissionStatus.InProgress)
                     && m.PeriodEndUtc > nowUtc
                     && m.PeriodEndUtc <= weeklyLookaheadEnd)
            .OrderBy(m => m.StudentXpProfile.StudentId)
            .ThenBy(m => m.PeriodEndUtc)
            .Select(m => new
            {
                StudentId  = m.StudentXpProfile.StudentId,
                m.Progress,
                m.Target,
                m.PeriodEndUtc,
                m.PeriodKey,
            })
            .Take(500)
            .ToListAsync(ct);

        if (weeklyReminderStudents.Count == 500)
        {
            _logger.LogInfo(
                "P9-06: StreakAtRiskJob — weekly-mission-reminder page guard hit (count=500). " +
                "Consider cursor-based pagination when student count grows.");
        }

        // Deduplicate to one row per student (most-urgent already first due to ThenBy PeriodEndUtc).
        var weeklyReminderByStudent = weeklyReminderStudents
            .GroupBy(r => r.StudentId)
            .Select(g => g.First())
            .ToList();

        int weeklyReminderPublished = 0;
        int weeklyReminderFailed = 0;

        foreach (var s in weeklyReminderByStudent)
        {
            var minutesRemaining = (int)(s.PeriodEndUtc - nowUtc).TotalMinutes;

            try
            {
                await publisher.Publish(new WeeklyMissionReminderIntegrationEvent(
                    EventId:          Guid.NewGuid(),
                    OccurredOnUtc:    nowUtc,
                    StudentId:        s.StudentId,
                    Progress:         s.Progress,
                    Target:           s.Target,
                    MinutesRemaining: minutesRemaining,
                    PeriodKey:        s.PeriodKey), ct);

                weeklyReminderPublished++;
            }
            catch (Exception ex)
            {
                weeklyReminderFailed++;
                _logger.LogError(ex,
                    $"P9-06: StreakAtRiskJob — weekly-mission-reminder publish failed for studentId={s.StudentId}.");
            }
        }

        _logger.LogInfo(
            $"P4-09/P9-06: StreakAtRiskJob complete — " +
            $"streakAtRiskCount={atRiskStudents.Count}, streakPublished={streakPublished}, streakFailed={streakFailed}; " +
            $"missionReminderCount={reminderStudentIds.Count}, reminderPublished={reminderPublished}, reminderFailed={reminderFailed}; " +
            $"weeklyReminderCount={weeklyReminderByStudent.Count}, weeklyReminderPublished={weeklyReminderPublished}, weeklyReminderFailed={weeklyReminderFailed}.");
    }
}

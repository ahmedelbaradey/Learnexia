using Learnexia.Modules.Gamification.Application.Configuration;
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
/// Hangfire recurring job — runs Sunday at 12:00 UTC (<c>"0 12 * * 0"</c>) (P4-09, AC1).
///
/// <para><b>Purpose:</b> one-shot lapse win-back nudge. Fires on the exact day a student crosses
/// the lapse threshold (default: 3 days with no activity). Publishes one
/// <see cref="LapseWinBackIntegrationEvent"/> per matching student.</para>
///
/// <para><b>Lapse window (D8 — one-shot semantics):</b>
/// <c>LastActivityDateUtc &gt; today - (LapseDays + 1) AND LastActivityDateUtc &lt;= today - LapseDays</c>.
/// This fires once on the exact lapse transition day. Redis SETNX dedupe at the Notifications side
/// prevents re-fire while still lapsed. Students with &gt; <c>MaxAbsenceDays</c> inactivity are
/// excluded (no more pestering after the cold-churn threshold).</para>
///
/// <para><b>Pagination note:</b> <c>Take(500)</c> page guard for MVP scale. Log WARN if hit.</para>
///
/// <para><b>Fail-soft:</b> each publish is individually try/caught per ADR 0002 §3.</para>
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; wired in <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class LapseWinBackJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly IOptions<ReengagementOptions> _options;
    private readonly IOptions<StreakOptions> _streakOptions;
    private readonly ILoggerManager _logger;

    public LapseWinBackJob(
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
    /// Executes the lapse win-back sweep.
    /// Creates a fresh DI scope (Hangfire worker has no HTTP request scope — mandatory).
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var nowUtc   = _clock.UtcNow.ToUniversalTime();
        var todayUtc = StreakDayCalculator.Today(_streakOptions.Value.TimeZoneId, _clock);

        var opts = _options.Value;

        // One-shot lapse window: fires on the exact day the student crosses the lapse threshold.
        // lapseThreshold  = today - LapseDays       (e.g. today - 3 = 3 days ago)
        // maxAbsence      = today - MaxAbsenceDays  (e.g. today - 30 = 30 days ago)
        // WHERE LastActivityDateUtc > maxAbsence    (not cold-churned)
        //   AND LastActivityDateUtc <= lapseThreshold  (at or past the lapse boundary)
        //   AND LastActivityDateUtc > lapseThreshold.AddDays(-1)  (did NOT lapse before yesterday = exact day)
        var lapseThreshold    = todayUtc.AddDays(-opts.LapseDays);
        var oneDayBeforeStart = lapseThreshold.AddDays(-1);
        var maxAbsence        = todayUtc.AddDays(-opts.MaxAbsenceDays);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db        = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var lapsedStudents = await db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.LastActivityDateUtc != null
                     && p.LastActivityDateUtc <= lapseThreshold
                     && p.LastActivityDateUtc > oneDayBeforeStart
                     && p.LastActivityDateUtc > maxAbsence)
            .Select(p => new { p.StudentId, p.LastActivityDateUtc })
            .Take(500)
            .ToListAsync(ct);

        if (lapsedStudents.Count == 500)
        {
            _logger.LogInfo(
                "P4-09: LapseWinBackJob — page guard hit (count=500). " +
                "Consider cursor-based pagination when student count grows.");
        }

        int published = 0;
        int failed = 0;

        foreach (var s in lapsedStudents)
        {
            // DayNumber arithmetic is offset-safe for DateOnly (no TZ issues).
            var daysSinceLastActivity = todayUtc.DayNumber - s.LastActivityDateUtc!.Value.DayNumber;

            try
            {
                await publisher.Publish(new LapseWinBackIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredOnUtc: nowUtc,
                    StudentId: s.StudentId,
                    DaysSinceLastActivity: daysSinceLastActivity), ct);

                published++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    $"P4-09: LapseWinBackJob — publish failed for studentId={s.StudentId}.");
            }
        }

        _logger.LogInfo(
            $"P4-09: LapseWinBackJob complete — " +
            $"lapsedCount={lapsedStudents.Count}, published={published}, failed={failed}, " +
            $"lapseThreshold={lapseThreshold:yyyy-MM-dd}.");
    }
}

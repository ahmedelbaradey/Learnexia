using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — expires active/not-started <see cref="StudentMission"/> rows
/// whose <c>PeriodEndUtc</c> has passed. Two registrations in <c>GamificationModule.InitializeAsync</c>:
/// <list type="bullet">
///   <item><c>"gamification:mission-rollover-daily"</c> — cron <c>"5 0 * * *"</c> (00:05 UTC daily).</item>
///   <item><c>"gamification:mission-rollover-weekly"</c> — cron <c>"10 0 * * 1"</c> (00:10 UTC Mondays).</item>
/// </list>
///
/// Purpose (AC4, D2): defensive closeout sweep. The lazy instantiation in
/// <see cref="Queries.StudentMissionsQuery"/> is the source of truth for new mission creation;
/// this job closes out yesterday's (or last week's) loose ends so they do NOT pollute the
/// dashboard for active students who would otherwise see stale <c>NotStarted</c> / <c>InProgress</c>
/// rows from the previous period.
///
/// Algorithm: one bulk <see cref="EntityFrameworkQueryableExtensions.ExecuteUpdateAsync{TSource}"/>
/// — sets <c>Status = Expired</c> for all <see cref="StudentMission"/> rows where
/// <c>MissionType = @cadence AND Status IN (NotStarted, InProgress) AND PeriodEndUtc &lt;= now</c>.
///
/// Idempotent: running twice consecutively → second pass touches 0 rows (all already = Expired).
///
/// Note: domain events (<c>MissionExpiredDomainEvent</c>) are NOT raised from the bulk update path
/// (EF <c>ExecuteUpdateAsync</c> bypasses the change tracker). This is intentional for MVP —
/// same precedent as <see cref="StreakSweepJob"/>. If P4-09 re-engagement nudges need an expiry
/// event, replace with a page-loop at that point.
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; the recurring jobs are wired in
/// <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class MissionRolloverJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public MissionRolloverJob(
        IServiceScopeFactory scopeFactory,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _clock        = clock;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the rollover sweep for the given <paramref name="cadence"/>.
    /// Creates a fresh DI scope with its own <see cref="GamificationDbContext"/>
    /// (Hangfire worker has no HTTP request scope — a new scope is mandatory).
    /// </summary>
    public async Task RunAsync(MissionType cadence, CancellationToken ct)
    {
        var now = _clock.UtcNow.ToUniversalTime();

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        // Bulk UPDATE — EF 7+ ExecuteUpdateAsync issues a single SQL statement.
        // Does NOT go through the change tracker → no domain events from this path.
        var rowsAffected = await db.StudentMissions
            .Where(m => m.MissionType == cadence
                     && (m.Status == MissionStatus.NotStarted || m.Status == MissionStatus.InProgress)
                     && m.PeriodEndUtc <= now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Status, MissionStatus.Expired),
                ct);

        _logger.LogInfo(
            $"P4-06: mission-rollover-{cadence} complete — " +
            $"rowsExpired={rowsAffected}, now={now:O}.");
    }
}

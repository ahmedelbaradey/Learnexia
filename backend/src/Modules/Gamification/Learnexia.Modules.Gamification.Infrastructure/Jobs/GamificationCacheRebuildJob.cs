using Learnexia.Modules.Gamification.Application.Caching.Rebuild;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job — runs daily at 03:00 UTC (<c>"0 3 * * *"</c>).
///
/// <para><b>Purpose:</b> drift safety net for the Redis sorted-set leaderboards. Rebuilds every
/// active week-cohort from the Postgres ledger so any cache drift accumulated during the day is
/// corrected. Runs after all rollover jobs (StreakSweepJob 00:05, MissionRolloverJob 00:10,
/// LeagueRolloverJob 00:15) to ensure the sorted-sets reflect post-rollover state.</para>
///
/// <para><b>Idempotent:</b> delegates to <see cref="IGamificationCacheRebuilder.RebuildSortedSetsAsync"/>
/// which is itself idempotent (DEL then ZADD). Running twice produces the same outcome.</para>
///
/// <para><b>Fail-soft:</b> the outer try/catch ensures a job failure is logged but never surfaced
/// as a Hangfire retry storm — the nightly schedule is the retry.</para>
///
/// Hangfire resolves this class via DI — registered as Transient in
/// <c>AddGamificationInfrastructure</c>; the recurring job is wired in
/// <c>GamificationModule.InitializeAsync</c>.
/// </summary>
public sealed class GamificationCacheRebuildJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public GamificationCacheRebuildJob(
        IServiceScopeFactory scopeFactory,
        ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Executes the sorted-set rebuild. Creates a fresh DI scope with its own
    /// <see cref="GamificationCacheRebuilder"/> (Scoped — owns its own <c>GamificationDbContext</c>).
    /// Hangfire worker has no HTTP request scope — a new scope is mandatory.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInfo("P4-10: GamificationCacheRebuildJob starting.");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var rebuilder = scope.ServiceProvider.GetRequiredService<IGamificationCacheRebuilder>();
            await rebuilder.RebuildSortedSetsAsync(ct);
        }
        catch (Exception ex)
        {
            // Outer fail-soft: job failure is logged but never re-thrown so Hangfire does not
            // retry endlessly. The next nightly execution is the implicit retry.
            _logger.LogError(ex,
                "P4-10: GamificationCacheRebuildJob failed — nightly schedule is the retry.");
        }

        _logger.LogInfo("P4-10: GamificationCacheRebuildJob completed.");
    }
}

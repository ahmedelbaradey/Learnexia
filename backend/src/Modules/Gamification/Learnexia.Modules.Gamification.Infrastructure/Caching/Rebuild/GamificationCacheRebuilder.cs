using Learnexia.Modules.Gamification.Application.Caching.Rebuild;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Caching.Rebuild;

/// <summary>
/// Postgres-to-Redis rebuild service for the league sorted-set leaderboards (P4-10 Batch 3).
///
/// <para>Rebuilds the sorted-set for every active league cohort belonging to the current ISO
/// week by issuing a DEL (clear stale state) then ZADD CH for each membership. Idempotent.
/// Fail-soft per cohort — one bad cohort's exception is caught and logged; remaining cohorts
/// are still rebuilt.</para>
///
/// <para>Postgres is always the source of truth; this service never mutates Postgres.</para>
///
/// Registered Scoped in <c>AddGamificationInfrastructure</c> because it injects
/// <see cref="GamificationDbContext"/> (also Scoped). Hangfire creates a fresh scope per job
/// execution via <see cref="IServiceScopeFactory"/> in the job wrapper, so the lifetime is safe.
/// </summary>
internal sealed class GamificationCacheRebuilder : IGamificationCacheRebuilder
{
    private readonly GamificationDbContext _db;
    private readonly ILeagueLeaderboard _leaderboard;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public GamificationCacheRebuilder(
        GamificationDbContext db,
        ILeagueLeaderboard leaderboard,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _db          = db;
        _leaderboard = leaderboard;
        _clock       = clock;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task RebuildSortedSetsAsync(CancellationToken ct)
    {
        var (periodKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(
            MissionType.Weekly, _clock.UtcNow.ToUniversalTime());

        // Load all memberships for the current week, ordered by league then join order
        // for deterministic rebuild. AsNoTracking — pure read, no Postgres mutations.
        var memberships = await _db.LeagueMemberships
            .AsNoTracking()
            .Where(m => m.PeriodKey == periodKey)
            .OrderBy(m => m.LeagueId)
            .ThenBy(m => m.JoinOrder)
            .ToListAsync(ct);

        if (memberships.Count == 0)
        {
            _logger.LogInfo(
                $"P4-10: cache rebuild for week {periodKey} — no memberships found, no-op.");
            return;
        }

        // Group by LeagueId; each group is one cohort's sorted-set.
        var byLeague = memberships.GroupBy(m => m.LeagueId).ToList();
        var rebuilt  = 0;

        foreach (var cohort in byLeague)
        {
            try
            {
                // Step 1: DEL — atomically clear the stale sorted-set.
                await _leaderboard.DeleteAsync(cohort.Key, ct);

                // Step 2: ZADD CH — re-seed every member with its current encoded score.
                foreach (var m in cohort)
                {
                    await _leaderboard.UpsertMembershipAsync(
                        leagueId:     m.LeagueId,
                        membershipId: m.Id,
                        weeklyXp:     m.WeeklyXp,
                        joinOrder:    m.JoinOrder,
                        ct:           ct);
                }

                rebuilt += cohort.Count();
            }
            catch (Exception)
            {
                // Fail-soft per cohort: one bad cohort does not abort the rest.
                // Do NOT log ex.Message (F-09 — no infra detail leaks).
                _logger.LogWarn(
                    $"P4-10: cache rebuild for leagueId={cohort.Key} periodKey={periodKey} failed — continuing.");
            }
        }

        _logger.LogInfo(
            $"P4-10: cache rebuild for week {periodKey} complete — " +
            $"{rebuilt} memberships across {byLeague.Count} cohorts.");
    }
}

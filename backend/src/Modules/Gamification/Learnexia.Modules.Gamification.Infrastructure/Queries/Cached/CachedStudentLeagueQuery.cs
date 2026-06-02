using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IStudentLeagueQuery"/> — P4-10 Batch 1-A + 1-B.
/// Wraps <see cref="PostgresStudentLeagueQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IStudentLeagueQuery"/> at DI composition root.
///
/// The cache key includes the current weekly period key (e.g., <c>W:2026-23</c>), so the key
/// rotates at the ISO week boundary — forcing a cache miss and triggering the inner's
/// lazy-instantiation side effect (league membership placement) for the new week.
///
/// The lazy-placement side effect runs ONLY on a cache miss: the inner is called once,
/// membership is placed, and the post-placement snapshot is cached. On a cache hit the inner
/// is never called — the cached snapshot includes the rank from the Postgres computation at
/// cache-write time. Rank staleness is bounded by <see cref="GamificationCacheOptions.LeagueSnapshotTtlSeconds"/>
/// (30s).
///
/// <para><b>Q13 — sorted-set rank injection (not implemented):</b>
/// The Q13 path would re-derive <c>CurrentRank</c> via <c>ILeagueLeaderboard.GetRankAsync</c> on
/// every read, making the rank always current without a Postgres round-trip. However,
/// <see cref="StudentLeagueSnapshot"/> only exposes <c>(Tier, WeeklyXp, CurrentRank, GroupSize)</c>
/// — it carries neither <c>LeagueId</c> nor <c>MembershipId</c>. Injecting Q13 here would require
/// either (a) a Postgres fetch on every cache hit (negating the cache benefit) or (b) extending
/// <see cref="StudentLeagueSnapshot"/> with IDs (a cross-module contract change, out of scope).
/// Therefore Q13 is deferred: <c>CurrentRank</c> remains the Postgres-computed value cached at
/// miss-time and refreshed every <see cref="GamificationCacheOptions.LeagueSnapshotTtlSeconds"/>.
/// <see cref="ILeagueLeaderboard"/> is injected for forward-compatibility and is used elsewhere
/// (IncrementLeagueXpCommandHandler, LeagueRolloverJob) per Batch 1-B scope.</para>
///
/// Always returns a non-null snapshot (sentinel for brand-new students); the snapshot is always cached.
/// </summary>
internal sealed class CachedStudentLeagueQuery : IStudentLeagueQuery
{
    private readonly IStudentLeagueQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public CachedStudentLeagueQuery(
        IStudentLeagueQuery inner,
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _inner  = inner;
        _cache  = cache;
        _opts   = opts.Value;
        _clock  = clock;
        _logger = logger;
    }

    public async Task<StudentLeagueSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetByStudentIdAsync(studentId, ct);

        // Period key rotates at ISO week boundary — cache key changes, forcing a miss
        // and triggering the inner's lazy-placement for the new week.
        var nowUtc = _clock.UtcNow.ToUniversalTime();
        var (weeklyKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, nowUtc);

        var key = GamificationCacheKeys.LeagueSnapshot(studentId, weeklyKey);
        var cached = await _cache.TryGetAsync<StudentLeagueSnapshot>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo($"cache.gamification.league.hit student={studentId}");
            return cached;
        }

        _logger.LogInfo($"cache.gamification.league.miss student={studentId}");

        // Inner call runs lazy-placement if needed; we cache the post-placement snapshot
        // (including Postgres-computed rank). Q13 rank-via-ZREVRANK is deferred — see
        // the class-level doc comment for why.
        var fresh = await _inner.GetByStudentIdAsync(studentId, ct);

        await _cache.SetAsync(
            key, fresh, TimeSpan.FromSeconds(_opts.LeagueSnapshotTtlSeconds), ct);

        return fresh;
    }
}

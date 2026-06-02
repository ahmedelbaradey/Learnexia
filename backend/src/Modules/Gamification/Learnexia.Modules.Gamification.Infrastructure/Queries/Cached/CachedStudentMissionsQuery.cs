using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IStudentMissionsQuery"/> — P4-10 Batch 1-A.
/// Wraps <see cref="PostgresStudentMissionsQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IStudentMissionsQuery"/> at DI composition root.
///
/// The cache key includes the current daily period key (e.g., <c>D:2026-06-02</c>), so the key
/// naturally rotates at midnight UTC — forcing a cache miss and triggering the inner's
/// lazy-instantiation side effect for the new period.
///
/// The lazy-instantiation side effect (writing missions rows to Postgres on first call per period)
/// runs ONLY on a cache miss: the inner is called once, missions are instantiated, and the
/// post-instantiation snapshot is cached. On a cache hit the inner is never called and no
/// side effect runs — this is correct and desired.
///
/// Always returns a non-null snapshot (sentinel for brand-new students); the snapshot is always cached.
/// </summary>
internal sealed class CachedStudentMissionsQuery : IStudentMissionsQuery
{
    private readonly IStudentMissionsQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public CachedStudentMissionsQuery(
        IStudentMissionsQuery inner,
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

    public async Task<StudentMissionsSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetByStudentIdAsync(studentId, ct);

        // Period key rotates naturally at midnight UTC — cache key changes at period boundary,
        // forcing a miss and triggering the inner's lazy-instantiation for the new period.
        var nowUtc = _clock.UtcNow.ToUniversalTime();
        var (dailyKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, nowUtc);

        var key = GamificationCacheKeys.Missions(studentId, dailyKey);
        var cached = await _cache.TryGetAsync<StudentMissionsSnapshot>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo($"cache.gamification.missions.hit student={studentId}");
            return cached;
        }

        _logger.LogInfo($"cache.gamification.missions.miss student={studentId}");

        // Inner call runs lazy-instantiation if needed; we cache the post-instantiation snapshot.
        var fresh = await _inner.GetByStudentIdAsync(studentId, ct);

        await _cache.SetAsync(
            key, fresh, TimeSpan.FromSeconds(_opts.MissionsTtlSeconds), ct);

        return fresh;
    }
}

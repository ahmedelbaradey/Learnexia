using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IStudentBadgesQuery"/> — P4-10 Batch 1-A.
/// Wraps <see cref="PostgresStudentBadgesQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IStudentBadgesQuery"/> at DI composition root.
///
/// The interface has no <c>recentTake</c> parameter — the inner impl uses <c>Take(3)</c>
/// hard-coded. The cache key therefore uses a fixed <c>:3</c> suffix per the P4-10 key
/// convention (<c>gamification:student:{id}:badges:3</c>).
///
/// Always returns a non-null snapshot (sentinel for brand-new students); the snapshot is always cached.
/// </summary>
internal sealed class CachedStudentBadgesQuery : IStudentBadgesQuery
{
    private readonly IStudentBadgesQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public CachedStudentBadgesQuery(
        IStudentBadgesQuery inner,
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _inner  = inner;
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<StudentBadgesSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetByStudentIdAsync(studentId, ct);

        // Key includes ":3" suffix (the hard-coded Take(3) in the inner impl).
        var key = GamificationCacheKeys.Badges(studentId, take: 3);
        var cached = await _cache.TryGetAsync<StudentBadgesSnapshot>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo($"cache.gamification.badges.hit student={studentId}");
            return cached;
        }

        _logger.LogInfo($"cache.gamification.badges.miss student={studentId}");
        var fresh = await _inner.GetByStudentIdAsync(studentId, ct);

        await _cache.SetAsync(
            key, fresh, TimeSpan.FromSeconds(_opts.BadgesTtlSeconds), ct);

        return fresh;
    }
}

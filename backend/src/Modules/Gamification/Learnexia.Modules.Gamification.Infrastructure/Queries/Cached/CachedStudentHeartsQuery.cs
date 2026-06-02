using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IStudentHeartsQuery"/> — P4-10 Batch 1-A.
/// Wraps <see cref="PostgresStudentHeartsQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IStudentHeartsQuery"/> at DI composition root.
///
/// Critical: <see cref="PostgresStudentHeartsQuery"/> has a lazy-refill side effect on read
/// (persist-on-read, D5 / Q1.bis). The decorator caches the snapshot AFTER the inner call
/// returns the post-refill value. On a cache hit the inner is never called, so the lazy-refill
/// side effect is skipped — this is correct and desired: the TTL (30s) bounds the staleness
/// window; a new heart that should have been refilled will be visible within 30s.
///
/// Always returns a non-null snapshot (sentinel for brand-new students); the snapshot is always cached.
/// </summary>
internal sealed class CachedStudentHeartsQuery : IStudentHeartsQuery
{
    private readonly IStudentHeartsQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public CachedStudentHeartsQuery(
        IStudentHeartsQuery inner,
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _inner  = inner;
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<StudentHeartsSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetByStudentIdAsync(studentId, ct);

        var key = GamificationCacheKeys.Hearts(studentId);
        var cached = await _cache.TryGetAsync<StudentHeartsSnapshot>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo($"cache.gamification.hearts.hit student={studentId}");
            return cached;
        }

        _logger.LogInfo($"cache.gamification.hearts.miss student={studentId}");

        // Inner call runs the lazy-refill side effect; we cache the post-refill snapshot.
        var fresh = await _inner.GetByStudentIdAsync(studentId, ct);

        await _cache.SetAsync(
            key, fresh, TimeSpan.FromSeconds(_opts.HeartsTtlSeconds), ct);

        return fresh;
    }
}

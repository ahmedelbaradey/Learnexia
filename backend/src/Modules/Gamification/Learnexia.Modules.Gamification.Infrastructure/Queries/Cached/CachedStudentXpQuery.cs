using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IStudentXpQuery"/> — P4-10 Batch 1-A.
/// Wraps <see cref="PostgresStudentXpQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IStudentXpQuery"/> at DI composition root;
/// consumers (Learning dashboard) see no change.
///
/// Null result (brand-new student with no profile) is NOT cached — the decorator falls through
/// on every request until the profile is created. This is safe: brand-new-student misses are
/// low-frequency and non-null profiles are created on first XP award.
/// </summary>
internal sealed class CachedStudentXpQuery : IStudentXpQuery
{
    private readonly IStudentXpQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public CachedStudentXpQuery(
        IStudentXpQuery inner,
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _inner  = inner;
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<StudentXpSnapshot?> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetByStudentIdAsync(studentId, ct);

        var key = GamificationCacheKeys.Xp(studentId);
        var cached = await _cache.TryGetAsync<StudentXpSnapshot>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo($"cache.gamification.xp.hit student={studentId}");
            return cached;
        }

        _logger.LogInfo($"cache.gamification.xp.miss student={studentId}");
        var fresh = await _inner.GetByStudentIdAsync(studentId, ct);

        if (fresh is not null)
        {
            await _cache.SetAsync(
                key, fresh, TimeSpan.FromSeconds(_opts.XpTtlSeconds), ct);
        }

        return fresh;
    }
}

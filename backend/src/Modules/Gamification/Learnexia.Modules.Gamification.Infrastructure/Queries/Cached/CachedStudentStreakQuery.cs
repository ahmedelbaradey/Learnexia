using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;

/// <summary>
/// Cache decorator for <see cref="IStudentStreakQuery"/> — P4-10 Batch 1-A.
/// Wraps <see cref="PostgresStudentStreakQuery"/> with a Redis read-through layer.
/// Registered as the resolved <see cref="IStudentStreakQuery"/> at DI composition root.
///
/// Null result (brand-new student with no profile) is NOT cached — same rationale as
/// <see cref="CachedStudentXpQuery"/>: brand-new-student misses are low-frequency.
/// </summary>
internal sealed class CachedStudentStreakQuery : IStudentStreakQuery
{
    private readonly IStudentStreakQuery _inner;
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _opts;
    private readonly ILoggerManager _logger;

    public CachedStudentStreakQuery(
        IStudentStreakQuery inner,
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> opts,
        ILoggerManager logger)
    {
        _inner  = inner;
        _cache  = cache;
        _opts   = opts.Value;
        _logger = logger;
    }

    public async Task<StudentStreakSnapshot?> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        if (!_opts.Enabled)
            return await _inner.GetByStudentIdAsync(studentId, ct);

        var key = GamificationCacheKeys.Streak(studentId);
        var cached = await _cache.TryGetAsync<StudentStreakSnapshot>(key, ct);
        if (cached is not null)
        {
            _logger.LogInfo($"cache.gamification.streak.hit student={studentId}");
            return cached;
        }

        _logger.LogInfo($"cache.gamification.streak.miss student={studentId}");
        var fresh = await _inner.GetByStudentIdAsync(studentId, ct);

        if (fresh is not null)
        {
            await _cache.SetAsync(
                key, fresh, TimeSpan.FromSeconds(_opts.StreakTtlSeconds), ct);
        }

        return fresh;
    }
}

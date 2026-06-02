using Learnexia.Modules.Gamification.Application.Caching;

namespace Learnexia.Modules.Gamification.Infrastructure.Caching;

/// <summary>
/// No-op cache used when Redis is not configured (dev/local without a Redis instance).
/// All reads return null/empty, all writes are no-ops. Decorators fall through to Postgres
/// unconditionally.
/// </summary>
internal sealed class NullGamificationCache : IGamificationCache
{
    public Task<T?> TryGetAsync<T>(string key, CancellationToken ct) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
        => Task.CompletedTask;

    public Task DeleteAsync(string key, CancellationToken ct)
        => Task.CompletedTask;

    public Task SortedSetUpsertAsync(string key, long score, string member, TimeSpan? expire, CancellationToken ct)
        => Task.CompletedTask;

    public Task<long?> SortedSetGetRankAsync(string key, string member, bool descending, CancellationToken ct)
        => Task.FromResult<long?>(null);

    public Task<long> SortedSetCardAsync(string key, CancellationToken ct)
        => Task.FromResult(0L);

    public Task<IReadOnlyList<(string member, double score)>> SortedSetRangeAsync(
        string key, long start, long stop, bool descending, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<(string, double)>>(Array.Empty<(string, double)>());

    public Task<double?> SortedSetGetScoreAsync(string key, string member, CancellationToken ct)
        => Task.FromResult<double?>(null);
}

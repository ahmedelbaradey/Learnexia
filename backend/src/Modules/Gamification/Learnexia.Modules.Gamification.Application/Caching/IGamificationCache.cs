namespace Learnexia.Modules.Gamification.Application.Caching;

/// <summary>
/// Fail-soft Redis wrapper used by Cached*Query decorators and invalidators.
/// All methods MUST swallow RedisException and return null/false/no-op so callers
/// fall through to Postgres. Postgres is the source of truth; Redis mirrors.
/// </summary>
public interface IGamificationCache
{
    Task<T?> TryGetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
    Task DeleteAsync(string key, CancellationToken ct);

    // Sorted-set primitives (used by Batch 1-B leaderboard)
    Task SortedSetUpsertAsync(string key, long score, string member, TimeSpan? expire, CancellationToken ct);
    Task<long?> SortedSetGetRankAsync(string key, string member, bool descending, CancellationToken ct);
    Task<long> SortedSetCardAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<(string member, double score)>> SortedSetRangeAsync(
        string key, long start, long stop, bool descending, CancellationToken ct);
    Task<double?> SortedSetGetScoreAsync(string key, string member, CancellationToken ct);
}

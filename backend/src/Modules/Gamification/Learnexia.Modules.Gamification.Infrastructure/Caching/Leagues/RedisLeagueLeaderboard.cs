using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Caching.Leagues;

/// <summary>
/// Redis sorted-set implementation of <see cref="ILeagueLeaderboard"/> (P4-10 Batch 1-B).
///
/// Composes <see cref="IGamificationCache"/> for all sorted-set operations.
/// Score encoding is delegated to <see cref="LeaderboardScoreEncoder"/> (24-bit packed integer).
///
/// All methods are fail-soft: any Redis error surfaces as null / empty / no-op so the caller
/// falls through to the Postgres rank computation. Postgres is always the source of truth.
///
/// Registered as Singleton in <c>AddGamificationCache</c> — uses IGamificationCache which is
/// itself Singleton.
/// </summary>
internal sealed class RedisLeagueLeaderboard : ILeagueLeaderboard
{
    private readonly IGamificationCache _cache;
    private readonly GamificationCacheOptions _options;

    public RedisLeagueLeaderboard(
        IGamificationCache cache,
        IOptions<GamificationCacheOptions> options)
    {
        _cache   = cache;
        _options = options.Value;
    }

    // ── Key helper ───────────────────────────────────────────────────────────────────────────────

    private static string Key(int leagueId) => GamificationCacheKeys.LeaderboardSortedSet(leagueId);

    private static TimeSpan Ttl(GamificationCacheOptions opts)
        => TimeSpan.FromSeconds(opts.LeaderboardSortedSetTtlSeconds);

    // ── ILeagueLeaderboard ───────────────────────────────────────────────────────────────────────

    public Task UpsertMembershipAsync(
        int leagueId, int membershipId, int weeklyXp, int joinOrder, CancellationToken ct)
    {
        var score = LeaderboardScoreEncoder.Encode(weeklyXp, joinOrder);
        return _cache.SortedSetUpsertAsync(
            Key(leagueId),
            score,
            membershipId.ToString(),
            Ttl(_options),
            ct);
    }

    public async Task<int?> GetRankAsync(int leagueId, int membershipId, CancellationToken ct)
    {
        var rank = await _cache.SortedSetGetRankAsync(
            Key(leagueId),
            membershipId.ToString(),
            descending: true,
            ct);

        // ZREVRANK is 0-based; callers expect 1-based rank.
        return rank is null ? null : checked((int)(rank.Value + 1));
    }

    public async Task<int> GetCohortSizeAsync(int leagueId, CancellationToken ct)
    {
        var card = await _cache.SortedSetCardAsync(Key(leagueId), ct);
        return checked((int)card);
    }

    public async Task<IReadOnlyList<int>> GetTopMembershipIdsAsync(
        int leagueId, int count, CancellationToken ct)
    {
        if (count <= 0) return Array.Empty<int>();

        var range = await _cache.SortedSetRangeAsync(
            Key(leagueId), 0, count - 1, descending: true, ct);

        var result = new List<int>(range.Count);
        foreach (var (member, _) in range)
        {
            if (int.TryParse(member, out var id))
                result.Add(id);
        }
        return result;
    }

    public async Task<IReadOnlyList<int>> GetBottomMembershipIdsAsync(
        int leagueId, int count, CancellationToken ct)
    {
        if (count <= 0) return Array.Empty<int>();

        // Bottom = ascending order (lowest score first).
        var range = await _cache.SortedSetRangeAsync(
            Key(leagueId), 0, count - 1, descending: false, ct);

        var result = new List<int>(range.Count);
        foreach (var (member, _) in range)
        {
            if (int.TryParse(member, out var id))
                result.Add(id);
        }
        return result;
    }

    public Task DeleteAsync(int leagueId, CancellationToken ct)
        => _cache.DeleteAsync(Key(leagueId), ct);
}

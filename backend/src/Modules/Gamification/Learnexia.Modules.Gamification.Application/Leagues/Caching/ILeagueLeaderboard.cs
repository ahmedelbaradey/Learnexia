namespace Learnexia.Modules.Gamification.Application.Leagues.Caching;

/// <summary>
/// Read/write seam over the Redis sorted-set leaderboard (P4-10 Batch 1-B).
/// Postgres remains the source of truth; this is a fast cohort-rank cache that mirrors
/// the ledger via ZADD CH on every XP commit.
///
/// <para>All leagueId and membershipId values use the int PK type from the domain entities
/// (<see cref="Domain.Entities.League.Id"/> and <see cref="Domain.Entities.LeagueMembership.Id"/>).</para>
///
/// <para>Implementations must be fail-soft: all methods return null/empty/no-op on Redis error
/// so callers fall through to Postgres. Postgres is always the source of truth.</para>
/// </summary>
public interface ILeagueLeaderboard
{
    /// <summary>
    /// ZADD CH the membership's encoded score (LeaderboardScoreEncoder.Encode(weeklyXp, joinOrder)).
    /// Called after IncrementLeagueXp commits to mirror the post-mutation state.
    /// </summary>
    Task UpsertMembershipAsync(int leagueId, int membershipId, int weeklyXp, int joinOrder, CancellationToken ct);

    /// <summary>
    /// ZREVRANK to find membership's 1-based rank. Returns null on miss/error.
    /// </summary>
    Task<int?> GetRankAsync(int leagueId, int membershipId, CancellationToken ct);

    /// <summary>
    /// ZCARD — cohort size (number of members in the sorted set).
    /// </summary>
    Task<int> GetCohortSizeAsync(int leagueId, CancellationToken ct);

    /// <summary>
    /// ZREVRANGE 0..count-1 (descending). Returns membership IDs ordered by rank (highest first).
    /// Used by the rollover job for top-N promotion / bottom-N relegation.
    /// </summary>
    Task<IReadOnlyList<int>> GetTopMembershipIdsAsync(int leagueId, int count, CancellationToken ct);

    /// <summary>
    /// ZRANGE 0..count-1 (ascending — lowest score first). Returns membership IDs for bottom-N.
    /// </summary>
    Task<IReadOnlyList<int>> GetBottomMembershipIdsAsync(int leagueId, int count, CancellationToken ct);

    /// <summary>
    /// DEL the sorted-set key (rollover cleanup after commit).
    /// </summary>
    Task DeleteAsync(int leagueId, CancellationToken ct);
}

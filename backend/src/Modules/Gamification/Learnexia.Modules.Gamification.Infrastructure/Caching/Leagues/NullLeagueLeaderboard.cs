using Learnexia.Modules.Gamification.Application.Leagues.Caching;

namespace Learnexia.Modules.Gamification.Infrastructure.Caching.Leagues;

/// <summary>
/// No-op fallback implementation of <see cref="ILeagueLeaderboard"/> (P4-10 Batch 1-B).
///
/// Registered when <c>IConnectionMultiplexer</c> is NOT in DI (local dev, CI without Redis).
/// Every method is a safe no-op / returns null/empty so callers transparently fall through
/// to the Postgres rank computation.
///
/// Mirrors the <c>NullGamificationCache</c> precedent from Batch 0.
/// </summary>
internal sealed class NullLeagueLeaderboard : ILeagueLeaderboard
{
    public Task UpsertMembershipAsync(
        int leagueId, int membershipId, int weeklyXp, int joinOrder, CancellationToken ct)
        => Task.CompletedTask;

    public Task<int?> GetRankAsync(int leagueId, int membershipId, CancellationToken ct)
        => Task.FromResult<int?>(null);

    public Task<int> GetCohortSizeAsync(int leagueId, CancellationToken ct)
        => Task.FromResult(0);

    public Task<IReadOnlyList<int>> GetTopMembershipIdsAsync(
        int leagueId, int count, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

    public Task<IReadOnlyList<int>> GetBottomMembershipIdsAsync(
        int leagueId, int count, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

    public Task DeleteAsync(int leagueId, CancellationToken ct)
        => Task.CompletedTask;
}

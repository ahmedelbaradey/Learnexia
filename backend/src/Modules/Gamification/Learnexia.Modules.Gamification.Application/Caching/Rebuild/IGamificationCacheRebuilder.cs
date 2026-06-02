namespace Learnexia.Modules.Gamification.Application.Caching.Rebuild;

/// <summary>
/// Rebuilds derived Redis state from the Postgres ledger after cache loss.
///
/// <para>Postgres is always the source of truth; Redis is a derived mirror. This service is a
/// pure read-from-Postgres + write-to-Redis projection — it never mutates Postgres.</para>
///
/// <para>All methods are idempotent: running them any number of times produces the same Redis
/// state that matches the Postgres ledger at the time of the last call.</para>
///
/// <para>Fail-soft per cohort (for <see cref="RebuildSortedSetsAsync"/>): one bad cohort does
/// not prevent the other cohorts from being rebuilt.</para>
/// </summary>
public interface IGamificationCacheRebuilder
{
    /// <summary>
    /// Rebuilds the sorted-set leaderboards for the current week from Postgres.
    ///
    /// For each active league cohort (matching the current ISO week <c>PeriodKey</c>):
    /// <list type="number">
    ///   <item>DEL the sorted-set key to clear stale state.</item>
    ///   <item>ZADD CH each <c>LeagueMembership</c> with its encoded score
    ///         (<c>LeaderboardScoreEncoder.Encode(WeeklyXp, JoinOrder)</c>).</item>
    /// </list>
    ///
    /// Idempotent: safe to call any number of times — final state always matches Postgres.
    /// Fail-soft per cohort: one bad cohort's exception is caught and logged; remaining
    /// cohorts are still rebuilt.
    /// Logs per-cohort counts. Called by <c>GamificationCacheRebuildJob</c> nightly at 03:00 UTC.
    /// </summary>
    Task RebuildSortedSetsAsync(CancellationToken ct);
}

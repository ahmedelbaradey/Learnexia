namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements; P9-12 (and any future consumer) injects.
/// Returns a bounded, on-demand cohort of recently-active student IDs who are scope-eligible
/// for the given timed event at <paramref name="atUtc"/>.
///
/// "Eligible" = students whose <c>StudentXpProfile.LastActivityDateUtc</c> falls within
/// <c>TimedEventParticipationOptions.EligibilityWindowDays</c> of <paramref name="atUtc"/> —
/// i.e. recently-active students who would earn qualifying XP during the event window.
/// This reuses the same in-module activity signal already scanned by <c>StreakAtRiskJob</c>;
/// no new cross-module seam is needed (module isolation rule 1 preserved).
///
/// For <c>AllXp</c> scope, "recently active" is the correct cohort.
/// <c>MissionXp</c>/<c>LeagueXp</c> scopes are deferred (no narrowing) — forward-compat only.
///
/// Bounded by <c>Take(500)</c> page guard (P4-12 MVP scale); a WARN is logged when the cap
/// is hit. Registered Scoped in <c>AddGamificationInfrastructure()</c>.
/// </summary>
public interface IEligibleStudentsForTimedEventQuery
{
    /// <summary>
    /// Returns a bounded list of opaque student IDs eligible for <paramref name="timedEventId"/>
    /// at <paramref name="atUtc"/>. Never returns null — returns an empty list when none qualify.
    /// </summary>
    Task<IReadOnlyList<int>> GetEligibleAsync(
        int timedEventId,
        DateTime atUtc,
        CancellationToken ct = default);
}

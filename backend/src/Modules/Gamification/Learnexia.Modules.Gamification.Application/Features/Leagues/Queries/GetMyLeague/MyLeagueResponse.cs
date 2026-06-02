using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;

/// <summary>
/// Full league view response for <c>GET /api/Gamification/Leagues/Me</c> (P4-07, AC3).
///
/// Contains the caller's current cohort standings (top-30, anonymized per D2),
/// the caller's own rank + XP, and the week boundary timestamps.
///
/// <see cref="LeagueTierDto"/> is the cross-module mirror of
/// <c>Learnexia.Modules.Gamification.Domain.Enums.LeagueTier</c>.
///
/// P4-10 note: the standings query is designed to be swap-friendly — the <see cref="Standings"/>
/// list is the only Redis-replaceable surface. The response contract must not change when
/// P4-10 swaps the read path to a Redis sorted-set.
/// </summary>
/// <param name="Tier">The student's current tier (read from the membership's League.Tier).</param>
/// <param name="PeriodKey">ISO week key — e.g. "W:2026-23".</param>
/// <param name="PeriodStartUtc">Monday 00:00 UTC — start of this week's competition period.</param>
/// <param name="PeriodEndUtc">Next Monday 00:00 UTC — exclusive end of this week's competition period.</param>
/// <param name="MyRank">The caller's current 1-based rank within the cohort (0 = brand-new, no membership).</param>
/// <param name="MyWeeklyXp">The caller's weekly XP in this period.</param>
/// <param name="Standings">Full sorted standings list (up to 30 entries, WeeklyXp DESC / JoinedAtUtc ASC).</param>
/// <param name="PromotionCutoffRank">Top-N rank threshold: members ranked ≤ this are promoted. 0 when Diamond.</param>
/// <param name="DemotionCutoffRank">Bottom-N rank threshold: members ranked ≥ this are demoted. 0 when Bronze.</param>
public sealed record MyLeagueResponse(
    LeagueTierDto Tier,
    string PeriodKey,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int MyRank,
    int MyWeeklyXp,
    IReadOnlyList<LeagueStandingDto> Standings,
    int PromotionCutoffRank,
    int DemotionCutoffRank);

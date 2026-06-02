using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Domain.Services;

/// <summary>
/// Pure static helper that computes promotion/demotion cutoffs and assigns
/// <see cref="MembershipStatus"/> to each cohort member after weekly rollover.
/// No DI, no DB access — fully unit-testable in isolation.
///
/// Mirrors the pure-static shape of <see cref="BadgePredicateEvaluator"/> and
/// <see cref="MissionPeriodCalculator"/> (no constructor, no fields, no side effects).
///
/// Called by <c>LeaguePromotionJob</c> on Monday 00:15 UTC (P4-07).
/// </summary>
public static class LeagueStandings
{
    /// <summary>
    /// Cohort promotion/demotion count plan for a single tier + cohort size.
    /// <see cref="StayCount"/> = <c>cohortSize - PromoteCount - DemoteCount</c> (always &gt;= 0).
    /// </summary>
    public readonly record struct PromotionPlan(int PromoteCount, int DemoteCount, int StayCount);

    /// <summary>
    /// Computes how many members promote, demote, and stay for a cohort of
    /// <paramref name="cohortSize"/> competing at <paramref name="currentTier"/>.
    ///
    /// <para><b>Standard cohort (&#8805;12 members)</b>:
    /// <c>PromoteCount = basePromote (7)</c>, <c>DemoteCount = baseDemote (5)</c>.</para>
    ///
    /// <para><b>Small cohort (&lt;12 members)</b>: counts scaled proportionally using integer
    /// floor division of <c>N * base / baseCohort</c>. Minimum is 0 (not 1) to allow all-stay
    /// when the cohort is very small. If the sum would exceed the cohort size, both counts are
    /// reduced proportionally until the sum &#8804; cohortSize.</para>
    ///
    /// <para><b>Tier extremes</b>:
    /// <list type="bullet">
    ///   <item><c>Diamond</c> (highest tier): <c>PromoteCount</c> set to 0 — nowhere up.</item>
    ///   <item><c>Bronze</c> (lowest tier): <c>DemoteCount</c> set to 0 — nowhere down.</item>
    /// </list></para>
    /// </summary>
    /// <param name="cohortSize">Actual member count in the cohort (&#8805;1).</param>
    /// <param name="currentTier">The tier this cohort competes in.</param>
    /// <param name="basePromote">Promotion count at standard cohort size (default 7).</param>
    /// <param name="baseDemote">Demotion count at standard cohort size (default 5).</param>
    /// <param name="baseCohort">Standard cohort size used as denominator for scaling (default 30).</param>
    public static PromotionPlan ComputeCutoffs(
        int cohortSize,
        LeagueTier currentTier,
        int basePromote = 7,
        int baseDemote = 5,
        int baseCohort = 30)
    {
        int promoteN;
        int demoteN;

        if (cohortSize < 12)
        {
            // Small cohort: scale proportionally; minimum 0 to allow all-stay for tiny cohorts.
            promoteN = (int)Math.Floor((double)cohortSize * basePromote / baseCohort);
            demoteN  = (int)Math.Floor((double)cohortSize * baseDemote  / baseCohort);

            // Clamp: if scaled counts would overlap (sum > cohortSize), reduce proportionally.
            if (promoteN + demoteN > cohortSize)
            {
                double ratio = (double)cohortSize / (promoteN + demoteN);
                promoteN = (int)Math.Floor(promoteN * ratio);
                demoteN  = cohortSize - promoteN;  // remainder goes to demotion side
            }
        }
        else
        {
            promoteN = basePromote;
            demoteN  = baseDemote;
        }

        // Tier extremes: cap at 0 where movement is impossible.
        if (currentTier == LeagueTier.Diamond) promoteN = 0;  // nowhere up
        if (currentTier == LeagueTier.Bronze)  demoteN  = 0;  // nowhere down

        int stayN = Math.Max(0, cohortSize - promoteN - demoteN);
        return new PromotionPlan(promoteN, demoteN, stayN);
    }

    /// <summary>
    /// Assigns promotion status and <c>TierAfter</c> to each member in a ranked cohort.
    ///
    /// <para>The <paramref name="ranked"/> list MUST be pre-sorted
    /// <c>WeeklyXp DESC, JoinedAtUtc ASC</c> (D4 tiebreak). Index 0 = rank 1 = best performer.</para>
    ///
    /// <para>Return value is a list parallel to <paramref name="ranked"/>, each entry:
    /// <c>(MembershipId, Status, FinalRank, TierAfter)</c>.</para>
    ///
    /// <para>Status assignment:
    /// <list type="bullet">
    ///   <item>Rank &#8804; <c>PromoteCount</c> → <see cref="MembershipStatus.Promoted"/>
    ///     (or <see cref="MembershipStatus.Stayed"/> at Diamond — stays in Diamond).</item>
    ///   <item>Rank &gt; <c>cohortSize - DemoteCount</c> → <see cref="MembershipStatus.Demoted"/>
    ///     (or <see cref="MembershipStatus.Stayed"/> at Bronze — stays in Bronze).</item>
    ///   <item>Otherwise → <see cref="MembershipStatus.Stayed"/>.</item>
    /// </list></para>
    /// </summary>
    /// <param name="ranked">Pre-sorted cohort membership list (WeeklyXp DESC, JoinedAtUtc ASC).</param>
    /// <param name="currentTier">The tier this cohort competes in.</param>
    /// <param name="basePromote">Promotion count at standard cohort size (default 7).</param>
    /// <param name="baseDemote">Demotion count at standard cohort size (default 5).</param>
    /// <param name="baseCohort">Standard cohort size used as denominator for scaling (default 30).</param>
    public static IReadOnlyList<(int MembershipId, MembershipStatus Status, int FinalRank, LeagueTier TierAfter)> Apply(
        IReadOnlyList<LeagueMembership> ranked,
        LeagueTier currentTier,
        int basePromote = 7,
        int baseDemote = 5,
        int baseCohort = 30)
    {
        var plan    = ComputeCutoffs(ranked.Count, currentTier, basePromote, baseDemote, baseCohort);
        var results = new List<(int, MembershipStatus, int, LeagueTier)>(ranked.Count);

        for (int i = 0; i < ranked.Count; i++)
        {
            int rank = i + 1;   // 1-based rank
            MembershipStatus status;
            LeagueTier tierAfter;

            if (i < plan.PromoteCount)
            {
                // Top-N: promote (or stay at Diamond — nowhere up).
                if (currentTier == LeagueTier.Diamond)
                {
                    status    = MembershipStatus.Stayed;
                    tierAfter = LeagueTier.Diamond;
                }
                else
                {
                    status    = MembershipStatus.Promoted;
                    tierAfter = (LeagueTier)((int)currentTier + 1);
                }
            }
            else if (plan.DemoteCount > 0 && i >= ranked.Count - plan.DemoteCount)
            {
                // Bottom-N: demote (or stay at Bronze — nowhere down).
                if (currentTier == LeagueTier.Bronze)
                {
                    status    = MembershipStatus.Stayed;
                    tierAfter = LeagueTier.Bronze;
                }
                else
                {
                    status    = MembershipStatus.Demoted;
                    tierAfter = (LeagueTier)((int)currentTier - 1);
                }
            }
            else
            {
                status    = MembershipStatus.Stayed;
                tierAfter = currentTier;
            }

            results.Add((ranked[i].Id, status, rank, tierAfter));
        }

        return results;
    }
}

using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LeagueStandings.ComputeCutoffs"/> and <see cref="LeagueStandings.Apply"/>.
///
/// Pure static service — no DB or DI required. Deterministic by design.
///
/// Coverage map (9 test cases from P4-07 Batch 6 spec):
///   LS1  Standard cohort (30, Silver)    → Promote=7, Demote=5, Stay=18
///   LS2  Small cohort (10, Silver)       → proportional scaling: floor(10*7/30)=2, floor(10*5/30)=1, stay=7
///   LS3  Minimum cohort (3, Silver)      → at least 0 each with min-clamping (floor math gives 0 promote, 0 demote, 3 stay)
///   LS4  Diamond cohort (30)             → top 7 Stayed (Diamond), bottom 5 Demoted to Gold
///   LS5  Bronze cohort (30)              → top 7 Promoted to Silver, bottom 5 Stayed (Bronze)
///   LS6  Cohort of exactly 1             → 1 entry, Stayed
///   LS7  Apply with ranked memberships  → correct rank ordering + TierAfter assignment
///   LS8  Tiebreak: same WeeklyXp, earlier JoinedAtUtc wins
///   LS9  Invariant: Promote + Demote + Stay == cohort size always
/// </summary>
public sealed class LeagueStandingsTests
{
    // =========================================================================
    // LS1 — Standard cohort (30, Silver) → Promote=7, Demote=5, Stay=18
    // =========================================================================

    [Fact(DisplayName = "LS1 Standard cohort (30 members, Silver) → PromoteCount=7, DemoteCount=5, StayCount=18")]
    public void LS1_StandardCohort_Silver_CorrectCounts()
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize: 30, currentTier: LeagueTier.Silver);

        plan.PromoteCount.Should().Be(7, "top 7 promote in a standard 30-member cohort");
        plan.DemoteCount.Should().Be(5, "bottom 5 demote in a standard 30-member cohort");
        plan.StayCount.Should().Be(18, "remaining 18 stay (30 - 7 - 5)");
    }

    // =========================================================================
    // LS2 — Small cohort (10, Silver) → proportional scaling
    // =========================================================================

    [Fact(DisplayName = "LS2 Small cohort (10 members, Silver) → proportional: floor(10*7/30)=2, floor(10*5/30)=1, stay=7")]
    public void LS2_SmallCohort_Silver_ProportionalScaling()
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize: 10, currentTier: LeagueTier.Silver);

        // floor(10 * 7 / 30) = floor(2.33) = 2
        plan.PromoteCount.Should().Be(2, "floor(10*7/30) = 2");
        // floor(10 * 5 / 30) = floor(1.67) = 1
        plan.DemoteCount.Should().Be(1, "floor(10*5/30) = 1");
        plan.StayCount.Should().Be(7, "10 - 2 - 1 = 7");
    }

    // =========================================================================
    // LS3 — Minimum cohort (3, Silver) → very small, scale to 0 each (floor math)
    // =========================================================================

    [Fact(DisplayName = "LS3 Minimum cohort (3 members, Silver) → floor math gives 0/0/3 (all stay)")]
    public void LS3_MinimumCohort_Silver_AllStay()
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize: 3, currentTier: LeagueTier.Silver);

        // floor(3 * 7 / 30) = floor(0.7) = 0
        // floor(3 * 5 / 30) = floor(0.5) = 0
        plan.PromoteCount.Should().Be(0, "floor(3*7/30) = 0 — cohort too small to produce a promote slot");
        plan.DemoteCount.Should().Be(0, "floor(3*5/30) = 0 — cohort too small to produce a demote slot");
        plan.StayCount.Should().Be(3, "all 3 members stay when cohort is too small to scale");

        // Invariant always holds
        (plan.PromoteCount + plan.DemoteCount + plan.StayCount).Should().Be(3);
    }

    // =========================================================================
    // LS4 — Diamond cohort (30) → top 7 "Stayed" (Diamond, nowhere up), bottom 5 Demoted to Gold
    // =========================================================================

    [Fact(DisplayName = "LS4 Diamond cohort (30 members) → PromoteCount=0, DemoteCount=5, StayCount=25 (top 7 Stayed at Diamond)")]
    public void LS4_DiamondCohort_TopStayDiamondBottomDemote()
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize: 30, currentTier: LeagueTier.Diamond);

        plan.PromoteCount.Should().Be(0, "Diamond is the top tier — nowhere up, so PromoteCount=0");
        plan.DemoteCount.Should().Be(5, "bottom 5 still demote to Gold");
        plan.StayCount.Should().Be(25, "25 = 30 - 0 - 5 stay (includes the top 7 who would normally promote)");
    }

    [Fact(DisplayName = "LS4b Diamond Apply: top 7 get Status=Stayed TierAfter=Diamond, bottom 5 Status=Demoted TierAfter=Gold")]
    public void LS4b_DiamondApply_CorrectStatuses()
    {
        var members = CreateMemberships(30, startXp: 3000, xpStep: -10);
        var results = LeagueStandings.Apply(members, LeagueTier.Diamond);

        results.Should().HaveCount(30);

        // Top 7 (rank 1..7): stayed at Diamond
        for (int i = 0; i < 7; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Stayed,
                $"rank {i + 1} at Diamond — stayed (nowhere up)");
            results[i].TierAfter.Should().Be(LeagueTier.Diamond,
                $"rank {i + 1} stays in Diamond");
        }

        // Bottom 5 (rank 26..30): demoted to Gold
        for (int i = 25; i < 30; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Demoted,
                $"rank {i + 1} at Diamond — demoted to Gold");
            results[i].TierAfter.Should().Be(LeagueTier.Gold,
                $"rank {i + 1} demotes from Diamond to Gold");
        }
    }

    // =========================================================================
    // LS5 — Bronze cohort (30) → top 7 Promoted to Silver, bottom 5 "Stayed" (Bronze, nowhere down)
    // =========================================================================

    [Fact(DisplayName = "LS5 Bronze cohort (30 members) → PromoteCount=7, DemoteCount=0, StayCount=23 (bottom 5 Stayed at Bronze)")]
    public void LS5_BronzeCohort_BottomStayBronzeTopPromote()
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize: 30, currentTier: LeagueTier.Bronze);

        plan.PromoteCount.Should().Be(7, "top 7 promote to Silver");
        plan.DemoteCount.Should().Be(0, "Bronze is the bottom tier — nowhere down, DemoteCount=0");
        plan.StayCount.Should().Be(23, "23 = 30 - 7 - 0 stay (includes bottom 5 who would normally demote)");
    }

    [Fact(DisplayName = "LS5b Bronze Apply: top 7 Promoted to Silver, bottom 5 Status=Stayed TierAfter=Bronze")]
    public void LS5b_BronzeApply_CorrectStatuses()
    {
        var members = CreateMemberships(30, startXp: 300, xpStep: -10);
        var results = LeagueStandings.Apply(members, LeagueTier.Bronze);

        results.Should().HaveCount(30);

        // Top 7 (rank 1..7): promoted to Silver
        for (int i = 0; i < 7; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Promoted,
                $"rank {i + 1} at Bronze — promoted to Silver");
            results[i].TierAfter.Should().Be(LeagueTier.Silver,
                $"rank {i + 1} moves from Bronze to Silver");
        }

        // Bottom 5 (rank 26..30): stayed at Bronze
        for (int i = 25; i < 30; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Stayed,
                $"rank {i + 1} at Bronze — stayed (nowhere down)");
            results[i].TierAfter.Should().Be(LeagueTier.Bronze,
                $"rank {i + 1} stays in Bronze");
        }
    }

    // =========================================================================
    // LS6 — Cohort of exactly 1 → 1 entry, Stayed (degenerate case)
    // =========================================================================

    [Fact(DisplayName = "LS6 Cohort of exactly 1 member (Silver) → the sole member Stays")]
    public void LS6_SingleMemberCohort_Stays()
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize: 1, currentTier: LeagueTier.Silver);

        (plan.PromoteCount + plan.DemoteCount + plan.StayCount).Should().Be(1,
            "total must equal cohort size even for 1-member cohort");

        var members = CreateMemberships(1, startXp: 100, xpStep: 0);
        var results = LeagueStandings.Apply(members, LeagueTier.Silver);

        results.Should().HaveCount(1);
        results[0].FinalRank.Should().Be(1, "only member must have rank 1");
        results[0].Status.Should().Be(MembershipStatus.Stayed,
            "sole member cannot promote nor demote — stays");
        results[0].TierAfter.Should().Be(LeagueTier.Silver, "stays in Silver");
    }

    // =========================================================================
    // LS7 — Apply with ranked memberships → correct rank ordering + TierAfter assignment
    // =========================================================================

    [Fact(DisplayName = "LS7 Apply with pre-sorted memberships assigns rank 1..N and correct TierAfter per silver cohort rules")]
    public void LS7_Apply_RankOrderingAndTierAfter_Silver()
    {
        // 30 members, Silver, rank 1 has highest XP
        var members = CreateMemberships(30, startXp: 3000, xpStep: -10);
        var results = LeagueStandings.Apply(members, LeagueTier.Silver);

        results.Should().HaveCount(30);

        // Ranks must be 1..30 in order
        for (int i = 0; i < 30; i++)
            results[i].FinalRank.Should().Be(i + 1, $"rank at index {i} must be {i + 1}");

        // Rank 1..7 → Promoted to Gold
        for (int i = 0; i < 7; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Promoted, $"rank {i + 1} promoted");
            results[i].TierAfter.Should().Be(LeagueTier.Gold, $"rank {i + 1} goes to Gold");
        }

        // Rank 8..25 → Stayed in Silver
        for (int i = 7; i < 25; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Stayed, $"rank {i + 1} stayed");
            results[i].TierAfter.Should().Be(LeagueTier.Silver, $"rank {i + 1} stays Silver");
        }

        // Rank 26..30 → Demoted to Bronze
        for (int i = 25; i < 30; i++)
        {
            results[i].Status.Should().Be(MembershipStatus.Demoted, $"rank {i + 1} demoted");
            results[i].TierAfter.Should().Be(LeagueTier.Bronze, $"rank {i + 1} goes to Bronze");
        }
    }

    // =========================================================================
    // LS8 — Tiebreak: same WeeklyXp, earlier JoinedAtUtc gets better rank
    // =========================================================================

    [Fact(DisplayName = "LS8 Tiebreak: when two members have equal WeeklyXp, earlier JoinedAtUtc wins (gets lower rank number)")]
    public void LS8_Tiebreak_EarlierJoinedAtUtcWins()
    {
        // Two members with identical WeeklyXp; joiner A joined before joiner B.
        var baseTime = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);

        var memberA = CreateMembership(id: 1, weeklyXp: 100, joinedAt: baseTime.AddMinutes(5));  // joined later
        var memberB = CreateMembership(id: 2, weeklyXp: 100, joinedAt: baseTime.AddMinutes(1));  // joined earlier

        // Pre-sort as the job/query would do: WeeklyXp DESC, JoinedAtUtc ASC
        var sorted = new List<LeagueMembership> { memberB, memberA }; // B first (earlier join time)

        var results = LeagueStandings.Apply(sorted, LeagueTier.Silver);

        results.Should().HaveCount(2);
        // B joined earlier → rank 1
        results[0].MembershipId.Should().Be(memberB.Id, "earlier joiner (B) should rank higher (rank 1)");
        results[0].FinalRank.Should().Be(1);
        // A joined later → rank 2
        results[1].MembershipId.Should().Be(memberA.Id, "later joiner (A) should rank lower (rank 2)");
        results[1].FinalRank.Should().Be(2);
    }

    // =========================================================================
    // LS9 — Invariant: PromoteCount + DemoteCount + StayCount == cohort size always
    // =========================================================================

    [Theory(DisplayName = "LS9 Invariant: PromoteCount + DemoteCount + StayCount == cohortSize for any input")]
    [InlineData(1,  LeagueTier.Silver)]
    [InlineData(2,  LeagueTier.Silver)]
    [InlineData(3,  LeagueTier.Silver)]
    [InlineData(5,  LeagueTier.Gold)]
    [InlineData(10, LeagueTier.Bronze)]
    [InlineData(11, LeagueTier.Diamond)]
    [InlineData(12, LeagueTier.Silver)]
    [InlineData(15, LeagueTier.Gold)]
    [InlineData(20, LeagueTier.Silver)]
    [InlineData(30, LeagueTier.Silver)]
    [InlineData(30, LeagueTier.Bronze)]
    [InlineData(30, LeagueTier.Gold)]
    [InlineData(30, LeagueTier.Diamond)]
    public void LS9_Invariant_TotalEqualsSize(int cohortSize, LeagueTier tier)
    {
        var plan = LeagueStandings.ComputeCutoffs(cohortSize, tier);

        (plan.PromoteCount + plan.DemoteCount + plan.StayCount).Should().Be(cohortSize,
            $"PromoteCount + DemoteCount + StayCount must always equal cohortSize " +
            $"(cohortSize={cohortSize}, tier={tier})");
    }

    // =========================================================================
    // Helper builders
    // =========================================================================

    /// <summary>
    /// Creates N stub <see cref="LeagueMembership"/> objects already ordered by WeeklyXp DESC.
    /// Uses reflection to bypass the EF private constructor since LeagueMembership requires
    /// a tracked League + Profile graph. We do not need persistence — just in-memory objects
    /// for the pure-function <see cref="LeagueStandings.Apply"/> call.
    /// </summary>
    private static List<LeagueMembership> CreateMemberships(
        int count, int startXp, int xpStep,
        DateTime? baseJoinedAt = null)
    {
        var list = new List<LeagueMembership>(count);
        var joinBase = baseJoinedAt ?? new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < count; i++)
        {
            list.Add(CreateMembership(
                id:       i + 1,
                weeklyXp: startXp + (xpStep * i),
                joinedAt: joinBase.AddMinutes(i)));
        }

        return list;
    }

    /// <summary>
    /// Builds a single <see cref="LeagueMembership"/> stub via reflection to bypass
    /// the private constructor (pure domain-service unit tests only — no DB involved).
    /// </summary>
    private static LeagueMembership CreateMembership(int id, int weeklyXp, DateTime joinedAt)
    {
        // Use FormatterServices / reflection to construct without calling private ctor.
        var m = (LeagueMembership)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(LeagueMembership));

        // Set backing fields via reflection (LeagueMembership uses auto-properties with private/internal setters).
        var type = typeof(LeagueMembership);

        SetBackingField(m, type, "Id", id);
        SetBackingField(m, type, "WeeklyXp", weeklyXp);
        SetBackingField(m, type, "JoinedAtUtc", joinedAt);
        SetBackingField(m, type, "Status", MembershipStatus.Active);
        SetBackingField(m, type, "JoinOrder", id);
        SetBackingField(m, type, "PeriodKey", "W:2026-23");

        return m;
    }

    private static void SetBackingField(object obj, Type type, string propertyName, object value)
    {
        // EF Core entities generated with auto-properties have a backing field pattern.
        // Try property set first (internal set), then backing field.
        var prop = type.GetProperty(propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        if (prop is not null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return;
        }

        // Find the backing field: "_xxx" or "<Xxx>k__BackingField"
        var fieldName = $"<{propertyName}>k__BackingField";
        var field = type.GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field is not null)
        {
            field.SetValue(obj, value);
            return;
        }

        // As a last resort, scan base types
        var baseType = type.BaseType;
        while (baseType is not null)
        {
            field = baseType.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field is not null)
            {
                field.SetValue(obj, value);
                return;
            }
            baseType = baseType.BaseType;
        }

        throw new InvalidOperationException(
            $"Cannot set field/property '{propertyName}' on type '{type.FullName}'");
    }
}

using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Gamification;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Compile-time parity assertions for the <see cref="LeagueTier"/> domain enum and its
/// cross-module mirror <see cref="LeagueTierDto"/> introduced in P4-07.
///
/// Cross-module cast pattern used throughout P4-07:
///   <c>(LeagueTierDto)(int)leagueTier</c>  — used in <see cref="StudentLeagueQuery"/>
///   <c>(LeagueTier)(int)leagueTierDto</c>  — used in rollover projections
///
/// If either enum is edited without updating the other, these assertions fail at runtime,
/// surfacing the drift before it reaches production.
///
/// Mirrors <see cref="MissionEnumDriftTests"/> (P4-06) and
/// <see cref="BadgePredicateEvaluatorTests"/> BadgeRarityDto drift check (P4-05).
///
/// Coverage map (4 value assertions — one per tier):
///   ED1  Bronze=1   in both LeagueTier and LeagueTierDto
///   ED2  Silver=2   in both LeagueTier and LeagueTierDto
///   ED3  Gold=3     in both LeagueTier and LeagueTierDto
///   ED4  Diamond=4  in both LeagueTier and LeagueTierDto
/// </summary>
public sealed class LeagueTierEnumDriftTests
{
    [Fact(DisplayName = "ED1 (int)LeagueTier.Bronze == (int)LeagueTierDto.Bronze == 1")]
    public void Bronze_ValueEquals1_InBothEnums()
    {
        ((int)LeagueTier.Bronze).Should().Be(1,
            "LeagueTier.Bronze must be 1 (D6 locked decision)");
        ((int)LeagueTierDto.Bronze).Should().Be(1,
            "LeagueTierDto.Bronze must be 1 (cross-module cast parity)");
        ((int)LeagueTier.Bronze).Should().Be((int)LeagueTierDto.Bronze,
            "LeagueTier.Bronze == LeagueTierDto.Bronze — cross-module cast safety");
    }

    [Fact(DisplayName = "ED2 (int)LeagueTier.Silver == (int)LeagueTierDto.Silver == 2")]
    public void Silver_ValueEquals2_InBothEnums()
    {
        ((int)LeagueTier.Silver).Should().Be(2,
            "LeagueTier.Silver must be 2 (D6 locked decision)");
        ((int)LeagueTierDto.Silver).Should().Be(2,
            "LeagueTierDto.Silver must be 2 (cross-module cast parity)");
        ((int)LeagueTier.Silver).Should().Be((int)LeagueTierDto.Silver,
            "LeagueTier.Silver == LeagueTierDto.Silver — cross-module cast safety");
    }

    [Fact(DisplayName = "ED3 (int)LeagueTier.Gold == (int)LeagueTierDto.Gold == 3")]
    public void Gold_ValueEquals3_InBothEnums()
    {
        ((int)LeagueTier.Gold).Should().Be(3,
            "LeagueTier.Gold must be 3 (D6 locked decision)");
        ((int)LeagueTierDto.Gold).Should().Be(3,
            "LeagueTierDto.Gold must be 3 (cross-module cast parity)");
        ((int)LeagueTier.Gold).Should().Be((int)LeagueTierDto.Gold,
            "LeagueTier.Gold == LeagueTierDto.Gold — cross-module cast safety");
    }

    [Fact(DisplayName = "ED4 (int)LeagueTier.Diamond == (int)LeagueTierDto.Diamond == 4")]
    public void Diamond_ValueEquals4_InBothEnums()
    {
        ((int)LeagueTier.Diamond).Should().Be(4,
            "LeagueTier.Diamond must be 4 (D6 locked decision)");
        ((int)LeagueTierDto.Diamond).Should().Be(4,
            "LeagueTierDto.Diamond must be 4 (cross-module cast parity)");
        ((int)LeagueTier.Diamond).Should().Be((int)LeagueTierDto.Diamond,
            "LeagueTier.Diamond == LeagueTierDto.Diamond — cross-module cast safety");
    }
}

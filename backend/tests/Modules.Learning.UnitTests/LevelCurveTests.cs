using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LevelCurve"/>.
///
/// Pure static function — no DbContext required. All cases are deterministic.
///
/// Level thresholds (cumulative XP):
///   L1: 0      L2: 100    L3: 250    L4: 500    L5: 1000
///   L6: 2000   L7: 4000   L8: 7000   L9: 11000  L10: 16000
///   L11+: 16000 + (level-10)*5000   e.g. L11=21000, L12=26000
///
/// Coverage map (all cases from the P4-02 Batch-4 spec):
///   LevelForXp:
///     U01  LevelForXp(-1) → ArgumentOutOfRangeException
///     U02  LevelForXp(0)  → 1
///     U03  LevelForXp(99) → 1
///     U04  LevelForXp(100) → 2  (exactly on L2 threshold)
///     U05  LevelForXp(249) → 2
///     U06  LevelForXp(250) → 3  (exactly on L3 threshold)
///     U07  LevelForXp(499) → 3
///     U08  LevelForXp(500) → 4  (exactly on L4 threshold)
///     U09  LevelForXp(999) → 4
///     U10  LevelForXp(1000) → 5
///     U11  LevelForXp(1999) → 5
///     U12  LevelForXp(2000) → 6
///     U13  LevelForXp(3999) → 6
///     U14  LevelForXp(4000) → 7
///     U15  LevelForXp(6999) → 7
///     U16  LevelForXp(7000) → 8
///     U17  LevelForXp(10999) → 8
///     U18  LevelForXp(11000) → 9
///     U19  LevelForXp(15999) → 9
///     U20  LevelForXp(16000) → 10 (exactly on L10 threshold)
///     U21  LevelForXp(16001) → 10 (L11+ extension starts strictly above 16000+5000=21000)
///     U22  LevelForXp(20999) → 10
///     U23  LevelForXp(21000) → 11 (exactly L11: 16000 + 5000)
///     U24  LevelForXp(26000) → 12 (exactly L12: 16000 + 2*5000)
///     U25  LevelForXp(100000) → 26 ((100000-16000)/5000 + 10 = 84000/5000 + 10 = 16+10 = 26)
///   XpForLevel:
///     U26  XpForLevel(0)  → ArgumentOutOfRangeException
///     U27  XpForLevel(1)  → 0
///     U28  XpForLevel(2)  → 100
///     U29  XpForLevel(11) → 21000
///   Compute:
///     U30  Compute(0)     → (1, 0, 100)  [L1: floor=0, next=100, remaining=100]
///     U31  Compute(150)   → (2, 50, 100) [L2: floor=100, into=50, remaining=250-150=100]
///     U32  Compute(16000) → (10, 0, 5000) [L10: floor=16000, remaining=21000-16000=5000]
/// </summary>
public sealed class LevelCurveTests
{
    // ==========================================================================
    // LevelForXp — negative input
    // ==========================================================================

    [Fact(DisplayName = "P402-U01 LevelForXp(-1) → ArgumentOutOfRangeException")]
    public void LevelForXp_Negative_Throws()
    {
        var act = () => LevelCurve.LevelForXp(-1);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "XP cannot be negative — the spec requires an ArgumentOutOfRangeException for negative input.");
    }

    // ==========================================================================
    // LevelForXp — L1 boundaries
    // ==========================================================================

    [Fact(DisplayName = "P402-U02 LevelForXp(0) → 1 (L1 floor)")]
    public void LevelForXp_Zero_ReturnsLevel1()
        => LevelCurve.LevelForXp(0).Should().Be(1, "0 XP is the L1 floor");

    [Fact(DisplayName = "P402-U03 LevelForXp(99) → 1 (below L2 threshold)")]
    public void LevelForXp_99_ReturnsLevel1()
        => LevelCurve.LevelForXp(99).Should().Be(1, "99 XP is below the L2 threshold of 100");

    // ==========================================================================
    // LevelForXp — L2 boundaries (threshold = 100)
    // ==========================================================================

    [Fact(DisplayName = "P402-U04 LevelForXp(100) → 2 (exactly on L2 threshold)")]
    public void LevelForXp_100_ReturnsLevel2()
        => LevelCurve.LevelForXp(100).Should().Be(2, "100 XP is exactly the L2 threshold");

    [Fact(DisplayName = "P402-U05 LevelForXp(249) → 2 (below L3 threshold)")]
    public void LevelForXp_249_ReturnsLevel2()
        => LevelCurve.LevelForXp(249).Should().Be(2, "249 XP is below the L3 threshold of 250");

    // ==========================================================================
    // LevelForXp — L3 boundaries (threshold = 250)
    // ==========================================================================

    [Fact(DisplayName = "P402-U06 LevelForXp(250) → 3 (exactly on L3 threshold)")]
    public void LevelForXp_250_ReturnsLevel3()
        => LevelCurve.LevelForXp(250).Should().Be(3, "250 XP is exactly the L3 threshold");

    [Fact(DisplayName = "P402-U07 LevelForXp(499) → 3 (below L4 threshold)")]
    public void LevelForXp_499_ReturnsLevel3()
        => LevelCurve.LevelForXp(499).Should().Be(3, "499 XP is below the L4 threshold of 500");

    // ==========================================================================
    // LevelForXp — L4 through L10 exact thresholds
    // ==========================================================================

    [Theory(DisplayName = "P402-U08..U20 LevelForXp — exact thresholds L4→L10")]
    [InlineData(500,   4,  "L4 threshold = 500")]
    [InlineData(1000,  5,  "L5 threshold = 1000")]
    [InlineData(2000,  6,  "L6 threshold = 2000")]
    [InlineData(4000,  7,  "L7 threshold = 4000")]
    [InlineData(7000,  8,  "L8 threshold = 7000")]
    [InlineData(11000, 9,  "L9 threshold = 11000")]
    [InlineData(16000, 10, "L10 threshold = 16000")]
    public void LevelForXp_ExactThresholds_L4_to_L10(int xp, int expectedLevel, string reason)
        => LevelCurve.LevelForXp(xp).Should().Be(expectedLevel, reason);

    // ==========================================================================
    // LevelForXp — one-below exact thresholds (L4→L9)
    // ==========================================================================

    [Theory(DisplayName = "P402-U08b..U19b LevelForXp — one-below each threshold L4→L10")]
    [InlineData(999,   4,  "999 XP is below L5 threshold of 1000")]
    [InlineData(1999,  5,  "1999 XP is below L6 threshold of 2000")]
    [InlineData(3999,  6,  "3999 XP is below L7 threshold of 4000")]
    [InlineData(6999,  7,  "6999 XP is below L8 threshold of 7000")]
    [InlineData(10999, 8,  "10999 XP is below L9 threshold of 11000")]
    [InlineData(15999, 9,  "15999 XP is below L10 threshold of 16000")]
    public void LevelForXp_OneBelowThresholds_L4_to_L10(int xp, int expectedLevel, string reason)
        => LevelCurve.LevelForXp(xp).Should().Be(expectedLevel, reason);

    // ==========================================================================
    // LevelForXp — L11+ extension (linear +5000 per level above L10)
    // ==========================================================================

    [Fact(DisplayName = "P402-U21 LevelForXp(16001) → 10 (L11 band starts at 21000)")]
    public void LevelForXp_16001_ReturnsLevel10()
        => LevelCurve.LevelForXp(16001).Should().Be(10,
            "16001 XP is inside the L10 band (16000–20999); L11 starts at 21000");

    [Fact(DisplayName = "P402-U22 LevelForXp(20999) → 10 (top of L10 band, one below L11)")]
    public void LevelForXp_20999_ReturnsLevel10()
        => LevelCurve.LevelForXp(20999).Should().Be(10,
            "20999 XP is the highest value still at L10; L11 starts at exactly 21000");

    [Fact(DisplayName = "P402-U23 LevelForXp(21000) → 11 (exactly L11: 16000 + 5000)")]
    public void LevelForXp_21000_ReturnsLevel11()
        => LevelCurve.LevelForXp(21000).Should().Be(11,
            "21000 XP = 16000 + 1*5000 → L11");

    [Fact(DisplayName = "P402-U24 LevelForXp(26000) → 12 (exactly L12: 16000 + 2*5000)")]
    public void LevelForXp_26000_ReturnsLevel12()
        => LevelCurve.LevelForXp(26000).Should().Be(12,
            "26000 XP = 16000 + 2*5000 → L12");

    [Fact(DisplayName = "P402-U25 LevelForXp(100000) → 26 ((100000-16000)/5000 + 10 = 16+10 = 26)")]
    public void LevelForXp_100000_ReturnsLevel26()
        => LevelCurve.LevelForXp(100000).Should().Be(26,
            "(100000 - 16000) / 5000 = 84000 / 5000 = 16 (floor) → level = 16 + 10 = 26");

    // ==========================================================================
    // XpForLevel — boundary cases
    // ==========================================================================

    [Fact(DisplayName = "P402-U26 XpForLevel(0) → ArgumentOutOfRangeException")]
    public void XpForLevel_Zero_Throws()
    {
        var act = () => LevelCurve.XpForLevel(0);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "Level 0 is invalid — levels are 1-based.");
    }

    [Fact(DisplayName = "P402-U27 XpForLevel(1) → 0")]
    public void XpForLevel_1_Returns0()
        => LevelCurve.XpForLevel(1).Should().Be(0, "Level 1 requires 0 cumulative XP");

    [Fact(DisplayName = "P402-U28 XpForLevel(2) → 100")]
    public void XpForLevel_2_Returns100()
        => LevelCurve.XpForLevel(2).Should().Be(100, "Level 2 requires 100 cumulative XP");

    [Fact(DisplayName = "P402-U29 XpForLevel(11) → 21000")]
    public void XpForLevel_11_Returns21000()
        => LevelCurve.XpForLevel(11).Should().Be(21000, "Level 11 requires 16000 + 5000 = 21000 cumulative XP");

    // ==========================================================================
    // Compute — progress tuple
    //
    // XpToNext semantics (fixed in P4-02 fix-up Bug 2):
    //   XpToNext = XpForLevel(currentLevel + 1) - totalXp
    //   This is the REMAINING XP to reach the next level, NOT the band width.
    //
    //   Compute(0)     → (1,  0, 100)   [need 100 more XP to reach L2]
    //   Compute(150)   → (2, 50, 100)   [250 − 150 = 100 remaining to L3]
    //   Compute(16000) → (10, 0, 5000)  [21000 − 16000 = 5000 remaining to L11]
    // ==========================================================================

    [Fact(DisplayName = "P402-U30 Compute(0) → (Level=1, XpIntoLevel=0, XpToNext=100)")]
    public void Compute_0_ReturnsLevel1ZeroProgress()
    {
        var (level, xpInto, xpToNext) = LevelCurve.Compute(0);
        level.Should().Be(1, "0 XP is L1");
        xpInto.Should().Be(0, "no XP earned into this level yet");
        xpToNext.Should().Be(100, "L1 band width = 100 (threshold[1]-threshold[0] = 100-0)");
    }

    [Fact(DisplayName = "P402-U31 Compute(150) → (Level=2, XpIntoLevel=50, XpToNext=100) [remaining XP to next level]")]
    public void Compute_150_ReturnsLevel2With50Into()
    {
        var (level, xpInto, xpToNext) = LevelCurve.Compute(150);
        level.Should().Be(2, "150 XP is L2 (100–249)");
        xpInto.Should().Be(50, "150 - 100 = 50 XP into L2");
        xpToNext.Should().Be(100,
            "XpToNext is the remaining XP to reach the next level: XpForLevel(3) - 150 = 250 - 150 = 100. " +
            "Fixed from the original band-width implementation (P4-02 fix-up Bug 2).");
    }

    [Fact(DisplayName = "P402-U32 Compute(16000) → (Level=10, XpIntoLevel=0, XpToNext=5000)")]
    public void Compute_16000_ReturnsLevel10ZeroProgress()
    {
        var (level, xpInto, xpToNext) = LevelCurve.Compute(16000);
        level.Should().Be(10, "16000 XP is exactly the L10 threshold");
        xpInto.Should().Be(0, "0 XP into L10 (we are exactly at the floor)");
        xpToNext.Should().Be(5000, "L10→L11 band width = 21000 - 16000 = 5000");
    }
}

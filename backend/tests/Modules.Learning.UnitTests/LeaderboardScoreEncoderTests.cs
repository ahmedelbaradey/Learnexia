using FluentAssertions;
using Learnexia.Modules.Gamification.Application.Caching;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LeaderboardScoreEncoder"/> — P4-10 Batch 4.
///
/// Pure static function — no DI or DB required.
///
/// Encoding formula (locked Q5):
///   score = (long)weeklyXp * (1 &lt;&lt; 24) + ((1 &lt;&lt; 24) - 1 - joinOrder)
///
/// Properties to verify:
///   1. Higher XP → higher score (ZREVRANK ranks higher = rank 1 = best).
///   2. Equal XP, lower JoinOrder → higher score (earlier joiner wins tie).
///   3. Round-trip: Decode(Encode(xp, jo)) == (xp, jo).
///   4. Boundary: Encode(0, 0) > 0.
///   5. Negative XP → ArgumentOutOfRangeException.
///   6. Negative joinOrder → ArgumentOutOfRangeException.
///
/// Coverage map:
///   U01  Encode(0, 0) > 0  (minimum score is positive)
///   U02  Encode(1, 0) > Encode(0, 0)  (any XP outranks zero XP)
///   U03  Encode(200, 5) > Encode(100, 1)  (higher XP always wins regardless of joinOrder)
///   U04  Encode(100, 1) > Encode(100, 2)  (equal XP, lower joinOrder wins)
///   U05  Encode(100, 0) > Encode(100, 1)  (joinOrder=0 outranks joinOrder=1)
///   U06  Round-trip Decode(Encode(0, 0)) == (0, 0)
///   U07  Round-trip Decode(Encode(500, 10)) == (500, 10)
///   U08  Round-trip Decode(Encode(1, 16777215)) == (1, 16777215)  (max joinOrder)
///   U09  Negative weeklyXp → ArgumentOutOfRangeException
///   U10  Negative joinOrder → ArgumentOutOfRangeException
///   U11  joinOrder > 16777215 (2^24) → ArgumentOutOfRangeException
///   U12  Double precision safe: Encode(10_000_000, 0) fits in long without overflow
/// </summary>
public sealed class LeaderboardScoreEncoderTests
{
    // ──────────────────────────────────────────────────────────────────────────────────
    // U01 — Encode(0, 0) > 0
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U01 Encode(0, 0) > 0 — minimum score is positive")]
    public void U01_Encode_ZeroXp_ZeroJoinOrder_IsPositive()
    {
        var score = LeaderboardScoreEncoder.Encode(0, 0);
        score.Should().BeGreaterThan(0,
            "Encode(0, 0) should yield a positive score because joinOrder=0 inverts to (2^24 - 1 - 0) which is the tiebreak headroom");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U02 — Any XP outranks zero XP
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U02 Encode(1, 0) > Encode(0, 0) — any positive XP outranks zero XP")]
    public void U02_HigherXp_AlwaysOutranks_LowerXp()
    {
        var scoreWith1Xp  = LeaderboardScoreEncoder.Encode(1, 0);
        var scoreWith0Xp  = LeaderboardScoreEncoder.Encode(0, 0);
        scoreWith1Xp.Should().BeGreaterThan(scoreWith0Xp,
            "1 XP must rank higher than 0 XP regardless of joinOrder");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U03 — Higher XP always wins, regardless of joinOrder
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U03 Encode(200, 5) > Encode(100, 1) — higher XP dominates joinOrder")]
    public void U03_HigherXp_DominatesJoinOrder()
    {
        var scoreA = LeaderboardScoreEncoder.Encode(200, 5);
        var scoreB = LeaderboardScoreEncoder.Encode(100, 1);
        scoreA.Should().BeGreaterThan(scoreB,
            "200 XP (joinOrder=5) must rank higher than 100 XP (joinOrder=1); XP dominates joinOrder");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U04 — Equal XP, lower joinOrder wins tie
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U04 Equal XP + lower joinOrder → higher score (earlier joiner wins)")]
    public void U04_EqualXp_LowerJoinOrder_Wins()
    {
        var scoreEarlier = LeaderboardScoreEncoder.Encode(100, 1);
        var scoreLater   = LeaderboardScoreEncoder.Encode(100, 2);
        scoreEarlier.Should().BeGreaterThan(scoreLater,
            "with equal XP, joinOrder=1 (earlier joiner) must score higher than joinOrder=2 (later joiner)");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U05 — joinOrder=0 outranks joinOrder=1 for same XP
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U05 Equal XP: joinOrder=0 > joinOrder=1 (earliest possible joiner wins)")]
    public void U05_JoinOrder0_OutranksJoinOrder1()
    {
        var scoreFirst  = LeaderboardScoreEncoder.Encode(50, 0);
        var scoreSecond = LeaderboardScoreEncoder.Encode(50, 1);
        scoreFirst.Should().BeGreaterThan(scoreSecond,
            "joinOrder=0 is the earliest possible entry; it should have a higher tiebreak score than joinOrder=1");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U06 — Round-trip: Decode(Encode(0, 0)) == (0, 0)
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U06 Round-trip Decode(Encode(0, 0)) == (0, 0)")]
    public void U06_RoundTrip_ZeroZero()
    {
        var encoded = LeaderboardScoreEncoder.Encode(0, 0);
        var (xp, joinOrder) = LeaderboardScoreEncoder.Decode(encoded);
        xp.Should().Be(0, "decoded weeklyXp must equal original 0");
        joinOrder.Should().Be(0, "decoded joinOrder must equal original 0");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U07 — Round-trip realistic values: Decode(Encode(500, 10)) == (500, 10)
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U07 Round-trip Decode(Encode(500, 10)) == (500, 10)")]
    public void U07_RoundTrip_RealisticValues()
    {
        var encoded = LeaderboardScoreEncoder.Encode(500, 10);
        var (xp, joinOrder) = LeaderboardScoreEncoder.Decode(encoded);
        xp.Should().Be(500, "decoded weeklyXp must be 500");
        joinOrder.Should().Be(10, "decoded joinOrder must be 10");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U08 — Round-trip at max joinOrder: Decode(Encode(1, 16777215)) == (1, 16777215)
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U08 Round-trip at max joinOrder (16777215 = 2^24 - 1)")]
    public void U08_RoundTrip_MaxJoinOrder()
    {
        const int maxJoinOrder = (1 << 24) - 1; // 16777215
        var encoded = LeaderboardScoreEncoder.Encode(1, maxJoinOrder);
        var (xp, joinOrder) = LeaderboardScoreEncoder.Decode(encoded);
        xp.Should().Be(1, "decoded weeklyXp must be 1");
        joinOrder.Should().Be(maxJoinOrder, "decoded joinOrder must equal max joinOrder");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U09 — Negative weeklyXp → ArgumentOutOfRangeException
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U09 Encode(-1, 0) → ArgumentOutOfRangeException")]
    public void U09_NegativeXp_ThrowsArgumentOutOfRange()
    {
        var act = () => LeaderboardScoreEncoder.Encode(-1, 0);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "negative weeklyXp must be rejected — XP is always non-negative");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U10 — Negative joinOrder → ArgumentOutOfRangeException
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U10 Encode(0, -1) → ArgumentOutOfRangeException")]
    public void U10_NegativeJoinOrder_ThrowsArgumentOutOfRange()
    {
        var act = () => LeaderboardScoreEncoder.Encode(0, -1);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "negative joinOrder must be rejected");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U11 — joinOrder exceeding 2^24 → ArgumentOutOfRangeException
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U11 Encode(0, 16777216) → ArgumentOutOfRangeException (> 2^24-1)")]
    public void U11_JoinOrderExceedsMax_ThrowsArgumentOutOfRange()
    {
        const int overMaxJoinOrder = 1 << 24; // 16777216 — one more than allowed
        var act = () => LeaderboardScoreEncoder.Encode(0, overMaxJoinOrder);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "joinOrder exceeding 2^24 - 1 must be rejected; the 24-bit field cannot represent it");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U12 — Double-precision safety for large realistic XP values
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U12 Large XP (10_000_000) encodes without overflow, round-trips correctly")]
    public void U12_LargeXp_DoublePrecisionSafe()
    {
        const int largeXp = 10_000_000;
        const int joinOrder = 0;

        var encoded = LeaderboardScoreEncoder.Encode(largeXp, joinOrder);
        encoded.Should().BeGreaterThan(0, "encoded score must be positive for large XP");

        // Double cast (for Redis storage) must not lose precision.
        var asDouble = (double)encoded;
        var backToLong = (long)asDouble;
        backToLong.Should().Be(encoded,
            "the score must survive a round-trip through double (Redis stores scores as doubles); " +
            "53-bit mantissa of double must cover the encoded value without precision loss");

        // Round-trip through Decode.
        var (xp, jo) = LeaderboardScoreEncoder.Decode(encoded);
        xp.Should().Be(largeXp, "decoded weeklyXp must equal original");
        jo.Should().Be(joinOrder, "decoded joinOrder must equal original");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // U13 — Ordering invariant: strictly decreasing scores for increasing joinOrder at same XP
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "LeaderboardScoreEncoder-U13 Scores strictly decrease as joinOrder increases (same XP = tiebreak ladder)")]
    public void U13_Tiebreak_StrictlyDecreasingScores()
    {
        const int xp = 100;
        var scores = Enumerable.Range(0, 10)
            .Select(i => LeaderboardScoreEncoder.Encode(xp, i))
            .ToList();

        for (int i = 1; i < scores.Count; i++)
        {
            scores[i - 1].Should().BeGreaterThan(scores[i],
                "score for joinOrder={0} must be greater than score for joinOrder={1} at same XP={2}",
                i - 1, i, xp);
        }
    }
}

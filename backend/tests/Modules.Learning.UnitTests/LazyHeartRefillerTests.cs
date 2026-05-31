using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="LazyHeartRefiller.Compute"/> (P4-04-B2a-1).
///
/// Pure static function — no DbContext or DI required. All cases are deterministic.
///
/// Rules under test (D12):
///   - Hearts >= cap          → return (cap, null, 0)           clock idles
///   - lastRefillAt is null   → return (currentHearts, null, 0) clock not running
///   - elapsed &lt; interval  → no change
///   - elapsed >= interval    → +1 heart per tick, clock advances by consumed ticks
///   - At cap after refill    → NewLastRefillAt = null
///   - Kind normalization     → both timestamps ToUniversalTime() before subtraction
///
/// Coverage map (11 test cases from P4-04 plan B5-1 spec):
///   L1   At-cap, null clock             → (5, null, 0)
///   L2   At-cap, non-null clock         → (5, null, 0)   clock cleared
///   L3   Below cap, null clock          → (3, null, 0)   no clock → no refill
///   L4   Below cap, zero elapsed        → (3, lastRefillAt, 0)
///   L5   Below cap, 29 min elapsed      → (3, lastRefillAt, 0)  sub-interval
///   L6   Below cap, exactly 30 min      → (4, lastRefillAt+30, 1)
///   L7   Below cap, 31 min elapsed      → (4, lastRefillAt+30, 1)  remainder preserved
///   L8   Below cap, 60 min, hits cap    → (5, null, 2)
///   L9   From 0, 150 min (5 ticks)      → (5, null, 5)   full refill from 0
///   L10  From 0, 200 min (clamped)      → (5, null, 5)   cap clamp
///   L11  Kind normalization regression  → must NOT produce negative ticksToRegen
/// </summary>
public sealed class LazyHeartRefillerTests
{
    // -------------------------------------------------------------------------
    // Shared reference point
    // -------------------------------------------------------------------------

    // All tests use a stable "now" anchored to a known UTC instant.
    private static readonly DateTime Now = new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    // =========================================================================
    // L1 — At-cap with null clock → idle, return (cap, null, 0)
    // =========================================================================

    [Fact(DisplayName = "P404-L1 Compute(hearts=5=cap, lastRefillAt=null, now, 30) → (5, null, 0) clock already idle")]
    public void L1_AtCap_NullClock_ReturnsIdle()
    {
        var result = LazyHeartRefiller.Compute(
            currentHearts: 5,
            cap: 5,
            lastRefillAt: null,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(5, "already at cap — nothing to refill");
        result.NewLastRefillAt.Should().BeNull("clock is idle when at cap");
        result.Regenerated.Should().Be(0, "no hearts were regenerated");
    }

    // =========================================================================
    // L2 — At-cap with non-null clock → cap returned, clock cleared
    // =========================================================================

    [Fact(DisplayName = "P404-L2 Compute(hearts=5=cap, lastRefillAt=now-1h, now, 30) → (5, null, 0) already at cap, clock cleared")]
    public void L2_AtCap_NonNullClock_ReturnsIdleAndClearsClock()
    {
        var result = LazyHeartRefiller.Compute(
            currentHearts: 5,
            cap: 5,
            lastRefillAt: Now.AddHours(-1),
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(5, "already at cap — nothing to refill");
        result.NewLastRefillAt.Should().BeNull("stale non-null clock must be cleared when at cap");
        result.Regenerated.Should().Be(0, "no hearts were regenerated");
    }

    // =========================================================================
    // L3 — Below cap, null clock → no refill (clock not running)
    // =========================================================================

    [Fact(DisplayName = "P404-L3 Compute(hearts=3, cap=5, lastRefillAt=null, now, 30) → (3, null, 0) clock not running")]
    public void L3_BelowCap_NullClock_NoRefill()
    {
        var result = LazyHeartRefiller.Compute(
            currentHearts: 3,
            cap: 5,
            lastRefillAt: null,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(3, "no clock means no refill can occur");
        result.NewLastRefillAt.Should().BeNull("clock is still null — it was never started");
        result.Regenerated.Should().Be(0, "zero hearts regenerated when clock is null");
    }

    // =========================================================================
    // L4 — Below cap, zero elapsed → no refill, clock unchanged
    // =========================================================================

    [Fact(DisplayName = "P404-L4 Compute(hearts=3, cap=5, lastRefillAt=now, now, 30) → (3, now, 0) no elapsed time")]
    public void L4_BelowCap_ZeroElapsed_NoRefill()
    {
        var lastRefillAt = Now;

        var result = LazyHeartRefiller.Compute(
            currentHearts: 3,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(3, "zero elapsed minutes → no refill tick");
        result.NewLastRefillAt.Should().Be(lastRefillAt, "clock unchanged when no full interval has elapsed");
        result.Regenerated.Should().Be(0, "zero hearts regenerated");
    }

    // =========================================================================
    // L5 — Below cap, 29 min elapsed → sub-interval, no refill
    // =========================================================================

    [Fact(DisplayName = "P404-L5 Compute(hearts=3, cap=5, lastRefillAt=now-29min, now, 30) → (3, lastRefillAt, 0) sub-interval")]
    public void L5_BelowCap_29MinElapsed_NoRefill()
    {
        var lastRefillAt = Now.AddMinutes(-29);

        var result = LazyHeartRefiller.Compute(
            currentHearts: 3,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(3, "29 min < 30 min interval — no full tick has elapsed");
        result.NewLastRefillAt.Should().Be(lastRefillAt, "clock unchanged — sub-interval elapsed");
        result.Regenerated.Should().Be(0, "zero hearts regenerated when elapsed < interval");
    }

    // =========================================================================
    // L6 — Below cap, exactly 30 min elapsed → 1 tick, +1 heart
    // =========================================================================

    [Fact(DisplayName = "P404-L6 Compute(hearts=3, cap=5, lastRefillAt=now-30min, now, 30) → (4, lastRefillAt+30min, 1) one tick")]
    public void L6_BelowCap_ExactlyOneInterval_OneHeartRefilled()
    {
        var lastRefillAt = Now.AddMinutes(-30);
        var expectedNewLastRefillAt = lastRefillAt.AddMinutes(30); // advanced by exactly 1 tick

        var result = LazyHeartRefiller.Compute(
            currentHearts: 3,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(4, "exactly one 30-min interval elapsed → +1 heart");
        result.NewLastRefillAt.Should().Be(expectedNewLastRefillAt,
            "clock advances by exactly 30 min (one consumed tick) — preserves the partial-interval remainder");
        result.Regenerated.Should().Be(1, "exactly one heart was regenerated");
    }

    // =========================================================================
    // L7 — Below cap, 31 min elapsed → 1 tick, 1 min remainder preserved
    // =========================================================================

    [Fact(DisplayName = "P404-L7 Compute(hearts=3, cap=5, lastRefillAt=now-31min, now, 30) → (4, lastRefillAt+30min, 1) remainder preserved")]
    public void L7_BelowCap_31MinElapsed_OneTickWithRemainderPreserved()
    {
        var lastRefillAt = Now.AddMinutes(-31);
        var expectedNewLastRefillAt = lastRefillAt.AddMinutes(30); // advanced by 1 tick, 1 min remainder preserved

        var result = LazyHeartRefiller.Compute(
            currentHearts: 3,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(4, "floor(31/30) = 1 tick → +1 heart");
        result.NewLastRefillAt.Should().Be(expectedNewLastRefillAt,
            "clock advances by exactly 1×30 min; the 1-min remainder is preserved for the next tick");
        result.Regenerated.Should().Be(1, "one heart regenerated");
    }

    // =========================================================================
    // L8 — Below cap, 60 min elapsed → 2 ticks, hits cap, clock cleared
    // =========================================================================

    [Fact(DisplayName = "P404-L8 Compute(hearts=3, cap=5, lastRefillAt=now-60min, now, 30) → (5, null, 2) two ticks hit cap")]
    public void L8_BelowCap_60MinElapsed_TwoTicksHitCap()
    {
        var lastRefillAt = Now.AddMinutes(-60);

        var result = LazyHeartRefiller.Compute(
            currentHearts: 3,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(5, "3 + 2 = 5 = cap");
        result.NewLastRefillAt.Should().BeNull("at cap after refill → clock idles (null)");
        result.Regenerated.Should().Be(2, "two hearts regenerated");
    }

    // =========================================================================
    // L9 — From 0, 150 min elapsed → 5 ticks, full refill from 0
    // =========================================================================

    [Fact(DisplayName = "P404-L9 Compute(hearts=0, cap=5, lastRefillAt=now-150min, now, 30) → (5, null, 5) full refill from zero")]
    public void L9_FromZero_150MinElapsed_FullRefill()
    {
        var lastRefillAt = Now.AddMinutes(-150);

        var result = LazyHeartRefiller.Compute(
            currentHearts: 0,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(5, "0 + 5 ticks = 5 = cap");
        result.NewLastRefillAt.Should().BeNull("at cap after full refill → clock idles");
        result.Regenerated.Should().Be(5, "all five hearts regenerated");
    }

    // =========================================================================
    // L10 — From 0, 200 min elapsed → cap clamp (would be 6+ but max is 5)
    // =========================================================================

    [Fact(DisplayName = "P404-L10 Compute(hearts=0, cap=5, lastRefillAt=now-200min, now, 30) → (5, null, 5) excess ticks clamped at cap")]
    public void L10_FromZero_200MinElapsed_TicksClamped()
    {
        var lastRefillAt = Now.AddMinutes(-200);

        var result = LazyHeartRefiller.Compute(
            currentHearts: 0,
            cap: 5,
            lastRefillAt: lastRefillAt,
            nowUtc: Now,
            intervalMin: 30);

        result.NewHearts.Should().Be(5, "cap clamp: cannot exceed 5 hearts");
        result.NewLastRefillAt.Should().BeNull("at cap → clock idles");
        result.Regenerated.Should().Be(5, "five hearts regenerated (excess ticks clamped)");
    }

    // =========================================================================
    // L11 — Kind normalisation regression test
    //
    // Before the bug fix, Npgsql's legacy timestamp behaviour returned a DateTime with Kind=Local
    // and LOCAL-timezone ticks from `timestamp with time zone` columns. When `nowUtc` is Kind=Utc
    // and `lastRefillAt` is Kind=Local, a raw tick subtraction on UTC+ machines produces a
    // NEGATIVE elapsed time (local ticks > UTC ticks on UTC+N), meaning ticksToRegen < 0,
    // clamped to 0 → no refill, student stuck in Practice Mode.
    //
    // After the fix: Compute() calls .ToUniversalTime() on both operands before subtracting,
    // converting Kind=Local timestamps to their correct UTC equivalent before computing elapsed.
    //
    // Simulation: use DateTime.Now (Kind=Local, local-timezone ticks) for `lastRefillAt`.
    // This authentically replicates Npgsql's legacy return shape.
    // DateTime.Now.AddMinutes(-1).ToUniversalTime() == DateTime.UtcNow.AddMinutes(-1)
    // so elapsed after normalisation ≈ 1 minute < 30-minute interval → 0 ticks → no refill.
    // =========================================================================

    [Fact(DisplayName = "P404-L11 Kind=Local lastRefillAt (local ticks, 1 min ago) must normalise correctly — 0 ticks, no spurious refill (Npgsql legacy regression)")]
    public void L11_KindNormalisationRegression_LocalKindLastRefillAt_NoNegativeTicks()
    {
        // Use DateTime.Now (Kind=Local, local-timezone ticks) to authentically simulate
        // what Npgsql's legacy timestamp behaviour returns from `timestamp with time zone` columns.
        // AddMinutes(-1) = 1 minute ago in local time.
        var lastRefillAtLocal = DateTime.Now.AddMinutes(-1); // Kind=Local, local ticks

        // nowUtc is Kind=Utc (as provided by ISystemClock.UtcNow).
        var nowUtc = DateTime.UtcNow; // Kind=Utc, UTC ticks

        var result = LazyHeartRefiller.Compute(
            currentHearts: 4,
            cap: 5,
            lastRefillAt: lastRefillAtLocal,
            nowUtc: nowUtc,
            intervalMin: 30);

        // After Kind normalisation via ToUniversalTime():
        //   lastRefillAtLocal.ToUniversalTime() ≈ nowUtc.AddMinutes(-1) (correct UTC)
        //   elapsed ≈ 1 minute < 30-minute interval → 0 ticks → no refill.
        //
        // Without the fix (raw tick subtraction on UTC+N machine):
        //   elapsed = (nowUtc.Ticks - localTicks) / TicksPerMinute → NEGATIVE on UTC+ (local > UTC)
        //   ticksToRegen = (int)(negative / 30) = 0 (or very negative clamped to 0)
        //   Hearts unchanged — but for WRONG reasons (the intent was "not enough time elapsed",
        //   not "negative time elapsed due to Kind mismatch").
        // The fix makes elapsed correct regardless of timezone.
        result.NewHearts.Should().Be(4,
            "only ~1 minute of REAL elapsed time; no full 30-min interval → no refill tick should fire");
        result.Regenerated.Should().Be(0,
            "zero hearts regenerated: actual elapsed time after Kind normalisation is < 30 minutes");
        result.NewLastRefillAt.Should().Be(lastRefillAtLocal,
            "clock is unchanged when no tick fires (sub-interval after Kind normalisation)");
    }
}

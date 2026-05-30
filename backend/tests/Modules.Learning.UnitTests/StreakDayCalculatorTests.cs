using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="StreakDayCalculator"/>.
///
/// Pure static helpers — no DbContext, no DI required.
/// Every branch of <c>Classify</c> is covered; <c>DayOf</c> UTC and Cairo-TZ boundaries are
/// spot-checked; <c>Today</c> is exercised via a stub <see cref="ISystemClock"/>.
///
/// Coverage map (P4-03 Batch-4 spec):
///   SC01  Classify(null, today) → FirstActivity
///   SC02  Classify(today, today) → NoOp (same day)
///   SC03  Classify(yesterday, today) → Advance (consecutive)
///   SC04  Classify(today-2, today) → Reset (1-day gap)
///   SC05  Classify(2026-05-01, 2026-05-30) → Reset (large gap)
///   SC06  Classify(future, today) → InvalidOperationException
///   SC07  Classify(2026-12-31, 2027-01-01) → Advance (year boundary)
///   SC08  Classify(2024-02-29, 2024-03-01) → Advance (leap-year boundary)
///   SC09  DayOf(2026-05-30T00:00:00Z, "UTC") → 2026-05-30
///   SC10  DayOf(2026-05-30T23:59:59Z, "UTC") → 2026-05-30
///   SC11  DayOf(2026-05-30T23:59:59Z, "Africa/Cairo") → 2026-05-31
///   SC12  DayOf(2026-05-31T00:00:00Z, "UTC") → 2026-05-31
///   SC13  Today("UTC", fixedClock) → converts UtcNow to DateOnly
/// </summary>
public sealed class StreakDayCalculatorTests
{
    // =========================================================================
    // Classify — null last-activity → FirstActivity
    // =========================================================================

    [Fact(DisplayName = "P403-SC01 Classify(null, 2026-05-30) → FirstActivity (brand-new student)")]
    public void Classify_NullLastActivity_ReturnsFirstActivity()
    {
        var today = new DateOnly(2026, 5, 30);
        var result = StreakDayCalculator.Classify(null, today);
        result.Should().Be(StreakDayCalculator.Transition.FirstActivity,
            "null last-activity means the student has never had a qualifying activity");
    }

    // =========================================================================
    // Classify — same calendar day → NoOp
    // =========================================================================

    [Fact(DisplayName = "P403-SC02 Classify(2026-05-30, 2026-05-30) → NoOp (same day)")]
    public void Classify_SameDay_ReturnsNoOp()
    {
        var today = new DateOnly(2026, 5, 30);
        var result = StreakDayCalculator.Classify(today, today);
        result.Should().Be(StreakDayCalculator.Transition.NoOp,
            "activity on the same calendar day must not advance the streak");
    }

    // =========================================================================
    // Classify — consecutive day (yesterday → today) → Advance
    // =========================================================================

    [Fact(DisplayName = "P403-SC03 Classify(2026-05-29, 2026-05-30) → Advance (consecutive days)")]
    public void Classify_ConsecutiveDay_ReturnsAdvance()
    {
        var lastActivity = new DateOnly(2026, 5, 29);
        var today        = new DateOnly(2026, 5, 30);
        var result = StreakDayCalculator.Classify(lastActivity, today);
        result.Should().Be(StreakDayCalculator.Transition.Advance,
            "activity exactly one day after the last activity must advance the streak");
    }

    // =========================================================================
    // Classify — 1-day gap (the day between was missed) → Reset
    // =========================================================================

    [Fact(DisplayName = "P403-SC04 Classify(2026-05-28, 2026-05-30) → Reset (1-day gap)")]
    public void Classify_OneDayGap_ReturnsReset()
    {
        var lastActivity = new DateOnly(2026, 5, 28);
        var today        = new DateOnly(2026, 5, 30);
        var result = StreakDayCalculator.Classify(lastActivity, today);
        result.Should().Be(StreakDayCalculator.Transition.Reset,
            "a gap of 2 days (one missed day) must reset the streak");
    }

    // =========================================================================
    // Classify — large gap → Reset
    // =========================================================================

    [Fact(DisplayName = "P403-SC05 Classify(2026-05-01, 2026-05-30) → Reset (large gap)")]
    public void Classify_LargeGap_ReturnsReset()
    {
        var lastActivity = new DateOnly(2026, 5, 1);
        var today        = new DateOnly(2026, 5, 30);
        var result = StreakDayCalculator.Classify(lastActivity, today);
        result.Should().Be(StreakDayCalculator.Transition.Reset,
            "a 29-day gap must also reset the streak");
    }

    // =========================================================================
    // Classify — future last-activity → OutOfOrder (stale / out-of-order event)
    // =========================================================================

    [Fact(DisplayName = "P403-SC06 Classify(2026-05-31, 2026-05-30) → OutOfOrder (future last-activity)")]
    public void Classify_FutureLastActivity_ReturnsOutOfOrder()
    {
        var lastActivity = new DateOnly(2026, 5, 31);   // in the future
        var today        = new DateOnly(2026, 5, 30);

        var result = StreakDayCalculator.Classify(lastActivity, today);

        result.Should().Be(StreakDayCalculator.Transition.OutOfOrder,
            "a last-activity date in the future relative to today is a stale/out-of-order event; " +
            "Classify returns OutOfOrder so the handler can soft-skip without a try/catch");
    }

    // =========================================================================
    // Classify — year boundary (Dec 31 → Jan 1) → Advance
    // =========================================================================

    [Fact(DisplayName = "P403-SC07 Classify(2026-12-31, 2027-01-01) → Advance (year boundary)")]
    public void Classify_YearBoundary_ReturnsAdvance()
    {
        var lastActivity = new DateOnly(2026, 12, 31);
        var today        = new DateOnly(2027, 1, 1);
        var result = StreakDayCalculator.Classify(lastActivity, today);
        result.Should().Be(StreakDayCalculator.Transition.Advance,
            "activity on Jan 1 after Dec 31 is a consecutive day — advance the streak");
    }

    // =========================================================================
    // Classify — leap-year boundary (Feb 29 → Mar 1) → Advance
    // =========================================================================

    [Fact(DisplayName = "P403-SC08 Classify(2024-02-29, 2024-03-01) → Advance (leap-year boundary)")]
    public void Classify_LeapYearBoundary_ReturnsAdvance()
    {
        var lastActivity = new DateOnly(2024, 2, 29);   // 2024 is a leap year
        var today        = new DateOnly(2024, 3, 1);
        var result = StreakDayCalculator.Classify(lastActivity, today);
        result.Should().Be(StreakDayCalculator.Transition.Advance,
            "Mar 1 is exactly one day after Feb 29 in a leap year — advance the streak");
    }

    // =========================================================================
    // DayOf — UTC midnight → same UTC day
    // =========================================================================

    [Fact(DisplayName = "P403-SC09 DayOf(2026-05-30T00:00:00Z, 'UTC') → 2026-05-30")]
    public void DayOf_UtcMidnight_ReturnsSameDay()
    {
        var utc = new DateTime(2026, 5, 30, 0, 0, 0, DateTimeKind.Utc);
        var result = StreakDayCalculator.DayOf(utc, "UTC");
        result.Should().Be(new DateOnly(2026, 5, 30),
            "midnight UTC maps to the same UTC calendar day");
    }

    // =========================================================================
    // DayOf — 23:59:59 UTC → same UTC day
    // =========================================================================

    [Fact(DisplayName = "P403-SC10 DayOf(2026-05-30T23:59:59Z, 'UTC') → 2026-05-30")]
    public void DayOf_UtcEndOfDay_ReturnsSameDay()
    {
        var utc = new DateTime(2026, 5, 30, 23, 59, 59, DateTimeKind.Utc);
        var result = StreakDayCalculator.DayOf(utc, "UTC");
        result.Should().Be(new DateOnly(2026, 5, 30),
            "23:59:59 UTC on May 30 is still May 30 in UTC");
    }

    // =========================================================================
    // DayOf — 23:59:59 UTC, Africa/Cairo TZ → next calendar day in Cairo
    //
    // Cairo is UTC+3 during EET (Eastern European Time; no summer DST in 2026 —
    // Egypt abolished DST in 2011). 23:59:59 UTC = 02:59:59+03:00 on May 31.
    // =========================================================================

    [Fact(DisplayName = "P403-SC11 DayOf(2026-05-30T23:59:59Z, 'Africa/Cairo') → 2026-05-31 (Cairo UTC+3)")]
    public void DayOf_UtcEndOfDay_CairoTz_ReturnsNextDay()
    {
        var utc = new DateTime(2026, 5, 30, 23, 59, 59, DateTimeKind.Utc);

        // Guard: Africa/Cairo must be resolvable (IANA timezone — works on Linux/.NET 6+; on Windows
        // the runtime maps "Africa/Cairo" through ICU. Skip the test if the TZ can't be found rather
        // than failing with an exception from the OS.
        TimeZoneInfo? cairoTz = null;
        try { cairoTz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (TimeZoneNotFoundException) { return; }  // skip on environments without this TZ

        var result = StreakDayCalculator.DayOf(utc, "Africa/Cairo");

        // Africa/Cairo = UTC+3 (no DST in 2026), so 23:59:59 UTC = 02:59:59 on May 31.
        result.Should().Be(new DateOnly(2026, 5, 31),
            "23:59:59 UTC on May 30 is 02:59:59 on May 31 in Cairo (UTC+3) — so it's May 31");
    }

    // =========================================================================
    // DayOf — start of next UTC day
    // =========================================================================

    [Fact(DisplayName = "P403-SC12 DayOf(2026-05-31T00:00:00Z, 'UTC') → 2026-05-31")]
    public void DayOf_UtcStartOfNextDay_ReturnsNextDay()
    {
        var utc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);
        var result = StreakDayCalculator.DayOf(utc, "UTC");
        result.Should().Be(new DateOnly(2026, 5, 31),
            "midnight UTC on May 31 is May 31");
    }

    // =========================================================================
    // Today — uses ISystemClock seam
    // =========================================================================

    [Fact(DisplayName = "P403-SC13 Today('UTC', fixedClock) → converts UtcNow to DateOnly correctly")]
    public void Today_WithFixedClock_ReturnsExpectedDate()
    {
        var fixedUtc  = new DateTime(2026, 5, 30, 14, 30, 0, DateTimeKind.Utc);
        var clock     = new FixedClock(fixedUtc);
        var result    = StreakDayCalculator.Today("UTC", clock);
        result.Should().Be(new DateOnly(2026, 5, 30),
            "Today() must delegate to DayOf(clock.UtcNow, tz) and return the correct calendar date");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Test-only fixed-time <see cref="ISystemClock"/> for SC13.</summary>
    private sealed class FixedClock(DateTime utcNow) : ISystemClock
    {
        public DateTime UtcNow => utcNow;
    }
}

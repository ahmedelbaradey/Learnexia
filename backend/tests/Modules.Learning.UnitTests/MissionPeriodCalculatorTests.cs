using System.Globalization;
using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="MissionPeriodCalculator.GetCurrentPeriod"/> (P4-06, Batch 5).
///
/// Pure static function — no DbContext or DI required. All cases are deterministic.
///
/// Method signature under test:
///   GetCurrentPeriod(MissionType cadence, DateTime nowUtc)
///   → (string PeriodKey, DateTime StartUtc, DateTime EndUtc)
///
/// Coverage map (10 test cases — P1..P10):
///   P1   Daily, 2026-06-01T10:00:00Z → PeriodKey="D:2026-06-01", Start=2026-06-01T00:00Z, End=2026-06-02T00:00Z
///   P2   Daily, 2026-06-01T23:59:59Z → same as P1 (last second same day)
///   P3   Daily, 2026-06-02T00:00:00Z → PeriodKey="D:2026-06-02" (new day)
///   P4   Weekly, 2026-06-01T10:00:00Z (Monday ISO week 23 of 2026) → PeriodKey="W:2026-23", Start=Mon, End=Mon+7d
///   P5   Weekly, 2026-06-07T23:59:59Z (Sunday, same ISO week) → same as P4
///   P6   Weekly, 2026-06-08T00:00:00Z (Monday, ISO week 24) → PeriodKey="W:2026-24"
///   P7   Weekly, 2027-01-01T00:00:00Z (Friday — ISO week may belong to 2026) → PeriodKey reflects ISO year
///   P8   Leap day daily, 2028-02-29T12:00:00Z → PeriodKey="D:2028-02-29"
///   P9   Kind=Local input, 2026-06-01T10:00:00 → still produces correct UTC-based period
///   P10  Unknown cadence (int 99 cast to MissionType) → throws ArgumentException
/// </summary>
public sealed class MissionPeriodCalculatorTests
{
    // =========================================================================
    // P1 — Daily mid-morning: key is today
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P1 Daily 2026-06-01T10:00Z → PeriodKey='D:2026-06-01', Start=00:00Z, End=next-day 00:00Z")]
    public void P1_Daily_MidMorning_CorrectPeriod()
    {
        var now = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, now);

        key.Should().Be("D:2026-06-01");
        start.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        start.Kind.Should().Be(DateTimeKind.Utc, "output datetimes must carry UTC kind");
        end.Kind.Should().Be(DateTimeKind.Utc);
    }

    // =========================================================================
    // P2 — Daily last second of day: still same day
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P2 Daily 2026-06-01T23:59:59Z → same PeriodKey as P1 (still 'D:2026-06-01')")]
    public void P2_Daily_LastSecondOfDay_SamePeriodAsP1()
    {
        var now = new DateTime(2026, 6, 1, 23, 59, 59, DateTimeKind.Utc);

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, now);

        key.Should().Be("D:2026-06-01",
            "23:59:59 is still within the same calendar day as 10:00:00");
        start.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    // =========================================================================
    // P3 — Daily midnight boundary: next day
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P3 Daily 2026-06-02T00:00:00Z → flips to next day 'D:2026-06-02'")]
    public void P3_Daily_Midnight_FlipsToNextDay()
    {
        var now = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, now);

        key.Should().Be("D:2026-06-02");
        start.Should().Be(new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    // =========================================================================
    // P4 — Weekly: Monday 2026-06-01 is ISO week 23 of 2026
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P4 Weekly 2026-06-01T10:00Z (Monday) → 'W:2026-23', Start=Mon 00:00Z, End=next-Mon 00:00Z")]
    public void P4_Weekly_Monday_IsoWeek23()
    {
        var now = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        // Verify assumption: 2026-06-01 is indeed ISO week 23
        var actualIsoWeek = ISOWeek.GetWeekOfYear(now);
        var actualIsoYear = ISOWeek.GetYear(now);
        actualIsoWeek.Should().Be(23,
            "2026-06-01 must be ISO week 23 of 2026 (first Monday of ISO week 23 is 2026-06-01)");
        actualIsoYear.Should().Be(2026);

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

        key.Should().Be("W:2026-23");
        start.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            "ISO weeks start on Monday; 2026-06-01 is that Monday");
        end.Should().Be(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            "week ends at the following Monday midnight");
        start.Kind.Should().Be(DateTimeKind.Utc);
        end.Kind.Should().Be(DateTimeKind.Utc);
    }

    // =========================================================================
    // P5 — Weekly: Sunday 2026-06-07 (last second) — same ISO week as P4
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P5 Weekly 2026-06-07T23:59:59Z (Sunday) → same week 'W:2026-23' as P4")]
    public void P5_Weekly_SundayLastSecond_SameWeekAsP4()
    {
        var now = new DateTime(2026, 6, 7, 23, 59, 59, DateTimeKind.Utc);

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

        key.Should().Be("W:2026-23",
            "2026-06-07 (Sunday) is still within ISO week 23 — same week as 2026-06-01");
        start.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));
    }

    // =========================================================================
    // P6 — Weekly: Monday 2026-06-08 flips to ISO week 24
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P6 Weekly 2026-06-08T00:00:00Z (Monday) → 'W:2026-24' (next week)")]
    public void P6_Weekly_NextMonday_FlipsToWeek24()
    {
        var now = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        var actualIsoWeek = ISOWeek.GetWeekOfYear(now);
        actualIsoWeek.Should().Be(24, "2026-06-08 is ISO week 24");

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

        key.Should().Be("W:2026-24");
        start.Should().Be(new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    // =========================================================================
    // P7 — ISO week year boundary: 2027-01-01 (Friday) — may belong to ISO week of 2026
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P7 Weekly 2027-01-01T00:00Z (Friday) → ISO week year reflects ISO 8601 (not Gregorian year)")]
    public void P7_Weekly_IsoYearBoundary_2027Jan01()
    {
        var now = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Determine what ISO year/week 2027-01-01 actually belongs to
        int isoWeek = ISOWeek.GetWeekOfYear(now);
        int isoYear = ISOWeek.GetYear(now);

        // 2027-01-01 is a Friday. ISO week 1 of 2027 must contain the first Thursday of 2027.
        // 2027's first Thursday is 2027-01-07. So 2027-01-01 (Friday) still belongs to week 53 of 2026.
        isoYear.Should().Be(2026,
            "2027-01-01 is a Friday; ISO week 1 of 2027 starts on 2027-01-04 (Monday); " +
            "therefore 2027-01-01 belongs to ISO week 53 of 2026");
        isoWeek.Should().Be(53);

        var expectedKey = $"W:{isoYear:0000}-{isoWeek:00}";

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

        key.Should().Be(expectedKey,
            "PeriodKey must use ISO week-numbering year (isoYear), not Gregorian calendar year");

        // Start of ISO week 53/2026 is Monday 2026-12-28
        start.Should().Be(new DateTime(2026, 12, 28, 0, 0, 0, DateTimeKind.Utc),
            "ISO week 53/2026 starts on Monday 2026-12-28");
        end.Should().Be(start.AddDays(7),
            "week end is start + 7 days");
    }

    // =========================================================================
    // P8 — Leap day 2028-02-29
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P8 Daily 2028-02-29T12:00Z (leap day) → PeriodKey='D:2028-02-29'")]
    public void P8_Daily_LeapDay_2028Feb29()
    {
        var now = new DateTime(2028, 2, 29, 12, 0, 0, DateTimeKind.Utc);

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, now);

        key.Should().Be("D:2028-02-29",
            "2028 is a leap year; Feb 29 must produce a valid period key");
        start.Should().Be(new DateTime(2028, 2, 29, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2028, 3, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    // =========================================================================
    // P9 — Local kind input: normalised to UTC-based period
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P9 Daily input with Kind=Local → implementation normalises to UTC (no exception, produces 'D:' key)")]
    public void P9_Daily_LocalKindInput_NormalisedToUtc()
    {
        // The calculator does DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc) internally.
        // A Local-kind input is normalised to the same *value* with Utc kind.
        // We verify: no exception thrown, output Key is "D:" prefixed, StartUtc.Kind == Utc.
        var local = DateTime.SpecifyKind(new DateTime(2026, 6, 1, 10, 0, 0), DateTimeKind.Local);

        var act = () => MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, local);

        act.Should().NotThrow("Kind=Local input must be normalised, not thrown");

        var (key, start, end) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Daily, local);

        key.Should().StartWith("D:", "daily key must be in 'D:yyyy-MM-dd' format");
        start.Kind.Should().Be(DateTimeKind.Utc, "output start must carry UTC kind");
        end.Kind.Should().Be(DateTimeKind.Utc, "output end must carry UTC kind");
        end.Should().Be(start.AddDays(1), "period duration is exactly 1 day");
    }

    // =========================================================================
    // P10 — Unknown cadence: ArgumentException
    // =========================================================================

    [Fact(DisplayName = "P4-06-Unit-P10 Unknown cadence (cast int 99 to MissionType) → throws ArgumentException")]
    public void P10_UnknownCadence_ThrowsArgumentException()
    {
        var unknownCadence = (MissionType)99;
        var now = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        var act = () => MissionPeriodCalculator.GetCurrentPeriod(unknownCadence, now);

        act.Should().Throw<ArgumentException>(
            "the switch has a default throw for unknown cadence values — impossible-state guard");
    }
}

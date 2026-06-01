using System.Globalization;
using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Domain.Services;

/// <summary>
/// Pure static helper that computes the current period key, start UTC, and end UTC for a given
/// cadence (Daily or Weekly). No DI, no DB access — fully unit-testable in isolation.
///
/// Mirrors the pure-static shape of <see cref="StreakDayCalculator"/>, <see cref="LevelCurve"/>,
/// and <see cref="LazyHeartRefiller"/> (no constructor, no fields, no side effects).
///
/// All returned <see cref="DateTime"/> values carry <see cref="DateTimeKind.Utc"/> to prevent the
/// mixed-Kind subtraction bug discovered in P4-04's <see cref="LazyHeartRefiller"/> refill clock.
/// </summary>
public static class MissionPeriodCalculator
{
    /// <summary>
    /// Returns <c>(PeriodKey, StartUtc, EndUtc)</c> for the period containing <paramref name="nowUtc"/>.
    ///
    /// <para>
    /// <b>Daily</b>: key = <c>"D:yyyy-MM-dd"</c>,
    /// start = today 00:00 UTC, end = tomorrow 00:00 UTC.
    /// </para>
    /// <para>
    /// <b>Weekly</b>: key = <c>"W:ISOyyyy-WW"</c> (ISO 8601 week-numbering year + ISO week,
    /// e.g. <c>"W:2026-22"</c>),
    /// start = Monday 00:00 UTC of that ISO week, end = next Monday 00:00 UTC.
    /// </para>
    ///
    /// Throws <see cref="ArgumentException"/> for an unknown <paramref name="cadence"/> value
    /// (impossible-state guard — satisfies the compiler and prevents silent no-ops on future enum
    /// additions).
    /// </summary>
    /// <param name="cadence">Daily or Weekly (maps from <see cref="MissionType"/>).</param>
    /// <param name="nowUtc">UTC instant to compute the period for. Must have Kind = Utc (or Unspecified).</param>
    public static (string PeriodKey, DateTime StartUtc, DateTime EndUtc) GetCurrentPeriod(
        MissionType cadence, DateTime nowUtc)
    {
        return cadence switch
        {
            MissionType.Daily  => ComputeDaily(nowUtc),
            MissionType.Weekly => ComputeWeekly(nowUtc),
            _ => throw new ArgumentException($"Unknown cadence: {cadence}", nameof(cadence)),
        };
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static (string PeriodKey, DateTime StartUtc, DateTime EndUtc) ComputeDaily(DateTime nowUtc)
    {
        // Normalise to UTC Kind so arithmetic is safe regardless of caller.
        var utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var date  = DateOnly.FromDateTime(utc);
        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end   = start.AddDays(1);
        var key   = $"D:{date:yyyy-MM-dd}";

        return (key, start, end);
    }

    private static (string PeriodKey, DateTime StartUtc, DateTime EndUtc) ComputeWeekly(DateTime nowUtc)
    {
        // Normalise to UTC Kind so arithmetic is safe regardless of caller.
        var utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        // ISO 8601: week 1 contains the year's first Thursday.
        // ISOWeek.GetWeekOfYear and ISOWeek.GetYear handle the week-numbering year correctly
        // (late-December days that belong to week 1 of the *next* ISO year, and vice-versa).
        int isoWeek = ISOWeek.GetWeekOfYear(utc);
        int isoYear = ISOWeek.GetYear(utc);

        // ISO weeks start on Monday (DayOfWeek.Monday = 1).
        // DotNet DayOfWeek: Sunday=0, Monday=1, …, Saturday=6.
        // Offset to Monday: (dow == 0 ? 6 : dow - 1) gives Mon=0, Tue=1, …, Sun=6.
        int dow    = (int)utc.DayOfWeek;
        int offset = dow == 0 ? 6 : dow - 1;   // days since Monday

        var monday = DateOnly.FromDateTime(utc.AddDays(-offset));
        var start  = monday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end    = start.AddDays(7);
        var key    = $"W:{isoYear:0000}-{isoWeek:00}";

        return (key, start, end);
    }
}

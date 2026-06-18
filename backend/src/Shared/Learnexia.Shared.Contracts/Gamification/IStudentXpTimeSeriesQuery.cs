namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Parent (and any future consumer) injects.
/// Returns time-series XP analytics for a given student, derived entirely from the append-only
/// <c>XpAward</c> ledger + the <c>StudentXpProfile</c> snapshot.
///
/// <para>Registered in <c>AddGamificationInfrastructure()</c> as Scoped.</para>
///
/// <para>All methods are sentinel-safe: a brand-new student with no <c>XpAward</c> rows or no
/// <c>StudentXpProfile</c> returns zeroed / empty results — never null, never throws (AC3).</para>
///
/// <para>Purely read-only — never writes. Safe to call from parent-scoped read endpoints
/// (P5-08-BE-4 pure-read contract).</para>
///
/// <para>P5-08-BE-1 (Gamification batch 1a).</para>
/// </summary>
public interface IStudentXpTimeSeriesQuery
{
    /// <summary>
    /// Returns the daily XP series for <paramref name="studentId"/> over the UTC window
    /// [<paramref name="fromDate"/>, <paramref name="toDate"/>].
    ///
    /// <para>Every calendar day in the window is present in the result (even days with zero XP),
    /// ordered ascending by date. Callers use this for the Reports chart (E6 — daily-XP series).</para>
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="fromDate">Window start (inclusive), UTC date.</param>
    /// <param name="toDate">Window end (inclusive), UTC date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An ordered list of <see cref="DailyXpEntry"/> — never null.
    /// Returns a list of zero-XP entries when the student has no data in the window.
    /// </returns>
    Task<IReadOnlyList<DailyXpEntry>> GetDailySeriesAsync(
        int studentId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a 20-day XP trend (each entry = one day's total XP) ending on <paramref name="asOfDate"/>
    /// (inclusive), in ascending date order.
    ///
    /// <para>Every day in the 20-day window appears in the result even when no XP was earned
    /// that day (zero-filled). Callers use this for the 20-day trend sparkline (E6).</para>
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="asOfDate">The end date of the trend window (inclusive, UTC date).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Exactly 20 <see cref="DailyXpEntry"/> values — never null.
    /// All zeroed when the student has no XP history.
    /// </returns>
    Task<IReadOnlyList<DailyXpEntry>> GetTwentyDayTrendAsync(
        int studentId,
        DateOnly asOfDate,
        CancellationToken ct = default);

    /// <summary>
    /// Returns XP aggregated by time-of-day bucket for <paramref name="studentId"/> over the
    /// UTC window [<paramref name="fromDate"/>, <paramref name="toDate"/>].
    ///
    /// <para>The four buckets (Morning / Afternoon / Evening / Night) are always present in the
    /// result even when a bucket has zero XP. Bucket boundaries are UTC hours:
    /// Morning = [05:00, 12:00), Afternoon = [12:00, 17:00), Evening = [17:00, 21:00),
    /// Night = [21:00, 05:00) wrap-around.</para>
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="fromDate">Window start (inclusive), UTC date.</param>
    /// <param name="toDate">Window end (inclusive), UTC date.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Exactly 4 <see cref="TimeOfDayXpBucket"/> values — never null. Zero-XP when no data.
    /// </returns>
    Task<IReadOnlyList<TimeOfDayXpBucket>> GetTimeOfDayBucketsAsync(
        int studentId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the XP + level snapshot for <paramref name="studentId"/> along with streak
    /// and best-streak values, sourced from <c>StudentXpProfile</c>.
    ///
    /// <para>Returns a zeroed <see cref="XpProfileSnapshot"/> when the student has no profile yet
    /// (brand-new student — AC3). Never null.</para>
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<XpProfileSnapshot> GetProfileSnapshotAsync(
        int studentId,
        CancellationToken ct = default);
}

/// <summary>
/// XP earned on a single UTC calendar day.
/// Produced by <see cref="IStudentXpTimeSeriesQuery.GetDailySeriesAsync"/> and
/// <see cref="IStudentXpTimeSeriesQuery.GetTwentyDayTrendAsync"/>.
/// </summary>
/// <param name="Day">UTC calendar date.</param>
/// <param name="Xp">Total XP earned on <paramref name="Day"/> (0 when no awards).</param>
public record DailyXpEntry(DateOnly Day, int Xp);

/// <summary>
/// Time-of-day XP bucket, aggregated over a UTC window.
/// Produced by <see cref="IStudentXpTimeSeriesQuery.GetTimeOfDayBucketsAsync"/>.
/// </summary>
/// <param name="Bucket">One of: Morning, Afternoon, Evening, Night (see UTC hour ranges on the seam).</param>
/// <param name="TotalXp">Sum of all XP awards whose <c>OccurredAtUtc</c> falls in this bucket.</param>
public record TimeOfDayXpBucket(string Bucket, int TotalXp);

/// <summary>
/// Combined XP, level, and streak snapshot for a student.
/// Produced by <see cref="IStudentXpTimeSeriesQuery.GetProfileSnapshotAsync"/>.
/// </summary>
/// <param name="TotalXp">Cumulative XP — 0 for a brand-new student.</param>
/// <param name="CurrentLevel">Current level — 1 for a brand-new student.</param>
/// <param name="CurrentStreak">Current consecutive-day streak — 0 when no active streak.</param>
/// <param name="BestStreak">All-time longest streak — 0 for a brand-new student.</param>
public record XpProfileSnapshot(int TotalXp, int CurrentLevel, int CurrentStreak, int BestStreak);

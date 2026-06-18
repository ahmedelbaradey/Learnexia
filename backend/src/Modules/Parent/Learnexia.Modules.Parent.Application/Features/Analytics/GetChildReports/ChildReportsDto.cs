namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildReports;

/// <summary>
/// E6 response DTO — chart data for the parent Reports screen.
/// Covers all fields from <c>P5-05-FE-2/3</c> chart contract in the FE gold standard.
/// </summary>
public sealed class ChildReportsDto
{
    /// <summary>
    /// Daily XP for each day in the rolling 7-day window (Mon–Sun localised display by FE).
    /// Each entry is guaranteed; zero XP on no-activity days.
    /// </summary>
    public IReadOnlyList<DailyXpEntryDto> DailyXpSeries { get; init; } = [];

    /// <summary>
    /// 20-day XP trend ending today.
    /// Exactly 20 entries; zero-filled on no-activity days.
    /// </summary>
    public IReadOnlyList<DailyXpEntryDto> XpTrend20Day { get; init; } = [];

    /// <summary>
    /// XP aggregated by time-of-day bucket (Morning / Afternoon / Evening / Night).
    /// Exactly 4 entries; zero when bucket has no activity.
    /// </summary>
    public IReadOnlyList<TimeOfDayBucketDto> TimeOfDayBuckets { get; init; } = [];
}

public sealed class DailyXpEntryDto
{
    /// <summary>UTC calendar date in ISO-8601 format (yyyy-MM-dd).</summary>
    public string Day { get; init; } = string.Empty;

    /// <summary>Total XP earned on this day (0 when none).</summary>
    public int Xp { get; init; }
}

public sealed class TimeOfDayBucketDto
{
    /// <summary>One of: Morning, Afternoon, Evening, Night.</summary>
    public string Bucket { get; init; } = string.Empty;

    /// <summary>Total XP earned in this time bucket.</summary>
    public int TotalXp { get; init; }
}

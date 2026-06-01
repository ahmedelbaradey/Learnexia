namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration options for the mission engine (P4-06). Bound from
/// <c>appsettings.json</c> section <c>"Gamification:Mission"</c>.
///
/// Mirrors <see cref="StreakOptions"/> in shape. Defaults are valid out-of-the-box;
/// override in environment-specific appsettings as needed.
/// </summary>
public sealed class MissionOptions
{
    /// <summary>Config section path — matches the appsettings.json key.</summary>
    public const string SectionName = "Gamification:Mission";

    /// <summary>IANA time zone id for period boundary calculations. Default UTC.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Hangfire cron expression for the daily mission-rollover job. Default 00:05 UTC.</summary>
    public string DailyJobCron { get; set; } = "5 0 * * *";

    /// <summary>Hangfire cron expression for the weekly mission-rollover job (Mondays only). Default 00:10 UTC Monday.</summary>
    public string WeeklyJobCron { get; set; } = "10 0 * * 1";

    /// <summary>First day of the ISO week. Default Monday (ISO 8601).</summary>
    public DayOfWeek WeeklyRolloverDayOfWeek { get; set; } = DayOfWeek.Monday;
}

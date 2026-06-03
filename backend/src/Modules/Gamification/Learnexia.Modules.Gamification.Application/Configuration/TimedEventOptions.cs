namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration options for the timed-event engine (P4-11). Bound from
/// <c>appsettings.json</c> section <c>"Gamification:TimedEvent"</c>.
///
/// Mirrors <see cref="StreakOptions"/> in shape. Defaults are valid out-of-the-box;
/// override in environment-specific appsettings as needed.
/// </summary>
public sealed class TimedEventOptions
{
    /// <summary>Config section path — matches the appsettings.json key.</summary>
    public const string SectionName = "Gamification:TimedEvent";

    /// <summary>
    /// Cron expression for the sweep job. Default every 2 minutes.
    /// </summary>
    public string SweepCron { get; set; } = "*/2 * * * *";

    /// <summary>
    /// Time zone for the cron schedule. Default UTC.
    /// Must be a valid IANA or Windows time zone ID recognised by
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";
}

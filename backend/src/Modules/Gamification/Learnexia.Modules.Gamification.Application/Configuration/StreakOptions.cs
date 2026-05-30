namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration for the streak engine. Bound from <c>appsettings.json:Gamification:Streak</c>.
///
/// Defaults:
///   <see cref="TimeZoneId"/> = <c>"UTC"</c> — Q2 decision. Per-user timezone deferred to a future
///   story (no <c>User.TimeZoneId</c> field exists yet).
///   <see cref="DailyJobCron"/> = <c>"5 0 * * *"</c> — 00:05 UTC daily. The 5-minute buffer gives
///   late-night activity enough time to commit before the sweep fires (D4 decision).
/// </summary>
public sealed class StreakOptions
{
    /// <summary>Config section key — <c>"Gamification:Streak"</c>.</summary>
    public const string SectionName = "Gamification:Streak";

    /// <summary>
    /// Windows/IANA timezone ID used to convert UTC event timestamps to calendar days.
    /// Default: <c>"UTC"</c>. Override with <c>"Africa/Cairo"</c> etc. via appsettings.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// Hangfire cron expression for the daily sweep job.
    /// Default: <c>"5 0 * * *"</c> (00:05 UTC daily).
    /// </summary>
    public string DailyJobCron { get; set; } = "5 0 * * *";
}

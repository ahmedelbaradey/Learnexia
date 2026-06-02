namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration options for the P4-09 re-engagement Hangfire jobs.
/// Bound from <c>appsettings.json</c> section <c>"Gamification:Reengagement"</c>.
///
/// Mirrors <see cref="LeagueOptions"/> and <see cref="MissionOptions"/> in shape.
/// Defaults are valid out-of-the-box; override in environment-specific appsettings as needed.
/// </summary>
public sealed class ReengagementOptions
{
    /// <summary>Config section path — matches the appsettings.json key.</summary>
    public const string SectionName = "Gamification:Reengagement";

    /// <summary>
    /// Hangfire cron expression for the daily streak-at-risk + mission-reminder job.
    /// Default: <c>"0 18 * * *"</c> (18:00 UTC daily).
    /// </summary>
    public string StreakAtRiskCron { get; set; } = "0 18 * * *";

    /// <summary>
    /// Hangfire cron expression for the weekly lapse win-back job.
    /// Default: <c>"0 12 * * 0"</c> (Sunday 12:00 UTC).
    /// </summary>
    public string LapseWinBackCron { get; set; } = "0 12 * * 0";

    /// <summary>
    /// Number of days of inactivity before a student is considered lapsed.
    /// Default: 3. One-shot semantics: fires once on the exact lapse transition day.
    /// </summary>
    public int LapseDays { get; set; } = 3;

    /// <summary>
    /// Maximum inactivity days beyond which we stop sending lapse win-back nudges.
    /// Prevents pestering cold-churned users. Default: 30.
    /// </summary>
    public int MaxAbsenceDays { get; set; } = 30;

    /// <summary>
    /// Lookahead window in hours for the daily mission reminder sub-pass in
    /// <see cref="StreakAtRiskJob"/>. Default: 6 (missions expiring within the next 6 hours).
    /// </summary>
    public int MissionReminderLookaheadHours { get; set; } = 6;

    /// <summary>
    /// IANA time-zone identifier used to compute the UTC "today" boundary.
    /// Default: <c>"UTC"</c>. Must be a valid <c>TimeZoneInfo</c> ID.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";
}

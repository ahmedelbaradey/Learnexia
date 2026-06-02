namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration options for the league engine (P4-07). Bound from
/// <c>appsettings.json</c> section <c>"Gamification:League"</c>.
///
/// Mirrors <see cref="MissionOptions"/> in shape. Defaults are valid out-of-the-box;
/// override in environment-specific appsettings as needed.
/// </summary>
public sealed class LeagueOptions
{
    /// <summary>Config section path — matches the appsettings.json key.</summary>
    public const string SectionName = "Gamification:League";

    /// <summary>
    /// Maximum number of participants per cohort. When a cohort is full, a new
    /// <c>(Tier, PeriodKey, GroupIndex+1)</c> cohort is created. Default 30.
    /// </summary>
    public int CohortSize { get; set; } = 30;

    /// <summary>
    /// Number of top-ranked members promoted to the next tier on weekly rollover.
    /// Scaled down proportionally for small cohorts (&lt;12 members). Default 7.
    /// </summary>
    public int PromoteCount { get; set; } = 7;

    /// <summary>
    /// Number of bottom-ranked members demoted to the previous tier on weekly rollover.
    /// Scaled down proportionally for small cohorts (&lt;12 members). Default 5.
    /// </summary>
    public int DemoteCount { get; set; } = 5;

    /// <summary>
    /// Hangfire cron expression for the weekly league promotion job.
    /// Runs Monday 00:15 UTC — after StreakSweepJob (00:05) and MissionRolloverJob (00:10).
    /// Default: <c>"15 0 * * 1"</c>.
    /// </summary>
    public string WeeklyRolloverCron { get; set; } = "15 0 * * 1";
}

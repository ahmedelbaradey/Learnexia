namespace Learnexia.Modules.Gamification.Application.Configuration;

/// <summary>
/// Configuration options for the timed-event participation engine (P4-12). Bound from
/// <c>appsettings.json</c> section <c>"Gamification:TimedEventParticipation"</c>.
///
/// Mirrors <see cref="MissionOptions"/> in shape. Defaults are valid out-of-the-box;
/// override in environment-specific appsettings as needed.
/// </summary>
public sealed class TimedEventParticipationOptions
{
    /// <summary>Config section path — matches the appsettings.json key.</summary>
    public const string SectionName = "Gamification:TimedEventParticipation";

    /// <summary>
    /// Fallback participation goal used when <c>TimedEvent.ParticipationTarget</c> is null.
    /// How many qualifying in-window XP actions a student must complete to finish the event.
    /// Default: 10.
    /// </summary>
    public int DefaultTarget { get; set; } = 10;

    /// <summary>
    /// Percentage of <c>Target</c> at which the halfway-milestone integration event is emitted.
    /// Fires at most once per participation (one-time milestone, not repeated).
    /// Default: 50 (50 %).
    /// </summary>
    public int HalfwayThresholdPercent { get; set; } = 50;

    /// <summary>
    /// Lookahead window in hours for the ending-soon scan pass in <see cref="StreakAtRiskJob"/>.
    /// Default: 6 (participations whose event window closes within the next 6 hours).
    /// </summary>
    public int EndingSoonLookaheadHours { get; set; } = 6;

    /// <summary>
    /// Recency window in days used by <c>IEligibleStudentsForTimedEventQuery</c> to define
    /// "recently active" when building the eligibility cohort from <c>StudentXpProfile.LastActivityDateUtc</c>.
    /// Default: 7 (students active in the past 7 days).
    /// </summary>
    public int EligibilityWindowDays { get; set; } = 7;

    /// <summary>
    /// Reward XP granted when a student completes a timed-event participation.
    /// Applied through the existing XP chokepoint (<c>StudentXpProfile.RecordTimedEventCompleted</c>),
    /// subject to any active multiplier at the moment of completion.
    /// Default: 50.
    /// </summary>
    public int CompletionRewardXp { get; set; } = 50;
}

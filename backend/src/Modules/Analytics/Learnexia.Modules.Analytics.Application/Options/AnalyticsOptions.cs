namespace Learnexia.Modules.Analytics.Application.Options;

/// <summary>
/// Configuration options for the Analytics module. Bound from <c>Analytics</c> in appsettings / env.
/// </summary>
public sealed class AnalyticsOptions
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "Analytics";

    /// <summary>
    /// Inactivity window (in minutes) used to split a student's event stream into sessions.
    /// A gap of <c>SessionGapMinutes</c> or more between two consecutive events of the same
    /// student marks a session boundary.
    /// Default: <c>30</c> (industry standard for "active session" heuristic).
    /// </summary>
    public int SessionGapMinutes { get; set; } = 30;
}

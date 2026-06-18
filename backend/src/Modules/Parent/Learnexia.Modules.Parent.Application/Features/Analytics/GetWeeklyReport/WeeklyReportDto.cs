namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetWeeklyReport;

/// <summary>
/// Response DTO for E9 — GET /Parent/Children/{id}/WeeklyReport.
///
/// <para>When no report has been generated yet (first-week / no-activity-week), the handler
/// returns a 200 OK with a zeroed instance and a "not yet generated" message — never a 404.</para>
/// </summary>
public sealed class WeeklyReportDto
{
    /// <summary>
    /// <c>true</c> when a stored report was found; <c>false</c> for the "no report yet" zero-state.
    /// Lets the FE distinguish "here is your report" from "not yet available".
    /// </summary>
    public bool ReportFound { get; init; }

    /// <summary>Monday 00:00 UTC of the report week. <c>null</c> when no report exists.</summary>
    public DateTime? WeekStartUtc { get; init; }

    /// <summary>XP earned during the report week (0 when no report).</summary>
    public int XpEarned { get; init; }

    /// <summary>Number of distinct skills whose mastery improved during the week (0 when no report).</summary>
    public int SkillsImproved { get; init; }

    /// <summary>
    /// Weak areas snapshot as of the end of the report week.
    /// Deserialized list of weak-area entries (empty list when no weak areas or no report).
    /// </summary>
    public IReadOnlyList<WeakAreaSnapshotItem> WeakAreas { get; init; } = [];

    /// <summary>
    /// Recommendations for the child based on the week's weak areas.
    /// Empty list when no report or no recommendations.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>UTC timestamp when the report was generated. <c>null</c> when no report exists.</summary>
    public DateTime? GeneratedAtUtc { get; init; }
}

/// <summary>A single weak area entry stored in the weekly report's JSON snapshot.</summary>
public sealed class WeakAreaSnapshotItem
{
    /// <summary>Intra-Learning skill identifier.</summary>
    public int SkillId { get; init; }

    /// <summary>Human-readable skill name.</summary>
    public string SkillName { get; init; } = string.Empty;

    /// <summary>Subject code (int matching Learning.Domain.Enums.SubjectCode).</summary>
    public int SubjectCode { get; init; }

    /// <summary>Current mastery percentage [0, 100].</summary>
    public int MasteryPercent { get; init; }

    /// <summary>Severity: 1 = Low, 2 = Medium, 3 = High.</summary>
    public int Severity { get; init; }
}

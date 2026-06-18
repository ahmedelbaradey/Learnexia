namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetFamilySummary;

/// <summary>
/// E2 response DTO — "Family this week" strip shown on the parent overview screen.
/// Covers all fields from <c>FamilyTotalsStub</c> in the FE gold contract.
/// </summary>
public sealed class FamilySummaryDto
{
    /// <summary>Number of children who completed at least one lesson in the current rolling-7-day window.</summary>
    public int ActiveLearners { get; init; }

    /// <summary>Total distinct lessons completed across all children in the rolling-7-day window.</summary>
    public int LessonsCompleted { get; init; }

    /// <summary>Total XP earned across all children in the rolling-7-day window.</summary>
    public int TotalXp { get; init; }

    /// <summary>The single best (maximum) streak across all children (current streaks).</summary>
    public int BestStreakDays { get; init; }

    /// <summary>
    /// Total badges earned across all children (cumulative total, not windowed).
    /// Sourced from <c>IStudentBadgesQuery.GetByStudentIdAsync</c> — TotalCount field.
    /// </summary>
    public int BadgesEarned { get; init; }
}

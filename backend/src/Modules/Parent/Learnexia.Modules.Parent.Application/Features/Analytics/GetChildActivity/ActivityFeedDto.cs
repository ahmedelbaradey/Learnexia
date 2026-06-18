namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildActivity;

/// <summary>
/// E8 response DTO — recent activity feed items displayed on the parent activity screen.
/// Covers all fields from <c>ActivityEventStub[]</c> in the FE gold contract.
/// Read-time composed from Gamification (XP/badges) and Billing (energy) seams.
/// </summary>
public sealed class ActivityFeedDto
{
    public IReadOnlyList<ActivityEventDto> Events { get; init; } = [];
}

/// <summary>A single activity event in the feed.</summary>
public sealed class ActivityEventDto
{
    /// <summary>
    /// Activity kind identifier.
    /// Values: xp_earned | lesson_completed | badge_earned | energy_spent.
    /// These literals match the FE contract ACTIVITY_KIND values.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Event category for display grouping.
    /// Values: badge | energy | lesson | xp.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>UTC timestamp of the event.</summary>
    public DateTime OccurredAtUtc { get; init; }

    /// <summary>Numeric amount associated with the event (XP earned, energy spent, etc.). Zero when not applicable.</summary>
    public int Amount { get; init; }
}

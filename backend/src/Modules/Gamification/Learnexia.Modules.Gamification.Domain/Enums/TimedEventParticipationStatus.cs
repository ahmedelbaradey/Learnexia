namespace Learnexia.Modules.Gamification.Domain.Enums;

/// <summary>
/// Lifecycle state of a <see cref="Entities.TimedEventParticipation"/> row.
///
/// Intentionally separate from <see cref="MissionStatus"/> because timed-event participations
/// have no "Expired" semantic — a participation row is frozen in-place when the event window
/// closes (<c>EventEndUtc</c> passes), not transitioned to a distinct terminal state.
/// The ending-soon scan uses <c>EventEndUtc</c> directly, not an Expired flag.
///
/// Values must be stable (stored as int). Do not renumber.
/// </summary>
public enum TimedEventParticipationStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed  = 2,
}

namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised by <c>StreakAtRiskJob</c> (Pass 4, P4-12) for students whose in-progress timed-event
/// participation has <c>EventEndUtc</c> within <c>TimedEventParticipationOptions.EndingSoonLookaheadHours</c>
/// of the current scan time. Consumed by P9-12 to dispatch an ending-soon nudge.
///
/// Payload carries opaque int IDs + numeric scalars only — NO PII.
/// <see cref="Code"/> lets P9-12 deep-link to the specific event.
/// <see cref="MinutesRemaining"/> is the computed distance to <c>EventEndUtc</c> at scan time.
/// Mirrors <see cref="WeeklyMissionReminderIntegrationEvent"/> shape (P9-06).
/// </summary>
public sealed record TimedEventParticipationEndingSoonIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int TimedEventId,
    string Code,
    int Progress,
    int Target,
    int MinutesRemaining) : IIntegrationEvent;

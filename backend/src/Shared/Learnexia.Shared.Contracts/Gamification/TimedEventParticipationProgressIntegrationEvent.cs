namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student's timed-event participation first crosses the halfway-progress threshold.
/// Re-published from <c>TimedEventParticipationProgressDomainEvent</c> by the Gamification module
/// so the Notifications module (P9-12) can dispatch a halfway nudge without referencing
/// Gamification.Domain (module isolation rule 1).
///
/// Payload carries opaque int IDs + numeric scalars only — NO PII.
/// <see cref="Code"/> lets P9-12 deep-link to the specific event.
/// Mirrors <see cref="MissionCompletedIntegrationEvent"/> shape (P4-12).
/// </summary>
public sealed record TimedEventParticipationProgressIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int TimedEventId,
    string Code,
    int Progress,
    int Target) : IIntegrationEvent;

namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student completes a timed-event participation (Progress reaches Target) and the
/// completion reward XP is committed. Re-published from
/// <c>TimedEventParticipationCompletedDomainEvent</c> by the Gamification module so the
/// Notifications module (P9-12) can dispatch an achievement nudge without referencing
/// Gamification.Domain (module isolation rule 1).
///
/// Payload carries opaque int IDs + numeric scalars only — NO PII.
/// <see cref="Code"/> lets P9-12 deep-link to the specific event.
/// Mirrors <see cref="MissionCompletedIntegrationEvent"/> shape (P4-12).
/// </summary>
public sealed record TimedEventParticipationCompletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int TimedEventId,
    string Code) : IIntegrationEvent;

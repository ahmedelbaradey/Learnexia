namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student's hearts reach zero. Re-published by the Gamification module from
/// <c>HeartsDepletedDomainEvent</c> so Notifications can dispatch a re-engagement nudge
/// (StreakAtRisk category — "your hearts refill at HH:MM") without referencing Gamification.Domain
/// (module isolation, rule 1).
///
/// No event-specific payload beyond the base fields is required — the nudge copy is generated
/// from the student's profile at dispatch time.
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record HeartsDepletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId) : IIntegrationEvent;

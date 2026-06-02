namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student's hearts refill (passive or active). Re-published by the Gamification
/// module from <c>HeartsRefilledDomainEvent</c> so Notifications can dispatch an Achievement
/// nudge ("Your hearts are full — time to practice!") without referencing Gamification.Domain
/// (module isolation, rule 1).
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record HeartsRefilledIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int HeartsAfter) : IIntegrationEvent;

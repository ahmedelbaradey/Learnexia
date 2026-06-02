namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student levels up (XP threshold crossed). Re-published by the Gamification
/// module from <c>StudentLeveledUpDomainEvent</c> for downstream consumers (e.g. analytics,
/// potential Achievement nudge) without referencing Gamification.Domain (module isolation, rule 1).
///
/// Note: per P4-09 brief §"Not in scope", a level-up push notification is deferred; the
/// in-app popup is sufficient for MVP. This event is published for forward-compatibility
/// (analytics P5-03, future nudge). Notifications consumers are not required in P4-09.
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape.
/// </summary>
public sealed record StudentLeveledUpIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    int OldLevel,
    int NewLevel) : IIntegrationEvent;

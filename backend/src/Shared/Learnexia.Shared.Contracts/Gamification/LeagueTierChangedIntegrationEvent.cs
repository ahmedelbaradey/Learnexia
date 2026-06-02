namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student's league tier changes after a weekly rollover (promotion or demotion).
/// Re-published by the Gamification module from <c>LeagueTierChangedDomainEvent</c> for
/// downstream consumers without referencing Gamification.Domain (module isolation, rule 1).
///
/// <para>Tier values (e.g. "Bronze", "Silver", "Gold", "Platinum", "Diamond") and Status
/// ("Promoted", "Stayed", "Relegated") are carried as plain strings so the contract stays stable
/// across module-isolated enum changes (cross-module string convention, per P4-09 plan).</para>
///
/// Note: per P4-09 brief §"Not in scope", a league-tier push notification is deferred to a
/// follow-up story. This event is published for forward-compatibility. Notifications consumers
/// are not required in P4-09.
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape.
/// </summary>
public sealed record LeagueTierChangedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    string OldTier,
    string NewTier,
    string Status) : IIntegrationEvent;

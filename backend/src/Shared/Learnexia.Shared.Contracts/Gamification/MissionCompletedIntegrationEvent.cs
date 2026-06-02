namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student completes a mission. Re-published by the Gamification module from
/// <c>MissionCompletedDomainEvent</c> so Notifications can dispatch a re-engagement nudge
/// (Achievement category) without referencing Gamification.Domain (module isolation, rule 1).
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record MissionCompletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    string MissionCode,
    int RewardXp) : IIntegrationEvent;

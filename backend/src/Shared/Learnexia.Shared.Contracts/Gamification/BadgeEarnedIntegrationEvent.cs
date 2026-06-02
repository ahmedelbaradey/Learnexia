namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a student earns a badge. Re-published by the Gamification module from
/// <c>BadgeEarnedDomainEvent</c> so Notifications can dispatch a re-engagement nudge
/// (Achievement category) without referencing Gamification.Domain (module isolation, rule 1).
///
/// <para>Rarity is carried as a plain string (e.g. "Common", "Rare", "Epic") rather than the
/// domain enum so the contract stays stable across module-isolated enum changes.</para>
///
/// Payload carries opaque int IDs only — NO PII (no name, email, or child-data fields).
/// Mirrors <see cref="Learning.LessonCompletedIntegrationEvent"/> shape (FR-GM-8 / P4-09).
/// </summary>
public sealed record BadgeEarnedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int StudentId,
    string BadgeCode,
    int RewardXp,
    string Rarity) : IIntegrationEvent;

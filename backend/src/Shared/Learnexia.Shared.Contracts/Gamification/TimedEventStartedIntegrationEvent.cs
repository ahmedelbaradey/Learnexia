namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a timed event transitions to active (its window's <c>StartUtc</c> has been reached).
/// Re-published synthetically by <c>TimedEventStartedRepublisher</c> so the Notifications module
/// (and future consumers) can react without referencing Gamification.Domain (module isolation rule 1).
///
/// <para><c>Scope</c> is <see cref="TimedEventScopeDto"/> so consumers can interpret it without
/// a Gamification.Domain reference.</para>
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="StreakBrokenIntegrationEvent"/> shape.
/// </summary>
public sealed record TimedEventStartedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int TimedEventId,
    string Code,
    decimal Multiplier,
    TimedEventScopeDto Scope,
    DateTime StartUtc,
    DateTime EndUtc) : IIntegrationEvent;

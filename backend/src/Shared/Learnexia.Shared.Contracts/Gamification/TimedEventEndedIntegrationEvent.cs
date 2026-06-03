namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Raised when a timed event transitions to inactive (its window's <c>EndUtc</c> has passed).
/// Re-published synthetically by <c>TimedEventEndedRepublisher</c> so the Notifications module
/// (and future consumers) can react without referencing Gamification.Domain (module isolation rule 1).
///
/// Payload carries opaque int IDs only — NO PII.
/// Mirrors <see cref="TimedEventStartedIntegrationEvent"/> shape (abbreviated — no multiplier/scope
/// needed after the event ends; consumers use <c>Code</c> as the correlation key).
/// </summary>
public sealed record TimedEventEndedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int TimedEventId,
    string Code) : IIntegrationEvent;

using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Infrastructure.Jobs.TimedEventSweepJob"/> when a <c>TimedEvent</c>
/// transitions from inactive to active (its window's <c>StartUtc</c> has been reached).
///
/// Dispatched post-commit (sweep job calls <see cref="MediatR.IPublisher.Publish"/> directly
/// after saving the <c>IsActive = true</c> state — the job does not run inside a MediatR
/// command so <c>UnitOfWorkBehavior</c> is bypassed; domain events are raised explicitly here
/// by the job). Mirrors the <c>StreakBrokenIntegrationEvent</c> direct-publish pattern.
///
/// Consumed by:
///   <c>TimedEventStartedCacheInvalidator</c> — DELs <c>gamification:timed_events:active</c>.
///   <c>TimedEventStartedRepublisher</c> — re-publishes as <see cref="Shared.Contracts.Gamification.TimedEventStartedIntegrationEvent"/>.
/// </summary>
public sealed record TimedEventStartedDomainEvent(
    int TimedEventId,
    string Code,
    decimal Multiplier,
    TimedEventScope Scope,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = OccurredAtUtc;
}

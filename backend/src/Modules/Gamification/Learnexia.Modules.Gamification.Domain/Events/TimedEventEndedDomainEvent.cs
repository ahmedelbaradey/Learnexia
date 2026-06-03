using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Infrastructure.Jobs.TimedEventSweepJob"/> when a <c>TimedEvent</c>
/// transitions from active to inactive (its window's <c>EndUtc</c> has passed).
///
/// Dispatched post-commit (sweep job calls <see cref="MediatR.IPublisher.Publish"/> directly
/// after saving the <c>IsActive = false</c> state — the job does not run inside a MediatR
/// command so <c>UnitOfWorkBehavior</c> is bypassed; domain events are raised explicitly here
/// by the job). Mirrors <see cref="TimedEventStartedDomainEvent"/>.
///
/// Consumed by:
///   <c>TimedEventEndedCacheInvalidator</c> — DELs <c>gamification:timed_events:active</c>.
///   <c>TimedEventEndedRepublisher</c> — re-publishes as <see cref="Shared.Contracts.Gamification.TimedEventEndedIntegrationEvent"/>.
/// </summary>
public sealed record TimedEventEndedDomainEvent(
    int TimedEventId,
    string Code,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = OccurredAtUtc;
}

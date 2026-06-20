using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised when a student's timed-event participation first crosses the halfway-progress threshold
/// (configurable via <c>TimedEventParticipationOptions.HalfwayThresholdPercent</c>, default 50 %).
///
/// Dispatched strictly AFTER the UoW commit by <c>UnitOfWorkBehavior</c> (ADR 0002 §2).
/// Re-published as <see cref="Shared.Contracts.Gamification.TimedEventParticipationProgressIntegrationEvent"/>
/// by <c>TimedEventParticipationProgressDomainEventRepublisher</c> (P4-12).
///
/// Carries opaque ids + numeric scalars only — NO PII.
/// </summary>
public sealed record TimedEventParticipationProgressDomainEvent(
    int StudentId,
    int TimedEventId,
    string Code,
    int Progress,
    int Target) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

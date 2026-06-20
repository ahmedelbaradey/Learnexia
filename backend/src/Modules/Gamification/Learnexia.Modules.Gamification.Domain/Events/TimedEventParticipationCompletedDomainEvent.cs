using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised when a student completes a timed-event participation (Progress first reaches Target)
/// and the completion reward XP is applied. Dispatched strictly AFTER the UoW commit by
/// <c>UnitOfWorkBehavior</c> (ADR 0002 §2).
///
/// Re-published as <see cref="Shared.Contracts.Gamification.TimedEventParticipationCompletedIntegrationEvent"/>
/// by <c>TimedEventParticipationCompletedDomainEventRepublisher</c> (P4-12).
///
/// Carries opaque ids + numeric scalars only — NO PII.
/// Mirrors <see cref="MissionCompletedDomainEvent"/> shape.
/// </summary>
public sealed record TimedEventParticipationCompletedDomainEvent(
    int StudentId,
    int TimedEventId,
    string Code,
    int RewardXp,
    DateTime CompletedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

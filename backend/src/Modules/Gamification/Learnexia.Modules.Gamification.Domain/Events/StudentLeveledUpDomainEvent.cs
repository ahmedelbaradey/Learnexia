using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.ApplyAward"/> when the student's computed
/// level increases. Dispatched strictly AFTER a successful commit by <c>UnitOfWorkBehavior</c>.
///
/// No consumer exists in P4-02 — defined for forward-compat (P4-08 level-up confetti animation,
/// future analytics). The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public record StudentLeveledUpDomainEvent(int StudentId, int OldLevel, int NewLevel) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

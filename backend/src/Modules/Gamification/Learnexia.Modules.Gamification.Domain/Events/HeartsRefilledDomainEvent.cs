using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.RegainHeartFromPractice"/> and
/// <see cref="Entities.StudentXpProfile.RefreshHeartsAgainst"/> when hearts are restored — P4-04.
///
/// Dispatched strictly AFTER a successful commit by <c>UnitOfWorkBehavior</c>.
///
/// No consumer in P4-04 — defined for forward-compat:
///   P4-09 re-engagement analytics,
///   P4-08 FE hearts animation.
/// The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public sealed record HeartsRefilledDomainEvent(
    int StudentId,
    int NewHearts,
    string Source) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

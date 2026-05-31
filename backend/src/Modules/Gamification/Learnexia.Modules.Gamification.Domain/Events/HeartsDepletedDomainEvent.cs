using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.LoseHeart"/> when a student's Hearts counter
/// transitions to 0 (enters Practice Mode) — P4-04.
///
/// Dispatched strictly AFTER a successful commit by <c>UnitOfWorkBehavior</c>.
///
/// No consumer in P4-04 — defined for forward-compat:
///   P4-05 badge engine ("Practice Mode survivor"),
///   P4-09 re-engagement nudge ("your hearts refill at HH:MM").
/// The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public sealed record HeartsDepletedDomainEvent(int StudentId, DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = OccurredAtUtc;
}

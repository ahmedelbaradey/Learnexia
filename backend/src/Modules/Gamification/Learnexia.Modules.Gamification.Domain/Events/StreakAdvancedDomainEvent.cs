using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.AdvanceStreak"/> and
/// <see cref="Entities.StudentXpProfile.ResetStreakAndStart"/> when the student's streak advances
/// (including day-1 after a reset). Dispatched strictly AFTER a successful commit by
/// <c>UnitOfWorkBehavior</c>.
///
/// No consumer in P4-03 — defined for forward-compat:
///   P4-05 badge engine ("7-day streak" badge),
///   P4-08 FE flame animation,
///   P4-09 re-engagement analytics.
/// The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public record StreakAdvancedDomainEvent(
    int StudentId,
    int NewStreak,
    int LongestStreak,
    DateOnly ActivityDate) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

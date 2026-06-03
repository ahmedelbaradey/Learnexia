using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.ConsumeFreeze"/> when a freeze is consumed
/// to preserve the student's streak — P4-11.
///
/// Dispatched strictly AFTER a successful commit by <c>UnitOfWorkBehavior</c> (ADR 0002).
///
/// Consumed by:
///   <c>StreakFreezeConsumedCacheInvalidator</c> — DELs the student-streak cache key so the
///   updated <c>FreezeBalance</c> is returned on the next dashboard read.
///   <c>StreakFreezeConsumedRepublisher</c> — re-publishes as
///   <see cref="Learnexia.Shared.Contracts.Gamification.StreakFreezeConsumedIntegrationEvent"/>
///   so Notifications can dispatch a "freeze saved your streak" nudge (P4-09 forward-compat).
/// </summary>
public sealed record StreakFreezeConsumedDomainEvent(
    int StudentId,
    int CurrentStreak,
    int RemainingFreezeBalance,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = OccurredAtUtc;
}

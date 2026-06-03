using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.GrantFreeze"/> when a freeze is granted
/// to a student on a streak milestone — P4-11 security fix (Medium #2).
///
/// Dispatched strictly AFTER a successful commit by <c>UnitOfWorkBehavior</c> (ADR 0002).
///
/// Consumed by:
///   <c>StreakFreezeGrantedCacheInvalidator</c> — DELs the student-streak cache key so the
///   updated <c>FreezeBalance</c> is returned on the next dashboard read within 60s.
///
/// No integration event or re-engagement nudge — the grant is a quiet internal milestone
/// (no FE banner needed for Phase 3).
/// </summary>
public sealed record StreakFreezeGrantedDomainEvent(
    int StudentId,
    int NewFreezeBalance,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = OccurredAtUtc;
}

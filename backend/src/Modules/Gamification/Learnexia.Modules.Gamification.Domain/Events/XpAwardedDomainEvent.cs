using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised by <see cref="Entities.StudentXpProfile.ApplyAward"/> every time XP is added to a
/// student's profile — the single chokepoint for XP increments.
///
/// Consumed in-module by <c>XpAwardedLeagueHandler</c> (P4-07) which increments the student's
/// <c>LeagueMembership.WeeklyXp</c> counter. Forward-compatible for P4-10 (Redis sorted-set
/// writer can also subscribe without any contract change).
///
/// Dispatched strictly AFTER a successful commit by <c>UnitOfWorkBehavior</c> (ADR 0002).
/// </summary>
public sealed record XpAwardedDomainEvent(
    int StudentId,
    int Amount,
    int TotalXpAfter,
    XpReason Reason,
    DateTime OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised after a <see cref="Entities.StudentBadge"/> ledger row is committed.
/// Dispatched strictly AFTER the UoW commit by <c>UnitOfWorkBehavior</c> (ADR 0002 §2).
///
/// No consumer this cycle — defined for forward-compat:
///   P4-08 badge-earned pop-in animation,
///   P4-09 re-engagement nudge ("you earned the Streak Master badge!"),
///   P5-04 parent weekly report.
/// The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public sealed record BadgeEarnedDomainEvent(
    int StudentId,
    int BadgeDefinitionId,
    string Code,
    BadgeRarity Rarity,
    int RewardXp,
    DateTime AwardedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

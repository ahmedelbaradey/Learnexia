using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised after a <see cref="Entities.StudentMission"/> transitions to Completed and its reward XP
/// is committed. Dispatched strictly AFTER the UoW commit by <c>UnitOfWorkBehavior</c> (ADR 0002 §2).
///
/// No consumer this cycle — defined for forward-compat:
///   P4-08 mission-completed pop-in animation,
///   P4-09 re-engagement nudge ("You completed your daily mission!"),
///   P5-04 parent weekly report.
/// The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public sealed record MissionCompletedDomainEvent(
    int StudentId,
    int MissionDefinitionId,
    string Code,
    int RewardXp,
    DateTime CompletedAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

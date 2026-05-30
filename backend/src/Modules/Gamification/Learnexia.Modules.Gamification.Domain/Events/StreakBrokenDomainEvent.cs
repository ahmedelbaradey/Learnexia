using Learnexia.Shared.Kernel.DomainEvents;

namespace Learnexia.Modules.Gamification.Domain.Events;

/// <summary>
/// Raised when a student's streak is broken — either on the next qualifying activity after a gap
/// (lazy detection via <see cref="Entities.StudentXpProfile.ResetStreakAndStart"/>) or by the
/// daily sweep job (proactive detection via <see cref="Entities.StudentXpProfile.BreakStreak"/>).
///
/// No consumer in P4-03 — defined for forward-compat:
///   P4-09 re-engagement nudge ("streak-at-risk" and "streak-broken" notifications).
/// The <c>IsolatedNotificationPublisher</c> tolerates "no handler" gracefully.
/// </summary>
public record StreakBrokenDomainEvent(
    int StudentId,
    int PreviousStreak,
    DateOnly BrokenAtDate) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

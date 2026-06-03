using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="StreakFreezeConsumedDomainEvent"/> as
/// <see cref="StreakFreezeConsumedIntegrationEvent"/> so the Notifications module can dispatch
/// a "freeze saved your streak — keep going!" nudge without referencing Gamification.Domain
/// (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class StreakFreezeConsumedRepublisher
    : INotificationHandler<StreakFreezeConsumedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public StreakFreezeConsumedRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(StreakFreezeConsumedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new StreakFreezeConsumedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId,
                CurrentStreak: notification.CurrentStreak,
                RemainingFreezeBalance: notification.RemainingFreezeBalance), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-11: republish failed for {nameof(StreakFreezeConsumedDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId} — nudge may be missed.");
        }
    }
}

using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="MissionCompletedDomainEvent"/> as <see cref="MissionCompletedIntegrationEvent"/>
/// so the Notifications module can dispatch an Achievement nudge without referencing
/// Gamification.Domain (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class MissionCompletedDomainEventRepublisher
    : INotificationHandler<MissionCompletedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public MissionCompletedDomainEventRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(MissionCompletedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new MissionCompletedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId,
                MissionCode: notification.Code,
                RewardXp: notification.RewardXp), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: republish failed for {nameof(MissionCompletedDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"code={notification.Code} — nudge may be missed.");
        }
    }
}

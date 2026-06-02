using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="HeartsDepletedDomainEvent"/> as <see cref="HeartsDepletedIntegrationEvent"/>
/// so the Notifications module can dispatch a re-engagement nudge ("your hearts refill at HH:MM")
/// without referencing Gamification.Domain (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class HeartsDepletedDomainEventRepublisher
    : INotificationHandler<HeartsDepletedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public HeartsDepletedDomainEventRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(HeartsDepletedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new HeartsDepletedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: republish failed for {nameof(HeartsDepletedDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId} — nudge may be missed.");
        }
    }
}

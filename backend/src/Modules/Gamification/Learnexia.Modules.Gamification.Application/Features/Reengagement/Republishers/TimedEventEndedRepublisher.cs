using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="TimedEventEndedDomainEvent"/> as
/// <see cref="TimedEventEndedIntegrationEvent"/> so the Notifications module (and any future
/// consumer) can react without referencing Gamification.Domain (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class TimedEventEndedRepublisher
    : INotificationHandler<TimedEventEndedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public TimedEventEndedRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task Handle(TimedEventEndedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new TimedEventEndedIntegrationEvent(
                EventId:      Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                TimedEventId: notification.TimedEventId,
                Code:         notification.Code), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-11: republish failed for {nameof(TimedEventEndedDomainEvent)} " +
                $"eventId={notification.EventId}, code={notification.Code} — downstream consumers may miss this event.");
        }
    }
}

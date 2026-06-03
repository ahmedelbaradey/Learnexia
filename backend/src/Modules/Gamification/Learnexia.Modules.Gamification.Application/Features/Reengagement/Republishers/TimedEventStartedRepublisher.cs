using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="TimedEventStartedDomainEvent"/> as
/// <see cref="TimedEventStartedIntegrationEvent"/> so the Notifications module (and any future
/// consumer) can react without referencing Gamification.Domain (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class TimedEventStartedRepublisher
    : INotificationHandler<TimedEventStartedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public TimedEventStartedRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task Handle(TimedEventStartedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new TimedEventStartedIntegrationEvent(
                EventId:       Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                TimedEventId:  notification.TimedEventId,
                Code:          notification.Code,
                Multiplier:    notification.Multiplier,
                Scope:         (TimedEventScopeDto)(int)notification.Scope,
                StartUtc:      notification.StartUtc,
                EndUtc:        notification.EndUtc), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-11: republish failed for {nameof(TimedEventStartedDomainEvent)} " +
                $"eventId={notification.EventId}, code={notification.Code} — downstream consumers may miss this event.");
        }
    }
}

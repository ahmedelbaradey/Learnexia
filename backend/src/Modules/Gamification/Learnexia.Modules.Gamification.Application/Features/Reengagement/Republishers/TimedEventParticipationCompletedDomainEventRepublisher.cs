using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="TimedEventParticipationCompletedDomainEvent"/> as
/// <see cref="TimedEventParticipationCompletedIntegrationEvent"/> so the Notifications module
/// (P9-12) can dispatch a completion nudge without referencing Gamification.Domain
/// (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// Mirrors <see cref="MissionCompletedDomainEventRepublisher"/> shape exactly (P4-12).
/// </summary>
public sealed class TimedEventParticipationCompletedDomainEventRepublisher
    : INotificationHandler<TimedEventParticipationCompletedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public TimedEventParticipationCompletedDomainEventRepublisher(
        IPublisher publisher,
        ILoggerManager logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task Handle(
        TimedEventParticipationCompletedDomainEvent notification,
        CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new TimedEventParticipationCompletedIntegrationEvent(
                EventId:      Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId:    notification.StudentId,
                TimedEventId: notification.TimedEventId,
                Code:         notification.Code), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-12: republish failed for {nameof(TimedEventParticipationCompletedDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"code={notification.Code} — completion nudge may be missed.");
        }
    }
}

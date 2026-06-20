using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="TimedEventParticipationProgressDomainEvent"/> as
/// <see cref="TimedEventParticipationProgressIntegrationEvent"/> so the Notifications module
/// (P9-12) can dispatch a halfway-progress nudge without referencing Gamification.Domain
/// (module isolation rule 1).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// Mirrors <see cref="MissionCompletedDomainEventRepublisher"/> shape exactly (P4-12).
/// </summary>
public sealed class TimedEventParticipationProgressDomainEventRepublisher
    : INotificationHandler<TimedEventParticipationProgressDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public TimedEventParticipationProgressDomainEventRepublisher(
        IPublisher publisher,
        ILoggerManager logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task Handle(
        TimedEventParticipationProgressDomainEvent notification,
        CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new TimedEventParticipationProgressIntegrationEvent(
                EventId:      Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId:    notification.StudentId,
                TimedEventId: notification.TimedEventId,
                Code:         notification.Code,
                Progress:     notification.Progress,
                Target:       notification.Target), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-12: republish failed for {nameof(TimedEventParticipationProgressDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"code={notification.Code} — halfway nudge may be missed.");
        }
    }
}

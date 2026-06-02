using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="HeartsRefilledDomainEvent"/> as <see cref="HeartsRefilledIntegrationEvent"/>
/// so the Notifications module can dispatch an Achievement nudge ("Your hearts are full — time to
/// practice!") without referencing Gamification.Domain (module isolation rule 1).
///
/// <c>HeartsAfter</c> is carried from <c>NewHearts</c> to give the Notifications copy template the
/// current hearts count; <c>Source</c> is not exposed in the integration event (Notifications does
/// not need to distinguish passive vs practice-regain for the nudge copy).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class HeartsRefilledDomainEventRepublisher
    : INotificationHandler<HeartsRefilledDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public HeartsRefilledDomainEventRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(HeartsRefilledDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new HeartsRefilledIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId,
                HeartsAfter: notification.NewHearts), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: republish failed for {nameof(HeartsRefilledDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId} — nudge may be missed.");
        }
    }
}

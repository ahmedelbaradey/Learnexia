using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="StudentLeveledUpDomainEvent"/> as <see cref="StudentLeveledUpIntegrationEvent"/>
/// for downstream consumers (analytics P5-03, future Achievement nudge) without referencing
/// Gamification.Domain (module isolation rule 1).
///
/// Note: per P4-09 brief §"Not in scope", a level-up push notification is deferred; the in-app
/// popup is sufficient for MVP. This republisher is shipped for forward-compatibility.
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class StudentLeveledUpDomainEventRepublisher
    : INotificationHandler<StudentLeveledUpDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public StudentLeveledUpDomainEventRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(StudentLeveledUpDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new StudentLeveledUpIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId,
                OldLevel: notification.OldLevel,
                NewLevel: notification.NewLevel), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: republish failed for {nameof(StudentLeveledUpDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"oldLevel={notification.OldLevel}, newLevel={notification.NewLevel} — analytics may be missed.");
        }
    }
}

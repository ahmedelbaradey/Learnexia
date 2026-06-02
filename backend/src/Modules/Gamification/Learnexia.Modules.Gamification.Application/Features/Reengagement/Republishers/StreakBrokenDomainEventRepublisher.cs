using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="StreakBrokenDomainEvent"/> as <see cref="StreakBrokenIntegrationEvent"/>
/// so the Notifications module can dispatch a StreakAtRisk nudge without referencing
/// Gamification.Domain (module isolation rule 1).
///
/// This path fires when <c>StudentXpProfile.ResetStreakAndStart</c> detects a gap (lazy detection
/// via <c>AdvanceStreakCommandHandler</c>). The <c>StreakSweepJob</c> emits the integration event
/// directly (no domain event raised from the bulk-update path). Both paths produce
/// <see cref="StreakBrokenIntegrationEvent"/> — deduplication at the Notifications side.
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class StreakBrokenDomainEventRepublisher
    : INotificationHandler<StreakBrokenDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public StreakBrokenDomainEventRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(StreakBrokenDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new StreakBrokenIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId,
                PreviousStreak: notification.PreviousStreak,
                BrokenAtDate: notification.BrokenAtDate), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: republish failed for {nameof(StreakBrokenDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId} — nudge may be missed.");
        }
    }
}

using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Reengagement.Republishers;

/// <summary>
/// Re-publishes <see cref="BadgeEarnedDomainEvent"/> as <see cref="BadgeEarnedIntegrationEvent"/>
/// so the Notifications module can dispatch an Achievement nudge without referencing
/// Gamification.Domain (module isolation rule 1).
///
/// <c>Rarity</c> is carried as a plain string (enum.ToString()) so the contract stays stable
/// across module-isolated enum changes (cross-module string convention).
///
/// Fail-soft per ADR 0002 §3: catch + log; do NOT rethrow.
/// Auto-registered via <c>Gamification.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class BadgeEarnedDomainEventRepublisher
    : INotificationHandler<BadgeEarnedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public BadgeEarnedDomainEventRepublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(BadgeEarnedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new BadgeEarnedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: notification.OccurredOnUtc,
                StudentId: notification.StudentId,
                BadgeCode: notification.Code,
                RewardXp: notification.RewardXp,
                Rarity: notification.Rarity.ToString()), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: republish failed for {nameof(BadgeEarnedDomainEvent)} " +
                $"eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"code={notification.Code} — nudge may be missed.");
        }
    }
}

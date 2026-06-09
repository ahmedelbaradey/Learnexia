using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Relay;

/// <summary>
/// Relays the intra-module <see cref="AdminActionPerformedDomainEvent"/> into the cross-module
/// <see cref="AdminActionPerformedEvent"/> integration event (Shared.Contracts).
///
/// Mirrors <c>Learning.Application.Features.Admin.Relay.AdminActionPerformedDomainEventRelayHandler</c>
/// exactly — same fail-soft semantics, same post-commit guarantee via <c>UnitOfWorkBehavior</c>.
///
/// Fail-soft: any exception is caught and logged. A relay failure must NEVER propagate back to the
/// producing command — the primary mutation has already committed (ADR 0002 §3).
/// </summary>
public sealed class AdminActionPerformedDomainEventRelayHandler
    : INotificationHandler<AdminActionPerformedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public AdminActionPerformedDomainEventRelayHandler(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(AdminActionPerformedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new AdminActionPerformedEvent(
                EventId:          notification.EventId,
                OccurredAtUtc:    notification.OccurredOnUtc,
                AdminUserId:      notification.AdminUserId,
                Action:           notification.Action,
                TargetEntityType: notification.TargetEntityType,
                TargetEntityId:   notification.TargetEntityId,
                Details:          notification.Details), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-13: relay failed for {nameof(AdminActionPerformedDomainEvent)} " +
                $"eventId={notification.EventId}, action={notification.Action}, " +
                $"entityType={notification.TargetEntityType}, entityId={notification.TargetEntityId} " +
                $"— audit row may be missed but primary mutation is committed.");
        }
    }
}

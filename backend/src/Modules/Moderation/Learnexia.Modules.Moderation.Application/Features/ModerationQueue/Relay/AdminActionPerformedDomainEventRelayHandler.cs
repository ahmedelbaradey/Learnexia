using Learnexia.Modules.Moderation.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Relay;

/// <summary>
/// Relays the intra-module <see cref="AdminActionPerformedDomainEvent"/> (raised by
/// <c>ReviewModerationItemCommandHandler</c> on the <c>ModerationItem</c> aggregate) into the
/// cross-module <see cref="AdminActionPerformedEvent"/> integration event (Shared.Contracts).
///
/// <para>This is the Moderation module's mirror of
/// <c>Learning.Application.Features.Admin.Relay.AdminActionPerformedDomainEventRelayHandler</c>.
/// The pattern is identical (ADR 0002 §2):</para>
/// <list type="number">
///   <item>The handler raises <see cref="AdminActionPerformedDomainEvent"/> on the <c>ModerationItem</c>
///     aggregate before returning.</item>
///   <item><c>UnitOfWorkBehavior</c> commits, then dispatches domain events via
///     <c>IDomainEventDispatcher</c>.</item>
///   <item>This relay handler receives the dispatched event and publishes the integration event
///     (cross-module, fan-out to <c>AuditLogEventHandler</c> in the same Moderation module).</item>
/// </list>
///
/// <para>Fail-soft: any exception is caught and logged. A relay failure MUST NEVER propagate back
/// to the producing command (the primary mutation has already committed).</para>
/// </summary>
public sealed class AdminActionPerformedDomainEventRelayHandler
    : INotificationHandler<AdminActionPerformedDomainEvent>
{
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public AdminActionPerformedDomainEventRelayHandler(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger    = logger;
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
                $"P7-09 Moderation: relay failed for {nameof(AdminActionPerformedDomainEvent)} " +
                $"eventId={notification.EventId}, action={notification.Action}, " +
                $"entityType={notification.TargetEntityType}, entityId={notification.TargetEntityId} " +
                $"— audit row may be missed but primary mutation is committed.");
        }
    }
}

using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Learning.Application.Features.Admin.Relay;

/// <summary>
/// Relays the intra-module <see cref="AdminActionPerformedDomainEvent"/> into the cross-module
/// <see cref="AdminActionPerformedEvent"/> integration event (Shared.Contracts).
///
/// WHY THIS EXISTS (P7-12 post-commit fix, ADR 0002 §2):
/// Learning admin write handlers previously called <c>IPublisher.Publish(AdminActionPerformedEvent)</c>
/// INSIDE the handler body — BEFORE <c>UnitOfWorkBehavior.CommitAsync</c>, so a rolled-back command
/// would still emit an audit event (phantom). The fix:
///   1. Each handler raises <see cref="AdminActionPerformedDomainEvent"/> on the mutated
///      <see cref="Shared.Kernel.Entities.AggregateRoot"/> before returning.
///   2. <c>UnitOfWorkBehavior</c> collects tracked aggregate domain events and dispatches them
///      via <c>IDomainEventDispatcher</c> ONLY AFTER <c>CommitAsync</c> succeeds.
///   3. This handler receives the dispatched domain event and re-publishes the integration event.
///
/// The result: <see cref="AdminActionPerformedEvent"/> is now published strictly post-commit.
/// A rollback produces zero audit events because the domain event is never collected.
///
/// Fail-soft: any exception is caught and logged. A relay failure must NEVER propagate back to the
/// producing command — the primary mutation has already committed (ADR 0002 §3).
///
/// Auto-registered via <c>Learning.Application.AssemblyReference</c> (host MediatR scan).
/// Module isolation: this handler lives in Learning.Application; only the Shared.Contracts
/// integration event crosses the module boundary.
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
                $"P7-12: relay failed for {nameof(AdminActionPerformedDomainEvent)} " +
                $"eventId={notification.EventId}, action={notification.Action}, " +
                $"entityType={notification.TargetEntityType}, entityId={notification.TargetEntityId} " +
                $"— audit row may be missed but primary mutation is committed.");
        }
    }
}

using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Moderation.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AdminActionPerformedEvent"/> (produced by the Learning and
/// Identity modules after an admin mutation commits). Persists one immutable <see cref="Domain.Entities.AuditLog"/>
/// row per event into the Moderation module's own store via <see cref="IAuditLogWriter"/>.
///
/// <para>This handler is thin (Option C): it delegates all persistence, idempotency, and
/// fail-soft logic to <see cref="IAuditLogWriter.AppendIfNotExistsAsync"/>.</para>
///
/// <para>Design constraints (P7-12 brief §Piece 3):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — the writer skips if a row with the same <see cref="AdminActionPerformedEvent.EventId"/>
///   already exists, so redelivery never duplicates a row.</item>
///   <item><b>Fail-soft</b> — the writer swallows persistence exceptions; this handler never
///   throws to the publisher. The <c>IsolatedNotificationPublisher</c> at the Host already
///   isolates per-handler failures, but the writer's inner try/catch is the explicit guarantee.</item>
///   <item><b>No mutation</b> — the only write path. No Update or Delete is possible via this handler.</item>
///   <item><b>PII-safe</b> — the event's <c>Details</c> field already carries only ids + enum states.
///   This handler passes it through verbatim without enrichment.</item>
/// </list>
/// </summary>
public sealed class AuditLogEventHandler : INotificationHandler<AdminActionPerformedEvent>
{
    private readonly IAuditLogWriter _writer;
    private readonly ILoggerManager _logger;

    public AuditLogEventHandler(IAuditLogWriter writer, ILoggerManager logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public async Task Handle(AdminActionPerformedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Moderation: received AdminActionPerformedEvent " +
            $"(EventId={notification.EventId}, Action={notification.Action}, " +
            $"AdminUserId={notification.AdminUserId}).");

        // Delegate all persistence + idempotency + fail-soft to the writer.
        // The writer's own try/catch ensures no exception escapes to the publisher.
        await _writer.AppendIfNotExistsAsync(
            eventId:          notification.EventId,
            occurredAtUtc:    notification.OccurredAtUtc,
            adminUserId:      notification.AdminUserId,
            action:           notification.Action,
            targetEntityType: notification.TargetEntityType,
            targetEntityId:   notification.TargetEntityId,
            details:          notification.Details,
            cancellationToken: cancellationToken);
    }
}

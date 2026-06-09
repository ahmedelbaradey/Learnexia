using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Moderation.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AdminActionPerformedEvent"/> (produced by the Learning and
/// Identity modules after an admin mutation commits). Persists one immutable <see cref="AuditLog"/>
/// row per event into the Moderation module's own DbContext.
///
/// <para>Design constraints (P7-12 brief §Piece 3):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — skips if a row with the same <see cref="AdminActionPerformedEvent.EventId"/>
///   already exists, so redelivery never duplicates a row.</item>
///   <item><b>Fail-soft</b> — wraps the entire handler in try/catch + LogWarn. A persistence failure
///   is logged but MUST NOT propagate back to the publisher or abort sibling handlers. The
///   <c>IsolatedNotificationPublisher</c> at the Host already isolates per-handler failures, but the
///   inner try/catch is here for observability and explicit documentation of the intent.</item>
///   <item><b>No mutation</b> — the only write path. No Update or Delete is possible via this handler.</item>
///   <item><b>PII-safe</b> — the event's <c>Details</c> field already carries only ids + enum states.
///   This handler stores it verbatim without enrichment.</item>
/// </list>
///
/// Mirrors <c>Notifications/UserRegisteredIntegrationEventHandler</c> for the consumer shape.
/// </summary>
public sealed class AuditLogEventHandler : INotificationHandler<AdminActionPerformedEvent>
{
    private readonly IModerationDbContext _db;
    private readonly ILoggerManager _logger;

    public AuditLogEventHandler(IModerationDbContext db, ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(AdminActionPerformedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInfo(
                $"Moderation: received AdminActionPerformedEvent " +
                $"(EventId={notification.EventId}, Action={notification.Action}, " +
                $"AdminUserId={notification.AdminUserId}).");

            // Idempotency: skip if this event was already persisted (redelivery guard).
            var alreadyExists = await _db.AuditLogs
                .AnyAsync(a => a.EventId == notification.EventId, cancellationToken);

            if (alreadyExists)
            {
                _logger.LogInfo(
                    $"Moderation: AuditLog row for EventId={notification.EventId} already exists; skipping.");
                return;
            }

            // Map event → entity. OccurredAtUtc is sourced from the event (action time),
            // NOT DateTime.Now (which would record insertion time). Per the Npgsql legacy-timestamp
            // note: the AppContext switch "Npgsql.EnableLegacyTimestampBehavior" is enabled at the
            // Host, so DateTime values are accepted regardless of Kind without conversion needed here.
            var auditLog = new AuditLog
            {
                EventId          = notification.EventId,
                AdminUserId      = notification.AdminUserId,
                Action           = notification.Action,
                TargetEntityType = notification.TargetEntityType,
                TargetEntityId   = notification.TargetEntityId,
                Details          = notification.Details,
                OccurredAtUtc    = notification.OccurredAtUtc,
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInfo(
                $"Moderation: persisted AuditLog row Id={auditLog.Id} " +
                $"for EventId={notification.EventId}.");
        }
        catch (Exception ex)
        {
            // Fail-soft: a persistence failure MUST NOT propagate back to the publisher.
            // The audit log is an append-only best-effort store; a single failed row is
            // preferable to crashing the admin command that produced the event.
            // Log type name + EventId (GUID — no PII) in the warn; full exception in error.
            _logger.LogWarn(
                $"Moderation: AuditLogEventHandler failed ({ex.GetType().Name}) for EventId={notification.EventId}.");
            _logger.LogError(ex, $"Moderation: AuditLogEventHandler exception detail for EventId={notification.EventId}.");
        }
    }
}

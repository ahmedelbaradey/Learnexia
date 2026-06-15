using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Moderation.Infrastructure.Service;

/// <summary>
/// Option-C implementation of <see cref="IAuditLogWriter"/>.
/// Owns the full write path for the append-only audit store:
/// idempotency guard (AnyAsync on EventId), entity construction, and SaveChangesAsync.
///
/// <para><strong>Fail-soft contract:</strong> all persistence exceptions are caught, logged,
/// and swallowed. A failure here must never propagate back to the cross-module publisher
/// (<c>AdminActionPerformedEvent</c>) or abort sibling notification handlers.</para>
///
/// <para><strong>Idempotency:</strong> the <see cref="AuditLog.EventId"/> unique index on the
/// DB is the durable guard. The in-process <c>AnyAsync</c> check is a fast-path pre-check
/// that avoids the round-trip insert + unique-constraint violation on redelivery.</para>
/// </summary>
internal sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly ModerationDbContext _db;
    private readonly ILoggerManager _logger;

    public AuditLogWriter(ModerationDbContext db, ILoggerManager logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AppendIfNotExistsAsync(
        Guid eventId,
        DateTime occurredAtUtc,
        int adminUserId,
        string action,
        string targetEntityType,
        int targetEntityId,
        string? details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo(
                $"Moderation: AuditLogWriter received append request " +
                $"(EventId={eventId}, Action={action}, AdminUserId={adminUserId}).");

            // Fast-path idempotency: skip if a row for this EventId already exists.
            var alreadyExists = await _db.AuditLogs
                .AnyAsync(a => a.EventId == eventId, cancellationToken);

            if (alreadyExists)
            {
                _logger.LogInfo(
                    $"Moderation: AuditLog row for EventId={eventId} already exists; skipping.");
                return;
            }

            // Map event fields → entity. OccurredAtUtc is sourced from the event (action time),
            // NOT DateTime.Now (which would record insertion time). Per the Npgsql legacy-timestamp
            // note: the AppContext switch "Npgsql.EnableLegacyTimestampBehavior" is enabled at the
            // Host, so DateTime values are accepted regardless of Kind.
            var auditLog = new AuditLog
            {
                EventId          = eventId,
                AdminUserId      = adminUserId,
                Action           = action,
                TargetEntityType = targetEntityType,
                TargetEntityId   = targetEntityId,
                Details          = details,
                OccurredAtUtc    = occurredAtUtc,
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInfo(
                $"Moderation: persisted AuditLog row Id={auditLog.Id} " +
                $"for EventId={eventId}.");
        }
        catch (Exception ex)
        {
            // Fail-soft: a persistence failure MUST NOT propagate back to the publisher.
            // The audit log is an append-only best-effort store; a single failed row is
            // preferable to crashing the admin command that produced the event.
            // Log type name + EventId (GUID — no PII) in the warn; full exception in error.
            _logger.LogWarn(
                $"Moderation: AuditLogWriter failed ({ex.GetType().Name}) for EventId={eventId}.");
            _logger.LogError(ex, $"Moderation: AuditLogWriter exception detail for EventId={eventId}.");
        }
    }
}

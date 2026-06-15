namespace Learnexia.Modules.Moderation.Application.Abstractions;

/// <summary>
/// Application-layer seam for the Moderation write side (append-only event store).
/// The implementation (in Infrastructure) owns the DbContext, idempotency AnyAsync guard,
/// entity construction, and the explicit SaveChangesAsync call.
///
/// <para><strong>Fail-soft contract:</strong> the implementation MUST swallow all persistence
/// exceptions (log them but not rethrow). A persistence failure must never propagate back to
/// the cross-module publisher or abort sibling notification handlers.</para>
///
/// <para><strong>Idempotency contract:</strong> calling this method more than once with the
/// same <paramref name="eventId"/> must produce exactly one row in the database.</para>
/// </summary>
public interface IAuditLogWriter
{
    /// <summary>
    /// Persists one <c>AuditLog</c> row if no row with <paramref name="eventId"/> already exists.
    /// Swallows all exceptions (fail-soft: logs but does not rethrow).
    /// </summary>
    Task AppendIfNotExistsAsync(
        Guid eventId,
        DateTime occurredAtUtc,
        int adminUserId,
        string action,
        string targetEntityType,
        int targetEntityId,
        string? details,
        CancellationToken cancellationToken = default);
}

using Learnexia.Modules.Moderation.Domain.Enums;

namespace Learnexia.Modules.Moderation.Application.Abstractions;

/// <summary>
/// Application-layer seam for the idempotent enqueue write path.
/// The implementation (in Infrastructure) owns the DbContext, idempotency AnyAsync guard,
/// entity construction, and the explicit SaveChangesAsync call (Option C).
///
/// <para><strong>Fail-soft contract:</strong> the implementation MUST swallow all persistence
/// exceptions (log them but not rethrow). A failure here must never propagate back to
/// the cross-module publisher or abort sibling notification handlers.</para>
///
/// <para><strong>Idempotency contract:</strong> calling this method more than once with the
/// same <paramref name="sourceEventId"/> must produce exactly one row (dedup on the unique index).</para>
/// </summary>
public interface IModerationQueueWriter
{
    /// <summary>
    /// Enqueues a <c>ModerationItem</c> if no row with <paramref name="sourceEventId"/> already exists.
    /// Sets <c>Status=Pending</c>. Swallows all exceptions (fail-soft: logs but does not rethrow).
    /// </summary>
    Task EnqueueIfNotExistsAsync(
        Guid sourceEventId,
        ModerationSource source,
        string contentReference,
        string safetyVerdict,
        int? studentId,
        string? taskKind,
        string? subjectCode,
        int? grade,
        DateTime detectedAt,
        CancellationToken cancellationToken = default);
}

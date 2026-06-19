using Learnexia.Modules.Analytics.Domain.Entities;

namespace Learnexia.Modules.Analytics.Application.Abstractions;

/// <summary>
/// Append-only store for <see cref="ActivityEvent"/> rows, injected into
/// integration-event capture consumers in the Application layer.
///
/// <para>This thin seam keeps consumers from taking a hard reference to
/// <c>AnalyticsDbContext</c> (which lives in Infrastructure). The Infrastructure
/// implementation wraps <c>AnalyticsDbContext</c> directly.</para>
///
/// <para><strong>Append-only:</strong> no Update or Delete methods exist.
/// Per ADR-0001: direct SaveChangesAsync is correct for append-only event logs.</para>
///
/// <para><strong>Fail-soft contract:</strong> the implementation MUST swallow all persistence
/// exceptions (log them but not rethrow). A failure here must never propagate back to
/// the cross-module publisher or abort sibling notification handlers.</para>
///
/// <para><strong>Idempotency contract:</strong> the implementation checks for an existing row
/// by <see cref="ActivityEvent.SourceEventId"/> before inserting. Redelivery of the same
/// event MUST produce exactly one row — the unique index on <c>SourceEventId</c> is the
/// durable guard.</para>
/// </summary>
public interface IActivityEventStore
{
    /// <summary>
    /// Appends an <see cref="ActivityEvent"/> if no row with the same
    /// <see cref="ActivityEvent.SourceEventId"/> already exists. Swallows all exceptions
    /// (fail-soft: logs but does not rethrow). Pass <c>userId: 0</c> — system telemetry row.
    /// </summary>
    Task AddAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default);
}

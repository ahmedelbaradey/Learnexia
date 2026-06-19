using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Analytics.Infrastructure.Persistence;

/// <summary>
/// Infrastructure implementation of <see cref="IActivityEventStore"/>.
/// Wraps <see cref="AnalyticsDbContext"/> for append-only <see cref="ActivityEvent"/> persistence.
///
/// <para>Write path: called from fail-soft <c>INotificationHandler&lt;T&gt;</c> capture consumers
/// in the Application layer. Append-only; no update or delete.
/// Per ADR-0001: direct SaveChangesAsync is correct for append-only event logs
/// (same pattern as AiUsageLogStore / AiSafetyEventStore).</para>
///
/// <para><strong>Idempotency:</strong> the <see cref="ActivityEvent.SourceEventId"/> unique index
/// on the DB is the durable guard. The in-process <c>AnyAsync</c> check is a fast-path pre-check
/// that avoids the round-trip insert + unique-constraint violation on redelivery.
/// Mirrors <c>ModerationQueueWriter.EnqueueIfNotExistsAsync</c>.</para>
///
/// <para>This class MUST NOT throw — any exception is caught, logged, and swallowed
/// so the producer's post-commit publish is unaffected (NFR-1 fail-soft).</para>
/// </summary>
internal sealed class ActivityEventStore : IActivityEventStore
{
    private readonly AnalyticsDbContext _db;
    private readonly ILoggerManager _logger;

    public ActivityEventStore(AnalyticsDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(ActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo(
                $"Analytics: ActivityEventStore.AddAsync " +
                $"(SourceEventId={activityEvent.SourceEventId}, EventType={activityEvent.EventType}, " +
                $"StudentId={activityEvent.StudentId}).");

            // Fast-path idempotency: skip if a row for this SourceEventId already exists.
            // The unique index on SourceEventId is the durable guard; this check is a
            // best-effort fast path to avoid the insert + constraint-violation round-trip.
            var alreadyExists = await _db.ActivityEvents
                .AnyAsync(e => e.SourceEventId == activityEvent.SourceEventId, cancellationToken);

            if (alreadyExists)
            {
                _logger.LogInfo(
                    $"Analytics: ActivityEvent for SourceEventId={activityEvent.SourceEventId} " +
                    $"already exists; skipping (idempotent redelivery).");
                return;
            }

            _db.ActivityEvents.Add(activityEvent);
            // Pass userId = 0: system-generated telemetry row, no authenticated user in the
            // consumer pipeline. Mirrors AiUsageLogStore / ModerationQueueWriter.
            await _db.SaveChangesAsync(userId: 0);

            _logger.LogInfo(
                $"Analytics: persisted ActivityEvent Id={activityEvent.Id} " +
                $"(SourceEventId={activityEvent.SourceEventId}).");
        }
        catch (Exception ex)
        {
            // Fail-soft: MUST NOT propagate — a sink failure must not break the producer's
            // post-commit publish or abort sibling notification handlers. Log and swallow.
            _logger.LogWarn(
                $"Analytics: ActivityEventStore failed ({ex.GetType().Name}) " +
                $"for SourceEventId={activityEvent.SourceEventId}, EventType={activityEvent.EventType}.");
            _logger.LogError(ex,
                $"Analytics: ActivityEventStore exception detail for SourceEventId={activityEvent.SourceEventId}.");
        }
    }
}

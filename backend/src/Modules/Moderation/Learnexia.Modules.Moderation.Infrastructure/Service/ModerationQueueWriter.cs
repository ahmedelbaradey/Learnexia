using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Moderation.Infrastructure.Service;

/// <summary>
/// Option-C implementation of <see cref="IModerationQueueWriter"/>.
/// Owns the full write path for the moderation queue append path:
/// idempotency guard (AnyAsync on SourceEventId), entity construction, and SaveChangesAsync.
///
/// <para><strong>Fail-soft contract:</strong> all persistence exceptions are caught, logged,
/// and swallowed. A failure here must never propagate back to the cross-module publisher
/// (<c>AiOutputFlaggedIntegrationEvent</c>) or abort sibling notification handlers.</para>
///
/// <para><strong>Idempotency:</strong> the <see cref="ModerationItem.SourceEventId"/> unique index
/// on the DB is the durable guard. The in-process <c>AnyAsync</c> check is a fast-path pre-check
/// that avoids the round-trip insert + unique-constraint violation on redelivery.</para>
/// </summary>
internal sealed class ModerationQueueWriter : IModerationQueueWriter
{
    private readonly ModerationDbContext _db;
    private readonly ILoggerManager _logger;

    public ModerationQueueWriter(ModerationDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task EnqueueIfNotExistsAsync(
        Guid sourceEventId,
        ModerationSource source,
        string contentReference,
        string safetyVerdict,
        int? studentId,
        string? taskKind,
        string? subjectCode,
        int? grade,
        DateTime detectedAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo(
                $"Moderation: ModerationQueueWriter enqueue request " +
                $"(SourceEventId={sourceEventId}, Source={source}, ContentRef={contentReference}).");

            // Fast-path idempotency: skip if a row for this SourceEventId already exists.
            var alreadyExists = await _db.ModerationItems
                .AnyAsync(m => m.SourceEventId == sourceEventId, cancellationToken);

            if (alreadyExists)
            {
                _logger.LogInfo(
                    $"Moderation: ModerationItem for SourceEventId={sourceEventId} already exists; skipping.");
                return;
            }

            var item = new ModerationItem
            {
                SourceEventId    = sourceEventId,
                Source           = source,
                Status           = ModerationStatus.Pending,
                ContentReference = contentReference,
                SafetyVerdict    = safetyVerdict,
                StudentId        = studentId,
                TaskKind         = taskKind,
                SubjectCode      = subjectCode,
                Grade            = grade,
                DetectedAt       = detectedAt,
            };

            _db.ModerationItems.Add(item);
            // Pass userId=0 (system-generated event, no authenticated user in the ingest path).
            await _db.SaveChangesAsync(userId: 0);

            _logger.LogInfo(
                $"Moderation: enqueued ModerationItem Id={item.Id} " +
                $"for SourceEventId={sourceEventId}.");
        }
        catch (Exception ex)
        {
            // Fail-soft: a persistence failure MUST NOT propagate back to the cross-module publisher.
            _logger.LogWarn(
                $"Moderation: ModerationQueueWriter failed ({ex.GetType().Name}) " +
                $"for SourceEventId={sourceEventId}.");
            _logger.LogError(ex,
                $"Moderation: ModerationQueueWriter exception detail for SourceEventId={sourceEventId}.");
        }
    }
}

using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Moderation.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AiOutputFlaggedIntegrationEvent"/> (produced by the
/// Ai module after the Safety Layer persists a blocked/flagged <see cref="Learnexia.Modules.Ai.Domain.Entities.SafetyEvent"/>).
/// Enqueues one <c>ModerationItem</c> (Status=Pending, Source=AiOutput) via
/// <see cref="IModerationQueueWriter"/> (Option C, idempotent).
///
/// <para>This handler is thin (Option C): it delegates all persistence, idempotency, and
/// fail-soft logic to <see cref="IModerationQueueWriter.EnqueueIfNotExistsAsync"/>.</para>
///
/// <para>Design constraints (P7-09 brief §Ingest):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — the writer skips if a row with the same
///     <see cref="AiOutputFlaggedIntegrationEvent.SourceEventId"/> already exists (unique index guard),
///     so redelivery never duplicates a row.</item>
///   <item><b>Fail-soft</b> — the writer swallows persistence exceptions; this handler never
///     throws to the publisher. The <c>IsolatedNotificationPublisher</c> at the Host already
///     isolates per-handler failures, but the writer's inner try/catch is the explicit guarantee.</item>
///   <item><b>PII-light invariant preserved</b> — no raw prompt or response content is written;
///     only reason codes, check names, and opaque content references are stored.</item>
///   <item><b>Module isolation</b> — this handler subscribes via <c>Shared.Contracts</c> only;
///     no project reference to <c>Ai.*</c>.</item>
/// </list>
/// </summary>
public sealed class AiOutputFlaggedEventHandler : INotificationHandler<AiOutputFlaggedIntegrationEvent>
{
    private readonly IModerationQueueWriter _writer;
    private readonly ILoggerManager _logger;

    public AiOutputFlaggedEventHandler(IModerationQueueWriter writer, ILoggerManager logger)
    {
        _writer = writer;
        _logger = logger;
    }

    public async Task Handle(AiOutputFlaggedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Moderation: received AiOutputFlaggedIntegrationEvent " +
            $"(SourceEventId={notification.SourceEventId}, Action={notification.ActionTaken}, " +
            $"TaskKind={notification.TaskKind}, StudentId={notification.StudentId}).");

        // Build the jsonb SafetyVerdict payload from the event fields.
        // PII-light: failed checks + reason codes + action taken — no raw content.
        var safetyVerdictJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            failedChecks = notification.FailedChecks,
            reasonCodes  = notification.ReasonCodes,
            actionTaken  = notification.ActionTaken,
            modelId      = notification.ModelId,
        });

        // Delegate all persistence + idempotency + fail-soft to the writer.
        // The writer's own try/catch ensures no exception escapes to the publisher.
        await _writer.EnqueueIfNotExistsAsync(
            sourceEventId:    notification.SourceEventId,
            source:           ModerationSource.AiOutput,
            contentReference: notification.ContentReference,
            safetyVerdict:    safetyVerdictJson,
            studentId:        notification.StudentId == 0 ? null : notification.StudentId,
            taskKind:         notification.TaskKind,
            subjectCode:      notification.SubjectCode,
            grade:            notification.Grade,
            detectedAt:       notification.DetectedAt,
            cancellationToken: cancellationToken);
    }
}

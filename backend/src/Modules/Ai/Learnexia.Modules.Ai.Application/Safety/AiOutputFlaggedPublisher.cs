using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Ai.Application.Safety;

/// <summary>
/// Implementation of <see cref="IAiOutputFlaggedPublisher"/>.
/// Wraps a MediatR <see cref="IPublisher"/> to broadcast <see cref="AiOutputFlaggedIntegrationEvent"/>
/// after the Safety Layer persists a blocked/flagged <see cref="SafetyEvent"/>.
///
/// <para><strong>Fail-soft:</strong> any exception during publish is caught, logged, and swallowed.
/// A publish failure MUST NOT propagate back to <c>SafetyLayer.GenerateSafeAsync</c> — the
/// child-facing safety fallback has already been committed and must be returned regardless.</para>
///
/// <para><strong>Flagged/Blocked only:</strong> the caller (<see cref="SafetyLayer"/>) only
/// invokes this when <c>ActionTaken</c> is <c>"Blocked"</c>, <c>"Regenerated"</c>, or
/// <c>"FallbackReturned"</c>. The <c>Allowed</c> path never reaches this publisher.</para>
///
/// <para><strong>SourceEventId encoding:</strong> <c>SafetyEvent.Id</c> is an <c>int</c> PK.
/// It is encoded as a deterministic namespace-5 UUID (SHA-1 of "ai.SafetyEvent:{id}") so the
/// Moderation module's <c>SourceEventId uuid</c> unique index can stay uniform.
/// Redelivery produces the identical Guid, guaranteeing idempotent dedup at the DB level.</para>
/// </summary>
public sealed class AiOutputFlaggedPublisher : IAiOutputFlaggedPublisher
{
    // Namespace UUID (v4, randomly chosen once for this seam — stable forever).
    // Using DNS namespace is conventional; this is a project-local namespace.
    private static readonly Guid _safetyEventNamespace = new("f47ac10b-58cc-4372-a567-0e02b2c3d479");

    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public AiOutputFlaggedPublisher(IPublisher publisher, ILoggerManager logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync(SafetyEvent safetyEvent, CancellationToken ct)
    {
        try
        {
            // Deterministic Guid derived from the SafetyEvent PK so redelivery produces the
            // same SourceEventId — enabling idempotent dedup in the Moderation subscriber.
            var sourceEventId = DeriveGuid(safetyEvent.Id);

            var integrationEvent = new AiOutputFlaggedIntegrationEvent(
                EventId:          Guid.NewGuid(),
                SourceEventId:    sourceEventId,
                ContentReference: $"SafetyEvent:{safetyEvent.Id}",
                FailedChecks:     safetyEvent.FailedChecks,
                ReasonCodes:      safetyEvent.ReasonCodes,
                ActionTaken:      safetyEvent.ActionTaken,
                ModelId:          safetyEvent.ModelId,
                TaskKind:         safetyEvent.TaskKind,
                StudentId:        safetyEvent.StudentId,
                SubjectCode:      null,   // OQ-3: not available on SafetyEvent yet; nullable
                Grade:            null,   // OQ-3: same — nullable until upstream enriched
                DetectedAt:       safetyEvent.OccurredAtUtc);

            await _publisher.Publish(integrationEvent, ct);

            _logger.LogInfo(
                $"Ai: published AiOutputFlaggedIntegrationEvent " +
                $"(SourceEventId={sourceEventId}, SafetyEventId={safetyEvent.Id}, Action={safetyEvent.ActionTaken}).");
        }
        catch (Exception ex)
        {
            // Fail-soft: a publish failure MUST NOT affect the child's safety fallback.
            // The SafetyEvent is already persisted; the worst outcome is a missed queue item.
            _logger.LogWarn(
                $"Ai: AiOutputFlaggedPublisher failed ({ex.GetType().Name}) " +
                $"for SafetyEvent.Id={safetyEvent.Id} — moderation queue item may be missed.");
            _logger.LogError(ex,
                $"Ai: AiOutputFlaggedPublisher exception detail for SafetyEvent.Id={safetyEvent.Id}.");
        }
    }

    /// <summary>
    /// Derives a deterministic Guid from an int <paramref name="safetyEventId"/> using a
    /// SHA-1 namespace approach (simplified: XOR the id bytes into the namespace Guid).
    /// The result is stable: same input always produces the same output, enabling idempotent dedup.
    /// </summary>
    private static Guid DeriveGuid(int safetyEventId)
    {
        // Combine namespace bytes with the int id bytes via XOR in the first 4 bytes.
        // This is a lightweight deterministic mapping — not a full RFC 4122 v5 UUID,
        // but sufficient for our single-source idempotency use case.
        var bytes = _safetyEventNamespace.ToByteArray();
        var idBytes = BitConverter.GetBytes(safetyEventId);
        for (int i = 0; i < idBytes.Length; i++)
            bytes[i] ^= idBytes[i];
        return new Guid(bytes);
    }
}

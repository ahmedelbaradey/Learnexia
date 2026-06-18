using Learnexia.Modules.Ai.Domain.Entities;

namespace Learnexia.Modules.Ai.Application.Safety;

/// <summary>
/// Application-layer seam for publishing the cross-module <c>AiOutputFlaggedIntegrationEvent</c>
/// after a <see cref="SafetyEvent"/> has been persisted.
///
/// <para>Placed in <c>Ai.Application</c> to keep the <see cref="SafetyLayer"/> free of a direct
/// MediatR <c>IPublisher</c> dependency and to allow unit-testing without a real publisher.</para>
///
/// <para><strong>Fail-soft contract:</strong> the implementation MUST wrap the publish in a
/// try/catch and NEVER throw. A publish failure must not affect the child-facing safety path.</para>
///
/// <para><strong>Flagged-only contract:</strong> only call this when the safety pipeline
/// produced an <c>ActionTaken</c> value of <c>"Blocked"</c>, <c>"Regenerated"</c>, or
/// <c>"FallbackReturned"</c> — never for a passed/allowed response.</para>
/// </summary>
public interface IAiOutputFlaggedPublisher
{
    /// <summary>
    /// Publishes an <c>AiOutputFlaggedIntegrationEvent</c> for the given persisted
    /// <see cref="SafetyEvent"/>. Must swallow all exceptions (fail-soft).
    /// </summary>
    Task PublishAsync(SafetyEvent safetyEvent, CancellationToken ct);
}

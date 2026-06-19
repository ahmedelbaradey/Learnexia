namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Published by the Ai module after the Safety Layer persists a <c>SafetyEvent</c> for a
/// BLOCKED or FALLBACK AI output (not Allowed). Consumed by the Moderation module to enqueue
/// a <c>ModerationItem</c> for human review (P7-09).
///
/// <para><strong>PII-light invariant (P3-02):</strong> this event carries only reason codes,
/// check names, and opaque references — never raw prompt or response text. The shape mirrors
/// the fields stored on <c>ai.SafetyEvents</c>.</para>
///
/// <para><strong>Dedup key:</strong> <see cref="SourceEventId"/> is the primary-key of the
/// persisted <c>SafetyEvent</c> row (int). The consumer stores it as a <c>Guid</c> idempotency
/// key by encoding it; the unique index on <c>moderation.ModerationItems.SourceEventId</c>
/// prevents double-enqueue on redelivery.</para>
///
/// <para><strong>Fail-soft publish:</strong> a failure to publish this event MUST NOT propagate
/// into the child-facing safety path — the Ai module wraps the publish in a try/catch.</para>
/// </summary>
public sealed record AiOutputFlaggedIntegrationEvent(
    /// <summary>
    /// Unique event id (new Guid per publish). Used as the <c>IIntegrationEvent.EventId</c>.
    /// </summary>
    Guid EventId,

    /// <summary>
    /// The primary key of the persisted <c>SafetyEvent</c> row in <c>ai.SafetyEvents</c>.
    /// Used by the Moderation module as a dedup / idempotency key (stored as the
    /// <c>ModerationItem.SourceEventId</c>).  Encoded as a deterministic Guid so the
    /// Moderation schema stays uniform (<c>SourceEventId uuid</c>).
    /// </summary>
    Guid SourceEventId,

    /// <summary>
    /// Opaque content reference — the <c>SafetyEvent.Id</c> serialized as a string.
    /// No raw prompt or response text.
    /// </summary>
    string ContentReference,

    /// <summary>
    /// JSON array of check names that failed (e.g. <c>["ToxicityCheck"]</c>).
    /// Copied verbatim from <c>SafetyEvent.FailedChecks</c>.
    /// </summary>
    string FailedChecks,

    /// <summary>
    /// JSON array of reason codes collected across all failed checks
    /// (e.g. <c>["TOXICITY_HIGH"]</c>). Copied from <c>SafetyEvent.ReasonCodes</c>.
    /// </summary>
    string ReasonCodes,

    /// <summary>
    /// The action taken by the safety pipeline: <c>"Blocked"</c>, <c>"Regenerated"</c>,
    /// or <c>"FallbackReturned"</c>. Copied from <c>SafetyEvent.ActionTaken</c>.
    /// </summary>
    string ActionTaken,

    /// <summary>
    /// The model that produced the flagged output.
    /// Copied from <c>SafetyEvent.ModelId</c>.
    /// </summary>
    string ModelId,

    /// <summary>
    /// The AI task kind that triggered the event (e.g. <c>"Hint"</c>, <c>"Explain"</c>).
    /// Copied from <c>SafetyEvent.TaskKind</c>.
    /// </summary>
    string TaskKind,

    /// <summary>
    /// Plain student id. 0 when not available (system-generated events). No cross-module FK.
    /// </summary>
    int StudentId,

    /// <summary>
    /// Subject code facet (nullable — not always available from the SafetyEvent).
    /// </summary>
    string? SubjectCode,

    /// <summary>
    /// School grade facet (nullable — not always available from the SafetyEvent).
    /// </summary>
    int? Grade,

    /// <summary>UTC time the safety event occurred (sourced from <c>SafetyEvent.OccurredAtUtc</c>).</summary>
    DateTime DetectedAt) : IIntegrationEvent
{
    DateTime IIntegrationEvent.OccurredOnUtc => DetectedAt;
}

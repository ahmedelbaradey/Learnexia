using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Ai.Domain.Entities;

/// <summary>
/// Immutable, append-only record of every AI safety block or flag event.
/// Written by <c>SafetyLayer</c> whenever a check blocks or flags AI content.
///
/// <para>Design decisions (P3-02 Q5/Q6):</para>
/// <list type="bullet">
///   <item><see cref="StudentId"/> — plain <c>int</c>, NO cross-module FK (module isolation rule 1).</item>
///   <item><see cref="FailedChecks"/> — JSON array of check names that failed (stored as jsonb).</item>
///   <item><see cref="ReasonCodes"/> — JSON array of all collected reason codes (stored as jsonb).</item>
///   <item><see cref="OccurredAtUtc"/> — the time the safety event occurred (UTC, indexed).</item>
/// </list>
///
/// <para>PII-light by design (Q5): no PromptText, no ResponseText, no StudentName, no Email.
/// Reason codes + check names only. Access-controlled quarantine store for raw content deferred to P7-09.</para>
///
/// <para>Feeds FR-ADM-8 (P7-09 moderation queue) and FR-ADM-10 (P7-11 safety dashboard).
/// Schema is stable: P7-09/P7-11 can read rows without a schema change.</para>
/// </summary>
public class SafetyEvent : CreationAuditedEntity
{
    /// <summary>
    /// The student who triggered the safety check. Plain int — no cross-module FK.
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// The AI task kind that triggered the event (e.g. "Explain", "Hint", "WhyWrong", "Practice").
    /// Stored as a string to remain stable if the enum evolves.
    /// </summary>
    public string TaskKind { get; set; } = null!;

    /// <summary>
    /// Names of the checks that failed (e.g. ["ToxicityCheck", "AgeAppropriatenessCheck"]).
    /// Serialized as a JSON array; stored as jsonb for queryability.
    /// Application layer owns the shape.
    /// </summary>
    public string FailedChecks { get; set; } = "[]";

    /// <summary>
    /// All reason codes collected across all enabled checks
    /// (e.g. ["TOXICITY_HIGH", "AGE_INAPPROPRIATE"]).
    /// Serialized as a JSON array; stored as jsonb for queryability.
    /// Stable codes — P7-09/P7-11 categorize by these.
    /// </summary>
    public string ReasonCodes { get; set; } = "[]";

    /// <summary>
    /// The action the safety pipeline took: "Blocked", "Regenerated", or "FallbackReturned".
    /// </summary>
    public string ActionTaken { get; set; } = null!;

    /// <summary>
    /// The model ID that produced the content that was evaluated (e.g. "claude-3-haiku-20240307").
    /// </summary>
    public string ModelId { get; set; } = null!;

    /// <summary>
    /// The UTC time the safety event occurred. Indexed for P7-09/P7-11 time-range queries.
    /// Stored as timestamptz.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }
}

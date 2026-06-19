using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Ai.Domain.Entities;

/// <summary>
/// Immutable, append-only telemetry record for every AI provider call.
/// Written by the gateway write path after each successful (or failed) AI invocation.
///
/// <para>Design decisions (P7-11 slice-a):</para>
/// <list type="bullet">
///   <item><see cref="StudentId"/> — plain <c>int?</c>, nullable, NO cross-module FK (module isolation rule 1).
///   The provider-neutral <c>AiRequest</c> does not carry StudentId in v1, so this field is always
///   <c>null</c> for now; reserved for future enrichment without a schema change.</item>
///   <item>No PII: no prompt text, no response text, no student name or email — token counts and cost only.</item>
///   <item><see cref="OccurredAtUtc"/> — primary filter column for P7-11 time-range dashboard queries (indexed).</item>
///   <item><see cref="ModelId"/> and <see cref="TaskKind"/> — indexed for breakdown-by-model and
///   breakdown-by-task-kind aggregations on the cost dashboard.</item>
///   <item><see cref="EstimatedCostUsd"/> — indicative only; six decimal places matches the gateway's <c>:F6</c>
///   log format and the <c>AiUsage</c> record in <c>Shared.Contracts</c>. Stored as <c>numeric(12,6)</c>.</item>
/// </list>
///
/// <para>Feeds FR-ADM-10 (P7-11 usage/cost dashboard).
/// Schema is stable: P7-11 read queries consume rows without a schema change.</para>
/// </summary>
public class AiUsageLog : CreationAuditedEntity
{
    /// <summary>
    /// AI provider name (e.g. "Claude", "OpenAi").
    /// </summary>
    public string Provider { get; set; } = null!;

    /// <summary>
    /// Model identifier actually used by the provider (e.g. "claude-haiku-4-5").
    /// Indexed for breakdown-by-model queries on the cost dashboard.
    /// </summary>
    public string ModelId { get; set; } = null!;

    /// <summary>
    /// The AI task kind that triggered the call (e.g. "Explain", "Hint", "WhyWrong", "Practice").
    /// Stored as a string to remain stable if the enum evolves.
    /// Indexed for breakdown-by-task-kind aggregations.
    /// </summary>
    public string TaskKind { get; set; } = null!;

    /// <summary>
    /// Input prompt tokens billed by the provider.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Output completion tokens billed by the provider.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Estimated USD cost approximated from per-model rates configured in the gateway.
    /// Indicative only — actual billing is from the provider dashboard.
    /// Stored as <c>numeric(12,6)</c>; six decimal places matches the gateway <c>:F6</c> log format.
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// End-to-end latency from gateway send to response received, in milliseconds.
    /// Stored as bigint.
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Whether the provider reported a cache hit on the input tokens.
    /// </summary>
    public bool WasCacheHit { get; set; }

    /// <summary>
    /// The student the AI call was made on behalf of. Plain <c>int?</c> — no cross-module FK.
    /// Always <c>null</c> in v1 (the provider-neutral <c>AiRequest</c> does not carry StudentId);
    /// reserved for future enrichment. Do not add an FK constraint.
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>
    /// The UTC time the AI call completed and this record was written. Indexed for time-range queries.
    /// Stored as timestamptz.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }
}

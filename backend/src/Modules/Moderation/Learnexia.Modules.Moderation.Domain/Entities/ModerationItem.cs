using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Moderation.Domain.Entities;

/// <summary>
/// A single item in the human-in-the-loop moderation queue (P7-09).
///
/// <para>Design decisions:</para>
/// <list type="bullet">
///   <item><see cref="SourceEventId"/> — idempotency key copied from the integration event. Unique index
///     prevents duplicate rows if the event is redelivered.</item>
///   <item><see cref="Source"/> / <see cref="Status"/> — persisted as strings via EF value-conversion
///     for stability (mirror <c>SafetyEvent.ActionTaken</c>).</item>
///   <item><see cref="ContentReference"/> — opaque reference to the source module's flagged artifact
///     (e.g. a <c>SafetyEvent.Id</c> value or a storage key). Never raw prompt/response text —
///     preserves the P3-02 PII-light invariant.</item>
///   <item><see cref="SafetyVerdict"/> — JSON copy of the safety signal (failed checks + reason codes +
///     action taken) from the event. Stored as jsonb, mirrors <c>SafetyEvent.FailedChecks</c> /
///     <c>ReasonCodes</c> pattern. Kept opaque here; application layer owns the shape.</item>
///   <item><see cref="StudentId"/>, <see cref="SubjectCode"/>, <see cref="Grade"/> — loose ids / nullable
///     facets; no cross-module FK (rule #1). Nullable because the AI source may not supply subject/grade.</item>
///   <item><see cref="ReviewedByUserId"/>, <see cref="ReviewedAtUtc"/>, <see cref="ReviewDecisionReason"/> —
///     set on review; nullable until the item transitions out of <see cref="ModerationStatus.Pending"/>.</item>
/// </list>
///
/// <para>PII-light invariant inherited from P3-02: no raw prompt text, no response text, no child names
/// or e-mail addresses stored here.</para>
/// </summary>
/// <remarks>
/// Derives from <see cref="AggregateRoot"/> (not just <see cref="FullAuditedEntity"/>) so the
/// review command handler can raise <see cref="Events.AdminActionPerformedDomainEvent"/> on this
/// aggregate for post-commit relay to the P7-12 audit log (ADR 0002 pattern).
/// </remarks>
public class ModerationItem : AggregateRoot
{
    /// <summary>
    /// Idempotency key sourced from the integration event. Unique index prevents duplicate rows on
    /// redelivery. Mirrors <see cref="AuditLog.EventId"/>.
    /// </summary>
    public Guid SourceEventId { get; set; }

    /// <summary>
    /// The origin of this flagged item. Persisted as a string.
    /// </summary>
    public ModerationSource Source { get; set; }

    /// <summary>
    /// Current lifecycle status. Default <see cref="ModerationStatus.Pending"/>. Persisted as a string.
    /// </summary>
    public ModerationStatus Status { get; set; } = ModerationStatus.Pending;

    /// <summary>
    /// Opaque reference to the flagged artifact in the source module — e.g. the <c>SafetyEvent.Id</c>
    /// (int) serialised as a string, or a storage object key for curriculum uploads.
    /// Never contains raw prompt or response text.
    /// </summary>
    public string ContentReference { get; set; } = null!;

    /// <summary>
    /// JSON copy of the safety verdict from the producing event: failed checks, reason codes, action
    /// taken. Stored as jsonb. Shape mirrors the AI Safety Layer's event contract (PII-light).
    /// </summary>
    public string SafetyVerdict { get; set; } = "{}";

    /// <summary>
    /// The student whose session triggered the flag. Plain int — no cross-module FK. 0 when unknown
    /// (e.g. curriculum-upload source). Nullable to distinguish "unknown" from "system" (0).
    /// </summary>
    public int? StudentId { get; set; }

    /// <summary>
    /// AI task kind that produced the flagged output (e.g. "Hint", "Explain").
    /// Nullable; only available for <see cref="ModerationSource.AiOutput"/> items.
    /// </summary>
    public string? TaskKind { get; set; }

    /// <summary>
    /// Subject code facet for filtering (e.g. "Math", "Arabic"). Nullable because the AI source
    /// does not always supply it (OQ-3 — ships nullable until enriched upstream).
    /// </summary>
    public string? SubjectCode { get; set; }

    /// <summary>
    /// School grade (1–12) facet for filtering. Nullable for the same reason as <see cref="SubjectCode"/>.
    /// </summary>
    public int? Grade { get; set; }

    /// <summary>
    /// UTC time the source system flagged the content (from the event). Indexed for time-range queries.
    /// Distinct from <see cref="Shared.Kernel.Entities.CreationAuditedEntity.CreatedAt"/> (the DB insert time).
    /// Stored as timestamptz.
    /// </summary>
    public DateTime DetectedAt { get; set; }

    // ── Review fields ────────────────────────────────────────────────────────────────────────────

    /// <summary>The admin who performed the review. Plain int — no cross-module FK. Null until reviewed.</summary>
    public int? ReviewedByUserId { get; set; }

    /// <summary>
    /// Optional free-text reason recorded by the reviewing admin. Required when
    /// <see cref="Status"/> transitions to <see cref="ModerationStatus.Rejected"/>.
    /// </summary>
    public string? ReviewDecisionReason { get; set; }

    /// <summary>UTC timestamp when the review action was performed. Null until reviewed.</summary>
    public DateTime? ReviewedAtUtc { get; set; }
}

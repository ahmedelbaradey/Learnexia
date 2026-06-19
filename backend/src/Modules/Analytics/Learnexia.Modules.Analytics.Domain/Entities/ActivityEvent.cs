using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Analytics.Domain.Entities;

/// <summary>
/// Append-only, PII-light product-analytics event record.
///
/// <para>Design decisions:</para>
/// <list type="bullet">
///   <item><b>Append-only</b> — no Update or Delete command, handler, or endpoint exists.
///   Written by fail-soft <c>INotificationHandler&lt;T&gt;</c> consumers of
///   <c>Shared.Contracts</c> integration events.</item>
///   <item><b>PII-light</b> — stores only plain int IDs, a stable event-type string, a UTC
///   timestamp, and optional numeric facets. No names, e-mail addresses, prompt text,
///   response text, or answer content ever land in this table.</item>
///   <item><see cref="StudentId"/> — plain <c>int</c>, no cross-module FK (module isolation
///   rule #1). Mirrors the <c>AiUsageLog.StudentId</c> pattern.</item>
///   <item><see cref="EventType"/> — stored as a string for forward-compatibility (mirror
///   <c>AiUsageLog.TaskKind</c>). Stable candidate values consumed from the existing event
///   backbone: <c>LessonCompleted</c>, <c>MissionCompleted</c>, <c>StudentLeveledUp</c>,
///   <c>AiHelpRequested</c>, <c>AiHelpDelivered</c>, <c>AiHintUsed</c>.</item>
///   <item><see cref="SubjectCode"/> — nullable; usually null in v1 (subject breakdown comes
///   from Learning's <c>BySubject</c> seam, not the event payload — OQ-3 resolution).
///   Reserved for future per-event subject tagging without a schema change.</item>
///   <item><see cref="DurationSeconds"/> — nullable; populated only when the source event
///   payload carries a server-authoritative duration.</item>
///   <item><see cref="OccurredAtUtc"/> — the instant the business event happened (from the
///   integration event's <c>OccurredOnUtc</c>). Primary time-range filter; indexed.
///   Stored as <c>timestamptz</c>.</item>
///   <item><see cref="SourceEventId"/> — the originating integration event's <c>EventId</c>
///   (uuid). Unique index enforces idempotent ingest: redelivery of the same event is a
///   silent no-op (mirrors <c>ModerationItem.SourceEventId</c> pattern).</item>
/// </list>
///
/// <para>Feeds P5-03 platform KPIs (DAU/WAU/MAU, session duration, retention) and the
/// P7-10 admin KPI dashboard deferred facets.</para>
/// </summary>
public class ActivityEvent : CreationAuditedEntity
{
    /// <summary>
    /// The learner the event is about. Plain <c>int</c> — no cross-module FK (rule #1).
    /// Indexed, as most queries are per-student timelines.
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Stable, indexed-friendly event discriminator. Stored as a plain string for
    /// forward-compatibility. Candidate values from the existing backbone:
    /// <c>LessonCompleted</c>, <c>MissionCompleted</c>, <c>StudentLeveledUp</c>,
    /// <c>AiHelpRequested</c>, <c>AiHelpDelivered</c>, <c>AiHintUsed</c>.
    /// Max length 64 (enough for any current or near-future value).
    /// </summary>
    public string EventType { get; set; } = null!;

    /// <summary>
    /// Optional subject-code facet (numeric discriminator for the four subjects —
    /// Math, Science, Arabic, English). Usually <c>null</c> in v1 because most current
    /// integration event payloads do not carry a subject code. Reserved for future
    /// per-event subject tagging without a schema change.
    /// </summary>
    public int? SubjectCode { get; set; }

    /// <summary>
    /// Optional wall-clock duration in seconds, populated only when the source event
    /// payload carries a server-authoritative duration (e.g. a future
    /// <c>AttemptCompleted</c> event). <c>null</c> for most current producers.
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// The UTC instant the business event occurred, sourced from the integration event's
    /// <c>OccurredOnUtc</c>. Primary time-range filter for all DAU/session/retention
    /// window queries. Indexed. Stored as <c>timestamptz</c>.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>
    /// The originating integration event's <c>EventId</c> (UUID). Unique index enforces
    /// idempotent ingest: if the same event is redelivered, the unique-violation is caught
    /// and swallowed as a silent no-op. Mirrors <c>ModerationItem.SourceEventId</c>.
    /// </summary>
    public Guid SourceEventId { get; set; }
}

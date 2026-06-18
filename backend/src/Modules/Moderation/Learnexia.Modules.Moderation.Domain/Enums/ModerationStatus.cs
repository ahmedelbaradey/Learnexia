namespace Learnexia.Modules.Moderation.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.ModerationItem"/> in the review queue.
/// Persisted as a string for stability.
///
/// <para>Allowed transitions (P7-09 OQ-4):</para>
/// <list type="bullet">
///   <item><see cref="Pending"/> → <see cref="Approved"/> | <see cref="Rejected"/> | <see cref="Flagged"/></item>
///   <item><see cref="Flagged"/> → <see cref="Approved"/> | <see cref="Rejected"/></item>
///   <item><see cref="Approved"/> and <see cref="Rejected"/> are terminal — no further transitions.</item>
/// </list>
/// </summary>
public enum ModerationStatus
{
    /// <summary>Newly enqueued; awaiting human review.</summary>
    Pending,

    /// <summary>Reviewed and approved by an admin.</summary>
    Approved,

    /// <summary>Reviewed and rejected by an admin (reason required).</summary>
    Rejected,

    /// <summary>Escalated / flagged for additional scrutiny. Not yet terminal.</summary>
    Flagged
}

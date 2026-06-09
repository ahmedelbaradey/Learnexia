using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Moderation.Domain.Entities;

/// <summary>
/// Immutable, append-only record of every admin mutation. Populated by consuming
/// <see cref="Shared.Contracts.Admin.AdminActionPerformedEvent"/>. No Update or Delete
/// command, handler, or endpoint exists — the API layer enforces append-only.
///
/// <para>Fields mirror the event contract exactly (P7-12 brief §Piece 3):</para>
/// <list type="bullet">
///   <item><see cref="EventId"/> — idempotency key; unique index; prevents duplicate rows on redelivery.</item>
///   <item><see cref="AdminUserId"/> — acting admin ID from JWT (plain int, no cross-module FK).</item>
///   <item><see cref="Action"/> — action constant string from <c>AdminActions.*</c>.</item>
///   <item><see cref="TargetEntityType"/> — e.g. "Subject", "Unit".</item>
///   <item><see cref="TargetEntityId"/> — primary key of affected row (0 for batch actions).</item>
///   <item><see cref="Details"/> — PII-safe summary (ids + enum states only; no names/emails).</item>
///   <item><see cref="OccurredAtUtc"/> — the action time from the event, NOT the insert time.</item>
/// </list>
/// </summary>
public class AuditLog : CreationAuditedEntity
{
    /// <summary>Idempotency key — sourced from the event. Unique index enforces no-duplicates on redelivery.</summary>
    public Guid EventId { get; set; }

    /// <summary>The authenticated admin who performed the action (from JWT). Plain int — no cross-module FK.</summary>
    public int AdminUserId { get; set; }

    /// <summary>Action constant string (e.g. "Subject.Created"). Uses <c>AdminActions.*</c> constants.</summary>
    public string Action { get; set; } = null!;

    /// <summary>Logical type of the affected entity (e.g. "Subject", "User").</summary>
    public string TargetEntityType { get; set; } = null!;

    /// <summary>Primary key of the affected row. 0 for multi-row batch operations.</summary>
    public int TargetEntityId { get; set; }

    /// <summary>
    /// PII-safe details summary (ids + enum/state values only — no child names, emails, or raw quiz answers).
    /// Nullable because some actions have no further detail beyond type + id.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// The time the admin action occurred, sourced directly from the event.
    /// Stored as UTC. This is the action time — audit base columns record insertion time.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }
}

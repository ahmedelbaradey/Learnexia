using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Append-only audit ledger for every seat lifecycle event (P10-15-BE-11).
///
/// <para>ALL seat events — purchase, cancel scheduling, removal at renewal, enforcement lock,
/// parent-choice, and reactivation — are routed here. The energy ledger
/// (<c>CreditTransaction</c>) is NEVER used for seat events (lead decision 2026-06-17).</para>
///
/// <para><strong>Append-only:</strong> rows are never updated. Re-entrancy is prevented via a
/// unique index on <see cref="IdempotencyKey"/> (filtered for non-null) in the EF config.</para>
///
/// <para>Module isolation — <see cref="ChildId"/> and <see cref="ParentUserId"/> are plain
/// <c>int</c>s; no cross-module FK.</para>
/// </summary>
public class SeatLedgerEntry : FullAuditedEntity
{
    // ── Ownership ────────────────────────────────────────────────────────────────

    /// <summary>
    /// FK to <see cref="Subscription"/> (billing-internal FK only, within the billing schema).
    /// Never null.
    /// </summary>
    public int SubscriptionId { get; set; }

    /// <summary>Navigation property for the owning subscription (billing-internal only).</summary>
    public Subscription Subscription { get; set; } = null!;

    // ── Actor (loose int — no cross-module FK) ───────────────────────────────────

    /// <summary>The parent user id at the time of the event. Plain int; no FK.</summary>
    public int ParentUserId { get; set; }

    /// <summary>
    /// The child user id this event pertains to. Plain int; no FK. Null for subscription-level
    /// events that are not child-specific (e.g. a bulk enforcement pass that is recorded once per
    /// child — in practice every entry here IS child-specific).
    /// </summary>
    public int? ChildId { get; set; }

    // ── Event classification ────────────────────────────────────────────────────

    /// <summary>Type of seat lifecycle event. Stored as <c>int</c>.</summary>
    public SeatLedgerEventType EventType { get; set; }

    // ── Optional payload ────────────────────────────────────────────────────────

    /// <summary>
    /// Monetary amount involved, if any (e.g. prorated amount for a seat reactivation or
    /// purchase). Null for administrative or enforcement events.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Seat quantity involved, if any (e.g. +1 for a purchase, -1 for a cancel-at-renewal).
    /// Null for events that operate on a fixed single-seat basis (lock, reactivate).
    /// </summary>
    public int? Quantity { get; set; }

    // ── Idempotency key ──────────────────────────────────────────────────────────

    /// <summary>
    /// Unique idempotency key for this event, if supplied by the originating service (P10-15-BE-11).
    ///
    /// <para>A unique index in EF config prevents double-appending the same event (filtered for
    /// non-null). Max 128 chars. Null for internally-triggered enforcement events where the job
    /// itself is idempotent-by-design (earliest-reserved tie-break + state check).</para>
    /// </summary>
    public string? IdempotencyKey { get; set; }

    // ── Timestamp ───────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when this ledger entry was created.</summary>
    public DateTime OccurredAt { get; set; }
}

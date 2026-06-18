using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Tracks per-child seat occupancy within a billing subscription (P10-14-BE-1).
///
/// <para><strong>Module isolation:</strong> <see cref="ChildId"/> is a plain <c>int</c>; there
/// is NO cross-module FK to the Identity or Parent module.</para>
///
/// <para><strong>Double-occupancy prevention:</strong> a unique filtered index on
/// <c>(SubscriptionId, ChildId) WHERE Status IN (Reserved, Active)</c> is enforced at the
/// database level (see <c>SeatReservationConfig</c>).</para>
///
/// <para><strong>Lifecycle:</strong>
/// <list type="bullet">
///   <item><see cref="SeatStatus.Reserved"/> — transient; written before child creation begins.</item>
///   <item><see cref="SeatStatus.Active"/>   — child created + linked; seat counts towards energy entitlement.</item>
///   <item><see cref="SeatStatus.Released"/> — compensation path (child creation failed) or seat cancel.</item>
///   <item><see cref="SeatStatus.NoSeat"/>   — P10-15 enforcement; child has no current entitlement.</item>
/// </list>
/// </para>
///
/// <para><strong>P10-15 extensions:</strong>
/// <list type="bullet">
///   <item><see cref="SeatState"/> — child seat lifecycle state (Active / NoSeatLocked).</item>
///   <item><see cref="RemovalScheduledAt"/> — set when parent voluntarily removes the seat (cycle-end effective).</item>
///   <item><see cref="IdempotencyKey"/> — unique per-attempt key for concurrency-safe reservation (P10-15-BE-10).</item>
/// </list>
/// </para>
/// </summary>
public class SeatReservation : FullAuditedEntity
{
    // ── Ownership ────────────────────────────────────────────────────────────────

    /// <summary>
    /// FK to <see cref="Subscription"/> (billing-internal FK only, within the billing schema).
    /// Never null.
    /// </summary>
    public int SubscriptionId { get; set; }

    /// <summary>Navigation property for the owning subscription (billing-internal only).</summary>
    public Subscription Subscription { get; set; } = null!;

    // ── Child (loose int — no cross-module FK) ───────────────────────────────────

    /// <summary>
    /// The child user id this reservation is for. Plain <c>int</c>; no cross-module FK.
    /// Corresponds to the <c>StudentId</c>/<c>ChildUserId</c> used throughout the system.
    /// </summary>
    public int ChildId { get; set; }

    // ── Audit columns for the parent-actor (for IDOR / reservation trail) ────────

    /// <summary>The parent user id that initiated this reservation. Plain int; no FK.</summary>
    public int ParentUserId { get; set; }

    // ── Status ──────────────────────────────────────────────────────────────────

    /// <summary>Current seat lifecycle status.</summary>
    public SeatStatus Status { get; set; } = SeatStatus.Reserved;

    // ── P10-15-BE-1: Seat lifecycle state (Active / NoSeatLocked) ───────────────

    /// <summary>
    /// Child seat lifecycle state (P10-15-BE-1). Stored as <c>int</c>.
    /// <see cref="SeatState.Active"/> by default; transitions to
    /// <see cref="SeatState.NoSeatLocked"/> on enforcement (never reverted inline — requires
    /// a reactivation payment).
    /// </summary>
    public SeatState SeatState { get; set; } = SeatState.Active;

    // ── P10-15-BE-1: Voluntary-removal scheduling ────────────────────────────────

    /// <summary>
    /// UTC timestamp when this seat is scheduled for removal at the next renewal boundary
    /// (P10-15-BE-1 / BE-3). Set when a parent voluntarily cancels the seat.
    ///
    /// <para>The seat remains <see cref="SeatStatus.Active"/> until this date passes.
    /// Child keeps allocation + AI access for the remainder of the cycle. No grace period is
    /// started (grace = payment-failure only). No energy is reclaimed.</para>
    /// </summary>
    public DateTime? RemovalScheduledAt { get; set; }

    // ── P10-15-BE-10: Idempotency key for concurrency-safe reservation ───────────

    /// <summary>
    /// Unique idempotency key for this reservation attempt (P10-15-BE-10).
    ///
    /// <para>Stored on the row so concurrent same-parent add-child calls never share a single
    /// placeholder row. Two simultaneous reservations with distinct keys create two distinct rows
    /// and are resolved against the unique filtered index independently. Max 128 chars.</para>
    /// </summary>
    public string? IdempotencyKey { get; set; }

    // ── Timestamps ──────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the reservation was first written (i.e. when <see cref="SeatStatus.Reserved"/> was set).</summary>
    public DateTime ReservedAt { get; set; }

    /// <summary>UTC timestamp when the seat was released or transitioned out of <see cref="SeatStatus.Active"/>. Null while active or reserved.</summary>
    public DateTime? ReleasedAt { get; set; }
}

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

    // ── Timestamps ──────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the reservation was first written (i.e. when <see cref="SeatStatus.Reserved"/> was set).</summary>
    public DateTime ReservedAt { get; set; }

    /// <summary>UTC timestamp when the seat was released or transitioned out of <see cref="SeatStatus.Active"/>. Null while active or reserved.</summary>
    public DateTime? ReleasedAt { get; set; }
}

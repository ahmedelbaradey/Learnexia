using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// A parent's subscription record.
///
/// <para>One active subscription per parent (enforced by a filtered unique index on
/// <c>ParentUserId WHERE Status = Active</c>). New parents begin on Free with
/// <see cref="SubscriptionStatus.Active"/>.</para>
///
/// <para>Module isolation rule — <see cref="ParentUserId"/> is a plain <c>int</c>; there
/// is NO cross-module FK to the Identity or Parent module.</para>
///
/// <para>When a parent requests an upgrade (<c>RequestUpgradeCommand</c>), the status
/// transitions to <see cref="SubscriptionStatus.PendingPayment"/>.
/// P10-06's webhook handler then flips it to <see cref="SubscriptionStatus.Active"/>
/// (Premium) on payment success, or leaves it as is on failure.</para>
/// </summary>
public class Subscription : FullAuditedEntity
{
    // ── Identity ───────────────────────────────────────────────────────────────────

    /// <summary>Owning parent user id (plain int — no cross-module FK).</summary>
    public int ParentUserId { get; set; }

    // ── Current plan ───────────────────────────────────────────────────────────────

    /// <summary>The current (effective) plan code.</summary>
    public PlanCode PlanCode { get; set; } = PlanCode.Free;

    /// <summary>Billing cadence (relevant when <see cref="PlanCode"/> is Premium).</summary>
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;

    /// <summary>Lifecycle status.</summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    // ── Billing cycle ──────────────────────────────────────────────────────────────

    /// <summary>Start of the current billing cycle. Null for Free with no history.</summary>
    public DateTime? CurrentCycleStart { get; set; }

    /// <summary>End of the current billing cycle. Used for cancel-at-period-end.</summary>
    public DateTime? CurrentCycleEnd { get; set; }

    // ── Pending plan change (set when Downgrading / after upgrade paid) ────────────

    /// <summary>
    /// The plan code that will become effective at the next cycle boundary.
    /// Set by <c>RequestDowngradeCommand</c>; cleared when the cycle rolls.
    /// </summary>
    public PlanCode? PendingPlanCode { get; set; }

    /// <summary>
    /// The billing period that will become effective at the next cycle boundary when the
    /// parent switches cadence while already on Premium (Monthly ↔ Annual).
    /// </summary>
    public BillingPeriod? PendingBillingPeriod { get; set; }

    // ── P10-09 Dunning fields ─────────────────────────────────────────────────────

    /// <summary>
    /// Number of consecutive failed payment attempts in the current dunning cycle.
    /// Reset to 0 on a successful payment. Never negative.
    /// </summary>
    public int FailedAttemptCount { get; set; }

    /// <summary>
    /// UTC timestamp when the next automatic retry should be attempted.
    /// Null when not in a dunning state or when retries are exhausted.
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// UTC timestamp when the grace window ends and the subscription automatically
    /// downgrades to Free. Set to <see cref="CurrentCycleEnd"/> when dunning retries
    /// are exhausted (<c>Status = Dunning</c>). Null otherwise.
    /// </summary>
    public DateTime? GraceEndsAt { get; set; }

    // ── P10-14: Extra seats (purchased add-on) ───────────────────────────────────

    /// <summary>
    /// Number of extra seats the parent has purchased as a monthly add-on (P10-14-BE-1).
    /// Default = 0. Incremented by the webhook after a successful <c>PaymentKind.Seat</c> payment.
    /// Combined with <see cref="Plan.IncludedSeats"/> and capped at <c>seats.max=5</c>
    /// (from <c>IGlobalSettingsProvider</c>) to yield the total active paid seat count.
    /// </summary>
    public int PurchasedExtraSeats { get; set; }

    /// <summary>
    /// Number of purchased extra seats the parent has scheduled for removal at the NEXT renewal
    /// boundary (P10-14-BE-8 / OQ-5, lead-approved 2026-06-17). A voluntary cancel records the intent
    /// here and is effective at CYCLE END — it does NOT reduce <see cref="PurchasedExtraSeats"/>
    /// mid-cycle, does NOT touch <see cref="GraceEndsAt"/> (grace = payment-failure only), and does
    /// NOT reclaim/forfeit energy. At renewal, P10-15 applies the removal:
    /// <c>PurchasedExtraSeats -= PendingExtraSeatRemovals</c> then resets this to 0.
    /// </summary>
    public int PendingExtraSeatRemovals { get; set; }

    /// <summary>
    /// When the scheduled extra-seat removal becomes effective — the next renewal boundary
    /// (= <see cref="CurrentCycleEnd"/> at the time of cancel). Null when no cancel is pending.
    /// This is a SCHEDULED-REMOVAL marker, NOT a grace marker.
    /// </summary>
    public DateTime? ExtraSeatCancelEffectiveAt { get; set; }
}

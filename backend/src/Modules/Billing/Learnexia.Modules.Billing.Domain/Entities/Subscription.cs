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
}

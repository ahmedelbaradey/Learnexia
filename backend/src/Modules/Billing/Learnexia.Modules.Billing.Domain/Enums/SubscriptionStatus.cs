namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Lifecycle state of a parent's <see cref="Entities.Subscription"/>.
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> in EF config.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Default Free plan — no payment required.
    /// A subscription row always exists; new parents start here.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Upgrade requested; awaiting payment confirmation from P10-06.
    /// Tier stays at <see cref="Active"/> Free until payment succeeds.
    /// </summary>
    PendingPayment = 1,

    /// <summary>
    /// Parent cancelled; access to Premium ends at <c>CurrentCycleEnd</c>.
    /// After that the job downgrades to Free (or P10-09 handles it).
    /// </summary>
    Canceled = 2,

    /// <summary>
    /// A downgrade to Free is scheduled; takes effect at next cycle.
    /// The current cycle still grants Premium access.
    /// </summary>
    Downgrading = 3,

    /// <summary>
    /// A recurring charge failed; a retry is scheduled (<c>NextRetryAt</c>).
    /// Parent still has Premium access during the grace window.
    /// </summary>
    PastDue = 4,

    /// <summary>
    /// Multiple retries have failed; entering final grace window before downgrade.
    /// <c>GraceEndsAt</c> = <c>CurrentCycleEnd</c>; after that the subscription downgrades to Free.
    /// </summary>
    Dunning = 5,
}

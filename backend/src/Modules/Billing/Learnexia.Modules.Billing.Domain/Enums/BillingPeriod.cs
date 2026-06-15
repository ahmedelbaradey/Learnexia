namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Billing cadence for a Premium subscription.
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> in EF config.
/// </summary>
public enum BillingPeriod
{
    /// <summary>Billed monthly (199 EGP/month). Default for new upgrades.</summary>
    Monthly = 0,

    /// <summary>Billed annually (1990 EGP/year). Locks in 2 free months.</summary>
    Annual = 1,
}

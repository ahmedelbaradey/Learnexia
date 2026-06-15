namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Identifies the subscription plan tier.
/// Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c> in EF config.
/// </summary>
public enum PlanCode
{
    /// <summary>Free plan — default. Includes <c>credits.free_monthly</c> monthly grant.</summary>
    Free = 0,

    /// <summary>Premium plan — includes <c>credits.premium_monthly</c> monthly grant.</summary>
    Premium = 1,
}

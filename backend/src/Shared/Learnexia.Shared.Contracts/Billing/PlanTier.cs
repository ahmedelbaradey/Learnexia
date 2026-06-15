namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Subscription tier for a child. Drives the monthly energy grant amount.
/// Declared in <c>Shared.Contracts</c> so both the Billing module (grant job) and the
/// future Subscription module (P10-05 real impl) can reference it without cross-module coupling.
/// </summary>
public enum PlanTier
{
    /// <summary>Free plan — default tier.</summary>
    Free = 0,

    /// <summary>Premium plan — higher energy grant.</summary>
    Premium = 1,
}

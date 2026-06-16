namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Identifies which non-convertible energy bucket a ledger row or balance belongs to.
///
/// <para><strong>LOCKED — two buckets only.</strong>
/// Subscription = temporary/monthly (resets each cycle, never converts to Purchased).
/// Purchased    = permanent/never-expires (shared family reserve, touched only on shortfall).</para>
/// </summary>
public enum EnergyBucket
{
    /// <summary>Temporary monthly subscription/entitlement bucket. Resets each cycle.</summary>
    Subscription = 1,

    /// <summary>Permanent purchased (pack-purchase) bucket. Never expires, never converts.</summary>
    Purchased = 2,
}

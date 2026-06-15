namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Identifies which balance pool a ledger entry applies to.
/// Granted credits expire; purchased credits never expire.
/// </summary>
public enum CreditPool
{
    /// <summary>Free/premium monthly grant pool (expires at cycle end).</summary>
    Granted = 1,

    /// <summary>Purchased pack pool (never expires).</summary>
    Purchased = 2,

    /// <summary>Mixed: a debit spanning both pools (Granted-first draw).</summary>
    Mixed = 3,
}

namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Discriminates the kind of ledger entry.  All amounts are always-positive;
/// the type determines whether the balance increases or decreases.
/// </summary>
public enum CreditTransactionType
{
    /// <summary>Monthly free/premium energy grant.</summary>
    Grant = 1,

    /// <summary>Debit charged when an AI help response is delivered.</summary>
    Spend = 2,

    /// <summary>Credit purchased via a one-off energy pack.</summary>
    Purchase = 3,

    /// <summary>Expiry of an unused granted balance at end of grant cycle.</summary>
    Expiry = 4,

    /// <summary>Refund of unspent purchased credits (pack refund).</summary>
    Refund = 5,

    /// <summary>Admin manual adjustment (positive or negative, always positive amount stored; pool indicates direction).</summary>
    Adjustment = 6,
}

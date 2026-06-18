namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Reason code for a purchased-energy refund request (P10-17).
///
/// <para>Stored as an integer on the <c>CreditTransaction.Reason</c> field
/// (serialised via <c>ToString()</c> or the enum name — never a free-text string).</para>
/// </summary>
public enum RefundReason
{
    /// <summary>The parent no longer needs the purchased energy.</summary>
    NoLongerNeeded = 1,

    /// <summary>The pack was purchased by mistake.</summary>
    PurchasedByMistake = 2,

    /// <summary>Technical / service issue reported by the parent.</summary>
    ServiceIssue = 3,

    /// <summary>Admin-initiated refund (support / dispute resolution).</summary>
    AdminInitiated = 4,

    /// <summary>Other reason — captured in an audit note.</summary>
    Other = 99,
}

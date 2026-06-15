namespace Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;

/// <summary>
/// A single entry in a parent's billing history list.
///
/// <para>Covers both original charges and refunds.
/// Refund rows are identified by <c>Status = Refunded</c> on the original payment row
/// (refunds flip the original Payment.Status in place — no separate refund row is created).</para>
/// </summary>
public sealed class BillingHistoryItemDto
{
    /// <summary>The internal payment row id.</summary>
    public int PaymentId { get; set; }

    /// <summary>
    /// UTC timestamp when the payment/refund was created.
    /// Newest-first ordering uses this field.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>Payment kind: <c>Subscription</c> or <c>Pack</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Amount in the smallest unit of <see cref="Currency"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code (always <c>EGP</c>).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Payment lifecycle status (e.g. <c>Succeeded</c>, <c>Refunded</c>, <c>Failed</c>).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// For <c>Pack</c> payments: the child who received (or would receive) the energy credits.
    /// Null for <c>Subscription</c> payments.
    /// </summary>
    public int? TargetChildId { get; set; }

    /// <summary>Provider's opaque payment reference (for support / reconciliation).</summary>
    public string? ProviderPaymentRef { get; set; }
}

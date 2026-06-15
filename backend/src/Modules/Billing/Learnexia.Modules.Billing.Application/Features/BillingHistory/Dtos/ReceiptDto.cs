namespace Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;

/// <summary>
/// Receipt DTO for a single payment — returned by <c>GetReceiptQuery</c>.
///
/// <para>v1: fields sufficient for a printable HTML receipt rendered by the FE.
/// Finance-gate fields (<c>VatAmount</c>, <c>NetAmount</c>, <c>GrossAmount</c>,
/// <c>InvoiceNumber</c>) are included as nullable and populated when available
/// (OQ-FIN-1 pending finance confirmation).</para>
///
/// <para>SECURITY: <c>IBillingHistoryQueryService.GetReceiptAsync</c> re-checks
/// <c>Payment.ParentUserId == callerParentUserId</c> before populating this DTO —
/// the handler never returns data without the ownership check.</para>
/// </summary>
public sealed class ReceiptDto
{
    // ── Payment identity ──────────────────────────────────────────────────────────

    /// <summary>Internal payment id.</summary>
    public int PaymentId { get; set; }

    /// <summary>Provider's opaque payment reference.</summary>
    public string? ProviderPaymentRef { get; set; }

    /// <summary>UTC timestamp of the payment.</summary>
    public DateTime PaidAt { get; set; }

    // ── Money ─────────────────────────────────────────────────────────────────────

    /// <summary>Total gross amount charged in <see cref="Currency"/>.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Currency code (always <c>EGP</c>).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Net amount before VAT (if finance-gate OQ-FIN-1 resolved; null otherwise).</summary>
    public decimal? NetAmount { get; set; }

    /// <summary>VAT portion (if finance-gate OQ-FIN-1 resolved; null otherwise).</summary>
    public decimal? VatAmount { get; set; }

    // ── Line-item description ─────────────────────────────────────────────────────

    /// <summary>Human-readable description: plan name or energy pack.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Payment kind: <c>Subscription</c> or <c>Pack</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>For Pack payments: the child id who received the credits.</summary>
    public int? TargetChildId { get; set; }

    // ── Seller / legal entity ─────────────────────────────────────────────────────

    /// <summary>Legal name of the seller (from config — never hard-coded).</summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>Seller's tax registration number (from config).</summary>
    public string? SellerTaxNumber { get; set; }

    /// <summary>Sequential invoice number (if finance-gate OQ-FIN-1 resolved; null otherwise).</summary>
    public string? InvoiceNumber { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────────

    /// <summary>Payment status at the time of receipt generation.</summary>
    public string Status { get; set; } = string.Empty;
}

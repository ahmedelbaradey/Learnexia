namespace Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;

/// <summary>
/// Result of <c>IRefundService.ComputeRefundableAsync</c> (P10-17-BE-1).
///
/// <para>All figures are reconciled from the immutable family ledger:
/// <c>Refundable = PurchasedTotal - ConsumedPurchased</c>, clamped ≥ 0,
/// and never exceeds the current shared <c>FamilyEnergyAccount.PurchasedBalance</c>.</para>
/// </summary>
public sealed record RefundableQuoteDto
{
    /// <summary>Total energy credited to the family via this purchase payment (from ledger <c>Purchase</c> rows).</summary>
    public int PurchasedTotal { get; init; }

    /// <summary>Energy from this payment's attribution that has been consumed via bucket-B spends (FIFO).</summary>
    public int ConsumedPurchased { get; init; }

    /// <summary>Refundable energy units: <c>PurchasedTotal - ConsumedPurchased</c>, clamped ≥ 0.</summary>
    public int Refundable { get; init; }

    /// <summary>Monetary value of the refundable energy at the original purchase price.</summary>
    public decimal RefundableAmount { get; init; }

    /// <summary>Currency of the refundable amount (always <c>"EGP"</c>).</summary>
    public string Currency { get; init; } = "EGP";

    /// <summary>The payment id that this quote applies to.</summary>
    public int PurchasePaymentId { get; init; }
}

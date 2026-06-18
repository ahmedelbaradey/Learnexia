namespace Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;

/// <summary>
/// Result returned from <c>RequestPurchasedEnergyRefundCommand</c> (parent) and
/// <c>AdminRequestPurchasedEnergyRefundCommand</c> (admin) after a successful refund initiation
/// (P10-17-BE-3 / BE-6).
///
/// <para>The actual ledger change and balance decrement are driven by the subsequent
/// <c>refund.succeeded</c> webhook — not by this command (consistent with P10-09).</para>
/// </summary>
public sealed record RefundRequestResultDto(
    int    PaymentId,
    string Message);

using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Commands.AdminRequestPurchasedEnergyRefund;

/// <summary>
/// Admin-initiated refund request for a family's unused purchased energy (P10-17-BE-6).
///
/// <para><strong>Authorization:</strong> <c>[Authorize(Policy = AuthorizationPolicies.AdminOnly)]</c>
/// enforced at the controller. Admin/support can refund ANY family's purchased pack — NOT
/// family-scoped. The target family/parent is resolved from the <c>purchasePaymentId</c>,
/// never from the admin JWT.</para>
///
/// <para><strong>Reuses</strong> <c>IRefundService.ComputeRefundableAsync</c> and
/// <c>InitiateProviderRefundAsync</c> — no duplicate reconciliation logic.
/// Settlement is still webhook-driven (BE-4): this command is initiate-only.</para>
///
/// <para><strong>Audit:</strong> the admin actor id is ledgered on the <c>Refund</c> row
/// via <c>SaveChangesAsync(adminUserId)</c> when the webhook settles.</para>
/// </summary>
public sealed record AdminRequestPurchasedEnergyRefundCommand
    : ICommand<BaseResponse<RefundRequestResultDto>>
{
    /// <summary>The <c>Payment.Id</c> of the pack purchase to refund. Must be &gt; 0.</summary>
    public int PurchasePaymentId { get; init; }

    /// <summary>Reason for the admin-initiated refund (enum — no free text).</summary>
    public RefundReason Reason { get; init; }
}

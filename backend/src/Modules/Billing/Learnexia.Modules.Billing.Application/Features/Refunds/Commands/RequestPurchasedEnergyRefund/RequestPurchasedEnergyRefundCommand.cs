using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Commands.RequestPurchasedEnergyRefund;

/// <summary>
/// Parent-initiated refund request for unused purchased energy (P10-17-BE-3).
///
/// <para><strong>Authorization:</strong> <c>[Authorize(Roles="Parent")]</c> is enforced at the
/// controller layer. Children/Students can never request a refund.
/// <c>ParentUserId</c> and <c>FamilyAccountId</c> are JWT-resolved in the handler — never from
/// the request body (IDOR guard).</para>
///
/// <para><strong>Flow (initiate-only):</strong>
/// Handler verifies family-scope ownership, calls <c>IRefundService.ComputeRefundableAsync</c>
/// (rejects if refundable ≤ 0), then calls <c>IRefundService.InitiateProviderRefundAsync</c>.
/// The ledger/balance change is driven by the subsequent <c>refund.succeeded</c> webhook (BE-4),
/// NOT by this command — consistent with P10-09 semantics.</para>
/// </summary>
public sealed record RequestPurchasedEnergyRefundCommand
    : ICommand<BaseResponse<RefundRequestResultDto>>
{
    /// <summary>The <c>Payment.Id</c> of the pack purchase to refund. Must be &gt; 0.</summary>
    public int PurchasePaymentId { get; init; }

    /// <summary>Reason for the refund request (enum — no free text).</summary>
    public RefundReason Reason { get; init; }
}

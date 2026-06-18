using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Queries.GetRefundableQuote;

/// <summary>
/// Admin query: returns the refundable purchased-energy quote for any pack payment (P10-17-BE-6).
///
/// <para><strong>Authorization:</strong> admin-role-only (<c>[Authorize(Policy = AuthorizationPolicies.AdminOnly)]</c>
/// at the controller). NOT family-scoped — admin can look up any family's payment.</para>
///
/// <para>Reuses the same <c>IRefundService.ComputeRefundableAsync</c> reconciliation as the
/// parent path (BE-5).</para>
/// </summary>
public sealed record GetAdminRefundableQuoteQuery(int PurchasePaymentId)
    : IQuery<BaseResponse<RefundableQuoteDto>>;

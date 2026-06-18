using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Refunds.Queries.GetRefundableQuote;

/// <summary>
/// Query: returns the refundable purchased-energy quote for a given pack payment (P10-17-BE-5).
///
/// <para><strong>Authorization:</strong> parent-JWT-only (<c>[Authorize(Roles="Parent")]</c>
/// at the controller). <c>ParentUserId</c> is JWT-resolved in the handler (IDOR guard).</para>
///
/// <para><strong>No ValidationBehavior:</strong> queries are not auto-validated per
/// CONVENTIONS §4. Input is validated inside the handler.</para>
/// </summary>
public sealed record GetRefundableQuoteQuery(int PurchasePaymentId)
    : IQuery<BaseResponse<RefundableQuoteDto>>;

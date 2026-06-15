using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.BillingHistory.Queries.GetReceipt;

/// <summary>
/// Query: returns a receipt DTO for a specific payment owned by the authenticated parent.
///
/// <para>IDOR: <c>ParentUserId</c> is always sourced from the JWT by the handler.
/// <see cref="Abstractions.IBillingHistoryQueryService.GetReceiptAsync"/> re-checks
/// <c>Payment.ParentUserId == ParentUserId</c> before returning data.</para>
///
/// <para>Queries are NOT auto-validated by <c>ValidationBehavior</c> — the handler validates
/// <c>PaymentId &gt; 0</c> before calling the service.</para>
/// </summary>
public sealed record GetReceiptQuery : IQuery<BaseResponse<ReceiptDto>>
{
    /// <summary>Owning parent's user id (resolved from JWT in the handler).</summary>
    public int ParentUserId { get; init; }

    /// <summary>The payment id for which a receipt is requested.</summary>
    public int PaymentId { get; init; }
}

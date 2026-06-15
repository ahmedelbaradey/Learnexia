using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Dtos;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Queries.GetBillingHistory;
using Learnexia.Modules.Billing.Application.Features.BillingHistory.Queries.GetReceipt;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// P10-08 — Billing history and receipts (parent-scoped, read-only).
///
/// <para><strong>Authorization:</strong> both endpoints require an authenticated JWT.
/// The handler resolves <c>parentId</c> from the JWT — never from the URL/body.</para>
///
/// <para><strong>IDOR:</strong> the service layer enforces that a parent can only see their
/// own payments. Receipt: re-checks <c>Payment.ParentUserId == caller</c> before returning.</para>
/// </summary>
[ApiController]
[Route("api/Billing/History")]
[Authorize]
public sealed class BillingHistoryController : AppControllerBase
{
    private readonly IMediator _mediator;

    public BillingHistoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// GET /api/Billing/History
    ///
    /// Returns a paginated, newest-first list of the authenticated parent's charges and refunds.
    /// </summary>
    /// <param name="pageNumber">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<BillingHistoryItemDto>>), 200)]
    public async Task<IActionResult> GetBillingHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize   = 20,
        CancellationToken ct       = default)
    {
        var result = await _mediator.Send(
            new GetBillingHistoryQuery
            {
                // parentId is resolved from JWT inside the handler — NOT taken from query params
                ParentUserId = 0,
                PageNumber   = pageNumber,
                PageSize     = pageSize,
            }, ct);

        return NewResult(result);
    }

    /// <summary>
    /// GET /api/Billing/History/Receipt/{paymentId}
    ///
    /// Returns the receipt DTO for a specific payment.
    /// Returns 404 if the payment does not exist or does not belong to the authenticated parent
    /// (intentionally no distinction — anti-IDOR).
    /// </summary>
    /// <param name="paymentId">The payment id.</param>
    [HttpGet("Receipt/{paymentId:int}")]
    [ProducesResponseType(typeof(BaseResponse<ReceiptDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<ReceiptDto>), 404)]
    public async Task<IActionResult> GetReceipt(
        [FromRoute] int paymentId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetReceiptQuery
            {
                // parentId is resolved from JWT inside the handler — NOT taken from the route
                ParentUserId = 0,
                PaymentId    = paymentId,
            }, ct);

        return NewResult(result);
    }
}

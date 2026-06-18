using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Refunds.Commands.AdminRequestPurchasedEnergyRefund;
using Learnexia.Modules.Billing.Application.Features.Refunds.Commands.RequestPurchasedEnergyRefund;
using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Modules.Billing.Application.Features.Refunds.Queries.GetRefundableQuote;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Parent and admin endpoints for purchased-energy refunds (P10-17).
///
/// <para><strong>Parent routes</strong> — <c>[Authorize(Roles="Parent")]</c>:
/// <list type="bullet">
///   <item><c>GET  /api/Billing/Refunds/Quote/{purchasePaymentId}</c> — refundable quote (BE-5).</item>
///   <item><c>POST /api/Billing/Refunds/Request</c> — parent-initiated refund request (BE-3).</item>
/// </list>
/// </para>
///
/// <para><strong>Admin routes</strong> — <c>[Authorize(Policy = AuthorizationPolicies.AdminOnly)]</c>:
/// <list type="bullet">
///   <item><c>GET  /api/Billing/Admin/Refunds/Quote/{purchasePaymentId}</c> — any-family refundable quote (BE-6).</item>
///   <item><c>POST /api/Billing/Admin/Refunds/Request</c> — admin-initiated refund (BE-6).</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
public sealed class RefundController : AppControllerBase
{
    // ── Parent routes (BE-5) ──────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/Billing/Refunds/Quote/{purchasePaymentId}
    ///
    /// Returns the FIFO-reconciled refundable purchased-energy quote for the authenticated parent.
    /// Family-scope IDOR guard enforced in the handler.
    /// </summary>
    [HttpGet("api/Billing/Refunds/Quote/{purchasePaymentId:int}")]
    [Authorize(Roles = "Parent")]
    [ProducesResponseType(typeof(BaseResponse<RefundableQuoteDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<RefundableQuoteDto>), 404)]
    public async Task<IActionResult> GetRefundableQuote(
        [FromRoute] int purchasePaymentId,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(new GetRefundableQuoteQuery(purchasePaymentId), ct));

    /// <summary>
    /// POST /api/Billing/Refunds/Request
    ///
    /// Parent-initiated refund request for unused purchased energy.
    /// Initiate-only: the ledger/balance change is driven by the <c>refund.succeeded</c> webhook.
    /// </summary>
    [HttpPost("api/Billing/Refunds/Request")]
    [Authorize(Roles = "Parent")]
    [ProducesResponseType(typeof(BaseResponse<RefundRequestResultDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<RefundRequestResultDto>), 404)]
    [ProducesResponseType(typeof(BaseResponse<RefundRequestResultDto>), 422)]
    public async Task<IActionResult> RequestRefund(
        [FromBody] RefundRequestBody request,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(
            new RequestPurchasedEnergyRefundCommand
            {
                PurchasePaymentId = request.PurchasePaymentId,
                Reason            = request.Reason,
            }, ct));

    // ── Admin routes (BE-6) ───────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/Billing/Admin/Refunds/Quote/{purchasePaymentId}
    ///
    /// Returns the FIFO-reconciled refundable quote for ANY family's pack payment.
    /// NOT family-scoped — admin/support can look up any family.
    /// </summary>
    [HttpGet("api/Billing/Admin/Refunds/Quote/{purchasePaymentId:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<RefundableQuoteDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<RefundableQuoteDto>), 404)]
    public async Task<IActionResult> GetAdminRefundableQuote(
        [FromRoute] int purchasePaymentId,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(new GetAdminRefundableQuoteQuery(purchasePaymentId), ct));

    /// <summary>
    /// POST /api/Billing/Admin/Refunds/Request
    ///
    /// Admin-initiated refund for any family's unused purchased energy.
    /// Reuses the same reconciliation logic as the parent path (no duplicate code).
    /// Initiate-only — webhook settlement drives the ledger change.
    /// </summary>
    [HttpPost("api/Billing/Admin/Refunds/Request")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<RefundRequestResultDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<RefundRequestResultDto>), 404)]
    [ProducesResponseType(typeof(BaseResponse<RefundRequestResultDto>), 422)]
    public async Task<IActionResult> AdminRequestRefund(
        [FromBody] AdminRefundRequestBody request,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(
            new AdminRequestPurchasedEnergyRefundCommand
            {
                PurchasePaymentId = request.PurchasePaymentId,
                Reason            = request.Reason,
            }, ct));
}

/// <summary>Request body for <c>POST /api/Billing/Refunds/Request</c>.</summary>
/// <param name="PurchasePaymentId">The pack payment id to refund.</param>
/// <param name="Reason">Reason for the refund (enum).</param>
public sealed record RefundRequestBody(int PurchasePaymentId, RefundReason Reason);

/// <summary>Request body for <c>POST /api/Billing/Admin/Refunds/Request</c>.</summary>
/// <param name="PurchasePaymentId">The pack payment id to refund.</param>
/// <param name="Reason">Reason for the admin-initiated refund (enum).</param>
public sealed record AdminRefundRequestBody(int PurchasePaymentId, RefundReason Reason);

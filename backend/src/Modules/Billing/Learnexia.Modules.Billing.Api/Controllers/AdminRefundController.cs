using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Refund.Commands.InitiateRefund;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// P10-09-BE-5 — Admin-initiated refund endpoint.
///
/// <para><strong>Authorization:</strong> <c>Billing.Admin</c> policy — admin JWT only.</para>
///
/// <para><strong>Flow:</strong> calls <c>IPaymentProvider.Refund</c> (no-op on Fake) and
/// records an audit event. The actual state change (credit clawback + status flip) is driven
/// by the subsequent <c>refund.succeeded</c> webhook to
/// <c>POST /api/Billing/Webhooks/Provider</c>.</para>
/// </summary>
[ApiController]
[Route("api/Admin/Billing/Refunds")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminRefundController : AppControllerBase
{
    private readonly IMediator _mediator;

    public AdminRefundController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// POST /api/Admin/Billing/Refunds/{paymentId}
    ///
    /// Admin-initiated refund for a <c>Succeeded</c> payment.
    /// The provider is called to initiate the refund. The credit clawback occurs asynchronously
    /// when the <c>refund.succeeded</c> webhook is received.
    /// </summary>
    /// <param name="paymentId">The id of the payment to refund.</param>
    /// <param name="request">Refund reason body.</param>
    [HttpPost("{paymentId:int}")]
    [ProducesResponseType(typeof(BaseResponse<InitiateRefundResultDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<InitiateRefundResultDto>), 404)]
    [ProducesResponseType(typeof(BaseResponse<InitiateRefundResultDto>), 422)]
    public async Task<IActionResult> InitiateRefund(
        [FromRoute] int paymentId,
        [FromBody]  InitiateRefundBody request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new InitiateRefundCommand
            {
                PaymentId = paymentId,
                Reason    = request.Reason,
            }, ct);

        return NewResult(result);
    }
}

/// <summary>Request body for <see cref="AdminRefundController.InitiateRefund"/>.</summary>
/// <param name="Reason">Admin-supplied reason (required; max 1000 chars).</param>
public sealed record InitiateRefundBody(string Reason);

using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.SimulateProviderPayment;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// DEV/STAGING ONLY — Simulate provider webhook events to close the mock-payment loop
/// without a real payment gateway.
///
/// <para><strong>SECURITY — triple gate (ALL three must be satisfied):</strong>
/// <list type="number">
///   <item><c>[Authorize(Policy = AdminOnly)]</c> — JWT required, admin/super-admin role only.</item>
///   <item><c>Billing:PaymentProvider:Provider == "Fake"</c> — active provider must be Fake.</item>
///   <item><c>Billing:PaymentProvider:AllowSimulation == true</c> — explicit opt-in flag
///         (default <c>false</c>; MUST stay <c>false</c> in production).</item>
/// </list>
/// When the config gate is not satisfied the handler returns <c>404 Not Found</c> — the
/// endpoint's existence is not disclosed to callers outside a permitted environment.
/// Gating on environment-name alone is intentionally NOT used; the explicit config flag
/// provides safe-by-default behaviour regardless of deployment label.</para>
///
/// <para><strong>Flow:</strong>
/// <list type="bullet">
///   <item>Resolves the <c>Payment</c> row and builds a correctly HMAC-signed fake webhook payload.</item>
///   <item>Runs the payload through the <strong>exact same</strong> <c>IWebhookEventService</c> path
///         as a real provider webhook — state machine (credit grant / subscription activation / refund)
///         fires with no logic duplication.</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/Billing/Webhooks")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class DevWebhookController : AppControllerBase
{
    /// <summary>
    /// POST /api/Billing/Webhooks/Simulate
    ///
    /// Simulates a payment provider webhook event for the given payment id.
    ///
    /// <para><strong>Returns 404</strong> when <c>Billing:PaymentProvider:AllowSimulation</c>
    /// is not <c>true</c> or provider is not <c>"Fake"</c>.</para>
    /// </summary>
    /// <param name="request">Simulation request body.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("Simulate")]
    [ProducesResponseType(typeof(BaseResponse<SimulatePaymentResultDto>), 200)]
    [ProducesResponseType(typeof(BaseResponse<SimulatePaymentResultDto>), 400)]
    [ProducesResponseType(typeof(BaseResponse<SimulatePaymentResultDto>), 404)]
    [ProducesResponseType(typeof(BaseResponse<SimulatePaymentResultDto>), 422)]
    [ProducesResponseType(typeof(BaseResponse<SimulatePaymentResultDto>), 500)]
    public async Task<IActionResult> Simulate(
        [FromBody] SimulateProviderPaymentCommand request,
        CancellationToken ct = default)
    {
        return NewResult(await Mediator.Send(request, ct));
    }
}

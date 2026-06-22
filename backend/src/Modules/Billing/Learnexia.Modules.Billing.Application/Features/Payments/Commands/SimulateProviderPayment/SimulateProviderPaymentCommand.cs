using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.SimulateProviderPayment;

/// <summary>
/// DEV/STAGING ONLY — Simulates a payment provider webhook event for a given payment.
///
/// <para><strong>Security gate (ALL three required):</strong>
/// <list type="number">
///   <item>Caller must hold the <c>AdminOnly</c> policy (JWT).</item>
///   <item>Config <c>Billing:PaymentProvider:Provider == "Fake"</c>.</item>
///   <item>Config <c>Billing:PaymentProvider:AllowSimulation == true</c> (default <c>false</c>).</item>
/// </list>
/// When the config gate is not satisfied the handler returns 404 — the endpoint's existence
/// is not disclosed to callers outside a permitted environment.</para>
///
/// <para><strong>Reuse:</strong> the handler delegates to <see cref="IPaymentSimulationService"/>
/// which builds a correctly signed fake webhook payload and runs it through
/// <c>IWebhookEventService</c> — the SAME path as a real provider webhook.
/// No state-machine logic is duplicated here.</para>
/// </summary>
public sealed record SimulateProviderPaymentCommand : ICommand<BaseResponse<SimulatePaymentResultDto>>
{
    /// <summary>Internal id of the target <c>Payment</c> row.</summary>
    public int PaymentId { get; init; }

    /// <summary>
    /// Normalized event type to simulate.
    /// Allowed: <c>"payment.succeeded"</c>, <c>"payment.failed"</c>, <c>"refund.succeeded"</c>.
    /// Defaults to <c>"payment.succeeded"</c> when not supplied.
    /// </summary>
    public string EventType { get; init; } = "payment.succeeded";
}

/// <summary>Result DTO returned by <see cref="SimulateProviderPaymentCommand"/>.</summary>
/// <param name="PaymentId">The payment id that was simulated.</param>
/// <param name="SimulatedEventId">The synthetic event id generated for this simulation run.</param>
/// <param name="EventType">The event type that was simulated.</param>
/// <param name="Outcome">Short outcome label from the webhook state machine (e.g. "PaymentSucceeded").</param>
public sealed record SimulatePaymentResultDto(
    int    PaymentId,
    string SimulatedEventId,
    string EventType,
    string Outcome);

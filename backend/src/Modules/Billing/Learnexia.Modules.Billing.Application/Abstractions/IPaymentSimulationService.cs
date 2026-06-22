namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for simulating provider webhook events in dev/staging.
///
/// <para><strong>Security gate — dual-key (BOTH required, fails-closed to 404):</strong>
/// <list type="number">
///   <item><c>Billing:PaymentProvider:Provider == "Fake"</c> — the active provider MUST be Fake.</item>
///   <item><c>Billing:PaymentProvider:AllowSimulation == true</c> — explicit opt-in flag
///         (default <c>false</c>; MUST stay <c>false</c> in production).</item>
/// </list>
/// The <c>IsSimulationAllowed()</c> check must be called by the handler BEFORE any state mutation.
/// The endpoint also enforces <c>[Authorize(Policy = AdminOnly)]</c> as defence-in-depth.</para>
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF and signing logic lives in the Infrastructure implementation. Handlers stay EF-free.</para>
/// </summary>
public interface IPaymentSimulationService
{
    /// <summary>
    /// Returns <c>true</c> when simulation is permitted — BOTH config gates are satisfied.
    ///
    /// <para>Callers MUST return 404 (not 403/401) when this returns <c>false</c> so the
    /// endpoint's existence is not disclosed to callers who are not in a permitted environment.</para>
    /// </summary>
    bool IsSimulationAllowed();

    /// <summary>
    /// Looks up the target <c>Payment</c> by <paramref name="paymentId"/> and builds a
    /// correctly HMAC-signed webhook payload via
    /// <see cref="FakePaymentProvider.BuildSignedWebhookPayload"/>, then runs it through
    /// the <strong>exact same processing path</strong> as a real provider webhook
    /// (<see cref="IWebhookEventService"/>) so the full state machine fires
    /// (credit grant / subscription activation / refund).
    ///
    /// <para>This method does NOT duplicate any webhook state-machine logic — it reuses
    /// <see cref="IWebhookEventService"/> directly.</para>
    ///
    /// <para>Pre-condition: <see cref="IsSimulationAllowed()"/> MUST be true before calling this.</para>
    /// </summary>
    /// <param name="paymentId">Internal <c>Payment.Id</c> of the target payment.</param>
    /// <param name="eventType">
    /// Normalized webhook event type to simulate.
    /// Allowed values: <c>"payment.succeeded"</c>, <c>"payment.failed"</c>, <c>"refund.succeeded"</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SimulatePaymentResult"/> describing the outcome.</returns>
    Task<SimulatePaymentResult> SimulateWebhookAsync(
        int paymentId,
        string eventType,
        CancellationToken ct);
}

/// <summary>Outcome of <see cref="IPaymentSimulationService.SimulateWebhookAsync"/>.</summary>
public sealed record SimulatePaymentResult
{
    public bool Succeeded { get; init; }
    public string? EventId { get; init; }
    public string? Outcome { get; init; }
    public SimulatePaymentFailureReason? FailureReason { get; init; }

    public static SimulatePaymentResult Ok(string eventId, string outcome)
        => new() { Succeeded = true, EventId = eventId, Outcome = outcome };

    public static SimulatePaymentResult Fail(SimulatePaymentFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Typed failure reasons for <see cref="IPaymentSimulationService.SimulateWebhookAsync"/>.</summary>
public enum SimulatePaymentFailureReason
{
    /// <summary>No payment with the given id was found.</summary>
    PaymentNotFound = 1,

    /// <summary>The payment does not have a provider payment ref yet (checkout session not started).</summary>
    ProviderRefMissing = 2,

    /// <summary>The webhook event type is not supported for simulation.</summary>
    UnsupportedEventType = 3,
}

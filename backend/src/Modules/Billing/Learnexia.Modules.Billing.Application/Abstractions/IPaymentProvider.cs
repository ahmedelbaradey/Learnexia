using Learnexia.Modules.Billing.Domain.Entities;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Payment provider seam — isolates the Billing application layer from any specific payment
/// gateway. The active provider (Fake or live) is injected by DI; the application layer
/// never references a concrete provider class.
///
/// <para><strong>MVP provider:</strong> <c>FakePaymentProvider</c> is the active implementation.
/// The real adapter (<c>PaymobPaymentProvider</c>) is [EXTERNAL] — swap it behind this seam
/// when credentials and onboarding are available. Config key: <c>Billing:PaymentProvider:Provider</c>.</para>
///
/// <para><strong>Mock simulation:</strong> to complete the mock-payment loop in dev/staging
/// (simulate a provider webhook after the parent is redirected to the fake checkout URL),
/// use <c>POST /api/Billing/Webhooks/Simulate</c> — requires AdminOnly JWT +
/// <c>Billing:PaymentProvider:AllowSimulation=true</c> + <c>Provider="Fake"</c>.
/// See <c>IPaymentSimulationService</c>.</para>
///
/// <para><strong>One interface, one impl now</strong> — per CLAUDE.md rule 8 no
/// Strategy/Factory/registry is introduced. Config-driven DI selection replaces patterns.</para>
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// Creates a hosted-checkout session with the provider and returns the redirect URL
    /// for the parent to complete payment.
    ///
    /// <para>The amount is always resolved server-side before this call — the provider
    /// receives the authoritative amount, not a client-supplied one.</para>
    /// </summary>
    /// <param name="payment">The <see cref="Payment"/> record (already persisted with <c>Initiated</c> status).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CheckoutSession"/> containing the redirect URL and provider ref.</returns>
    Task<CheckoutSession> CreateCheckoutSessionAsync(Payment payment, CancellationToken ct);

    /// <summary>
    /// Verifies the provider's HMAC signature on a raw webhook body.
    ///
    /// <para>SECURITY CRITICAL: this MUST be called before any state mutation.
    /// Uses constant-time comparison internally to prevent timing attacks.</para>
    /// </summary>
    /// <param name="rawBody">The raw bytes of the HTTP request body.</param>
    /// <param name="signatureHeader">The signature value from the provider HTTP header.</param>
    /// <returns><c>true</c> if the signature is valid; <c>false</c> otherwise.</returns>
    bool VerifyWebhookSignature(byte[] rawBody, string? signatureHeader);

    /// <summary>
    /// Parses a raw (already signature-verified) webhook body into a normalized event.
    /// </summary>
    /// <param name="rawBody">Raw bytes of the verified webhook body.</param>
    /// <returns>A <see cref="ParsedWebhookEvent"/> with the provider event id, type, and payload.</returns>
    ParsedWebhookEvent ParseWebhookEvent(byte[] rawBody);

    /// <summary>
    /// Cancels a recurring subscription with the provider (if supported).
    /// A <c>FakePaymentProvider</c> implementation is a no-op.
    /// </summary>
    /// <param name="providerPaymentRef">Provider's reference for the recurring agreement.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CancelRecurringAsync(string providerPaymentRef, CancellationToken ct);

    /// <summary>
    /// Initiates a refund for a previously succeeded payment at the provider.
    /// The provider will send a <c>refund.succeeded</c> webhook when the refund is processed.
    ///
    /// <para><c>FakePaymentProvider</c> is a deterministic no-op (no real money movement;
    /// returns success so the state machine can be tested end-to-end).</para>
    /// </summary>
    /// <param name="providerPaymentRef">Provider's opaque payment reference.</param>
    /// <param name="amount">Amount to refund (server-authoritative — never client-supplied).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the provider acknowledged the refund request; <c>false</c> on error.</returns>
    Task<bool> InitiateRefundAsync(string providerPaymentRef, decimal amount, CancellationToken ct);
}

/// <summary>Result of a successful <see cref="IPaymentProvider.CreateCheckoutSessionAsync"/> call.</summary>
/// <param name="RedirectUrl">URL the parent's browser should be redirected to for payment.</param>
/// <param name="ProviderPaymentRef">Provider's opaque reference for this transaction (stored on <see cref="Payment"/>).</param>
public sealed record CheckoutSession(string RedirectUrl, string ProviderPaymentRef);

/// <summary>
/// Normalized representation of a provider webhook event after
/// <see cref="IPaymentProvider.ParseWebhookEvent"/>.
/// </summary>
/// <param name="ProviderEventId">Provider's unique event identifier (idempotency key).</param>
/// <param name="EventType">Normalized event type string: <c>"payment.succeeded"</c>, <c>"payment.failed"</c>, etc.</param>
/// <param name="PaymentRef">Provider's payment reference (links to <see cref="Payment.ProviderPaymentRef"/>).</param>
/// <param name="AmountFromProvider">Charge amount as reported by provider. Reconciled against the server-side Payment record.</param>
/// <param name="RawPayload">Original JSON payload (for the <see cref="WebhookEvent"/> audit record).</param>
public sealed record ParsedWebhookEvent(
    string ProviderEventId,
    string EventType,
    string PaymentRef,
    decimal AmountFromProvider,
    string RawPayload);

/// <summary>Known normalized event type strings used by <see cref="ParsedWebhookEvent.EventType"/>.</summary>
public static class WebhookEventTypes
{
    public const string PaymentSucceeded = "payment.succeeded";
    public const string PaymentFailed    = "payment.failed";

    // ── P10-09 ────────────────────────────────────────────────────────────────────
    /// <summary>A recurring subscription charge attempt failed (triggers dunning).</summary>
    public const string ChargeFailed    = "charge.failed";
    /// <summary>A refund was processed successfully by the provider.</summary>
    public const string RefundSucceeded = "refund.succeeded";
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Billing.Infrastructure.Providers;

/// <summary>
/// Deterministic fake payment provider for development, test, and CI environments.
///
/// <para><strong>How it works:</strong>
/// <list type="bullet">
///   <item><see cref="CreateCheckoutSessionAsync"/> returns a synthetic redirect URL
///         and a deterministic provider payment ref (<c>fake-{paymentId}-{guid}</c>).</item>
///   <item><see cref="VerifyWebhookSignature"/> verifies a real HMAC-SHA256 signature
///         (computed from the raw body + the configurable signing secret
///         <c>Billing:PaymentProvider:FakeSigningSecret</c>). This gives tests a genuine
///         signature gate to probe — both valid and forged requests are testable.</item>
///   <item><see cref="ParseWebhookEvent"/> deserializes the synthetic JSON payload
///         produced by <see cref="BuildSignedWebhookPayload"/>.</item>
///   <item><see cref="CancelRecurringAsync"/> is a no-op (fake has no recurring agreements).</item>
/// </list>
/// </para>
///
/// <para><strong>Config selection:</strong> registered when
/// <c>Billing:PaymentProvider:Provider = "Fake"</c> (the default, MVP).
/// Swap to a real adapter by changing the config key — no code change needed.
/// The <c>PaymobPaymentProvider</c> adapter is [EXTERNAL] and slots in here when credentials
/// are available.</para>
///
/// <para><strong>Mock simulation (dev/staging):</strong> after the parent is redirected to the
/// fake checkout URL, complete the mock-payment loop by calling
/// <c>POST /api/Billing/Webhooks/Simulate</c> (AdminOnly JWT, <c>AllowSimulation=true</c>).
/// The simulate endpoint reuses <c>BuildSignedWebhookPayload</c> + <c>IWebhookEventService</c>
/// — no state-machine logic is duplicated. See <c>IPaymentSimulationService</c>.</para>
///
/// <para><strong>Secrets:</strong> <c>FakeSigningSecret</c> is read from config/environment
/// and defaults to an empty string (unsigned) only for local dev. Tests must supply a secret
/// to exercise the signature path. Production MUST NOT use this provider.</para>
/// </summary>
public sealed class FakePaymentProvider : IPaymentProvider
{
    private readonly string _signingSecret;
    private readonly ILoggerManager _logger;

    // Config path for the signing secret — tests set this to a known value.
    private const string SigningSecretConfigKey = "Billing:PaymentProvider:FakeSigningSecret";

    public FakePaymentProvider(IConfiguration configuration, ILoggerManager logger)
    {
        // Empty default: if no secret is configured, VerifyWebhookSignature will always fail
        // (which is the safe default — only tests that supply a secret can pass the gate).
        _signingSecret = configuration[SigningSecretConfigKey] ?? string.Empty;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<CheckoutSession> CreateCheckoutSessionAsync(Payment payment, CancellationToken ct)
    {
        var providerRef = $"fake-{payment.Id}-{Guid.NewGuid():N}";
        var redirectUrl = $"https://fake-payment.learnexia.test/pay?ref={providerRef}&amount={payment.Amount}&currency={payment.Currency}";

        _logger.LogInfo($"FakePaymentProvider.CreateCheckoutSession: paymentId={payment.Id}, ref={providerRef}.");

        return Task.FromResult(new CheckoutSession(redirectUrl, providerRef));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses HMAC-SHA256 with the configured <see cref="SigningSecretConfigKey"/>.
    /// Compares using <see cref="CryptographicOperations.FixedTimeEquals"/> (constant-time)
    /// to prevent timing-oracle attacks.
    /// </remarks>
    public bool VerifyWebhookSignature(byte[] rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogInfo("FakePaymentProvider.VerifyWebhookSignature: missing signature header.");
            return false;
        }

        if (string.IsNullOrEmpty(_signingSecret))
        {
            // No secret configured — all webhooks fail signature check (safe default).
            _logger.LogInfo("FakePaymentProvider.VerifyWebhookSignature: no signing secret configured — rejecting.");
            return false;
        }

        var secretBytes = Encoding.UTF8.GetBytes(_signingSecret);
        using var hmac = new HMACSHA256(secretBytes);
        var computedHashBytes = hmac.ComputeHash(rawBody);
        var computedHex = Convert.ToHexString(computedHashBytes).ToLowerInvariant();

        // The header format is: "sha256=<hex>" (mirroring GitHub/Stripe webhook convention).
        var expectedHeader = $"sha256={computedHex}";

        // Constant-time compare — prevents timing-oracle attacks.
        var headerBytes   = Encoding.UTF8.GetBytes(signatureHeader);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHeader);

        if (headerBytes.Length != expectedBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(headerBytes, expectedBytes);
    }

    /// <inheritdoc />
    public ParsedWebhookEvent ParseWebhookEvent(byte[] rawBody)
    {
        var json = Encoding.UTF8.GetString(rawBody);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var eventId   = root.GetProperty("eventId").GetString()
                        ?? throw new InvalidOperationException("Missing eventId in fake webhook payload.");
        var eventType = root.GetProperty("eventType").GetString()
                        ?? throw new InvalidOperationException("Missing eventType in fake webhook payload.");
        var paymentRef = root.GetProperty("paymentRef").GetString()
                        ?? throw new InvalidOperationException("Missing paymentRef in fake webhook payload.");
        var amount = root.GetProperty("amount").GetDecimal();

        return new ParsedWebhookEvent(
            ProviderEventId    : eventId,
            EventType          : eventType,
            PaymentRef         : paymentRef,
            AmountFromProvider : amount,
            RawPayload         : json);
    }

    /// <inheritdoc />
    public Task CancelRecurringAsync(string providerPaymentRef, CancellationToken ct)
    {
        // Fake has no recurring agreements — no-op.
        _logger.LogInfo($"FakePaymentProvider.CancelRecurring: no-op for ref={providerPaymentRef}.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Fake is a no-op — always returns <c>true</c> (deterministic success).
    /// The actual state change (credit clawback + status flip) is driven by
    /// the subsequent <c>refund.succeeded</c> webhook in tests.
    /// Production: swap to real adapter when Paymob integration is live.
    /// </remarks>
    public Task<bool> InitiateRefundAsync(string providerPaymentRef, decimal amount, CancellationToken ct)
    {
        _logger.LogInfo(
            $"FakePaymentProvider.InitiateRefund: no-op for ref={providerPaymentRef}, amount={amount}.");
        return Task.FromResult(true);
    }

    // ── Test-helper: build a correctly signed synthetic webhook payload ──────────────
    //
    // Tests call this to produce a (rawBody, signatureHeader) pair that passes
    // VerifyWebhookSignature. This is the only way to produce a valid signed webhook
    // in tests — simulating what the real provider would send.

    /// <summary>
    /// Builds a synthetic webhook payload for testing.
    ///
    /// <para>Returns the raw body bytes and the <c>sha256=...</c> signature header value
    /// that would be sent by the Fake provider for the given parameters.</para>
    /// </summary>
    /// <param name="eventId">Unique event id (use a fresh Guid per test).</param>
    /// <param name="eventType">Event type string (e.g. <c>"payment.succeeded"</c>).</param>
    /// <param name="paymentRef">Provider payment reference (must match Payment.ProviderPaymentRef).</param>
    /// <param name="amount">Amount in EGP.</param>
    /// <param name="signingSecret">The same secret used by the provider instance under test.</param>
    /// <returns>Tuple of (rawBody bytes, signatureHeader string).</returns>
    public static (byte[] RawBody, string SignatureHeader) BuildSignedWebhookPayload(
        string eventId,
        string eventType,
        string paymentRef,
        decimal amount,
        string signingSecret)
    {
        var json = JsonSerializer.Serialize(new
        {
            eventId,
            eventType,
            paymentRef,
            amount,
        });

        var rawBody = Encoding.UTF8.GetBytes(json);

        var secretBytes = Encoding.UTF8.GetBytes(signingSecret);
        using var hmac = new HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(rawBody);
        var signature = $"sha256={Convert.ToHexString(hashBytes).ToLowerInvariant()}";

        return (rawBody, signature);
    }
}

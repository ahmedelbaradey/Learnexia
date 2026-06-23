using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Billing.Infrastructure.Providers;

/// <summary>
/// STUB for the live Paymob payment adapter (P10-06-BE-3 — NOT yet implemented).
///
/// <para><strong>Why this exists:</strong> it fills the real-provider slot behind the
/// <see cref="IPaymentProvider"/> seam so that selecting <c>Billing:PaymentProvider:Provider = "Paymob"</c>
/// resolves to a dedicated type instead of silently falling back to <see cref="FakePaymentProvider"/>.
/// Silently using the Fake mock under a "Paymob" config would be dangerous — it would grant paid
/// entitlements (subscriptions, energy) with no real money movement.</para>
///
/// <para><strong>Behaviour:</strong> construction is a no-op (the app boots without Paymob secrets),
/// but EVERY operation throws a clear "not configured" error. So a deployment set to "Paymob" starts
/// normally yet fails LOUD on the first payment call until the adapter is implemented and keys are
/// provisioned — never silently mock-approving.</para>
///
/// <para><strong>To go live (P10-06-BE-3):</strong> implement these methods against the Paymob API
/// (hosted checkout / redirect, HMAC webhook verification, refund), read keys + webhook signing secret
/// from config/secret store (never hardcode), and keep the same <see cref="IPaymentProvider"/> contract
/// so no consumer changes. One interface, one impl per config value (CLAUDE.md rule 8 — no Strategy/Factory).
/// See <c>docs/dev/AI-ACTIVATION-RUNBOOK.md</c>-style activation + the HANDOFF prod deploy checklist.</para>
/// </summary>
public sealed class PaymobPaymentProvider : IPaymentProvider
{
    private readonly ILoggerManager _logger;

    public PaymobPaymentProvider(IConfiguration configuration, ILoggerManager logger)
    {
        // No-op construction: the app must boot even without Paymob secrets. Keys are read lazily
        // when the real implementation is added; the stub intentionally reads nothing here.
        _logger = logger;
    }

    private InvalidOperationException NotConfigured(string operation)
    {
        _logger.LogWarn(
            $"PaymobPaymentProvider.{operation} called but the live Paymob adapter is a STUB (P10-06-BE-3 " +
            "not implemented). Set Billing:PaymentProvider:Provider=Fake for dev/staging, or implement " +
            "the Paymob adapter + provision keys before using Provider=Paymob.");

        return new InvalidOperationException(
            $"PaymobPaymentProvider.{operation} is not implemented — the live Paymob adapter is a stub " +
            "(P10-06-BE-3). Provide Paymob API keys + webhook signing secret and implement the Paymob API " +
            "calls, or set Billing:PaymentProvider:Provider=Fake for dev/staging.");
    }

    /// <inheritdoc />
    public Task<CheckoutSession> CreateCheckoutSessionAsync(Payment payment, CancellationToken ct)
        => throw NotConfigured(nameof(CreateCheckoutSessionAsync));

    /// <inheritdoc />
    public bool VerifyWebhookSignature(byte[] rawBody, string? signatureHeader)
        => throw NotConfigured(nameof(VerifyWebhookSignature));

    /// <inheritdoc />
    public ParsedWebhookEvent ParseWebhookEvent(byte[] rawBody)
        => throw NotConfigured(nameof(ParseWebhookEvent));

    /// <inheritdoc />
    public Task CancelRecurringAsync(string providerPaymentRef, CancellationToken ct)
        => throw NotConfigured(nameof(CancelRecurringAsync));

    /// <inheritdoc />
    public Task<bool> InitiateRefundAsync(string providerPaymentRef, decimal amount, CancellationToken ct)
        => throw NotConfigured(nameof(InitiateRefundAsync));
}

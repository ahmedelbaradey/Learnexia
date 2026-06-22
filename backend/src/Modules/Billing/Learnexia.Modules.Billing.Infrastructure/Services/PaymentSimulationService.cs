using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IPaymentSimulationService"/> (Option C, dev/staging only).
///
/// <para><strong>What it does:</strong>
/// <list type="bullet">
///   <item>Enforces the dual security gate: <c>Billing:PaymentProvider:Provider == "Fake"</c>
///         AND <c>Billing:PaymentProvider:AllowSimulation == true</c>.</item>
///   <item>Loads the target <c>Payment</c> row to resolve <c>ProviderPaymentRef</c> and <c>Amount</c>.</item>
///   <item>Calls <c>FakePaymentProvider.BuildSignedWebhookPayload</c> (static helper) to produce a
///         correctly HMAC-signed payload — the SAME signature that <c>FakePaymentProvider.VerifyWebhookSignature</c>
///         would accept.</item>
///   <item>Passes the signed payload directly to <see cref="IWebhookEventService"/> — the EXACT
///         same service that processes real provider webhooks — so the full state machine fires.</item>
/// </list>
/// No state-machine logic is duplicated here.</para>
///
/// <para><strong>Prod safety:</strong> <see cref="IsSimulationAllowed"/> returns <c>false</c> unless
/// BOTH config keys are satisfied. The default for <c>AllowSimulation</c> is <c>false</c>.
/// Production deployments must never set it to <c>true</c>.</para>
/// </summary>
public sealed class PaymentSimulationService : IPaymentSimulationService
{
    // Config paths — defined once here, used in ctor + IsSimulationAllowed.
    private const string ProviderConfigKey       = "Billing:PaymentProvider:Provider";
    private const string AllowSimulationConfigKey = "Billing:PaymentProvider:AllowSimulation";
    private const string SigningSecretConfigKey   = "Billing:PaymentProvider:FakeSigningSecret";

    private readonly BillingDbContext _db;
    private readonly IWebhookEventService _webhookEventService;
    private readonly IConfiguration _configuration;
    private readonly ILoggerManager _logger;

    public PaymentSimulationService(
        BillingDbContext db,
        IWebhookEventService webhookEventService,
        IConfiguration configuration,
        ILoggerManager logger)
    {
        _db                  = db;
        _webhookEventService = webhookEventService;
        _configuration       = configuration;
        _logger              = logger;
    }

    /// <inheritdoc />
    public bool IsSimulationAllowed()
    {
        var providerName = _configuration[ProviderConfigKey] ?? "Fake";
        if (!string.Equals(providerName, "Fake", StringComparison.OrdinalIgnoreCase))
            return false;

        // Explicit opt-in: AllowSimulation must be explicitly "true" — any other value (including
        // absent) evaluates to false. This prevents simulation from turning on by accident.
        var allowStr = _configuration[AllowSimulationConfigKey];
        return bool.TryParse(allowStr, out var allow) && allow;
    }

    /// <inheritdoc />
    public async Task<SimulatePaymentResult> SimulateWebhookAsync(
        int paymentId,
        string eventType,
        CancellationToken ct)
    {
        // ── Validate event type ───────────────────────────────────────────────────────
        if (!IsAllowedEventType(eventType))
        {
            return SimulatePaymentResult.Fail(SimulatePaymentFailureReason.UnsupportedEventType);
        }

        // ── Load the Payment ──────────────────────────────────────────────────────────
        var payment = await _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        if (payment is null)
        {
            _logger.LogInfo($"PaymentSimulationService: payment {paymentId} not found.");
            return SimulatePaymentResult.Fail(SimulatePaymentFailureReason.PaymentNotFound);
        }

        if (string.IsNullOrEmpty(payment.ProviderPaymentRef))
        {
            _logger.LogInfo(
                $"PaymentSimulationService: payment {paymentId} has no ProviderPaymentRef — checkout not started.");
            return SimulatePaymentResult.Fail(SimulatePaymentFailureReason.ProviderRefMissing);
        }

        // ── Build signed payload via FakePaymentProvider static helper ────────────────
        // DETERMINISTIC ProviderEventId per (payment, eventType): replaying the same simulated
        // event resolves to the same id, so the webhook idempotency guard below short-circuits it
        // (prevents an admin double-granting energy / re-activating a subscription on a second call).
        // Distinct event types on the same payment (e.g. succeeded then refund) keep distinct ids.
        var signingSecret = _configuration[SigningSecretConfigKey] ?? string.Empty;
        var eventId       = $"sim-{paymentId}-{eventType}";

        var (rawBody, signatureHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId,
            eventType,
            payment.ProviderPaymentRef,
            payment.Amount,
            signingSecret);

        _logger.LogInfo(
            $"PaymentSimulationService: dispatching synthetic {eventType} for paymentId={paymentId}, " +
            $"ref={payment.ProviderPaymentRef}, eventId={eventId}.");

        // ── Run through the real webhook path ─────────────────────────────────────────
        // Signature verification inside HandleProviderWebhookCommandHandler is bypassed here
        // because this service calls IWebhookEventService directly (post-verification path).
        // The signed payload is produced with the SAME secret, so if you were to route it
        // through the full HTTP pipeline it would also pass VerifyWebhookSignature.
        //
        // We call the service directly (not via MediatR dispatch) to avoid re-doing signature
        // verification on a payload we just signed ourselves — the gate is already enforced at
        // the controller layer (AdminOnly + IsSimulationAllowed).

        // Parse the payload (same code path as the real handler — reuse ParseWebhookEvent logic).
        var parsed = ParsePayload(rawBody, eventId, eventType, payment.ProviderPaymentRef, payment.Amount);

        // Idempotency check (the service owns this check; pass-through even if already processed).
        if (await _webhookEventService.IsAlreadyProcessedAsync(parsed.ProviderEventId, ct))
        {
            _logger.LogInfo(
                $"PaymentSimulationService: duplicate eventId={eventId} (replay or concurrent race) — idempotent, no re-grant.");
            return SimulatePaymentResult.Ok(eventId, "Duplicate");
        }

        // Route to the appropriate service method — mirrors HandleProviderWebhookCommandHandler.
        var result = parsed.EventType switch
        {
            WebhookEventTypes.PaymentSucceeded =>
                await _webhookEventService.HandlePaymentSucceededAsync(parsed, ct),

            WebhookEventTypes.PaymentFailed =>
                await _webhookEventService.HandlePaymentFailedAsync(parsed, ct),

            WebhookEventTypes.RefundSucceeded =>
                await _webhookEventService.HandleRefundSucceededAsync(parsed, ct),

            _ => await _webhookEventService.HandleUnknownEventAsync(parsed, ct),
        };

        _logger.LogInfo(
            $"PaymentSimulationService: webhook result — succeeded={result.Succeeded}, outcome={result.Outcome}.");

        return SimulatePaymentResult.Ok(eventId, result.Outcome);
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────

    private static bool IsAllowedEventType(string eventType) =>
        eventType is WebhookEventTypes.PaymentSucceeded
                  or WebhookEventTypes.PaymentFailed
                  or WebhookEventTypes.RefundSucceeded;

    /// <summary>
    /// Builds a <see cref="ParsedWebhookEvent"/> directly from the known parameters
    /// instead of round-tripping through JSON deserialization.
    /// This avoids a redundant parse of a payload we just assembled ourselves.
    /// </summary>
    private static ParsedWebhookEvent ParsePayload(
        byte[] rawBody,
        string eventId,
        string eventType,
        string paymentRef,
        decimal amount)
    {
        // RawPayload is stored verbatim in the WebhookEvent row for audit.
        var rawJson = System.Text.Encoding.UTF8.GetString(rawBody);

        return new ParsedWebhookEvent(
            ProviderEventId    : eventId,
            EventType          : eventType,
            PaymentRef         : paymentRef,
            AmountFromProvider : amount,
            RawPayload         : rawJson);
    }
}

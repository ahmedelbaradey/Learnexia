namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for processing incoming payment-provider webhook events.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, idempotency, 23505-handling, and post-commit event-staging logic
/// lives in the Infrastructure implementation. Handlers stay EF-free.</para>
///
/// <para><strong>Security pipeline (order is mandatory, enforced INSIDE this implementation):</strong>
/// <list type="number">
///   <item>Signature verification is performed by the CALLER (handler) BEFORE calling this service —
///         the service assumes the payload has already been signature-verified.</item>
///   <item>Idempotency: <c>WebhookEvent.ProviderEventId</c> unique index guards against replays.</item>
///   <item>The <c>WebhookEvent</c> record is inserted FIRST (within the transaction) before
///         any state flip — ensures the idempotency record exists even if a later step fails.</item>
///   <item>Amount reconciliation: server-side <c>Payment.Amount</c> is authoritative;
///         provider-supplied amounts are logged but never used to inflate the credited tier.</item>
///   <item>Post-commit integration events (<c>SubscriptionActivatedIntegrationEvent</c>,
///         <c>PaymentFailedIntegrationEvent</c>) fire AFTER commit.</item>
/// </list>
/// </para>
/// </summary>
public interface IWebhookEventService
{
    /// <summary>
    /// Checks whether a webhook event with the given <paramref name="providerEventId"/>
    /// has already been processed.
    /// </summary>
    Task<bool> IsAlreadyProcessedAsync(string providerEventId, CancellationToken ct);

    /// <summary>
    /// Processes a <c>payment.succeeded</c> webhook event.
    ///
    /// <para>For <c>Pack</c> payments: inserts the <c>WebhookEvent</c> idempotency record and
    /// delegates credit application to <see cref="IEnergyPackService.CreditPurchasedPackAsync"/>.</para>
    ///
    /// <para>For <c>Subscription</c> payments: inserts the <c>WebhookEvent</c> record, flips
    /// <c>Payment → Succeeded</c>, flips <c>Subscription → Active Premium</c> with cycle dates,
    /// commits, and publishes <c>SubscriptionActivatedIntegrationEvent</c> post-commit.</para>
    /// </summary>
    Task<WebhookProcessResult> HandlePaymentSucceededAsync(ParsedWebhookEvent parsed, CancellationToken ct);

    /// <summary>
    /// Processes a <c>payment.failed</c> webhook event.
    /// Inserts the <c>WebhookEvent</c> record, flips <c>Payment → Failed</c>, commits,
    /// and publishes <c>PaymentFailedIntegrationEvent</c> post-commit.
    /// </summary>
    Task<WebhookProcessResult> HandlePaymentFailedAsync(ParsedWebhookEvent parsed, CancellationToken ct);

    /// <summary>
    /// Processes a <c>charge.failed</c> webhook event (dunning path).
    /// Inserts the <c>WebhookEvent</c> record, commits, then delegates dunning state update
    /// to <see cref="IRefundService.ProcessChargeFailedAsync"/>, and publishes
    /// <c>PaymentFailedIntegrationEvent</c> post-commit.
    /// </summary>
    Task<WebhookProcessResult> HandleChargeFailedAsync(ParsedWebhookEvent parsed, CancellationToken ct);

    /// <summary>
    /// Processes a <c>refund.succeeded</c> webhook event.
    /// Inserts the <c>WebhookEvent</c> record, commits, then delegates to
    /// <see cref="IRefundService.ProcessPackRefundAsync"/> or
    /// <see cref="IRefundService.ProcessSubscriptionRefundAsync"/> based on <c>Payment.Kind</c>.
    /// </summary>
    Task<WebhookProcessResult> HandleRefundSucceededAsync(ParsedWebhookEvent parsed, CancellationToken ct);

    /// <summary>
    /// Records an unhandled/unknown webhook event type for audit purposes.
    /// Takes no state action beyond inserting the <c>WebhookEvent</c> row.
    /// </summary>
    Task<WebhookProcessResult> HandleUnknownEventAsync(ParsedWebhookEvent parsed, CancellationToken ct);
}

/// <summary>Outcome of a webhook event processing operation.</summary>
public sealed record WebhookProcessResult
{
    public bool Succeeded { get; init; }
    public bool WasDuplicate { get; init; }

    /// <summary>Short outcome label for telemetry (e.g. "PaymentSucceeded", "PackCredited", "ConcurrentDuplicate").</summary>
    public string Outcome { get; init; } = string.Empty;

    /// <summary>True when a concurrent 23505 race was detected and treated as idempotent success.</summary>
    public bool WasConcurrentDuplicate { get; init; }

    public static WebhookProcessResult Ok(string outcome)
        => new() { Succeeded = true, Outcome = outcome };

    public static WebhookProcessResult Duplicate(string outcome = "Duplicate")
        => new() { Succeeded = true, WasDuplicate = true, Outcome = outcome };

    public static WebhookProcessResult ConcurrentDuplicate()
        => new() { Succeeded = true, WasConcurrentDuplicate = true, Outcome = "ConcurrentDuplicate" };

    public static WebhookProcessResult Fail(string outcome)
        => new() { Succeeded = false, Outcome = outcome };
}

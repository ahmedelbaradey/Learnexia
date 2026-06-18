using Learnexia.Modules.Billing.Application.Features.Refunds.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for payment refund and dunning state-management operations.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface,
/// not <see cref="IBillingDbContext"/>. All EF, transaction, ledger, and concurrency logic lives
/// in the Infrastructure implementation. Handlers stay EF-free.</para>
///
/// <para><strong>Idempotency:</strong>
/// <list type="bullet">
///   <item><see cref="ProcessPackRefundAsync"/> is guarded by the <c>WebhookEvent.ProviderEventId</c>
///         unique index (outer, per-event) AND by a PER-PAYMENT <c>CreditTransaction.IdempotencyKey</c>
///         (<c>"pack-refund:{paymentId}"</c>, DB-unique) + a <c>Payment.Status == Refunded</c> guard —
///         so a 2nd refund.succeeded with a DISTINCT event id cannot over-refund the same payment.</item>
///   <item><see cref="ProcessSubscriptionRefundAsync"/> is guarded by <c>WebhookEvent.ProviderEventId</c>
///         (webhook idempotency) and a status check (already-<c>Refunded</c> rows are no-ops).</item>
///   <item><see cref="ProcessChargeFailedAsync"/> is idempotent per (subscriptionId, providerEventId)
///         via the <c>WebhookEvent</c> table and a status re-check.</item>
///   <item><see cref="InitiateRefundAsync"/> is admin-triggered: idempotency is the provider's
///         responsibility — this call is a one-shot action that updates <c>Payment.Status</c>
///         to <c>Refunded</c> for the pending confirmation.</item>
/// </list>
/// </para>
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Processes a <c>refund.succeeded</c> webhook event for a <c>Pack</c> payment.
    ///
    /// <para>Claws back <em>unspent</em> purchased credits only (clamped to available
    /// <see cref="Domain.Entities.CreditAccount.PurchasedBalance"/> — never drives it negative).
    /// Appends a <see cref="Domain.Enums.CreditTransactionType.Refund"/>
    /// <see cref="Domain.Entities.CreditTransaction"/> and marks <c>Payment.Status = Refunded</c>
    /// in a single explicit transaction. Uses the <c>xmin</c> concurrency token to guard against
    /// concurrent negative-balance races.</para>
    ///
    /// <para>The outer idempotency guard (WebhookEvent unique index) is the caller's responsibility
    /// (webhook handler inserts the row before calling this method). This method also checks a
    /// per-call <c>CreditTransaction.IdempotencyKey</c> as an inner guard.</para>
    /// </summary>
    /// <param name="paymentId">The id of the Pack payment being refunded.</param>
    /// <param name="providerEventId">The provider's event id (forms the inner idempotency key).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="RefundResult"/> describing the outcome.</returns>
    Task<RefundResult> ProcessPackRefundAsync(
        int paymentId,
        string providerEventId,
        CancellationToken ct);

    /// <summary>
    /// Processes a <c>refund.succeeded</c> webhook event for a <c>Subscription</c> payment.
    ///
    /// <para>Policy: revoke Premium access by downgrades the <c>Subscription</c> to Free immediately
    /// (sets <c>PlanCode = Free</c>, <c>Status = Active</c>, clears cycle dates).
    /// Does NOT claw back already-granted monthly credits — those are consumed (policy decision).
    /// Marks <c>Payment.Status = Refunded</c>. Atomic explicit transaction.</para>
    /// </summary>
    /// <param name="paymentId">The id of the Subscription payment being refunded.</param>
    /// <param name="providerEventId">Provider event id (used for idempotency + logging).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="RefundResult"/> describing the outcome.</returns>
    Task<RefundResult> ProcessSubscriptionRefundAsync(
        int paymentId,
        string providerEventId,
        CancellationToken ct);

    /// <summary>
    /// Processes a <c>charge.failed</c> webhook event for a <c>Subscription</c> payment.
    ///
    /// <para>Increments <c>Subscription.FailedAttemptCount</c>, sets <c>Payment.Status = Failed</c>,
    /// and schedules the next retry (<c>NextRetryAt</c>) based on config-driven interval.
    /// When <c>FailedAttemptCount &ge; DunningMaxRetries</c>, transitions the subscription to
    /// <c>Status = Dunning</c> and sets <c>GraceEndsAt = CurrentCycleEnd</c> (downgrade at cycle end).
    /// Atomic explicit transaction. Idempotent per (subscriptionId, providerEventId).</para>
    /// </summary>
    /// <param name="paymentId">The failed payment's id.</param>
    /// <param name="subscriptionId">The subscription being dunned.</param>
    /// <param name="providerEventId">Provider event id (for idempotency).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ChargeFailedResult"/> describing the dunning state after processing.</returns>
    Task<ChargeFailedResult> ProcessChargeFailedAsync(
        int paymentId,
        int subscriptionId,
        string providerEventId,
        CancellationToken ct);

    /// <summary>
    /// Admin-initiated refund: calls <c>IPaymentProvider.Refund</c> and records a pending-refund
    /// state on the payment. The actual state change (credit clawback + status flip) is driven by
    /// the subsequent <c>refund.succeeded</c> webhook — not by this call.
    ///
    /// <para>SECURITY: admin-only. The <c>[Authorize(Policy="Billing.Admin")]</c> attribute
    /// lives on the controller; this method assumes the caller is already authorized.</para>
    /// </summary>
    /// <param name="paymentId">The payment to refund.</param>
    /// <param name="reason">Admin-supplied reason (for audit log / logging only).</param>
    /// <param name="adminUserId">Admin user id (from JWT, for audit).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="InitiateRefundResult"/> indicating whether the provider call succeeded.</returns>
    Task<InitiateRefundResult> InitiateRefundAsync(
        int paymentId,
        string reason,
        int adminUserId,
        CancellationToken ct);

    // ── P10-17 purchased-energy refund ───────────────────────────────────────────

    /// <summary>
    /// Computes the refundable purchased-energy balance for a specific pack payment
    /// (P10-17-BE-1 / BE-2).
    ///
    /// <para><strong>Reconciliation (FIFO per-payment):</strong>
    /// Reads the immutable family ledger to determine how much of the pack identified by
    /// <paramref name="purchasePaymentId"/> has been consumed (bucket-B spends, FIFO-attributed).
    /// Refundable = <c>purchasedTotal − consumedPurchased</c>, clamped ≥ 0 and never exceeding
    /// the current shared <c>FamilyEnergyAccount.PurchasedBalance</c>.
    /// <strong>Bucket A (subscription/entitlement) rows are NEVER included.</strong></para>
    ///
    /// <para><strong>Read-only, idempotent.</strong> No mutation occurs here.</para>
    /// </summary>
    /// <param name="familyAccountId">The <c>FamilyEnergyAccount.Id</c> to reconcile against.</param>
    /// <param name="purchasePaymentId">The <c>Payment.Id</c> of the pack purchase being quoted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="RefundableQuoteDto"/> with the reconciled figures, or <c>null</c> if the
    /// payment or family account is not found.</returns>
    Task<RefundableQuoteDto?> ComputeRefundableAsync(
        int familyAccountId,
        int purchasePaymentId,
        CancellationToken ct);

    /// <summary>
    /// Initiates a purchased-energy refund by calling <c>IPaymentProvider.InitiateRefundAsync</c>
    /// (P10-17-BE-3 / BE-6).
    ///
    /// <para><strong>Initiate-only:</strong> this call does NOT modify the ledger or balance.
    /// The actual state change (ledger <c>Refund</c> row + balance decrement) is driven by
    /// the subsequent <c>refund.succeeded</c> webhook handled by
    /// <see cref="ProcessPackRefundAsync"/> (BE-4). Consistent with P10-09 semantics.</para>
    ///
    /// <para><strong>Actor id:</strong> <paramref name="actorUserId"/> is ledgered on the audit
    /// trail so admin-initiated refunds are distinguishable from parent-initiated ones.</para>
    /// </summary>
    /// <param name="purchasePaymentId">The <c>Payment.Id</c> of the pack payment to refund.</param>
    /// <param name="refundableAmount">The monetary amount to refund (from <see cref="ComputeRefundableAsync"/>).</param>
    /// <param name="reason">Enum reason for the refund request (no free-text).</param>
    /// <param name="actorUserId">The user id of the requesting parent or admin (from JWT).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<InitiateRefundResult> InitiateProviderRefundAsync(
        int purchasePaymentId,
        decimal refundableAmount,
        RefundReason reason,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Processes all <c>PastDue</c> / <c>Dunning</c> subscriptions whose <c>NextRetryAt</c>
    /// has passed — called by <see cref="Jobs.DunningRetryJob"/>.
    ///
    /// <para>For each eligible subscription: attempts a retry charge via
    /// <c>IPaymentProvider.CreateCheckoutSessionAsync</c> (re-checkout — FakePaymentProvider
    /// models this as always-succeed / configurable-decline). Idempotent per subscription+cycle.
    /// Fail-soft: a failure on one subscription is caught, logged, and the sweep continues.</para>
    /// </summary>
    /// <param name="nowUtc">Current UTC time (injected for testability via <c>ISystemClock</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A summary of retries attempted, successes, failures.</returns>
    Task<DunningRetryResult> ProcessDunningRetriesAsync(
        DateTime nowUtc,
        CancellationToken ct);
}

/// <summary>Result of a refund operation.</summary>
public sealed record RefundResult
{
    public bool Succeeded { get; init; }
    public bool WasDuplicate { get; init; }
    public int ClawedBackAmount { get; init; }
    public RefundFailureReason? FailureReason { get; init; }

    public static RefundResult Ok(int clawedBack = 0) => new() { Succeeded = true, ClawedBackAmount = clawedBack };
    public static RefundResult Duplicate() => new() { Succeeded = true, WasDuplicate = true };
    public static RefundResult Fail(RefundFailureReason reason) => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Typed failure reasons for <see cref="RefundResult"/>.</summary>
public enum RefundFailureReason
{
    /// <summary>The payment record was not found.</summary>
    PaymentNotFound = 1,
    /// <summary>The payment is not in a refundable state.</summary>
    PaymentNotRefundable = 2,
    /// <summary>The associated credit account was not found (for pack refunds).</summary>
    CreditAccountNotFound = 3,
}

/// <summary>Result of processing a <c>charge.failed</c> webhook event.</summary>
public sealed record ChargeFailedResult
{
    public bool Succeeded { get; init; }
    public bool WasDuplicate { get; init; }
    public int FailedAttemptCount { get; init; }
    public bool DowngradeScheduled { get; init; }
    public DateTime? GraceEndsAt { get; init; }
    public DateTime? NextRetryAt { get; init; }

    public static ChargeFailedResult Ok(
        int attemptCount,
        bool downgradeScheduled,
        DateTime? graceEndsAt,
        DateTime? nextRetryAt)
        => new()
        {
            Succeeded = true,
            FailedAttemptCount = attemptCount,
            DowngradeScheduled = downgradeScheduled,
            GraceEndsAt = graceEndsAt,
            NextRetryAt = nextRetryAt,
        };

    public static ChargeFailedResult Duplicate() => new() { Succeeded = true, WasDuplicate = true };

    public static ChargeFailedResult Fail() => new() { Succeeded = false };
}

/// <summary>Result of an admin-initiated refund request.</summary>
public sealed record InitiateRefundResult
{
    public bool Succeeded { get; init; }
    public InitiateRefundFailureReason? FailureReason { get; init; }

    public static InitiateRefundResult Ok() => new() { Succeeded = true };
    public static InitiateRefundResult Fail(InitiateRefundFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Typed failure reasons for <see cref="InitiateRefundResult"/>.</summary>
public enum InitiateRefundFailureReason
{
    /// <summary>The payment record was not found.</summary>
    PaymentNotFound = 1,
    /// <summary>The payment has not succeeded (only Succeeded payments can be refunded).</summary>
    PaymentNotSucceeded = 2,
    /// <summary>The payment provider call failed.</summary>
    ProviderError = 3,
}

/// <summary>Summary of a <see cref="IRefundService.ProcessDunningRetriesAsync"/> sweep.</summary>
public sealed record DunningRetryResult(int Attempted, int Succeeded, int Failed);

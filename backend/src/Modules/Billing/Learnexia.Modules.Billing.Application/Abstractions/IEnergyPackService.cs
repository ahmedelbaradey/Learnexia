namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for energy-pack purchase operations.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface, not
/// <see cref="IBillingDbContext"/>. All EF, transaction, and ledger logic lives in the Infrastructure
/// implementation. Handlers stay EF-free.</para>
///
/// <para><strong>Idempotency:</strong>
/// <list type="bullet">
///   <item><see cref="StartPackCheckoutAsync"/> is idempotent within the same pack attempt — a duplicate
///         call with the same <c>idempotencyKey</c> returns the existing <see cref="PackCheckoutResult"/>
///         without creating a second Payment row.</item>
///   <item><see cref="CreditPurchasedPackAsync"/> uses the <c>providerEventId</c> (via the
///         <c>WebhookEvent</c> idempotency store) AND a <c>CreditTransaction.IdempotencyKey</c>
///         (DB-unique) to guard against double-crediting on replay.</item>
/// </list>
/// </para>
/// </summary>
public interface IEnergyPackService
{
    /// <summary>
    /// Validates family scope (parent owns child), resolves pack size + price from
    /// <c>IGlobalSettingsProvider</c>, creates a <c>Payment{Kind=Pack,Status=Initiated}</c> row,
    /// calls <c>IPaymentProvider.CreateCheckoutSessionAsync</c>, persists the provider ref, and
    /// returns the redirect URL + internal payment id.
    ///
    /// <para>SECURITY:
    /// <list type="bullet">
    ///   <item>Amount is always resolved server-side — never client-supplied.</item>
    ///   <item>Parent–child ownership is validated via <c>IParentChildQuery.IsParentOfChildAsync</c>
    ///         BEFORE creating any Payment row (IDOR guard).</item>
    ///   <item><c>parentId</c> MUST come from the JWT — the caller (handler) resolves it from
    ///         <c>ICurrentUserService.UserId</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="parentId">Owning parent's user id (from JWT, NOT from the request body).</param>
    /// <param name="childId">Target child's user id (from the request body — must be validated).</param>
    /// <param name="idempotencyKey">Caller-supplied idempotency key for the checkout attempt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="PackCheckoutResult"/> on success; a typed failure on error.</returns>
    Task<PackCheckoutResult> StartPackCheckoutAsync(
        int parentId,
        int childId,
        string idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Credits a completed pack purchase to the target child's <c>PurchasedBalance</c> (never expires).
    ///
    /// <para>Called from the webhook handler after a verified <c>payment.succeeded</c> event
    /// for a <c>Kind=Pack</c> payment. The entire operation (load account, increment balance,
    /// append <c>CreditTransaction</c>, mark Payment Succeeded) runs in a single explicit transaction.</para>
    ///
    /// <para><strong>Idempotency:</strong> guarded by TWO independent keys:
    /// <list type="number">
    ///   <item>The <c>WebhookEvent.ProviderEventId</c> unique constraint (already inserted by the
    ///         webhook handler before calling this method — the outer guard).</item>
    ///   <item>A per-call <c>CreditTransaction.IdempotencyKey = "pack-credit:{paymentId}:{providerEventId}"</c>
    ///         (DB-unique) — the inner guard that prevents double-crediting even if the webhook row
    ///         and credit somehow race.</item>
    /// </list>
    /// A unique-key violation on the credit key is treated as an idempotent success (no new credit).</para>
    ///
    /// <para><strong>Security:</strong> the pack size is re-read from <c>IGlobalSettingsProvider</c>
    /// (not from the provider payload) — the provider amount is reconciled but server-side size is
    /// authoritative for the credit.</para>
    /// </summary>
    /// <param name="paymentId">The internal Payment row id (from the pack Payment record).</param>
    /// <param name="targetChildId">The child who receives the credit (from Payment.TargetChildId).</param>
    /// <param name="providerEventId">The provider's event id (used as part of the credit idempotency key).</param>
    /// <param name="webhookRecord">
    /// The <c>WebhookEvent</c> row that was already inserted by the handler inside the outer transaction.
    /// The service updates its <c>Succeeded</c>/<c>ProcessedAt</c> fields.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="PackCreditResult"/> describing the outcome.</returns>
    Task<PackCreditResult> CreditPurchasedPackAsync(
        int paymentId,
        int targetChildId,
        string providerEventId,
        CancellationToken ct);
}

/// <summary>Result of <see cref="IEnergyPackService.StartPackCheckoutAsync"/>.</summary>
public sealed record PackCheckoutResult
{
    public bool Succeeded { get; init; }
    public int PaymentId { get; init; }
    public string? RedirectUrl { get; init; }
    public string? ProviderPaymentRef { get; init; }

    /// <summary>Failure reason when <see cref="Succeeded"/> is <c>false</c>.</summary>
    public PackCheckoutFailureReason? FailureReason { get; init; }

    public static PackCheckoutResult Ok(int paymentId, string redirectUrl, string providerRef)
        => new() { Succeeded = true, PaymentId = paymentId, RedirectUrl = redirectUrl, ProviderPaymentRef = providerRef };

    public static PackCheckoutResult Fail(PackCheckoutFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Typed failure reasons for <see cref="IEnergyPackService.StartPackCheckoutAsync"/>.</summary>
public enum PackCheckoutFailureReason
{
    /// <summary>The authenticated parent does not own the requested child.</summary>
    ChildNotOwnedByParent = 1,

    /// <summary>The payment provider returned an error when creating the checkout session.</summary>
    ProviderUnavailable = 2,
}

/// <summary>Result of <see cref="IEnergyPackService.CreditPurchasedPackAsync"/>.</summary>
public sealed record PackCreditResult
{
    public bool Succeeded { get; init; }
    public bool WasDuplicate { get; init; }
    public int CreditedAmount { get; init; }

    public static PackCreditResult Ok(int creditedAmount)
        => new() { Succeeded = true, CreditedAmount = creditedAmount };

    public static PackCreditResult Duplicate()
        => new() { Succeeded = true, WasDuplicate = true };

    public static PackCreditResult Fail()
        => new() { Succeeded = false };
}

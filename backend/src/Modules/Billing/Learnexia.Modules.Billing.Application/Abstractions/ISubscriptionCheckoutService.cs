using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for initiating a subscription checkout flow.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF writes, provider calls, and amount resolution live in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>Security:</strong>
/// <list type="bullet">
///   <item>Amount is always resolved server-side from <c>IGlobalSettingsProvider</c> —
///         never client-supplied.</item>
///   <item><c>parentUserId</c> MUST come from JWT (resolved by the handler via
///         <c>ICurrentUserService.UserId</c> before calling this method).</item>
/// </list>
/// </para>
/// </summary>
public interface ISubscriptionCheckoutService
{
    /// <summary>
    /// Starts a subscription checkout session for the given parent.
    ///
    /// <para>Pipeline:
    /// <list type="number">
    ///   <item>Loads the parent's <c>PendingPayment</c> subscription row.</item>
    ///   <item>Resolves the charge amount server-side from <c>BillingPeriod</c> + GlobalSettings.</item>
    ///   <item>Creates a <c>Payment{Kind=Subscription, Status=Initiated}</c> row.</item>
    ///   <item>Calls <c>IPaymentProvider.CreateCheckoutSessionAsync</c>.</item>
    ///   <item>Persists the provider ref on the Payment row and returns the redirect URL.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="parentUserId">Parent's user id (from JWT, NOT from the request body).</param>
    /// <param name="actorUserId">Actor user id for audit stamping (from JWT).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SubscriptionCheckoutResult> StartAsync(
        int parentUserId,
        int actorUserId,
        CancellationToken ct);
}

/// <summary>Result of <see cref="ISubscriptionCheckoutService.StartAsync"/>.</summary>
public sealed record SubscriptionCheckoutResult
{
    public bool Succeeded { get; init; }
    public SubscriptionCheckoutFailureReason? FailureReason { get; init; }
    public CheckoutResultDto? Data { get; init; }

    public static SubscriptionCheckoutResult Ok(CheckoutResultDto data)
        => new() { Succeeded = true, Data = data };

    public static SubscriptionCheckoutResult Fail(SubscriptionCheckoutFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Failure reasons for <see cref="ISubscriptionCheckoutService.StartAsync"/>.</summary>
public enum SubscriptionCheckoutFailureReason
{
    /// <summary>No PendingPayment subscription row exists for this parent.</summary>
    SubscriptionNotFound = 1,

    /// <summary>The payment provider call failed.</summary>
    ProviderUnavailable = 2,
}

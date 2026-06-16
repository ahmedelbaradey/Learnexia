using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for subscription lifecycle operations (cancel, downgrade, upgrade,
/// get current plan).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and subscription-state-machine logic lives in the Infrastructure
/// implementation. Handlers stay EF-free.</para>
///
/// <para><strong>IDOR guard:</strong> <c>parentUserId</c> MUST come from JWT — the handler
/// resolves it via <c>ICurrentUserService.UserId</c> before calling these methods.
/// The service never accepts a parentUserId from the request body.</para>
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Cancels the parent's active subscription.
    ///
    /// <para>State transitions: Active/PendingPayment/Downgrading → Canceled.
    /// After the local DB commit, attempts a best-effort
    /// <c>IPaymentProvider.CancelRecurringAsync</c> call (failure does not roll back the
    /// local Canceled state).</para>
    /// </summary>
    /// <param name="parentUserId">Parent user id (from JWT).</param>
    /// <param name="actorUserId">Actor user id for audit stamping.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SubscriptionOperationResult> CancelAsync(
        int parentUserId,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Transitions the parent's Active Premium subscription to <c>Downgrading</c>
    /// (<c>PendingPlanCode = Free</c> to take effect at cycle end).
    /// Returns a conflict result if the subscription is already on Free.
    /// </summary>
    /// <param name="parentUserId">Parent user id (from JWT).</param>
    /// <param name="actorUserId">Actor user id for audit stamping.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SubscriptionOperationResult> RequestDowngradeAsync(
        int parentUserId,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Transitions the parent's subscription to <c>PendingPayment</c>.
    ///
    /// <para>Business rules:
    /// <list type="bullet">
    ///   <item>If no subscription row exists, one is created.</item>
    ///   <item>If already Premium Active, records the cadence preference as a pending period change.</item>
    ///   <item>If Canceled or Downgrading, creates a new PendingPayment row (re-subscribe).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="parentUserId">Parent user id (from JWT).</param>
    /// <param name="billingPeriod">Requested billing cadence.</param>
    /// <param name="actorUserId">Actor user id for audit stamping.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SubscriptionOperationResult> RequestUpgradeAsync(
        int parentUserId,
        BillingPeriod billingPeriod,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Returns the parent's current plan snapshot (synthesises a Free plan if no row exists).
    /// </summary>
    /// <param name="parentUserId">Parent user id (from JWT).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SubscriptionDto?> GetCurrentPlanAsync(int parentUserId, CancellationToken ct);
}

/// <summary>Result of a subscription write operation.</summary>
public sealed record SubscriptionOperationResult
{
    public bool Succeeded { get; init; }
    public SubscriptionOperationFailureReason? FailureReason { get; init; }
    public SubscriptionDto? Data { get; init; }

    public static SubscriptionOperationResult Ok(SubscriptionDto dto)
        => new() { Succeeded = true, Data = dto };

    public static SubscriptionOperationResult Fail(SubscriptionOperationFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Failure reasons for subscription operations.</summary>
public enum SubscriptionOperationFailureReason
{
    /// <summary>No active subscription exists for this parent.</summary>
    SubscriptionNotFound = 1,

    /// <summary>The subscription is already on the Free plan (downgrade conflict).</summary>
    AlreadyFree = 2,
}

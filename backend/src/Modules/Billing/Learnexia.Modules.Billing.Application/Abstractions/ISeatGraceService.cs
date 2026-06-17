namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Manages the seat grace window that is opened on a payment failure (P10-15-BE-2).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and grace-window logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>Grace model (lead-approved 2026-06-17):</strong>
/// <list type="bullet">
///   <item>Grace is triggered ONLY by a payment failure (P10-09 <c>charge.failed</c> path).</item>
///   <item>The grace clock is <c>Subscription.GraceEndsAt = now + seats.grace_days</c>
///         (7 days by default, config-driven). <c>SeatGraceStartedAt</c> is audit-only.</item>
///   <item>Re-triggering within an OPEN window does NOT stack, extend, or shorten the window
///         (idempotency guard: if <c>GraceEndsAt &gt; now</c> already, skip).</item>
///   <item>Voluntary seat removal does NOT use this service — it uses
///         <see cref="ISeatSchedulingService"/>.</item>
/// </list>
/// </para>
/// </summary>
public interface ISeatGraceService
{
    /// <summary>
    /// Opens a payment-failure seat grace window on the subscription identified by
    /// <paramref name="subscriptionId"/>.
    ///
    /// <para><strong>Idempotent:</strong> if <c>GraceEndsAt &gt; utcNow</c> already (window still
    /// open), the call is a no-op — window is NOT extended or shortened.</para>
    ///
    /// <para>Sets:
    /// <list type="bullet">
    ///   <item><c>GraceEndsAt = utcNow + seats.grace_days</c> (config from <c>IGlobalSettingsProvider</c>).</item>
    ///   <item><c>SeatGraceStartedAt = utcNow</c> (audit only; null if already set in this window).</item>
    ///   <item><c>SeatGraceReason = PaymentFailure</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="subscriptionId">Billing-internal subscription id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StartPaymentFailureGraceAsync(int subscriptionId, CancellationToken ct = default);
}

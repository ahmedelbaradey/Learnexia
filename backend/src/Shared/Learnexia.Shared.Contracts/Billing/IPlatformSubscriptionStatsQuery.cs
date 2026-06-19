namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module platform-aggregate read seam: Billing implements, façade consumers inject.
/// Returns platform-wide subscription statistics.
///
/// <para>Registered in <c>AddBillingInfrastructure()</c> as Scoped.</para>
///
/// <para>P7-10 analytics dashboard (option a — honest v1).
/// Revenue is NOT included: payments are Fake-provider (synthetic) until Paymob is wired.
/// See <see cref="PlatformSubscriptionStats.RevenueNaReason"/> for the explicit N/A marker.</para>
///
/// <para>All results are sentinel-safe: returns zeroed stats when no subscriptions exist — never null, never throws.</para>
/// </summary>
public interface IPlatformSubscriptionStatsQuery
{
    /// <summary>
    /// Returns current platform-wide subscription counts, grouped by tier.
    /// Not windowed — reflects the current active state of the <c>Subscription</c> table.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PlatformSubscriptionStats"/> value — never null.
    /// </returns>
    Task<PlatformSubscriptionStats> GetPlatformAsync(CancellationToken ct = default);
}

/// <summary>
/// Platform-wide subscription snapshot.
/// Produced by <see cref="IPlatformSubscriptionStatsQuery"/>.
/// </summary>
/// <param name="TotalActiveSubscriptions">
/// Count of <c>Subscription</c> rows where <c>Status = Active</c>.
/// </param>
/// <param name="ByTier">
/// Per-<see cref="SubscriptionTierCount"/> breakdown (tier code + count).
/// </param>
/// <param name="RevenueNaReason">
/// Explicit N/A marker for revenue — set to a non-null string when revenue cannot be derived
/// (e.g. "N/A (no live payments — Fake provider active)").
/// Null means revenue data is available (not yet built; reserved for when Paymob integration lands).
/// </param>
public record PlatformSubscriptionStats(
    int TotalActiveSubscriptions,
    IReadOnlyList<SubscriptionTierCount> ByTier,
    string? RevenueNaReason);

/// <summary>
/// Count of active subscriptions for a single plan tier.
/// </summary>
/// <param name="PlanCode">The plan code string (e.g. "Free", "Premium").</param>
/// <param name="Count">Number of active subscriptions at this tier.</param>
public record SubscriptionTierCount(string PlanCode, int Count);

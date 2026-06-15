namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module seam: exposes the active children + their plan tier to the
/// <c>BillingGrantJob</c> without coupling the Billing module to the Parent or Subscription modules.
///
/// <para>Stub implementation (<c>ConfigDefaultSubscriptionContract</c>) returns all children as
/// Free tier until P10-05 (Subscription) lands and replaces the stub with a real implementation
/// backed by the <c>Subscription</c> entity.</para>
/// </summary>
public interface IBillingSubscriptionContract
{
    /// <summary>
    /// Returns the active children with their current plan tier.
    /// Consumers (e.g. <c>BillingGrantJob</c>) use this to resolve per-child grant amounts.
    /// </summary>
    Task<IReadOnlyList<ActiveChildPlanDto>> GetActiveChildrenWithTierAsync(CancellationToken ct = default);
}

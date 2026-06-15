namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Lightweight DTO returned by <see cref="IBillingSubscriptionContract.GetActiveChildrenWithTierAsync"/>.
/// Carries only what the grant job needs — no PII, no cross-module FK beyond the loose int ids.
/// </summary>
/// <param name="ChildId">The child's user id (plain int — no cross-module FK).</param>
/// <param name="PlanTier">The child's current plan tier (drives grant amount).</param>
/// <param name="ChildTimeZoneId">IANA timezone id for the child (e.g. <c>Africa/Cairo</c>).</param>
public sealed record ActiveChildPlanDto(
    int ChildId,
    PlanTier PlanTier,
    string ChildTimeZoneId = "Africa/Cairo");

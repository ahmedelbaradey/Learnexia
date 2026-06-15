using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;

/// <summary>
/// Read model returned by <c>GetCurrentPlanQuery</c>.
/// Carries the parent's active subscription state and the resolved plan benefits
/// from <c>IGlobalSettingsProvider</c>.
/// </summary>
public class SubscriptionDto
{
    public int Id { get; init; }
    public PlanCode PlanCode { get; init; }
    public string PlanName { get; init; } = null!;
    public BillingPeriod BillingPeriod { get; init; }
    public SubscriptionStatus Status { get; init; }
    public DateTime? CurrentCycleStart { get; init; }
    public DateTime? CurrentCycleEnd { get; init; }
    public PlanCode? PendingPlanCode { get; init; }
    public BillingPeriod? PendingBillingPeriod { get; init; }

    // Benefits resolved at query time from GlobalSettings.
    public int MonthlyCredits { get; init; }
    public int DailySoftCap { get; init; }
}

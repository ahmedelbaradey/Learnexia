namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;

/// <summary>
/// Returned by <c>GetPlanComparisonQuery</c>.
/// All prices and benefit values are resolved from <c>GlobalSettings</c> — never hard-coded.
/// </summary>
public class PlanComparisonDto
{
    public PlanOptionDto Free { get; init; } = null!;
    public PlanOptionDto Premium { get; init; } = null!;
}

/// <summary>A single plan option in the comparison.</summary>
public class PlanOptionDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int MonthlyCredits { get; init; }
    public int DailySoftCap { get; init; }
    public decimal MonthlyPriceEgp { get; init; }
    public decimal AnnualPriceEgp { get; init; }

    /// <summary>
    /// How much the parent saves by choosing annual billing vs 12 × monthly.
    /// Derived: <c>12 × MonthlyPriceEgp − AnnualPriceEgp</c>.
    /// </summary>
    public decimal AnnualSavingEgp => 12m * MonthlyPriceEgp - AnnualPriceEgp;
}

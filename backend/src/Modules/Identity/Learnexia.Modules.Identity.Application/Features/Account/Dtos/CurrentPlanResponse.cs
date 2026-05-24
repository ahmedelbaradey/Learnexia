namespace Learnexia.Modules.Identity.Application.Features.Account.Dtos;

/// <summary>
/// Stub DTO for the user's subscription plan (P2-12 PLAN). Hardcoded Free tier until the payments
/// story is implemented.
/// </summary>
/// <remarks>
/// TODO P2-12-PAYMENTS: replace hardcoded values with a real plan lookup once the payments module exists.
/// </remarks>
public sealed class CurrentPlanResponse
{
    public string PlanName { get; init; } = default!;
    public string Status { get; init; } = default!;
}

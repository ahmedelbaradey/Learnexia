namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Current energy balance for a child. Returned by <see cref="ICreditSpendService.GetBalanceAsync"/>.
/// Consumed by W2 AI handlers for the pre-authorize check (no cross-module FK — childId is a plain int).
/// </summary>
/// <param name="GrantedBalance">Monthly-grant pool (expires at <see cref="GrantExpiresAtUtc"/>).</param>
/// <param name="PurchasedBalance">Pack-purchase pool (never expires).</param>
/// <param name="TotalBalance">Derived sum.</param>
/// <param name="GrantExpiresAtUtc">When the current grant expires; null when no active grant.</param>
public sealed record EnergyBalance(
    int GrantedBalance,
    int PurchasedBalance,
    int TotalBalance,
    DateTime? GrantExpiresAtUtc);

namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Current energy balance for a child. Returned by <see cref="ICreditSpendService.GetBalanceAsync"/>.
/// Consumed by W2 AI handlers for the pre-authorize check (no cross-module FK — childId is a plain int).
///
/// <para><strong>P10-03 extension (W2b):</strong> daily-cap fields (<see cref="DailyUsed"/>,
/// <see cref="DailyCap"/>, <see cref="DailyCapReached"/>) are populated so AI handlers can
/// enforce the daily hard-stop without a second Billing round-trip.</para>
/// </summary>
/// <param name="GrantedBalance">Monthly-grant pool (expires at <see cref="GrantExpiresAtUtc"/>).</param>
/// <param name="PurchasedBalance">Pack-purchase pool (never expires).</param>
/// <param name="TotalBalance">Derived sum.</param>
/// <param name="GrantExpiresAtUtc">When the current grant expires; null when no active grant.</param>
/// <param name="DailyUsed">Credits spent today (lazy-reset to 0 when the stored date is stale).</param>
/// <param name="DailyCap">Daily soft-cap threshold resolved from <c>IGlobalSettingsProvider</c>.</param>
/// <param name="DailyCapReached">True when <see cref="DailyUsed"/> &gt;= <see cref="DailyCap"/>.</param>
public sealed record EnergyBalance(
    int GrantedBalance,
    int PurchasedBalance,
    int TotalBalance,
    DateTime? GrantExpiresAtUtc,
    int DailyUsed = 0,
    int DailyCap = 10,
    bool DailyCapReached = false);

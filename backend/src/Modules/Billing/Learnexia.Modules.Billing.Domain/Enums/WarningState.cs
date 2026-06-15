namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Typed warning state returned by <c>EnergyStatusQuery</c>.
///
/// <para>Design:
/// <list type="bullet">
///   <item><see cref="None"/> — no warning; the kid app shows no alert.</item>
///   <item><see cref="ApproachingDailyCap"/> — daily usage is at or above 80 % of the daily cap
///         (soft warning; spend continues unless <c>HardStopEnabled</c> is true and cap is reached).</item>
///   <item><see cref="DailyCapReached"/> — daily usage &ge; daily cap (soft warning by default;
///         hard-stop blocks spend when the <c>HardStopEnabled</c> feature flag is ON).</item>
///   <item><see cref="LowBalance"/> — total balance &le; 10 % of the monthly allotment
///         (independent of the daily counter).</item>
///   <item><see cref="Empty"/> — total balance = 0 (spend blocked; graceful decline served).</item>
/// </list>
/// </para>
///
/// <para>States are not mutually exclusive. The handler computes the highest-severity applicable state.</para>
/// </summary>
public enum WarningState
{
    /// <summary>No warning — balance and daily usage are healthy.</summary>
    None = 0,

    /// <summary>Daily usage is at or above 80 % of the cap — approaching limit (soft warning).</summary>
    ApproachingDailyCap = 1,

    /// <summary>Daily usage has reached the cap. By default this is a soft warning (spend allowed); hard-stop
    /// blocks spend when <c>Billing:HardStopEnabled</c> is <c>true</c>.</summary>
    DailyCapReached = 2,

    /// <summary>Total balance is low (≤ 10 % of monthly allotment) — encourage conserving energy.</summary>
    LowBalance = 3,

    /// <summary>Total balance is exhausted — all spend is gracefully declined until the next grant.</summary>
    Empty = 4,
}

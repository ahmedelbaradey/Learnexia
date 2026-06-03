namespace Learnexia.Modules.Gamification.Application.Features.Events.Boost;

/// <summary>
/// Configuration for the XP-boost calculator. Bound from
/// <c>appsettings.json:Gamification:Events</c> via
/// <c>IOptions&lt;GamificationEventsOptions&gt;</c>.
/// </summary>
public sealed class GamificationEventsOptions
{
    public const string SectionName = "Gamification:Events";

    /// <summary>
    /// Hard ceiling on the multiplier applied to any XP award, regardless of the
    /// number or values of overlapping events. Defence in depth alongside the
    /// per-event DB CHECK constraint (multiplier &lt;= 5.0).
    /// </summary>
    public decimal MaxMultiplierCeiling { get; set; } = 5.0m;
}

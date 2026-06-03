using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Features.Events.Boost;

/// <summary>
/// Computes the effective XP amount for an award by consulting active timed events.
/// Returns the input amount when no event applies. Multiple overlapping events resolve
/// via max(multipliers), capped at <see cref="GamificationEventsOptions.MaxMultiplierCeiling"/>.
/// Reason determines which event scopes apply.
/// </summary>
public interface IXpBoostCalculator
{
    Task<int> GetEffectiveAmountAsync(
        int baseAmount,
        XpReason reason,
        DateTime occurredAtUtc,
        CancellationToken ct = default);
}

using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Features.Events.Boost;

/// <summary>
/// Computes the effective XP amount by consulting active timed events via the cached
/// <see cref="IActiveTimedEventsQuery"/> cross-module seam (P4-11-B1-B).
///
/// Algorithm:
/// <list type="number">
///   <item>Retrieve the active-event list from the (cached) query seam.</item>
///   <item>Filter to events whose <c>Scope == AllXp</c> AND whose window contains <paramref name="occurredAtUtc"/>
///         (defence-in-depth; the cache already filters by <c>IsActive</c> + clock).</item>
///   <item>Take <c>max(Multiplier)</c> across applicable events (overlapping events do NOT compound).</item>
///   <item>Cap the result at <see cref="GamificationEventsOptions.MaxMultiplierCeiling"/>.</item>
///   <item>Return <c>(int)Math.Round(baseAmount × effective, MidpointRounding.AwayFromZero)</c>.</item>
/// </list>
///
/// Fail-soft: any exception from the query seam is swallowed — the calculator returns
/// <paramref name="baseAmount"/> unchanged (1× pass-through). This prevents a transient Redis
/// outage from halting XP awards entirely.
///
/// Logging: no per-call trace log (would flood at lesson/answer scale). Errors log a static
/// warn-level message (no <c>ex.Message</c> per CLAUDE.md security rule).
///
/// Phase 3 MVP: only <see cref="TimedEventScopeDto.AllXp"/> is wired. <c>MissionXp</c> and
/// <c>LeagueXp</c> are declared forward-compat but not filtered here in Phase 3.
/// </summary>
internal sealed class XpBoostCalculator : IXpBoostCalculator
{
    private readonly IActiveTimedEventsQuery _activeEvents;
    private readonly GamificationEventsOptions _options;
    private readonly ILoggerManager _logger;

    public XpBoostCalculator(
        IActiveTimedEventsQuery activeEvents,
        IOptions<GamificationEventsOptions> options,
        ILoggerManager logger)
    {
        _activeEvents = activeEvents;
        _options      = options.Value;
        _logger       = logger;
    }

    public async Task<int> GetEffectiveAmountAsync(
        int baseAmount,
        XpReason reason,
        DateTime occurredAtUtc,
        CancellationToken ct = default)
    {
        if (baseAmount <= 0) return baseAmount;

        IReadOnlyList<ActiveTimedEventSnapshot> events;
        try
        {
            events = await _activeEvents.GetActiveAtAsync(occurredAtUtc, ct);
        }
        catch (Exception)
        {
            // Fail-soft: transient query/cache failure → 1× pass-through.
            // Static warn message only — no ex.Message per F-09 (P4-11 audit Medium #3).
            _logger.LogWarn("P4-11: IActiveTimedEventsQuery failed — XP boost falling back to 1x.");
            return baseAmount;
        }

        if (events.Count == 0) return baseAmount;

        // Phase 3 ships AllXp only. MissionXp/LeagueXp declared but not wired.
        var applicable = events
            .Where(e => e.Scope == TimedEventScopeDto.AllXp)
            .Where(e => e.StartUtc <= occurredAtUtc && e.EndUtc > occurredAtUtc);

        decimal best = 1m;
        foreach (var e in applicable)
        {
            if (e.Multiplier > best) best = e.Multiplier;
        }

        if (best <= 1m) return baseAmount;

        var capped = Math.Min(best, _options.MaxMultiplierCeiling);
        // Round half-up to avoid silent under-award; XpReason-agnostic.
        var boosted = (int)Math.Round(baseAmount * capped, MidpointRounding.AwayFromZero);
        return boosted;
    }
}

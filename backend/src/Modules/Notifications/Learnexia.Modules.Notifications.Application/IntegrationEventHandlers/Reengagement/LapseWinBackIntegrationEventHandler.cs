using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;

/// <summary>
/// Consumes <see cref="LapseWinBackIntegrationEvent"/> (published by <c>LapseWinBackJob</c>)
/// and dispatches a <c>LapseWinBack</c> nudge selected by idle-day tier (P9-08).
///
/// <para><strong>P9-08 comeback escalation ladder:</strong> copy is selected by idle-day tier:</para>
/// <list type="bullet">
///   <item>Tier 1 — <c>LAPSE_WIN_BACK_GENTLE</c> (~day 2, default ≤ threshold_1): warm/gentle.</item>
///   <item>Tier 2 — <c>LAPSE_WIN_BACK_REPAIR</c> (~day 5, threshold_1 &lt; days ≤ threshold_2): streak-repair framing (copy only, no action — economy-dependent, Gap C).</item>
///   <item>Tier 3 — <c>LAPSE_WIN_BACK_FRESH_START</c> (~day 14, days &gt; threshold_2): fresh-start framing.</item>
///   <item>Fallback — <c>LAPSE_WIN_BACK</c>: used when <c>DaysSinceLastActivity</c> is 0 or negative (safety net).</item>
/// </list>
///
/// <para><strong>Per-tier (per-lapse-episode) dedupe:</strong> each tier fires at most once per
/// lapse episode via a tier-keyed Redis SETNX with an episode-length TTL
/// (<see cref="IReengagementDedupeStore.TryAcquireTierAsync"/>). The daily dedupe remains active
/// so the existing one-shot-per-day semantics are also preserved.</para>
///
/// <para>Config thresholds (<c>Notifications:LapseTiers</c>, default <c>2,5,14</c>) are tunable
/// without deploy via <see cref="IGlobalSettingsProvider"/>.</para>
///
/// The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied inside
/// <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// In-app inbox is always written by the dispatcher. Streak-repair action is OUT of scope
/// (economy-dependent, Gap C) — copy frames repair without performing it.
/// EF access lifted to services (Option-C rule).
/// </summary>
public sealed class LapseWinBackIntegrationEventHandler
    : INotificationHandler<LapseWinBackIntegrationEvent>
{
    // Config key for lapse tier thresholds (CSV of day counts, ascending).
    private const string LapseTiersKey     = "Notifications:LapseTiers";
    private const string DefaultLapseTiers = "2,5,14";

    // Tier codes (stable template keys).
    private const string TierCodeGentle     = "LAPSE_WIN_BACK_GENTLE";
    private const string TierCodeRepair     = "LAPSE_WIN_BACK_REPAIR";
    private const string TierCodeFreshStart = "LAPSE_WIN_BACK_FRESH_START";
    private const string TierCodeFallback   = "LAPSE_WIN_BACK";

    // Per-tier episode TTLs (longer than the daily dedupe so the tier doesn't re-fire daily).
    private static readonly TimeSpan GentleTierEpisodeTtl     = TimeSpan.FromDays(7);
    private static readonly TimeSpan RepairTierEpisodeTtl     = TimeSpan.FromDays(14);
    private static readonly TimeSpan FreshStartTierEpisodeTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan FallbackTierEpisodeTtl   = TimeSpan.FromDays(2);

    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IPublisher _publisher;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public LapseWinBackIntegrationEventHandler(
        IChildReengagementPreferenceService preferenceService,
        INotificationInboxService inboxService,
        IReengagementDedupeStore dedupeStore,
        IParentChildQuery parentChildQuery,
        INudgeDispatcher dispatcher,
        IPublisher publisher,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        ILoggerManager logger,
        IUserLookup? userLookup = null)
    {
        _preferenceService = preferenceService;
        _inboxService      = inboxService;
        _dedupeStore       = dedupeStore;
        _parentChildQuery  = parentChildQuery;
        _dispatcher        = dispatcher;
        _publisher         = publisher;
        _settings          = settings;
        _clock             = clock;
        _logger            = logger;
        _userLookup        = userLookup;
    }

    public async Task Handle(LapseWinBackIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo($"P4-09/P9-08: LapseWinBackHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.LapseWinBack;

            // ── P9-08: Tier selection by idle-day count ──────────────────────────────────────────
            var (tierCode, episodeTtl) = SelectTier(ev.DaysSinceLastActivity);

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo($"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            // ── Daily dedupe (one-shot-per-day semantics, as before) ─────────────────────────────
            var dailyAcquired = await ReengagementHandlerHelper.TryAcquireDedupeAsync(_dedupeStore, _logger, _publisher, ev.StudentId, category, tierCode, ev.OccurredOnUtc, _clock.UtcNow, ct);
            if (!dailyAcquired)
            {
                _logger.LogInfo($"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId}");
                return;
            }

            // ── P9-08: Per-tier episode dedupe (each tier fires at most once per lapse episode) ──
            var tierAcquired = await _dedupeStore.TryAcquireTierAsync(ev.StudentId, tierCode, episodeTtl, ct);
            if (!tierAcquired)
            {
                _logger.LogInfo($"analytics.reengagement.tier_dedupe_hit category={category} tier={tierCode} childId={ev.StudentId}");
                // P9-13 BE-3: Emit Deduped suppression for tier dedupe (fail-soft).
                await ReengagementHandlerHelper.PublishDedupeSuppressionAsync(_publisher, _logger, ev.StudentId, category, tierCode, _clock.UtcNow, ct);
                return;
            }

            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, tierCode, prefs, locale,
                ("daysIdle", ev.DaysSinceLastActivity.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo($"analytics.reengagement.sent category={category} childId={ev.StudentId} code={tierCode} daysIdle={ev.DaysSinceLastActivity}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P4-09/P9-08: LapseWinBackIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Selects the tier code and episode TTL based on idle-day count and config thresholds
    /// (<c>Notifications:LapseTiers</c>, default "2,5,14").
    ///
    /// Thresholds are parsed as [threshold_1, threshold_2, threshold_3] (ascending):
    ///   days &lt;= threshold_1 → Gentle (tier 1)
    ///   days &lt;= threshold_2 → Repair (tier 2)
    ///   days &gt; threshold_2  → FreshStart (tier 3)
    ///   Fallback for 0/negative days (safety net).
    /// </summary>
    private (string TierCode, TimeSpan EpisodeTtl) SelectTier(int daysIdle)
    {
        if (daysIdle <= 0)
            return (TierCodeFallback, FallbackTierEpisodeTtl);

        var tiersRaw   = _settings.GetString(LapseTiersKey, DefaultLapseTiers);
        var thresholds = ParseTiers(tiersRaw);

        // Defaults if parse fails: 2, 5, 14
        var t1 = thresholds.Length > 0 ? thresholds[0] : 2;
        var t2 = thresholds.Length > 1 ? thresholds[1] : 5;

        if (daysIdle <= t1)
            return (TierCodeGentle, GentleTierEpisodeTtl);

        if (daysIdle <= t2)
            return (TierCodeRepair, RepairTierEpisodeTtl);

        return (TierCodeFreshStart, FreshStartTierEpisodeTtl);
    }

    private static int[] ParseTiers(string csv)
    {
        try
        {
            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.Parse(s))
                .OrderBy(x => x)
                .ToArray();
        }
        catch
        {
            return [2, 5, 14];
        }
    }
}

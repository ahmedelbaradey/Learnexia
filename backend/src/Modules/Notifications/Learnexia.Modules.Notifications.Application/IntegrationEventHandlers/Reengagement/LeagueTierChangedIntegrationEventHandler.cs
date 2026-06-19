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
/// P9-05: Consumes <see cref="LeagueTierChangedIntegrationEvent"/> and dispatches an
/// <c>Achievement/LEAGUE_TIER_CHANGED</c> nudge.
///
/// <para>Copy selection: promotion (<c>ev.Status == "Promoted"</c>) uses the
/// <c>LEAGUE_TIER_CHANGED_PROMOTED</c> template ("ارتقيت لدوري أعلى!"); any other status
/// (Stayed, Relegated, etc.) uses the neutral <c>LEAGUE_TIER_CHANGED</c> template.</para>
///
/// The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied inside
/// <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// Category = Achievement (v1 per OQ-8 locked decision).
/// EF access lifted to services (Option-C rule).
/// </summary>
public sealed class LeagueTierChangedIntegrationEventHandler
    : INotificationHandler<LeagueTierChangedIntegrationEvent>
{
    private const string StatusPromoted = "Promoted";

    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public LeagueTierChangedIntegrationEventHandler(
        IChildReengagementPreferenceService preferenceService,
        INotificationInboxService inboxService,
        IReengagementDedupeStore dedupeStore,
        IParentChildQuery parentChildQuery,
        INudgeDispatcher dispatcher,
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
        _settings          = settings;
        _clock             = clock;
        _logger            = logger;
        _userLookup        = userLookup;
    }

    public async Task Handle(LeagueTierChangedIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo($"P9-05: LeagueTierChangedHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.Achievement;

            // Promotion vs neutral-move copy split (P9-05 AC: "distinguishes promotion from neutral move").
            var isPromotion = string.Equals(ev.Status, StatusPromoted, StringComparison.OrdinalIgnoreCase);
            var code        = isPromotion ? "LEAGUE_TIER_CHANGED_PROMOTED" : "LEAGUE_TIER_CHANGED";

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo($"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            var acquired = await ReengagementHandlerHelper.TryAcquireDedupeAsync(_dedupeStore, _logger, ev.StudentId, category, ev.OccurredOnUtc, ct);
            if (!acquired)
            {
                _logger.LogInfo($"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId}");
                return;
            }

            // Pass the effective global push budget so the dispatcher's arbiter gate uses the
            // per-child value (or config default if null). The dispatcher owns the arbitration.
            var globalBudget = ReengagementHandlerHelper.GetGlobalBudget(prefs, _settings);

            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("oldTier", ev.OldTier), ("newTier", ev.NewTier))
                with { GlobalDailyPushBudget = globalBudget };

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo($"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code} status={ev.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P9-05: LeagueTierChangedIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

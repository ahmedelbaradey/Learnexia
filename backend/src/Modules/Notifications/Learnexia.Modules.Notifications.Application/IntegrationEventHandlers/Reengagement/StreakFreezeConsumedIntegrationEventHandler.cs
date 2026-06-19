using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;

/// <summary>
/// P9-05: Consumes <see cref="StreakFreezeConsumedIntegrationEvent"/> and dispatches an
/// <c>Achievement/STREAK_FREEZE_CONSUMED</c> nudge ("سلسلتك محفوظة! ❄️ ارجع غداً تكمّل").
///
/// <para>Category = Achievement (v1 per OQ-8 locked decision). The freeze is a streak-saving
/// event that belongs to the Achievement parent-toggle in this release; finer per-type toggles
/// (StreakAtRisk vs Achievement) are deferred to P9-04 when the FE parent-controls UI ships.</para>
///
/// The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied inside
/// <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// EF access lifted to services (Option-C rule).
/// </summary>
public sealed class StreakFreezeConsumedIntegrationEventHandler
    : INotificationHandler<StreakFreezeConsumedIntegrationEvent>
{
    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public StreakFreezeConsumedIntegrationEventHandler(
        IChildReengagementPreferenceService preferenceService,
        INotificationInboxService inboxService,
        IReengagementDedupeStore dedupeStore,
        IParentChildQuery parentChildQuery,
        INudgeDispatcher dispatcher,
        ISystemClock clock,
        ILoggerManager logger,
        IUserLookup? userLookup = null)
    {
        _preferenceService = preferenceService;
        _inboxService      = inboxService;
        _dedupeStore       = dedupeStore;
        _parentChildQuery  = parentChildQuery;
        _dispatcher        = dispatcher;
        _clock             = clock;
        _logger            = logger;
        _userLookup        = userLookup;
    }

    public async Task Handle(StreakFreezeConsumedIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo($"P9-05: StreakFreezeConsumedHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.Achievement;
            const string code = "STREAK_FREEZE_CONSUMED";

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

            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("streakLength", ev.CurrentStreak.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo($"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P9-05: StreakFreezeConsumedIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

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
/// P9-06: Consumes <see cref="StreakMilestoneReachedIntegrationEvent"/> and dispatches an
/// <c>Achievement/STREAK_MILESTONE</c> nudge ("🔥 {streakLength} أيام متواصلة! إنت بطل").
///
/// <para>Category = <see cref="NotificationCategory.Achievement"/> (rides the existing enum
/// value — no new member added). Per the OQ-R3 locked decision, <c>Achievement</c> is
/// <strong>inbox-only in v1</strong>: it is not in the parent-managed push set until P9-04 FE
/// per-type toggles ship. The handler dispatches normally; the dispatcher's gate keeps it
/// inbox-only.</para>
///
/// <para>The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied
/// inside <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// Redis dedupe retains the existing (child, category, day) guard against duplicate delivery.</para>
///
/// <para>Producer-side dedupe by construction (brief §OQ-R4): <c>StreakAdvancedDomainEvent</c>
/// increments by 1 per day-transition → each milestone is crossed exactly once per streak
/// episode. The Redis dedupe here is a duplicate-delivery guard, not the primary dedup.</para>
///
/// EF access lifted to services (Option-C rule). Fail-soft per ADR 0002.
/// Auto-registered via <c>Notifications.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class StreakMilestoneReachedIntegrationEventHandler
    : INotificationHandler<StreakMilestoneReachedIntegrationEvent>
{
    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public StreakMilestoneReachedIntegrationEventHandler(
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

    public async Task Handle(StreakMilestoneReachedIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo(
                    $"P9-06: StreakMilestoneReachedHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.Achievement;
            const string code = "STREAK_MILESTONE";

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            var acquired = await ReengagementHandlerHelper.TryAcquireDedupeAsync(
                _dedupeStore, _logger, ev.StudentId, category, ev.OccurredOnUtc, ct);
            if (!acquired)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId}");
                return;
            }

            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("streakLength", ev.Milestone.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo(
                $"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code} milestone={ev.Milestone}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-06: StreakMilestoneReachedIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

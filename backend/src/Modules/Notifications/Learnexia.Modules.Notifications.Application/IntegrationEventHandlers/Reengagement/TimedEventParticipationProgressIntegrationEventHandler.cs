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
/// P9-12 BE-2: Consumes <see cref="TimedEventParticipationProgressIntegrationEvent"/> (raised when
/// a student's participation first crosses the halfway-progress threshold) and dispatches a
/// <c>TimedEvent/TIMED_EVENT_PROGRESS</c> halfway nudge to that student.
///
/// <para><strong>Category:</strong> <see cref="NotificationCategory.TimedEvent"/> (value 9) —
/// inbox-only in v1: not in <c>ReengagementCategories</c> push set.</para>
///
/// <para><strong>Dedupe:</strong> <c>TIMED_EVENT_PROGRESS:{TimedEventId}</c>, 72h TTL —
/// at most one halfway nudge per (child, timed-event) even on re-delivery of the event.</para>
///
/// <para>Placeholders: <c>{progress}</c> = <c>ev.Progress</c>, <c>{target}</c> = <c>ev.Target</c>.</para>
///
/// EF access lifted to services (Option-C rule). Fail-soft per ADR 0002.
/// Auto-registered via <c>Notifications.Application.AssemblyReference</c> (host MediatR scan).
/// Mirrors <see cref="WeeklyMissionReminderIntegrationEventHandler"/> field-for-field (rule 8 — no new pattern).
/// </summary>
public sealed class TimedEventParticipationProgressIntegrationEventHandler
    : INotificationHandler<TimedEventParticipationProgressIntegrationEvent>
{
    // Dedupe key: nudge-tier:{studentId}:TIMED_EVENT_PROGRESS:{TimedEventId}
    private const string ProgressDedupePrefix = "TIMED_EVENT_PROGRESS:";
    private static readonly TimeSpan DedupeTtl = TimeSpan.FromHours(72);

    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService           _inboxService;
    private readonly IReengagementDedupeStore            _dedupeStore;
    private readonly IParentChildQuery                   _parentChildQuery;
    private readonly INudgeDispatcher                    _dispatcher;
    private readonly IUserLookup?                        _userLookup;
    private readonly ISystemClock                        _clock;
    private readonly ILoggerManager                      _logger;

    public TimedEventParticipationProgressIntegrationEventHandler(
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

    public async Task Handle(TimedEventParticipationProgressIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo(
                    $"P9-12: TimedEventProgressHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.TimedEvent;
            const string code = "TIMED_EVENT_PROGRESS";

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            var tierKey  = $"{ProgressDedupePrefix}{ev.TimedEventId}";
            var acquired = await _dedupeStore.TryAcquireTierAsync(ev.StudentId, tierKey, DedupeTtl, ct);
            if (!acquired)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId} timedEventId={ev.TimedEventId}");
                return;
            }

            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("progress", ev.Progress.ToString()),
                ("target",   ev.Target.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo(
                $"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code} " +
                $"timedEventId={ev.TimedEventId} progress={ev.Progress} target={ev.Target}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-12: TimedEventParticipationProgressIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

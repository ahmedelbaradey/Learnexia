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
/// P9-12 BE-3: Consumes <see cref="TimedEventParticipationEndingSoonIntegrationEvent"/> (raised by
/// <c>StreakAtRiskJob</c> Pass 4 for students whose in-progress participation has
/// <c>EventEndUtc</c> within the configured lookahead window) and dispatches a
/// <c>TimedEvent/TIMED_EVENT_ENDING</c> ending-soon nudge to that student.
///
/// <para>"Close-but-incomplete" filtering is owned by the P4-12 producer (<c>StreakAtRiskJob</c>
/// Pass 4 only emits for <c>State == InProgress</c>); the handler trusts the event and does not
/// re-check participation state (no cross-module Gamification read).</para>
///
/// <para><strong>Category:</strong> <see cref="NotificationCategory.TimedEvent"/> (value 9) —
/// inbox-only in v1: not in <c>ReengagementCategories</c> push set.</para>
///
/// <para><strong>Dedupe:</strong> <c>TIMED_EVENT_ENDING:{TimedEventId}</c>, 72h TTL —
/// at most one ending-soon nudge per (child, timed-event) even if the daily job sweep runs
/// multiple times within the lookahead window.</para>
///
/// <para>Placeholders: <c>{remaining}</c> = <c>Target − Progress</c> (work left).</para>
///
/// EF access lifted to services (Option-C rule). Fail-soft per ADR 0002.
/// Auto-registered via <c>Notifications.Application.AssemblyReference</c> (host MediatR scan).
/// Mirrors <see cref="WeeklyMissionReminderIntegrationEventHandler"/> field-for-field (rule 8 — no new pattern).
/// </summary>
public sealed class TimedEventParticipationEndingSoonIntegrationEventHandler
    : INotificationHandler<TimedEventParticipationEndingSoonIntegrationEvent>
{
    // Dedupe key: nudge-tier:{studentId}:TIMED_EVENT_ENDING:{TimedEventId}
    private const string EndingDedupePrefix = "TIMED_EVENT_ENDING:";
    private static readonly TimeSpan DedupeTtl = TimeSpan.FromHours(72);

    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService           _inboxService;
    private readonly IReengagementDedupeStore            _dedupeStore;
    private readonly IParentChildQuery                   _parentChildQuery;
    private readonly INudgeDispatcher                    _dispatcher;
    private readonly IUserLookup?                        _userLookup;
    private readonly ISystemClock                        _clock;
    private readonly ILoggerManager                      _logger;

    public TimedEventParticipationEndingSoonIntegrationEventHandler(
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

    public async Task Handle(TimedEventParticipationEndingSoonIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo(
                    $"P9-12: TimedEventEndingSoonHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.TimedEvent;
            const string code = "TIMED_EVENT_ENDING";

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            var tierKey  = $"{EndingDedupePrefix}{ev.TimedEventId}";
            var acquired = await _dedupeStore.TryAcquireTierAsync(ev.StudentId, tierKey, DedupeTtl, ct);
            if (!acquired)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId} timedEventId={ev.TimedEventId}");
                return;
            }

            var remaining = (ev.Target - ev.Progress).ToString();
            var locale    = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message   = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("remaining", remaining),
                ("progress",  ev.Progress.ToString()),
                ("target",    ev.Target.ToString()),
                ("minutes",   ev.MinutesRemaining.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo(
                $"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code} " +
                $"timedEventId={ev.TimedEventId} progress={ev.Progress} target={ev.Target} " +
                $"remaining={remaining} minutesRemaining={ev.MinutesRemaining}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-12: TimedEventParticipationEndingSoonIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

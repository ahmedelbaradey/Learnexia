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
/// P9-12 BE-1: Consumes <see cref="TimedEventStartedIntegrationEvent"/> (published by the
/// <c>TimedEventStartedRepublisher</c> when a timed event transitions to active) and dispatches a
/// <c>TimedEvent/TIMED_EVENT_LIVE</c> nudge to the scope-eligible student cohort (join fan-out, 1→many).
///
/// <para><strong>Fan-out pattern:</strong> the cohort is resolved on demand via
/// <see cref="IEligibleStudentsForTimedEventQuery.GetEligibleAsync"/> (bounded by <c>Take(500)</c>
/// at the query level — no upfront materialisation). For each eligible student the standard per-student
/// pipeline runs: parent lookup → prefs → evaluator → dedupe → BuildMessage → dispatch.
/// Each student's pipeline runs inside its own try/catch so one failure never aborts the cohort
/// (fail-soft per-student, ADR 0002). The outer handler body is also wrapped in a try/catch
/// (fail-soft whole-handler, ADR 0002).</para>
///
/// <para><strong>Category:</strong> <see cref="NotificationCategory.TimedEvent"/> (value 9) —
/// inbox-only in v1: not in <c>ReengagementCategories</c> push set; <c>prefs.Push</c> defaults
/// false for unmanaged categories, so push falls out at the dispatcher. Listed in P9-04 parent
/// catalog so parents can toggle it once P9-04 FE per-type toggles ship.</para>
///
/// <para><strong>Dedupe:</strong> <c>TIMED_EVENT_LIVE:{TimedEventId}</c> with a TTL derived from
/// the event window length (<c>EndUtc − StartUtc</c>), capped at 72 h, so a re-published started
/// event does not re-recruit students already notified.</para>
///
/// EF access lifted to services (Option-C rule). Fail-soft per ADR 0002.
/// Auto-registered via <c>Notifications.Application.AssemblyReference</c> (host MediatR scan).
/// Mirrors <see cref="WeeklyMissionReminderIntegrationEventHandler"/> field-for-field (rule 8 — no new pattern).
/// </summary>
public sealed class TimedEventStartedIntegrationEventHandler
    : INotificationHandler<TimedEventStartedIntegrationEvent>
{
    // Dedupe prefix: per-(student, timed-event) join lock. Key: nudge-tier:{studentId}:TIMED_EVENT_LIVE:{TimedEventId}
    // TTL: event-window length (EndUtc − StartUtc), capped at 72h.
    private const string LiveDedupePrefix = "TIMED_EVENT_LIVE:";
    private static readonly TimeSpan MaxDedupeTtl = TimeSpan.FromHours(72);

    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService           _inboxService;
    private readonly IReengagementDedupeStore            _dedupeStore;
    private readonly IParentChildQuery                   _parentChildQuery;
    private readonly INudgeDispatcher                    _dispatcher;
    private readonly IEligibleStudentsForTimedEventQuery _eligibleStudentsQuery;
    private readonly IUserLookup?                        _userLookup;
    private readonly ISystemClock                        _clock;
    private readonly ILoggerManager                      _logger;

    public TimedEventStartedIntegrationEventHandler(
        IChildReengagementPreferenceService preferenceService,
        INotificationInboxService inboxService,
        IReengagementDedupeStore dedupeStore,
        IParentChildQuery parentChildQuery,
        INudgeDispatcher dispatcher,
        IEligibleStudentsForTimedEventQuery eligibleStudentsQuery,
        ISystemClock clock,
        ILoggerManager logger,
        IUserLookup? userLookup = null)
    {
        _preferenceService    = preferenceService;
        _inboxService         = inboxService;
        _dedupeStore          = dedupeStore;
        _parentChildQuery     = parentChildQuery;
        _dispatcher           = dispatcher;
        _eligibleStudentsQuery = eligibleStudentsQuery;
        _clock                = clock;
        _logger               = logger;
        _userLookup           = userLookup;
    }

    public async Task Handle(TimedEventStartedIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var category = NotificationCategory.TimedEvent;
            const string code = "TIMED_EVENT_LIVE";

            // Dedupe TTL = event window duration, capped at 72h, so re-published events don't re-recruit.
            var windowDuration = ev.EndUtc - ev.StartUtc;
            var dedupeTtl = windowDuration > MaxDedupeTtl ? MaxDedupeTtl : windowDuration;
            if (dedupeTtl <= TimeSpan.Zero) dedupeTtl = MaxDedupeTtl;

            var tierKey     = $"{LiveDedupePrefix}{ev.TimedEventId}";
            var eligibleIds = await _eligibleStudentsQuery.GetEligibleAsync(ev.TimedEventId, ev.OccurredOnUtc, ct);

            _logger.LogInfo(
                $"P9-12: TimedEventStartedHandler — timedEventId={ev.TimedEventId} code={ev.Code} " +
                $"eligibleCount={eligibleIds.Count}");

            foreach (var studentId in eligibleIds)
            {
                try
                {
                    var parentId = await _parentChildQuery.FindParentForChildAsync(studentId, ct);
                    if (parentId is null)
                    {
                        _logger.LogInfo(
                            $"P9-12: TimedEventStartedHandler — no parent for studentId={studentId}; skip.");
                        continue;
                    }

                    var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, studentId, category, ct);
                    var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, studentId, category, _clock.UtcNow, ct);
                    var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

                    if (!eval.Eligible)
                    {
                        _logger.LogInfo(
                            $"analytics.reengagement.not_eligible category={category} childId={studentId} reason={eval.Reason}");
                        continue;
                    }

                    var acquired = await _dedupeStore.TryAcquireTierAsync(studentId, tierKey, dedupeTtl, ct);
                    if (!acquired)
                    {
                        _logger.LogInfo(
                            $"analytics.reengagement.dedupe_hit category={category} childId={studentId} timedEventId={ev.TimedEventId}");
                        continue;
                    }

                    var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, studentId, ct);
                    var message = ReengagementHandlerHelper.BuildMessage(
                        studentId, parentId.Value, category, code, prefs, locale);

                    await _dispatcher.DispatchAsync(message, ct);
                    _logger.LogInfo(
                        $"analytics.reengagement.sent category={category} childId={studentId} code={code} timedEventId={ev.TimedEventId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"P9-12: TimedEventStartedHandler threw for studentId={studentId} timedEventId={ev.TimedEventId}.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-12: TimedEventStartedIntegrationEventHandler threw for timedEventId={ev.TimedEventId}.");
        }
    }
}

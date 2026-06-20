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
/// P9-06: Consumes <see cref="WeeklyMissionReminderIntegrationEvent"/> (published by
/// <c>StreakAtRiskJob</c> Pass 3, daily cron) and dispatches a
/// <c>WeeklyChallenge/WEEKLY_CHALLENGE_REMINDER</c> nudge
/// ("⏳ تحدي الأسبوع بيخلص — لسه ناقصك {remaining}! اخلصه قبل ما يفوت 💪").
///
/// <para><strong>Category:</strong> <see cref="NotificationCategory.WeeklyChallenge"/> (value 8) —
/// distinct from <c>DailyMissionReminder</c> so the parent can toggle weekly-challenge reminders
/// independently once P9-04 FE per-type toggles ship. <strong>Inbox-only in v1</strong>: not in
/// the parent-managed push set (<c>ReengagementCategories</c>); <c>prefs.Push</c> defaults false
/// for unmanaged categories, so push falls out automatically. The handler dispatches normally;
/// the dispatcher gate enforces the posture.</para>
///
/// <para><strong>Option-A period-scoped dedupe (P9-06):</strong> uses
/// <see cref="IReengagementDedupeStore.TryAcquireTierAsync"/> keyed on
/// <c>WEEKLY_CHALLENGE:{ev.PeriodKey}</c> with a TTL of (lookahead hours + margin ≈ 72 h) so at
/// most ONE weekly reminder fires per student per weekly period regardless of how many daily job
/// runs fall within the 48-hour lookahead window. This is intentionally NOT the standard
/// day-keyed <c>TryAcquireAsync</c>; see the brief OQ-1 decision (Option A).</para>
///
/// <para>Placeholders: <c>{remaining}</c> = <c>Target - Progress</c> (work left),
/// <c>{progress}</c> = current progress, <c>{target}</c> = mission target.</para>
///
/// <para>The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied inside
/// <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// Arbiter priority: <c>WeeklyChallenge</c> after <c>DailyMission</c>, before <c>LapseWinBack</c>.
/// Cooldown: <c>WEEKLY_CHALLENGE_REMINDER</c> = 168 h (≤1/week, matching <c>WEEKLY_RECAP</c>).</para>
///
/// EF access lifted to services (Option-C rule). Fail-soft per ADR 0002.
/// Auto-registered via <c>Notifications.Application.AssemblyReference</c> (host MediatR scan).
/// Mirrors <see cref="DailyMissionReminderIntegrationEventHandler"/> field-for-field (rule 8 — no new pattern).
/// </summary>
public sealed class WeeklyMissionReminderIntegrationEventHandler
    : INotificationHandler<WeeklyMissionReminderIntegrationEvent>
{
    // Option-A: period-scoped dedupe key prefix (no day component — period TTL governs).
    // Key: nudge-tier:{studentId}:WEEKLY_CHALLENGE:{periodKey}
    // TTL: lookahead window + margin → 72 h (longer than the 48h window ensures both possible
    // daily job runs within the same period see the lock even if the first run fires just before
    // the 48h window opens for the second).
    private const string PeriodDedupePrefix   = "WEEKLY_CHALLENGE:";
    private static readonly TimeSpan PeriodDedupeTtl = TimeSpan.FromHours(72);

    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public WeeklyMissionReminderIntegrationEventHandler(
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

    public async Task Handle(WeeklyMissionReminderIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo(
                    $"P9-06: WeeklyMissionReminderHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.WeeklyChallenge;
            const string code = "WEEKLY_CHALLENGE_REMINDER";

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_preferenceService, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_inboxService, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            // ── Option-A: period-scoped dedupe — one reminder per student per weekly period ───────
            // Key has no day component; TTL (72h) spans the entire 48h lookahead window + margin,
            // so the second daily job run within the same period always sees the lock.
            var tierKey  = $"{PeriodDedupePrefix}{ev.PeriodKey}";
            var acquired = await _dedupeStore.TryAcquireTierAsync(ev.StudentId, tierKey, PeriodDedupeTtl, ct);
            if (!acquired)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId} periodKey={ev.PeriodKey}");
                return;
            }

            var remaining = (ev.Target - ev.Progress).ToString();
            var locale    = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message   = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("remaining", remaining),
                ("progress",  ev.Progress.ToString()),
                ("target",    ev.Target.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo(
                $"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code} " +
                $"periodKey={ev.PeriodKey} progress={ev.Progress} target={ev.Target} remaining={remaining}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-06: WeeklyMissionReminderIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

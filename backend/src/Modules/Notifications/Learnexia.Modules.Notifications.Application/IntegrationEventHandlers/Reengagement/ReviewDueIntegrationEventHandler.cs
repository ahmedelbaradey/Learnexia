using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;

/// <summary>
/// P9-09: Consumes <see cref="ReviewDueIntegrationEvent"/> and dispatches a
/// <c>ReviewReminder/REVIEW_DUE</c> nudge ("وقت مراجعة سريعة لمهارة {skill} ({minutes} دقائق بس)").
///
/// <para>Category = <see cref="NotificationCategory.ReviewReminder"/> (new member, value 7 —
/// distinct from Achievement so the parent can toggle review reminders independently once P9-04
/// FE per-type toggles ship). Per the OQ-R3 locked decision, <c>ReviewReminder</c> is
/// <strong>inbox-only in v1</strong>: it is not in the parent-managed push set
/// (<c>ReengagementCategories</c>) until P9-04 FE ships. The handler dispatches normally;
/// the dispatcher's gate keeps it inbox-only because <c>prefs.Push</c> defaults to <c>false</c>.</para>
///
/// <para>The producer guarantees ≥1 entry in <see cref="ReviewDueIntegrationEvent.TopSkills"/>
/// (one digest per student per sweep, most-urgent-first). The handler guards defensively:
/// an empty <c>TopSkills</c> list is logged + skipped (never indexes <c>[0]</c> unguarded).</para>
///
/// <para>The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied
/// inside <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// Redis dedupe retains the existing (child, category, day) guard against duplicate
/// event delivery.</para>
///
/// EF access lifted to services (Option-C rule). Fail-soft per ADR 0002.
/// Auto-registered via <c>Notifications.Application.AssemblyReference</c> (host MediatR scan).
/// </summary>
public sealed class ReviewDueIntegrationEventHandler
    : INotificationHandler<ReviewDueIntegrationEvent>
{
    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public ReviewDueIntegrationEventHandler(
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

    public async Task Handle(ReviewDueIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            // Defensive guard: producer guarantees ≥1 entry, but never index [0] unguarded.
            if (ev.TopSkills.Count == 0)
            {
                _logger.LogInfo(
                    $"P9-09: ReviewDueIntegrationEventHandler — TopSkills empty for studentId={ev.StudentId}; skip.");
                return;
            }

            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo(
                    $"P9-09: ReviewDueIntegrationEventHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.ReviewReminder;
            const string code = "REVIEW_DUE";

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

            var top     = ev.TopSkills[0];
            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(
                ev.StudentId, parentId.Value, category, code, prefs, locale,
                ("skill",    top.SkillName),
                ("minutes",  top.EstimatedTimeMinutes.ToString()),
                ("dueCount", ev.DueCount.ToString()));

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo(
                $"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code} " +
                $"dueCount={ev.DueCount} skill={top.SkillName} minutes={top.EstimatedTimeMinutes}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-09: ReviewDueIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

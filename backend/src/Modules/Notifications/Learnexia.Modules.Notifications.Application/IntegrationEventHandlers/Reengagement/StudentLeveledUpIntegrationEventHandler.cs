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
/// P9-05: Consumes <see cref="StudentLeveledUpIntegrationEvent"/> and dispatches an
/// <c>Achievement/LEVELED_UP</c> nudge ("ارتقيت لمستوى جديد!") using the existing template.
/// The P9-07 <see cref="INudgeArbiter"/> gate (global budget + cooldown) is applied inside
/// <see cref="INudgeDispatcher"/> — the single choke point for all handler paths.
/// In-app inbox is always written by the dispatcher.
/// Category = Achievement (v1 per OQ-8 locked decision).
/// EF access lifted to services (Option-C rule).
/// </summary>
public sealed class StudentLeveledUpIntegrationEventHandler
    : INotificationHandler<StudentLeveledUpIntegrationEvent>
{
    private readonly IChildReengagementPreferenceService _preferenceService;
    private readonly INotificationInboxService _inboxService;
    private readonly IReengagementDedupeStore _dedupeStore;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public StudentLeveledUpIntegrationEventHandler(
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

    public async Task Handle(StudentLeveledUpIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo($"P9-05: StudentLeveledUpHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.Achievement;
            const string code = "LEVELED_UP";

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
                ev.StudentId, parentId.Value, category, code, prefs, locale);

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo($"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P9-05: StudentLeveledUpIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;

/// <summary>
/// Consumes <see cref="DailyMissionReminderIntegrationEvent"/> (published by <c>StreakAtRiskJob</c>
/// second pass) and dispatches a <c>DailyMissionReminder</c> nudge
/// ("Your daily mission ends soon — finish it for +30 XP") (P4-09 B4-5 / AC1).
/// </summary>
public sealed class DailyMissionReminderIntegrationEventHandler
    : INotificationHandler<DailyMissionReminderIntegrationEvent>
{
    private readonly INotificationsDbContext _db;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redisMultiplexer;
    private readonly INudgeDispatcher _dispatcher;
    private readonly IUserLookup? _userLookup;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public DailyMissionReminderIntegrationEventHandler(
        INotificationsDbContext db,
        IParentChildQuery parentChildQuery,
        IDistributedCache cache,
        INudgeDispatcher dispatcher,
        ISystemClock clock,
        ILoggerManager logger,
        IConnectionMultiplexer? redisMultiplexer = null,
        IUserLookup? userLookup = null)
    {
        _db               = db;
        _parentChildQuery = parentChildQuery;
        _cache            = cache;
        _redisMultiplexer = redisMultiplexer;
        _dispatcher       = dispatcher;
        _clock            = clock;
        _logger           = logger;
        _userLookup       = userLookup;
    }

    public async Task Handle(DailyMissionReminderIntegrationEvent ev, CancellationToken ct)
    {
        try
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(ev.StudentId, ct);
            if (parentId is null)
            {
                _logger.LogInfo($"P4-09: DailyMissionReminderHandler — no parent for studentId={ev.StudentId}; skip.");
                return;
            }

            var category = NotificationCategory.DailyMissionReminder;
            const string code = "DAILY_MISSION_REMINDER";

            var prefs      = await ReengagementHandlerHelper.GetOrDefaultPrefsAsync(_db, parentId.Value, ev.StudentId, category, ct);
            var sentsToday = await ReengagementHandlerHelper.CountSentTodayAsync(_db, ev.StudentId, category, _clock.UtcNow, ct);
            var eval       = ReengagementEvaluator.Evaluate(prefs, _clock.UtcNow, sentsToday);

            if (!eval.Eligible)
            {
                _logger.LogInfo($"analytics.reengagement.not_eligible category={category} childId={ev.StudentId} reason={eval.Reason}");
                return;
            }

            var acquired = await ReengagementHandlerHelper.TryAcquireDedupeAsync(_cache, _redisMultiplexer, _logger, ev.StudentId, category, ev.OccurredOnUtc, ct);
            if (!acquired)
            {
                _logger.LogInfo($"analytics.reengagement.dedupe_hit category={category} childId={ev.StudentId}");
                return;
            }

            var locale  = await ReengagementHandlerHelper.GetLocaleAsync(_userLookup, ev.StudentId, ct);
            var message = ReengagementHandlerHelper.BuildMessage(ev.StudentId, parentId.Value, category, code, prefs, locale);

            await _dispatcher.DispatchAsync(message, ct);
            _logger.LogInfo($"analytics.reengagement.sent category={category} childId={ev.StudentId} code={code}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P4-09: DailyMissionReminderIntegrationEventHandler threw for studentId={ev.StudentId}.");
        }
    }
}

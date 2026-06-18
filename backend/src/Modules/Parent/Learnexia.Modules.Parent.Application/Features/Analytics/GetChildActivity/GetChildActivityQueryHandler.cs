using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildActivity;

/// <summary>
/// E8 — GET /Parent/Children/{id}/Activity
///
/// Read-time composed activity feed from multiple seams (plan decision: read-time compose,
/// no new table — planner note item 4). Per-seam try/catch → graceful empty on seam failure.
///
/// Sources for the rolling-7-day window:
///   - XP earned from <see cref="IStudentXpTimeSeriesQuery.GetDailySeriesAsync"/> (xp_earned events)
///   - Badges from <see cref="IStudentBadgesQuery.GetByStudentIdAsync"/> (badge_earned events)
///   - Energy spend from <see cref="IChildEnergyUsageQuery.GetUsageByKindAsync"/> (energy_spent rollup)
///   - Lessons from <see cref="IStudentLearningStatsQuery.GetStatsAsync"/> (lesson_completed event count)
///
/// Note: the seams return aggregated/summary data, not per-event timestamped rows.
/// For MVP the feed is a daily-granularity summary (not a per-event stream) unless a
/// finer-grained per-event seam is added in a later phase.
/// </summary>
public class GetChildActivityQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildActivityQuery, BaseResponse<ActivityFeedDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentXpTimeSeriesQuery _xpTimeSeries;
    private readonly IStudentBadgesQuery _badgesQuery;
    private readonly IChildEnergyUsageQuery _energyUsage;
    private readonly IStudentLearningStatsQuery _learningStats;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildActivityQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentXpTimeSeriesQuery xpTimeSeries,
        IStudentBadgesQuery badgesQuery,
        IChildEnergyUsageQuery energyUsage,
        IStudentLearningStatsQuery learningStats,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _xpTimeSeries = xpTimeSeries;
        _badgesQuery = badgesQuery;
        _energyUsage = energyUsage;
        _learningStats = learningStats;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<ActivityFeedDto>> Handle(
        GetChildActivityQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<ActivityFeedDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<ActivityFeedDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<ActivityFeedDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            var now      = DateTime.UtcNow;
            var weekAgo  = now.AddDays(-7);
            var fromDate = DateOnly.FromDateTime(weekAgo);
            var toDate   = DateOnly.FromDateTime(now);

            var events = new List<ActivityEventDto>();

            // XP events: one synthetic event per day with non-zero XP.
            try
            {
                var series = await _xpTimeSeries.GetDailySeriesAsync(request.ChildId, fromDate, toDate, cancellationToken);
                foreach (var entry in series.Where(e => e.Xp > 0))
                {
                    events.Add(new ActivityEventDto
                    {
                        Kind          = ActivityKinds.XpEarned,
                        Category      = ActivityCategories.Xp,
                        OccurredAtUtc = entry.Day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        Amount        = entry.Xp,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery.GetDailySeriesAsync failed for childId={request.ChildId} (activity)");
            }

            // Badge events: use the recent-3 snapshot (timestamped).
            try
            {
                var badgeSnap = await _badgesQuery.GetByStudentIdAsync(request.ChildId, cancellationToken);
                foreach (var badge in badgeSnap.Recent.Where(b => b.AwardedAtUtc >= weekAgo))
                {
                    events.Add(new ActivityEventDto
                    {
                        Kind          = ActivityKinds.BadgeEarned,
                        Category      = ActivityCategories.Badge,
                        OccurredAtUtc = badge.AwardedAtUtc,
                        Amount        = 0,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentBadgesQuery.GetByStudentIdAsync failed for childId={request.ChildId} (activity)");
            }

            // Energy spend: one summary event per helper kind with spend > 0.
            try
            {
                var usageByKind = await _energyUsage.GetUsageByKindAsync(request.ChildId, weekAgo, now, cancellationToken);
                foreach (var usage in usageByKind.Where(u => u.Count > 0))
                {
                    events.Add(new ActivityEventDto
                    {
                        Kind          = ActivityKinds.EnergySpent,
                        Category      = ActivityCategories.Energy,
                        OccurredAtUtc = now, // Summary event — use current timestamp for the week.
                        Amount        = usage.Count,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IChildEnergyUsageQuery.GetUsageByKindAsync failed for childId={request.ChildId} (activity)");
            }

            // Lesson completions: one summary event for the week.
            try
            {
                var stats = await _learningStats.GetStatsAsync(request.ChildId, weekAgo, now, cancellationToken);
                if (stats.LessonsCompleted > 0)
                {
                    events.Add(new ActivityEventDto
                    {
                        Kind          = ActivityKinds.LessonCompleted,
                        Category      = ActivityCategories.Lesson,
                        OccurredAtUtc = now,
                        Amount        = stats.LessonsCompleted,
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentLearningStatsQuery.GetStatsAsync failed for childId={request.ChildId} (activity)");
            }

            // Sort by most recent first.
            var sorted = events.OrderByDescending(e => e.OccurredAtUtc).ToList();

            var dto = new ActivityFeedDto { Events = sorted };

            _logger.LogInfo($"Activity feed retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.ActivityFeedRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildActivityQueryHandler");
            return ServerError<ActivityFeedDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    // ── Activity kind + category constants (non-user-facing technical identifiers — permitted literals) ──

    private static class ActivityKinds
    {
        public const string XpEarned       = "xp_earned";
        public const string LessonCompleted = "lesson_completed";
        public const string BadgeEarned    = "badge_earned";
        public const string EnergySpent    = "energy_spent";
    }

    private static class ActivityCategories
    {
        public const string Xp     = "xp";
        public const string Lesson = "lesson";
        public const string Badge  = "badge";
        public const string Energy = "energy";
    }
}

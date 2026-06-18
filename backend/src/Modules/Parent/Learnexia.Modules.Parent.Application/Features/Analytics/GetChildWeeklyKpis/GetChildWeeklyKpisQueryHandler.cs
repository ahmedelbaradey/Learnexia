using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeeklyKpis;

/// <summary>
/// E3 — GET /Parent/Children/{id}/WeeklyKpis
///
/// Returns KPIs for a rolling-7-day window (this week) plus WoW deltas vs the prior 7-day window.
/// Window definitions (per planner OQ-2):
///   This week  = [now − 7 days, now) UTC.
///   Prior week = [now − 14 days, now − 7 days) UTC.
///   Time-learning = Σ Attempt.DurationSeconds ÷ 60 (minutes).
///   Lessons done  = distinct completed lessons.
/// Empty / first-week → all-zero state, never an error (AC3).
/// </summary>
public class GetChildWeeklyKpisQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildWeeklyKpisQuery, BaseResponse<WeeklyKpisDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentXpTimeSeriesQuery _xpTimeSeries;
    private readonly IStudentLearningStatsQuery _learningStats;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildWeeklyKpisQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentXpTimeSeriesQuery xpTimeSeries,
        IStudentLearningStatsQuery learningStats,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _xpTimeSeries = xpTimeSeries;
        _learningStats = learningStats;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<WeeklyKpisDto>> Handle(
        GetChildWeeklyKpisQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<WeeklyKpisDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<WeeklyKpisDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<WeeklyKpisDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            // ── Window boundaries ──
            var now       = DateTime.UtcNow;
            var thisFrom  = now.AddDays(-7);
            var priorFrom = now.AddDays(-14);
            var priorTo   = thisFrom;

            // ── This-week seams ──
            int thisXp       = 0;
            int thisSeconds  = 0;
            int thisLessons  = 0;
            int currentStreak = 0;

            try
            {
                var fromDate = DateOnly.FromDateTime(thisFrom);
                var toDate   = DateOnly.FromDateTime(now);
                var series   = await _xpTimeSeries.GetDailySeriesAsync(request.ChildId, fromDate, toDate, cancellationToken);
                thisXp = series.Sum(d => d.Xp);

                var profile = await _xpTimeSeries.GetProfileSnapshotAsync(request.ChildId, cancellationToken);
                currentStreak = profile.CurrentStreak;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery failed for childId={request.ChildId} (this-week KPI)");
            }

            try
            {
                var stats = await _learningStats.GetStatsAsync(request.ChildId, thisFrom, now, cancellationToken);
                thisSeconds = stats.TimeLearningSeconds;
                thisLessons = stats.LessonsCompleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentLearningStatsQuery failed for childId={request.ChildId} (this-week KPI)");
            }

            // ── Prior-week seams ──
            int priorXp      = 0;
            int priorSeconds = 0;
            int priorLessons = 0;

            try
            {
                var fromDate = DateOnly.FromDateTime(priorFrom);
                var toDate   = DateOnly.FromDateTime(priorTo);
                var series   = await _xpTimeSeries.GetDailySeriesAsync(request.ChildId, fromDate, toDate, cancellationToken);
                priorXp = series.Sum(d => d.Xp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery failed for childId={request.ChildId} (prior-week KPI)");
            }

            try
            {
                var stats = await _learningStats.GetStatsAsync(request.ChildId, priorFrom, priorTo, cancellationToken);
                priorSeconds = stats.TimeLearningSeconds;
                priorLessons = stats.LessonsCompleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentLearningStatsQuery failed for childId={request.ChildId} (prior-week KPI)");
            }

            var dto = new WeeklyKpisDto
            {
                TimeLearningMinutes      = thisSeconds  / 60,
                XpEarned                 = thisXp,
                LessonsDone              = thisLessons,
                StreakDays               = currentStreak,
                TimeLearningDeltaMinutes = (thisSeconds / 60) - (priorSeconds / 60),
                XpDelta                  = thisXp - priorXp,
                LessonsDelta             = thisLessons - priorLessons,
                StreakDelta              = 0, // Streak is a live value; delta is informational (MVP = 0).
            };

            _logger.LogInfo($"Weekly KPIs retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.WeeklyKpisRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildWeeklyKpisQueryHandler");
            return ServerError<WeeklyKpisDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

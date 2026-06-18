using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetFamilySummary;

/// <summary>
/// E2 — GET /Parent/Family/Summary
///
/// Loops all children for the authenticated parent (via <see cref="IParentChildQuery"/>),
/// fans out per-child with per-seam try/catch (graceful degradation), aggregates into
/// a family-level summary. Mirrors <c>GetUserActivitySummaryQueryHandler</c> fan-out shape.
///
/// Badge count for <c>BadgesEarned</c>: sourced from the existing
/// <see cref="IStudentBadgesQuery.GetByStudentIdAsync"/> seam — <c>TotalCount</c> field.
/// This reuses the same seam that the admin-activity handler already consumes; no new seam needed.
/// </summary>
public class GetFamilySummaryQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetFamilySummaryQuery, BaseResponse<FamilySummaryDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentXpTimeSeriesQuery _xpTimeSeries;
    private readonly IStudentLearningStatsQuery _learningStats;
    private readonly IStudentBadgesQuery _badgesQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetFamilySummaryQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentXpTimeSeriesQuery xpTimeSeries,
        IStudentLearningStatsQuery learningStats,
        IStudentBadgesQuery badgesQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _xpTimeSeries = xpTimeSeries;
        _learningStats = learningStats;
        _badgesQuery = badgesQuery;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<FamilySummaryDto>> Handle(
        GetFamilySummaryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<FamilySummaryDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Family scope: iterate only the authenticated parent's children.
            var childIds = await _parentChildQuery.GetChildIdsForParentAsync(parentId.Value, cancellationToken);

            if (childIds.Count == 0)
            {
                var empty = new FamilySummaryDto();
                var emptyResponse = Success(empty);
                emptyResponse.Message = _localizer[SharedResourcesKey.FamilySummaryRetrievedSuccessfully];
                return emptyResponse;
            }

            // Rolling 7-day window (UTC).
            var now     = DateTime.UtcNow;
            var from    = now.AddDays(-7);
            var fromDate = DateOnly.FromDateTime(from);
            var toDate   = DateOnly.FromDateTime(now);

            int totalXp          = 0;
            int lessonsCompleted = 0;
            int activeLearners   = 0;
            int bestStreak       = 0;
            int totalBadges      = 0;

            // Per-child fan-out — each seam call is best-effort.
            foreach (var childId in childIds)
            {
                // XP time-series → weekly XP + streak
                try
                {
                    var dailySeries = await _xpTimeSeries.GetDailySeriesAsync(childId, fromDate, toDate, cancellationToken);
                    totalXp += dailySeries.Sum(d => d.Xp);

                    var profile = await _xpTimeSeries.GetProfileSnapshotAsync(childId, cancellationToken);
                    if (profile.CurrentStreak > bestStreak)
                        bestStreak = profile.CurrentStreak;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"IStudentXpTimeSeriesQuery failed for childId={childId} in family summary");
                }

                // Learning stats → lessons completed + active-learner flag
                try
                {
                    var stats = await _learningStats.GetStatsAsync(childId, from, now, cancellationToken);
                    lessonsCompleted += stats.LessonsCompleted;

                    var lastActivity = await _learningStats.GetLastActivityUtcAsync(childId, cancellationToken);
                    if (lastActivity.HasValue && lastActivity.Value.Date == now.Date)
                        activeLearners++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"IStudentLearningStatsQuery failed for childId={childId} in family summary");
                }

                // Badge count — reuses the existing IStudentBadgesQuery seam (TotalCount).
                try
                {
                    var badgeSnap = await _badgesQuery.GetByStudentIdAsync(childId, cancellationToken);
                    totalBadges += badgeSnap.TotalCount;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"IStudentBadgesQuery failed for childId={childId} in family summary");
                }
            }

            var dto = new FamilySummaryDto
            {
                ActiveLearners   = activeLearners,
                LessonsCompleted = lessonsCompleted,
                TotalXp          = totalXp,
                BestStreakDays   = bestStreak,
                BadgesEarned     = totalBadges,
            };

            _logger.LogInfo($"Family summary retrieved for parentId={parentId}, childCount={childIds.Count}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.FamilySummaryRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetFamilySummaryQueryHandler");
            return ServerError<FamilySummaryDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

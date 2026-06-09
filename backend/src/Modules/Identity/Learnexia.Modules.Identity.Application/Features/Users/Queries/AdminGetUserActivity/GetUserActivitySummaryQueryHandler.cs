using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserActivity;

/// <summary>
/// Composes activity data from Gamification seams (best-effort).
/// Each seam call is wrapped in its own try/catch; a failing seam yields null in the DTO,
/// never a 500. This matches the wave brief requirement for graceful degradation.
/// </summary>
public class GetUserActivitySummaryQueryHandler : BaseResponseHandler, IQueryHandler<GetUserActivitySummaryQuery, BaseResponse<AdminActivitySummaryDto>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IStudentXpQuery _xpQuery;
    private readonly IStudentStreakQuery _streakQuery;
    private readonly IStudentBadgesQuery _badgesQuery;
    private readonly IStudentMissionsQuery _missionsQuery;
    private readonly IStudentLeagueQuery _leagueQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetUserActivitySummaryQueryHandler(
        IIdentityServiceManager service,
        IStudentXpQuery xpQuery,
        IStudentStreakQuery streakQuery,
        IStudentBadgesQuery badgesQuery,
        IStudentMissionsQuery missionsQuery,
        IStudentLeagueQuery leagueQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _xpQuery = xpQuery;
        _streakQuery = streakQuery;
        _badgesQuery = badgesQuery;
        _missionsQuery = missionsQuery;
        _leagueQuery = leagueQuery;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<AdminActivitySummaryDto>> Handle(GetUserActivitySummaryQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // ── Input guard ──
            if (request.UserId <= 0)
                return NotFound<AdminActivitySummaryDto>(_localizer[SharedResourcesKey.UserNotFound]);

            var user = await _service.UserManagmentService.FindByIdAsync(request.UserId.ToString());
            if (user == null)
                return NotFound<AdminActivitySummaryDto>(_localizer[SharedResourcesKey.UserNotFound]);

            // ── Gamification seams — each is best-effort ──
            XpSummary? xp = null;
            StreakSummary? streak = null;
            BadgesSummary? badges = null;
            MissionsSummary? missions = null;
            LeagueSummary? league = null;

            try
            {
                var xpSnap = await _xpQuery.GetByStudentIdAsync(request.UserId, cancellationToken);
                if (xpSnap != null)
                    xp = new XpSummary(xpSnap.TotalXp, xpSnap.CurrentLevel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpQuery failed for userId={request.UserId} (degraded to null)");
            }

            try
            {
                var streakSnap = await _streakQuery.GetByStudentIdAsync(request.UserId, cancellationToken);
                if (streakSnap != null)
                    streak = new StreakSummary(streakSnap.CurrentStreak, streakSnap.LongestStreak, streakSnap.LastActivityDateUtc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentStreakQuery failed for userId={request.UserId} (degraded to null)");
            }

            try
            {
                var badgesSnap = await _badgesQuery.GetByStudentIdAsync(request.UserId, cancellationToken);
                badges = new BadgesSummary(badgesSnap.TotalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentBadgesQuery failed for userId={request.UserId} (degraded to null)");
            }

            try
            {
                var missionsSnap = await _missionsQuery.GetByStudentIdAsync(request.UserId, cancellationToken);
                var completedCount = missionsSnap.Daily.Count(m => m.Status == MissionStatusDto.Completed);
                missions = new MissionsSummary(missionsSnap.Daily.Count, completedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentMissionsQuery failed for userId={request.UserId} (degraded to null)");
            }

            try
            {
                var leagueSnap = await _leagueQuery.GetByStudentIdAsync(request.UserId, cancellationToken);
                league = new LeagueSummary(
                    leagueSnap.Tier.ToString(),
                    leagueSnap.WeeklyXp,
                    leagueSnap.CurrentRank,
                    leagueSnap.GroupSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentLeagueQuery failed for userId={request.UserId} (degraded to null)");
            }

            var dto = new AdminActivitySummaryDto
            {
                UserId = request.UserId,
                Xp = xp,
                Streak = streak,
                Badges = badges,
                Missions = missions,
                League = league,
            };

            _logger.LogInfo($"Admin activity summary retrieved for userId={request.UserId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.AdminUserActivityRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserActivitySummaryQueryHandler");
            return ServerError<AdminActivitySummaryDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

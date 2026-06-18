using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildProgress;

/// <summary>
/// E1 — GET /Parent/Children/{id}/Progress
///
/// Fan-out handler: resolves parentId from the JWT, verifies parent-owns-child (IDOR guard),
/// then fans out to Gamification + Learning + Billing + weak-area seams with per-seam try/catch.
/// A failing seam yields a zeroed/null value — never a 500 (AC3 graceful degradation).
/// Mirrors <c>GetUserActivitySummaryQueryHandler</c> shape exactly (P5-08 reference handler).
/// </summary>
public class GetChildProgressQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildProgressQuery, BaseResponse<ChildProgressDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentXpTimeSeriesQuery _xpTimeSeries;
    private readonly IStudentMasterySummaryQuery _masterySummary;
    private readonly IStudentAllSubjectsWeakAreasQuery _weakAreas;
    private readonly IStudentLearningStatsQuery _learningStats;
    private readonly IChildEnergyUsageQuery _energyUsage;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildProgressQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentXpTimeSeriesQuery xpTimeSeries,
        IStudentMasterySummaryQuery masterySummary,
        IStudentAllSubjectsWeakAreasQuery weakAreas,
        IStudentLearningStatsQuery learningStats,
        IChildEnergyUsageQuery energyUsage,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _xpTimeSeries = xpTimeSeries;
        _masterySummary = masterySummary;
        _weakAreas = weakAreas;
        _learningStats = learningStats;
        _energyUsage = energyUsage;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<ChildProgressDto>> Handle(
        GetChildProgressQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── Input guard ──
            if (request.ChildId <= 0)
                return BadRequest<ChildProgressDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            // ── IDOR guard (must come BEFORE any seam fan-out) ──
            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<ChildProgressDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<ChildProgressDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            // ── Seam fan-out — each seam is best-effort (per-seam try/catch → graceful zero) ──

            XpProfileSnapshot? profile = null;
            try
            {
                profile = await _xpTimeSeries.GetProfileSnapshotAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery.GetProfileSnapshotAsync failed for childId={request.ChildId}");
            }

            MasterySummary? mastery = null;
            try
            {
                mastery = await _masterySummary.GetByStudentAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentMasterySummaryQuery.GetByStudentAsync failed for childId={request.ChildId}");
            }

            IReadOnlyList<WeakAreaEntry>? weakAreaList = null;
            try
            {
                weakAreaList = await _weakAreas.GetWeakAreasAsync(request.ChildId, maxResults: 1, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentAllSubjectsWeakAreasQuery.GetWeakAreasAsync failed for childId={request.ChildId}");
            }

            DateTime? lastActivity = null;
            try
            {
                lastActivity = await _learningStats.GetLastActivityUtcAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentLearningStatsQuery.GetLastActivityUtcAsync failed for childId={request.ChildId}");
            }

            ChildEnergySnapshot? energySnapshot = null;
            try
            {
                energySnapshot = await _energyUsage.GetSnapshotAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IChildEnergyUsageQuery.GetSnapshotAsync failed for childId={request.ChildId}");
            }

            // ── Map to DTO (zeroed-state when seam returned null/threw) ──
            var activeToday = lastActivity.HasValue
                && lastActivity.Value.Date == DateTime.UtcNow.Date;

            var weakest = weakAreaList?.Count > 0
                ? new WeakSkillDto
                {
                    SkillId       = weakAreaList[0].SkillId,
                    SkillName     = weakAreaList[0].SkillName,
                    SubjectCode   = weakAreaList[0].SubjectCode,
                    MasteryPercent = weakAreaList[0].MasteryPercent,
                    Severity      = (int)weakAreaList[0].Severity,
                }
                : null;

            var dto = new ChildProgressDto
            {
                Level          = profile?.CurrentLevel  ?? 1,
                TotalXp        = profile?.TotalXp       ?? 0,
                CurrentStreak  = profile?.CurrentStreak ?? 0,
                BestStreak     = profile?.BestStreak    ?? 0,
                MasteryPercent = mastery?.OverallPercent ?? 0,
                WeakestSkill   = weakest,
                ActiveToday    = activeToday,
                Energy = new EnergySnapshotDto
                {
                    Remaining        = energySnapshot?.Remaining        ?? 0,
                    Allocated        = energySnapshot?.Allocated        ?? 0,
                    Spent            = energySnapshot?.Spent            ?? 0,
                    PurchasedBalance = energySnapshot?.PurchasedBalance ?? 0,
                    CycleEndsAtUtc   = energySnapshot?.CycleEndsAtUtc,
                },
            };

            _logger.LogInfo($"Child progress retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.ChildProgressRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildProgressQueryHandler");
            return ServerError<ChildProgressDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildRecommendations;

/// <summary>
/// E10 — GET /Parent/Children/{id}/Recommendations
///
/// Consumes the <see cref="IStudentRecommendationsQuery"/> seam built in P5-09-BE-5.
/// An empty list (no recommendations yet / cold-start) is a valid, successful response —
/// not an error. Mirrors <see cref="GetChildWeakAreasQueryHandler"/> (E5) exactly.
///
/// IDOR guard: <see cref="IParentChildQuery.IsParentOfChildAsync"/> is called BEFORE the
/// seam fan-out. A request from a parent who does NOT own the child returns 403 Forbidden
/// (same intentionally vague message as E5 to prevent child-id enumeration).
///
/// EF-free: the handler injects the seam interface only, never a DbContext or repository.
/// </summary>
public sealed class GetChildRecommendationsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildRecommendationsQuery, BaseResponse<RecommendationsDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentRecommendationsQuery _recommendationsQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildRecommendationsQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentRecommendationsQuery recommendationsQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser          = currentUser;
        _parentChildQuery     = parentChildQuery;
        _recommendationsQuery = recommendationsQuery;
        _logger               = logger;
        _localizer            = localizer;
    }

    public async Task<BaseResponse<RecommendationsDto>> Handle(
        GetChildRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<RecommendationsDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<RecommendationsDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── IDOR guard: verify the authenticated parent owns this child. ─────────────────────
            var isOwned = await _parentChildQuery.IsParentOfChildAsync(
                parentId.Value, request.ChildId, cancellationToken);

            if (!isOwned)
                return Forbidden<RecommendationsDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            // ── Fan-out to the Learning seam (sentinel — never null, never throws). ───────────────
            IReadOnlyList<RecommendationItem> items = [];
            try
            {
                items = await _recommendationsQuery.GetLatestAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"IStudentRecommendationsQuery.GetLatestAsync failed for childId={request.ChildId}");
            }

            var dto = new RecommendationsDto
            {
                Items = items
                    .Select(i => new RecommendationItemDto
                    {
                        SkillId          = i.SkillId,
                        SubjectCode      = i.SubjectCode,
                        TitleKey         = i.TitleKey,
                        BodyKey          = i.BodyKey,
                        CtaKey           = i.CtaKey,
                        Severity         = i.Severity,
                        ActionType       = (int)i.ActionType,
                        TargetDifficulty = i.TargetDifficulty,
                    })
                    .ToList(),
            };

            _logger.LogInfo(
                $"Recommendations retrieved for childId={request.ChildId} by parentId={parentId}");

            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.RecommendationsRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildRecommendationsQueryHandler");
            return ServerError<RecommendationsDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

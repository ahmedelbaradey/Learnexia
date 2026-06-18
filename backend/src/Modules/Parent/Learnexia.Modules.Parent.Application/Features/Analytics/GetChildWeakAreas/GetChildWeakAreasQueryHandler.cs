using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeakAreas;

/// <summary>
/// E5 — GET /Parent/Children/{id}/WeakAreas
///
/// Consumes the all-subjects weak-area seam built in P5-02-BE-2/3 (P5-08-BE-12).
/// An empty list (no weak areas) is a valid, successful response — not an error.
/// </summary>
public class GetChildWeakAreasQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildWeakAreasQuery, BaseResponse<WeakAreasResponseDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentAllSubjectsWeakAreasQuery _weakAreas;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildWeakAreasQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentAllSubjectsWeakAreasQuery weakAreas,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _weakAreas = weakAreas;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<WeakAreasResponseDto>> Handle(
        GetChildWeakAreasQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<WeakAreasResponseDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<WeakAreasResponseDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<WeakAreasResponseDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            IReadOnlyList<WeakAreaEntry> weakAreaList = [];
            try
            {
                weakAreaList = await _weakAreas.GetWeakAreasAsync(request.ChildId, ct: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentAllSubjectsWeakAreasQuery.GetWeakAreasAsync failed for childId={request.ChildId}");
            }

            var dto = new WeakAreasResponseDto
            {
                Areas = weakAreaList
                    .Select(w => new WeakAreaItemDto
                    {
                        SkillId             = w.SkillId,
                        SkillName           = w.SkillName,
                        SubjectCode         = w.SubjectCode,
                        MasteryPercent      = w.MasteryPercent,
                        Severity            = (int)w.Severity,
                        SuggestedNextAction = w.SuggestedNextAction,
                    })
                    .ToList(),
            };

            _logger.LogInfo($"Weak areas retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.WeakAreasRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildWeakAreasQueryHandler");
            return ServerError<WeakAreasResponseDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

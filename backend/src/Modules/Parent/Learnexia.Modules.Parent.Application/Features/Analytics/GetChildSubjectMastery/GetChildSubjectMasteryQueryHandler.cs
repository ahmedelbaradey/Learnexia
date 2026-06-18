using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildSubjectMastery;

/// <summary>
/// E4 — GET /Parent/Children/{id}/SubjectMastery
/// </summary>
public class GetChildSubjectMasteryQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildSubjectMasteryQuery, BaseResponse<SubjectMasteryResponseDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentMasterySummaryQuery _masterySummary;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildSubjectMasteryQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentMasterySummaryQuery masterySummary,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _masterySummary = masterySummary;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SubjectMasteryResponseDto>> Handle(
        GetChildSubjectMasteryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<SubjectMasteryResponseDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<SubjectMasteryResponseDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<SubjectMasteryResponseDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            MasterySummary? mastery = null;
            try
            {
                mastery = await _masterySummary.GetByStudentAsync(request.ChildId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentMasterySummaryQuery.GetByStudentAsync failed for childId={request.ChildId}");
            }

            var dto = new SubjectMasteryResponseDto
            {
                OverallPercent = mastery?.OverallPercent ?? 0,
                BySubject = mastery?.BySubject
                    .Select(s => new SubjectMasteryItemDto { SubjectCode = s.SubjectCode, Percent = s.Percent })
                    .ToList()
                    ?? [],
            };

            _logger.LogInfo($"Subject mastery retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.SubjectMasteryRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildSubjectMasteryQueryHandler");
            return ServerError<SubjectMasteryResponseDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

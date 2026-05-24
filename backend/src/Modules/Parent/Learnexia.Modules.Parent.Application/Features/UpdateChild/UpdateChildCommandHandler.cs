using AutoMapper;
using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.UpdateChild;

// Relocated from Identity. Family-scope authorization is enforced here from the JWT-resolved parent id +
// the ParentStudent link check (parent schema). The child profile write goes through the IChildAccountService
// seam — this handler never touches the Identity User table.
public class UpdateChildCommandHandler : BaseResponseHandler, ICommandHandler<UpdateChildCommand, BaseResponse<UpdatedChildResponse>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateChildCommandHandler(
        IChildAccountService childAccountService,
        ILinkParentStudentService linkService,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _childAccountService = childAccountService;
        _linkService = linkService;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<UpdatedChildResponse>> Handle(UpdateChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<UpdatedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Family-scope: the parent may edit a child ONLY if a ParentStudent link exists for
            // (parentId, childId). A non-linked child → forbidden, never a silent edit of another child.
            var isLinked = await _linkService.IsLinkedAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isLinked)
            {
                _logger.LogWarn($"Update-Child forbidden: parent {parentId} is not linked to child {request.ChildId}.");
                return Forbidden<UpdatedChildResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]);
            }

            var updateResult = await _childAccountService.UpdateChildAsync(
                new UpdateChildRequest(
                    ChildUserId: request.ChildId,
                    FullName: request.FullName,
                    Grade: request.Grade,
                    Language: request.Language,
                    Country: request.Country),
                cancellationToken);

            if (!updateResult.Succeeded || updateResult.Profile is null)
            {
                _logger.LogError(null, $"Update-Child failed for parent {parentId}, child {request.ChildId}: {updateResult.ErrorCode}.");
                return updateResult.ErrorCode switch
                {
                    ChildAccountError.NotFound => Forbidden<UpdatedChildResponse>(_localizer[SharedResourcesKey.CannotEditChildNotInFamily]),
                    _ => BadRequest<UpdatedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]),
                };
            }

            _logger.LogInfo($"Updated child {updateResult.Profile.Id} for parent {parentId}.");
            return Success(_mapper.Map<UpdatedChildResponse>(updateResult.Profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateChildCommand");
            return ServerError<UpdatedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

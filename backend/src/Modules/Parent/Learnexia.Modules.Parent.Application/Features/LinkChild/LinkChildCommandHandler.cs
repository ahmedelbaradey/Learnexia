using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.LinkChild;

// Relocated from Identity. The child lookup / role check / created-by fact are read through the
// IChildAccountService seam (FindLinkableChildByEmailAsync); the parent-schema link rows are owned here.
public class LinkChildCommandHandler : BaseResponseHandler, ICommandHandler<LinkChildCommand, BaseResponse<LinkedChildResponse>>
{
    private readonly IChildAccountService _childAccountService;
    private readonly ILinkParentStudentService _linkService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public LinkChildCommandHandler(
        IChildAccountService childAccountService,
        ILinkParentStudentService linkService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _childAccountService = childAccountService;
        _linkService = linkService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<LinkedChildResponse>> Handle(LinkChildCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<LinkedChildResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // FAIL-CLOSED: every rejection returns the SAME generic message with the SAME shape so a caller
            // cannot tell a non-existent email from an existing-but-ineligible one (anti-enumeration).
            var genericFailure = BadRequest<LinkedChildResponse>(_localizer[SharedResourcesKey.CannotLinkChild]);

            var child = await _childAccountService.FindLinkableChildByEmailAsync(request.ChildEmail, cancellationToken);
            if (child is null)
            {
                _logger.LogInfo($"Link-Child rejected: no user for the supplied email (parent {parentId}).");
                return genericFailure;
            }

            if (child.Id == parentId)
            {
                _logger.LogInfo($"Link-Child rejected: self-link attempt by user {parentId}.");
                return genericFailure;
            }

            if (!child.IsStudent)
            {
                _logger.LogInfo($"Link-Child rejected: target user {child.Id} is not a Student (parent {parentId}).");
                return genericFailure;
            }

            // Already linked to THIS parent → idempotent success: no duplicate, return the summary.
            if (await _linkService.IsLinkedAsync(parentId.Value, child.Id, cancellationToken))
            {
                _logger.LogInfo($"Link-Child no-op: child {child.Id} already linked to parent {parentId}.");
                return await BuildResponseAsync(child.Id, cancellationToken);
            }

            // Cross-family guard: a parent may only link a child they CREATED, or a child not yet linked to
            // ANY family. Otherwise fail closed with the SAME generic message (no ownership disclosure).
            var createdByThisParent = child.CreatedByParentId == parentId;
            if (!createdByThisParent)
            {
                var alreadyHasParent = await _linkService.StudentHasAnyParentAsync(child.Id, cancellationToken);
                if (alreadyHasParent)
                {
                    _logger.LogWarn($"Link-Child rejected: child {child.Id} belongs to another family; parent {parentId} cannot claim it.");
                    return genericFailure;
                }
            }

            await _linkService.LinkAsync(parentId.Value, child.Id, cancellationToken);

            _logger.LogInfo($"Linked child {child.Id} to parent {parentId}.");
            return await BuildResponseAsync(child.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in LinkChildCommand");
            return ServerError<LinkedChildResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    private async Task<BaseResponse<LinkedChildResponse>> BuildResponseAsync(int childId, CancellationToken cancellationToken)
    {
        var profiles = await _childAccountService.GetChildrenAsync(new[] { childId }, cancellationToken);
        var p = profiles.FirstOrDefault();
        return Success(new LinkedChildResponse
        {
            Id = childId,
            FullName = p?.FullName ?? string.Empty,
            Email = p?.Email ?? string.Empty,
            LearningLanguage = p?.LearningLanguage ?? string.Empty,
        });
    }
}

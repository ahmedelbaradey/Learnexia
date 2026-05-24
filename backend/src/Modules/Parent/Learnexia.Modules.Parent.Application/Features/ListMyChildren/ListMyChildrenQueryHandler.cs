using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Application.Features.LinkChild;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.ListMyChildren;

// Relocated from Identity. The link rows are owned by the Parent module; child profiles are enriched via
// the IChildAccountService seam (no cross-module entity navigation).
public class ListMyChildrenQueryHandler : BaseResponseHandler, IQueryHandler<ListMyChildrenQuery, BaseResponse<IEnumerable<LinkedChildResponse>>>
{
    private readonly ILinkParentStudentService _linkService;
    private readonly IChildAccountService _childAccountService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ListMyChildrenQueryHandler(
        ILinkParentStudentService linkService,
        IChildAccountService childAccountService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _linkService = linkService;
        _childAccountService = childAccountService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<IEnumerable<LinkedChildResponse>>> Handle(ListMyChildrenQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Family scope: the list is keyed on the authenticated caller's id, never a body/query param.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<IEnumerable<LinkedChildResponse>>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var childIds = await _linkService.GetLinkedStudentIdsAsync(parentId.Value, cancellationToken);
            if (childIds.Count == 0)
                return Success<IEnumerable<LinkedChildResponse>>(Array.Empty<LinkedChildResponse>());

            var profiles = await _childAccountService.GetChildrenAsync(childIds, cancellationToken);

            var response = profiles
                .Select(p => new LinkedChildResponse
                {
                    Id = p.Id,
                    FullName = p.FullName,
                    Email = p.Email,
                })
                .ToList();

            return Success<IEnumerable<LinkedChildResponse>>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListMyChildrenQuery");
            return ServerError<IEnumerable<LinkedChildResponse>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

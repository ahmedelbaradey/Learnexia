using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Family.Queries.ListMyChildren;

public class ListMyChildrenQueryHandler : BaseResponseHandler, IQueryHandler<ListMyChildrenQuery, BaseResponse<IEnumerable<LinkedChildResponse>>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ListMyChildrenQueryHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<IEnumerable<LinkedChildResponse>>> Handle(ListMyChildrenQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Family scope (AC-3): the list is keyed on the authenticated caller's id, never a body/query param.
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<IEnumerable<LinkedChildResponse>>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var children = await _service.LinkParentStudentService.GetLinkedStudentsAsync(parentId.Value, cancellationToken);
            var response = _mapper.Map<IEnumerable<LinkedChildResponse>>(children);

            return Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListMyChildrenQuery");
            return ServerError<IEnumerable<LinkedChildResponse>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUsersByRole;

public class GetUsersByRoleQueryHandler : BaseResponseHandler, IQueryHandler<GetUsersByRoleQuery, PaginatedResult<GetUserListResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;

    public GetUsersByRoleQueryHandler(IIdentityServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _mapper = mapper;
        _service = service;
        _logger = logger;
    }

    public async Task<PaginatedResult<GetUserListResponse>> Handle(GetUsersByRoleQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var users = await _service.UserManagmentService.GetUsersByRole(request.RoleName);
            if (!users.Any())
                return PaginatedResult<GetUserListResponse>.EmptyCollection();

            var userList = _mapper.Map<List<GetUserListResponse>>(users);
            var paginatedResult = PaginatedResult<GetUserListResponse>.Success(
                userList,
                userList.Count,
                request.PageNumber,
                request.PageSize);
            return paginatedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserPaginatedListQuery");
            return PaginatedResult<GetUserListResponse>.ServerError("Error in GetUserPaginatedListQuery");
        }
    }
}

using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUserRoles;

public class GetUserRolesQueryHandler : BaseResponseHandler, IQueryHandler<GetUserRolesQuery, BaseResponse<ManageUserRolesResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;
    private readonly UserManager<User> _userManager;
    private readonly ILoggerManager _logger;

    public GetUserRolesQueryHandler(IIdentityServiceManager service, IMapper mapper, UserManager<User> userManager, ILoggerManager logger)
    {
        _service = service;
        _mapper = mapper;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<BaseResponse<ManageUserRolesResponse>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(request.Id.ToString());
            if (user == null)
                return NotFound<ManageUserRolesResponse>("user with this Id not Found!");

            var result = await _service.AuthorizationService.GetUsersRoles(user);
            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ServerError<ManageUserRolesResponse>(ex.Message);
        }
    }
}

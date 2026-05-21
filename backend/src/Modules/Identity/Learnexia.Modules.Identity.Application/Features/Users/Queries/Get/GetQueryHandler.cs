using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.Get;

public class GetQueryHandler : BaseResponseHandler, IQueryHandler<GetUserQuery, BaseResponse<GetUserResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;

    public GetQueryHandler(IIdentityServiceManager service, IMapper mapper)
    {
        _mapper = mapper;
        _service = service;
    }

    public async Task<BaseResponse<GetUserResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UserManagmentService.FindByIdAsync(request.Id.ToString());
            if (user == null)
                return NotFound<GetUserResponse>($"User with Id: {request.Id} not found!");

            var usermapper = _mapper.Map<GetUserResponse>(user);
            var roles = await _service.AuthorizationService.GetUsersRoles(user);
            if (roles.UserRoles.Any())
                usermapper.UserRoles = roles.UserRoles;

            return Success(usermapper);
        }
        catch (Exception ex)
        {
            return ServerError<GetUserResponse>(ex.Message);
        }
    }
}

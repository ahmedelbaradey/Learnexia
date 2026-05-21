using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.AddRole;

public class AddRoleCommandHandler : BaseResponseHandler, ICommandHandler<AddRoleCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;

    public AddRoleCommandHandler(IIdentityServiceManager service, ILoggerManager logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(AddRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var isExist = await _service.AuthorizationService.IsRoleNameExist(request.roleName);
            if (isExist)
                return BadRequest<string>("this role name is already exist.");

            var isAdded = await _service.AuthorizationService.AddRoleAsync(request.roleName, request.RoleClaims);
            if (!isAdded)
                return BadRequest<string>("Added operation failed.");

            return Success("Added Operation Successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ServerError<string>(ex.Message);
        }
    }
}

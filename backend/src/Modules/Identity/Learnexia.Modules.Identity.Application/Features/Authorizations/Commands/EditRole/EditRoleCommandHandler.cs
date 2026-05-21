using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.EditRole;

public class RoleCommandHandler : BaseResponseHandler, ICommandHandler<EditRoleCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;

    public RoleCommandHandler(IIdentityServiceManager service, ILoggerManager logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(EditRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var isExist = await _service.AuthorizationService.IsRoleNameExist(request.roleName);
            if (!isExist)
                return BadRequest<string>("this role name is not Exist.");

            var isEdited = await _service.AuthorizationService.EditRoleById(request.Id, request.roleName, request.RoleClaims);
            if (!isEdited)
                return BadRequest<string>("Edited operation failed.");

            return Success("Edited Operation Successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ServerError<string>(ex.Message);
        }
    }
}

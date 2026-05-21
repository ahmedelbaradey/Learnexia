using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserRoles;

public class RoleCommandHandler : BaseResponseHandler, ICommandHandler<EditUserRolesCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;

    public RoleCommandHandler(IIdentityServiceManager service, ILoggerManager logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(EditUserRolesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.AuthorizationService.UpdateUserRoles(request);
            switch (result)
            {
                case "UserNotFound":
                    return NotFound<string>("user with this id not found");
                case "FailedDeleted":
                    return BadRequest<string>("Deleted Operation Failed.");
                case "FailedAdded":
                    return Success("Added Operation Failed.");
            }

            return Success("Updated Operation Successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ServerError<string>(ex.Message);
        }
    }
}

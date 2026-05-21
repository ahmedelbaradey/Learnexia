using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.DeleteRole;

public class RoleCommandHandler : BaseResponseHandler, ICommandHandler<DeleteRoleCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;

    public RoleCommandHandler(IIdentityServiceManager service, ILoggerManager logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var role = await _service.AuthorizationService.GetRoleByID(request.Id);
            if (role == null)
                return NotFound<string>("role with this id not found!");

            var isDeleted = await _service.AuthorizationService.DeleteRoleById(role);
            if (!isDeleted)
                return BadRequest<string>("Deleted Operation Failed.");

            return Success("Deleted Operation Successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ServerError<string>(ex.Message);
        }
    }
}

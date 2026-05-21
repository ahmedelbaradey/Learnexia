using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteUser;

public class DeleteUserCommandHandler : BaseResponseHandler, ICommandHandler<DeleteUserCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;

    public DeleteUserCommandHandler(IIdentityServiceManager service, IMapper mapper)
    {
        _mapper = mapper;
        _service = service;
    }

    public async Task<BaseResponse<string>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _service.UserManagmentService.FindByIdAsync(request.Id.ToString());
            if (user == null)
                return NotFound<string>($"User with Id: {request.Id} not found!");

            var result = await _service.UserManagmentService.DeleteAsync(user);
            if (!result.Succeeded)
                return BadRequest<string>("Deleted Operation Failed.");

            return Success("Deleted Operation Successfully.");
        }
        catch (Exception ex)
        {
            return ServerError<string>(ex.Message);
        }
    }
}

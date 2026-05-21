using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.SetNewPassword;

public class SetNewPasswordCommandHandler : BaseResponseHandler, ICommandHandler<SetNewPasswordCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;

    public SetNewPasswordCommandHandler(
        IIdentityServiceManager service,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        IIdentityServiceManager identityServiceManager,
        ICurrentUserService currentUserService)
    {
        _mapper = mapper;
        _service = service;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _identityServiceManager = identityServiceManager;
    }

    public async Task<BaseResponse<string>> Handle(SetNewPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _identityServiceManager.UserManagmentService.FindByIdAsync(_currentUserService.UserId.ToString());
            if (user == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            bool isFirstTimePasswordChange = !user.RegistrationIsCompleted;
            IdentityResult result;
            if (isFirstTimePasswordChange)
            {
                if (await _identityServiceManager.AuthenticationService.HasPasswordAsync(user))
                    await _identityServiceManager.AuthenticationService.RemovePasswordAsync(user);

                result = await _identityServiceManager.AuthenticationService.AddPasswordAsync(user, request.NewPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return BadRequest<string>($"{_localizer[SharedResourcesKey.PasswordChangeSystemError]}: {errors}");
                }
            }
            else
            {
                return ServerError<string>(_localizer[SharedResourcesKey.PasswordChangeSystemError]);
            }

            if (isFirstTimePasswordChange)
            {
                user.RegistrationIsCompleted = true;
                user.UpdatedAt = DateTime.Now;
                user.UpdatedBy = _currentUserService.UserId;
                await _identityServiceManager.UserManagmentService.UpdateAsync(user);
            }

            return Success<string>(_localizer[SharedResourcesKey.PasswordChangedSuccessfully]);
        }
        catch (Exception)
        {
            return ServerError<string>(_localizer[SharedResourcesKey.PasswordChangeSystemError]);
        }
    }
}

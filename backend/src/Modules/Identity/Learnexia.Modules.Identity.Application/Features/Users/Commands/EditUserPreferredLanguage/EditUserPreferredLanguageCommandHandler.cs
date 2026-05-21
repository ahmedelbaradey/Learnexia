using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserPreferredLanguage;

public class EditUserPreferredLanguageCommandHandler : BaseResponseHandler, ICommandHandler<EditUserPreferredLanguageCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditUserPreferredLanguageCommandHandler(IIdentityServiceManager service, IMapper mapper, IStringLocalizer<SharedResources> localizer)
    {
        _mapper = mapper;
        _service = service;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(EditUserPreferredLanguageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var oldUser = await _service.UserManagmentService.FindByIdAsync(request.Id.ToString());
            if (oldUser == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            oldUser.PreferredLanguage = request.UserPreferredLanguage;
            var result = await _service.UserManagmentService.UpdateAsync(oldUser);
            if (!result.Succeeded)
                return BadRequest<string>(result.Errors.FirstOrDefault()?.Description);

            return Success<string>(_localizer[SharedResourcesKey.PreferredLanguageUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            return ServerError<string>(ex.Message);
        }
    }
}

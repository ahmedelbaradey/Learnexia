using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateUserProfile;

public class UpdateUserProfileCommandHandler : BaseResponseHandler, ICommandHandler<UpdateUserProfileCommand, BaseResponse<string>>
{
    private readonly UserManager<User> _userManager;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityServiceManager _identityService;

    public UpdateUserProfileCommandHandler(
        UserManager<User> userManager,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        IIdentityServiceManager identityService,
        ICurrentUserService currentUserService)
    {
        _userManager = userManager;
        _mapper = mapper;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    public async Task<BaseResponse<string>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            request.Id = _currentUserService.UserId.GetValueOrDefault();

            var user = await _identityService.UserManagmentService.FindByIdAsync(request.Id.ToString());
            if (user == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                var existingUserWithEmail = await _identityService.UserManagmentService.FindByEmailAsync(request.Email);
                if (existingUserWithEmail != null && existingUserWithEmail.Id != user.Id)
                    return BadRequest<string>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]);
            }

            string? newCVPath = null;
            string? newPhotoPath = null;

            if (request.CVFile != null)
                newCVPath = $"/uploads/cv/{request.Id}_{DateTime.Now:yyyyMMddHHmmss}_{request.CVFile.FileName}";

            if (request.PersonalPhoto != null)
                newPhotoPath = $"/uploads/photos/{request.Id}_{DateTime.Now:yyyyMMddHHmmss}_{request.PersonalPhoto.FileName}";

            _mapper.Map(request, user);


            if (newPhotoPath != null)
                user.PersonalPhotoPath = newPhotoPath;

            user.UpdatedAt = DateTime.Now;
            user.UpdatedBy = _currentUserService.UserId;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.ProfileSystemErrorSavingData]}: {errors}");
            }

            return Success<string>(_localizer[SharedResourcesKey.ProfileUpdatedSuccessfully]);
        }
        catch (Exception)
        {
            return ServerError<string>(_localizer[SharedResourcesKey.ProfileSystemErrorSavingData]);
        }
    }
}

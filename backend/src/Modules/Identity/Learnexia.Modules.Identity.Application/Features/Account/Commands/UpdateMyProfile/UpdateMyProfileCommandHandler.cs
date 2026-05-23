using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyProfile;

// Mirrors AddChildCommandHandler's posture: caller resolved from the JWT (never the body), generic
// localized failure messages, no Unit of Work (UserManager.UpdateAsync commits the single row).
public class UpdateMyProfileCommandHandler : BaseResponseHandler, ICommandHandler<UpdateMyProfileCommand, BaseResponse<AccountProfileResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateMyProfileCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<AccountProfileResponse>> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Self-scope (fail-closed): the edited user is ALWAYS the authenticated caller, resolved
            // from the JWT — never an id from the request body. No id means no token → Unauthorized.
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<AccountProfileResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _service.UserManagmentService.FindByIdAsync(userId.Value.ToString());
            if (user is null)
                return Unauthorized<AccountProfileResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Only the three self-editable fields are touched. Email/UserName/role/id are never
            // assigned here (no mass-assignment). Optional fields fall back to the trimmed input.
            user.FullName = request.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            user.Nationality = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim();

            // No Unit of Work — UpdateAsync commits the single row immediately. On failure return a
            // generic localized message; never echo raw Identity error descriptions to the client.
            var result = await _service.UserManagmentService.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError(null, $"Update-Profile UpdateAsync failed for user {userId}: {errors}");
                return BadRequest<AccountProfileResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            _logger.LogInfo($"Updated profile for user {userId}.");
            var response = Success(_mapper.Map<AccountProfileResponse>(user));
            response.Message = _localizer[SharedResourcesKey.ProfileUpdatedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateMyProfileCommand");
            return ServerError<AccountProfileResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.RemoveAvatar;

// Self-scoped (fail-closed): caller resolved from the JWT, never the request. Clears User.AvatarUrl
// (the stored object KEY) and persists. The backing object is intentionally LEFT in storage — object
// DELETE is not part of the IStorageService contract for the MVP (the 4 implemented methods are
// upload/download/preview-url/exists). The orphaned object is an accepted MVP trade-off; follow-up:
// add a delete method + a background sweep. No Unit of Work — UpdateAsync commits the single row.
public class RemoveAvatarCommandHandler : BaseResponseHandler, ICommandHandler<RemoveAvatarCommand, BaseResponse<AvatarUploadResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RemoveAvatarCommandHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<AvatarUploadResponse>> Handle(RemoveAvatarCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<AvatarUploadResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _service.UserManagmentService.FindByIdAsync(userId.Value.ToString());
            if (user is null)
                return Unauthorized<AvatarUploadResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Already cleared → idempotent success.
            if (string.IsNullOrWhiteSpace(user.AvatarUrl))
            {
                var noop = Success(new AvatarUploadResponse { AvatarUrl = null });
                noop.Message = _localizer[SharedResourcesKey.AvatarRemovedSuccessfully];
                return noop;
            }

            user.AvatarUrl = null;
            var update = await _service.UserManagmentService.UpdateAsync(user);
            if (!update.Succeeded)
            {
                var errors = string.Join(", ", update.Errors.Select(e => e.Description));
                _logger.LogError(null, $"Avatar remove UpdateAsync failed for user {userId}: {errors}");
                return BadRequest<AvatarUploadResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            _logger.LogInfo($"Avatar removed for user {userId} (object left in storage — delete not implemented for MVP).");
            var response = Success(new AvatarUploadResponse { AvatarUrl = null });
            response.Message = _localizer[SharedResourcesKey.AvatarRemovedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RemoveAvatarCommand");
            return ServerError<AvatarUploadResponse>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RevokeDevice;

/// <summary>
/// Deactivates a device push token for the authenticated user (P4-09 B4-4).
/// Anti-IDOR: only deactivates tokens belonging to the current user.
/// Idempotent: already-deactivated tokens are a no-op.
/// EF access lives in <see cref="IDeviceTokenService"/> (Option-C rule).
/// </summary>
public sealed class RevokeDeviceCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RevokeDeviceCommand, BaseResponse<string>>
{
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RevokeDeviceCommandHandler(
        IDeviceTokenService deviceTokenService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _deviceTokenService = deviceTokenService;
        _currentUserService = currentUserService;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<string>> Handle(
        RevokeDeviceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Anti-IDOR: DeactivateAsync only deactivates tokens belonging to this user.
            // Returns false when not found OR not owned — both map to NotFound (anti-enumeration).
            var found = await _deviceTokenService.DeactivateAsync(userId.Value, request.TokenId, cancellationToken);

            if (!found)
                return NotFound<string>(_localizer[SharedResourcesKey.DeviceTokenNotFound]);

            return Success<string>(_localizer[SharedResourcesKey.DeviceTokenRevokedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RevokeDeviceCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

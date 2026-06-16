using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RegisterDevice;

/// <summary>
/// Upserts a device push token for the authenticated user (P4-09 B4-4 / AC4).
/// Upsert logic (global-lookup + reassign-or-insert + F-05 per-user cap eviction) lives
/// inside <see cref="IDeviceTokenService"/> (Option-C rule).
/// </summary>
public sealed class RegisterDeviceCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RegisterDeviceCommand, BaseResponse<string>>
{
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemClock _clock;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RegisterDeviceCommandHandler(
        IDeviceTokenService deviceTokenService,
        ICurrentUserService currentUserService,
        ISystemClock clock,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _deviceTokenService = deviceTokenService;
        _currentUserService = currentUserService;
        _clock              = clock;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<string>> Handle(
        RegisterDeviceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            await _deviceTokenService.RegisterAsync(
                userId.Value,
                request.ExpoPushToken,
                request.Platform,
                _clock.UtcNow,
                cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.DeviceTokenRegisteredSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RegisterDeviceCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

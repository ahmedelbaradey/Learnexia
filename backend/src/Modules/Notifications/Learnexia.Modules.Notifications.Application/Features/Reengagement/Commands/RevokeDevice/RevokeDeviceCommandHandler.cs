using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RevokeDevice;

/// <summary>
/// Deactivates a device push token for the authenticated user (P4-09 B4-4).
/// Anti-IDOR: only deactivates tokens belonging to the current user.
/// Idempotent: already-deactivated tokens are a no-op.
/// </summary>
public sealed class RevokeDeviceCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RevokeDeviceCommand, BaseResponse<string>>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemClock _clock;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RevokeDeviceCommandHandler(
        INotificationsDbContext db,
        ICurrentUserService currentUserService,
        ISystemClock clock,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
        _currentUserService = currentUserService;
        _clock              = clock;
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

            // Anti-IDOR: only deactivate if the token row exists AND belongs to the current user.
            // Anti-enumeration: do NOT differentiate "not owned" from "not exists" — both return 404.
            var token = await _db.UserDeviceTokens
                .FirstOrDefaultAsync(
                    t => t.UserId == userId.Value && t.Id == request.TokenId,
                    cancellationToken);

            if (token is null)
            {
                // Not found (or not owned by this user) — return NotFound, not a silent success.
                return NotFound<string>(_localizer[SharedResourcesKey.DeviceTokenNotFound]);
            }

            if (token.IsActive)
            {
                token.Deactivate();
                await _db.SaveChangesAsync(cancellationToken);
            }

            return Success<string>(_localizer[SharedResourcesKey.DeviceTokenRevokedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RevokeDeviceCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

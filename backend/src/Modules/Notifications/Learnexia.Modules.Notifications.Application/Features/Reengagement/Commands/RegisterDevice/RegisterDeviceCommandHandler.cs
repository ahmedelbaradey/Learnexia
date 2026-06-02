using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RegisterDevice;

/// <summary>
/// Upserts a device push token for the authenticated user (P4-09 B4-4 / AC4).
/// Upsert logic:
/// 1. Token already registered for this user → update <c>LastUsedAtUtc</c> + re-activate if revoked.
/// 2. Token exists but belongs to a different user (device rotation) → transfer ownership to current user.
/// 3. Token doesn't exist → insert new row (subject to F-05 per-user cap enforcement).
/// F-05: before inserting a new token, if the user already has <see cref="MaxTokensPerUser"/>
/// active tokens, the oldest one is deactivated to prevent unbounded row growth from misbehaving clients.
/// </summary>
public sealed class RegisterDeviceCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RegisterDeviceCommand, BaseResponse<string>>
{
    // F-05: hard cap on active device tokens per user — deactivates the oldest when exceeded.
    private const int MaxTokensPerUser = 10;

    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemClock _clock;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RegisterDeviceCommandHandler(
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
        RegisterDeviceCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var nowUtc = _clock.UtcNow;

            // Look for the token globally (not just for this user) — handles rotation/transfer.
            var existing = await _db.UserDeviceTokens
                .FirstOrDefaultAsync(t => t.ExpoPushToken == request.ExpoPushToken, cancellationToken);

            if (existing is not null)
            {
                // Transfer ownership if the token moved to a new user (device shared/rotated).
                existing.ReassignTo(userId.Value, nowUtc);
            }
            else
            {
                // F-05: enforce per-user active token cap before inserting a new row.
                // A misbehaving client cannot create unbounded rows for the same user.
                int activeCount = await _db.UserDeviceTokens
                    .CountAsync(t => t.UserId == userId.Value && t.IsActive, cancellationToken);

                if (activeCount >= MaxTokensPerUser)
                {
                    var oldest = await _db.UserDeviceTokens
                        .Where(t => t.UserId == userId.Value && t.IsActive)
                        .OrderBy(t => t.RegisteredAtUtc)
                        .FirstAsync(cancellationToken);

                    oldest.Deactivate();
                    _logger.LogInfo($"P4-09: device token cap ({MaxTokensPerUser}) reached for userId={userId.Value}; deactivating oldest tokenId={oldest.Id}.");
                }

                var newToken = UserDeviceToken.Register(userId.Value, request.ExpoPushToken, request.Platform.ToLowerInvariant(), nowUtc);
                await _db.UserDeviceTokens.AddAsync(newToken, cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.DeviceTokenRegisteredSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RegisterDeviceCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

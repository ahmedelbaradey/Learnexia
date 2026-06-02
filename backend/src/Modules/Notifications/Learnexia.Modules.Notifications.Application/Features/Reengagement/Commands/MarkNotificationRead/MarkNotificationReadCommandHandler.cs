using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkNotificationRead;

/// <summary>
/// Marks one notification as read (P4-09 B4-4 / AC6).
/// Anti-IDOR: loads the row and asserts <c>RecipientExternalUserId == currentUser</c>.
/// Stamps <c>OpenedAtUtc</c> for analytics + emits <c>analytics.reengagement.opened</c> log marker.
/// </summary>
public sealed class MarkNotificationReadCommandHandler
    : BaseResponseHandler,
      ICommandHandler<MarkNotificationReadCommand, BaseResponse<string>>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemClock _clock;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public MarkNotificationReadCommandHandler(
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
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == request.NotificationId, cancellationToken);

            if (notification is null)
                return NotFound<string>(_localizer[SharedResourcesKey.NotificationNotFound]);

            // Anti-IDOR: only the recipient may mark their own notification as read.
            if (notification.RecipientExternalUserId != userId.Value)
                return Forbidden<string>(_localizer[SharedResourcesKey.NotificationAccessForbidden]);

            if (!notification.IsRead)
            {
                notification.MarkRead(_clock.UtcNow);
                await _db.SaveChangesAsync(cancellationToken);
            }

            // AC6: analytics emit — no PII (only IDs + category).
            _logger.LogInfo(
                $"analytics.reengagement.opened id={notification.Id} " +
                $"childId={userId.Value} category={notification.Category}");

            return Success<string>(_localizer[SharedResourcesKey.NotificationMarkedReadSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in MarkNotificationReadCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

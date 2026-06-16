using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkAllNotificationsRead;

/// <summary>
/// Bulk-marks all unread notifications as read for the authenticated user (P4-09 B4-4).
/// Self-scoped — only updates rows for the current user; no IDOR surface.
/// EF access lives in <see cref="INotificationInboxService"/> (Option-C rule).
/// </summary>
public sealed class MarkAllNotificationsReadCommandHandler
    : BaseResponseHandler,
      ICommandHandler<MarkAllNotificationsReadCommand, BaseResponse<string>>
{
    private readonly INotificationInboxService _inboxService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public MarkAllNotificationsReadCommandHandler(
        INotificationInboxService inboxService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _inboxService       = inboxService;
        _currentUserService = currentUserService;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<string>> Handle(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var updated = await _inboxService.MarkAllReadAsync(userId.Value, cancellationToken);

            _logger.LogInfo(
                $"P4-09: MarkAllRead — updatedCount={updated} userId={userId.Value}");

            return Success<string>(_localizer[SharedResourcesKey.AllNotificationsMarkedReadSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in MarkAllNotificationsReadCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

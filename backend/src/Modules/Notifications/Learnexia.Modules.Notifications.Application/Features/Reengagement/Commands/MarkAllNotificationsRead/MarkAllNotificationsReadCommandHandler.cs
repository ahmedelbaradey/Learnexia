using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkAllNotificationsRead;

/// <summary>
/// Bulk-marks all unread notifications as read for the authenticated user (P4-09 B4-4).
/// Self-scoped — only updates rows for the current user; no IDOR surface.
/// </summary>
public sealed class MarkAllNotificationsReadCommandHandler
    : BaseResponseHandler,
      ICommandHandler<MarkAllNotificationsReadCommand, BaseResponse<string>>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public MarkAllNotificationsReadCommandHandler(
        INotificationsDbContext db,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
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

            // EF 7+ ExecuteUpdateAsync — single SQL UPDATE, no change tracker, IDOR-safe by WHERE clause.
            var updated = await _db.Notifications
                .Where(n => n.RecipientExternalUserId == userId.Value && !n.IsRead)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(n => n.IsRead, true),
                    cancellationToken);

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

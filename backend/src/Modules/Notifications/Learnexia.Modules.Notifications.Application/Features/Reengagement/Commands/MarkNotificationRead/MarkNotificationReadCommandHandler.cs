using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkNotificationRead;

/// <summary>
/// Marks one notification as read (P4-09 B4-4 / AC6).
/// Anti-IDOR: asserts <c>RecipientExternalUserId == currentUser</c> via service.
/// Stamps <c>OpenedAtUtc</c> for analytics + emits <c>analytics.reengagement.opened</c> log marker.
/// EF access lives in <see cref="INotificationInboxService"/> (Option-C rule).
/// </summary>
public sealed class MarkNotificationReadCommandHandler
    : BaseResponseHandler,
      ICommandHandler<MarkNotificationReadCommand, BaseResponse<string>>
{
    private readonly INotificationInboxService _inboxService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemClock _clock;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public MarkNotificationReadCommandHandler(
        INotificationInboxService inboxService,
        ICurrentUserService currentUserService,
        ISystemClock clock,
        IPublisher publisher,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _inboxService       = inboxService;
        _currentUserService = currentUserService;
        _clock              = clock;
        _publisher          = publisher;
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

            var notification = await _inboxService.MarkReadAsync(
                request.NotificationId,
                userId.Value,
                _clock.UtcNow,
                cancellationToken);

            if (notification is null)
                return NotFound<string>(_localizer[SharedResourcesKey.NotificationNotFound]);

            // Anti-IDOR: only the recipient may mark their own notification as read.
            if (notification.RecipientExternalUserId != userId.Value)
                return Forbidden<string>(_localizer[SharedResourcesKey.NotificationAccessForbidden]);

            // AC6: analytics emit — no PII (only IDs + category).
            _logger.LogInfo(
                $"analytics.reengagement.opened id={notification.Id} " +
                $"childId={userId.Value} category={notification.Category}");

            // P9-11: Emit NotificationOpened analytics event (fail-soft — must NEVER affect
            // the mark-read response). Inbox-read path only; push-tap opened is deferred to P9-02 FE.
            // Mark-all-read Opened emission is deferred (ExecuteUpdateAsync path has no materialized entities).
            try
            {
                await _publisher.Publish(new NotificationOpenedIntegrationEvent(
                    EventId:       Guid.NewGuid(),
                    OccurredOnUtc: _clock.UtcNow,
                    StudentId:     userId.Value,
                    Code:          notification.Code,
                    Category:      (int)notification.Category), cancellationToken);
            }
            catch (Exception pubEx)
            {
                _logger.LogError(pubEx,
                    $"P9-11: MarkNotificationRead — failed to publish NotificationOpenedIntegrationEvent " +
                    $"id={notification.Id} childId={userId.Value} — analytics sink may miss this event.");
            }

            return Success<string>(_localizer[SharedResourcesKey.NotificationMarkedReadSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in MarkNotificationReadCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

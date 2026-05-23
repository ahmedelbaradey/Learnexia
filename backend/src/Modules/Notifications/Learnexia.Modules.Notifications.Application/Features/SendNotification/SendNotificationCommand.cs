using Learnexia.Shared.Kernel.Results;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.Features.SendNotification;

public sealed record SendNotificationCommand(
    Guid RecipientUserId,
    string Title,
    string Body,
    Guid NotificationTypeId,
    Guid? NotificationModuleId,
    // Recipient email address. Required to actually deliver the notification by email; callers that only
    // need an in-app notification row can leave it null (the email step is then skipped).
    string? RecipientEmail = null) : IRequest<Result>;

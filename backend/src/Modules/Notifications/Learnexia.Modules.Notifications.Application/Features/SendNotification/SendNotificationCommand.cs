using Learnexia.Shared.Kernel.Results;
using MediatR;

namespace Learnexia.Modules.Notifications.Application.Features.SendNotification;

public sealed record SendNotificationCommand(
    Guid RecipientUserId,
    string Title,
    string Body,
    Guid NotificationTypeId,
    Guid? NotificationModuleId) : IRequest<Result>;

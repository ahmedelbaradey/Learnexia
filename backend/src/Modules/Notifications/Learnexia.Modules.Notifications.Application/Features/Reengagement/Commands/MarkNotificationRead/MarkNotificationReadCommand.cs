using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkNotificationRead;

/// <summary>
/// Marks a single notification as read and stamps <c>OpenedAtUtc</c> (P4-09 B4-4 / AC6).
/// Emits an analytics log marker: <c>analytics.reengagement.opened</c>.
/// Anti-IDOR: handler asserts <c>RecipientExternalUserId == currentUser</c> before writing.
/// </summary>
public sealed record MarkNotificationReadCommand(Guid NotificationId)
    : ICommand<BaseResponse<string>>;

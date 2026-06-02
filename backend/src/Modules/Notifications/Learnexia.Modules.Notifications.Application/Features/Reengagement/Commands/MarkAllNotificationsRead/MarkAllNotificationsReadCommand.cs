using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkAllNotificationsRead;

/// <summary>
/// Bulk-marks all unread notifications as read for the authenticated user (P4-09 B4-4).
/// Self-scoped: only the current user's rows are updated (no IDOR surface).
/// <c>OpenedAtUtc</c> is NOT stamped on bulk-mark (only single-row <c>MarkRead</c> stamps it,
/// for analytics fidelity — see B4-4 decision in execution plan).
/// </summary>
public sealed record MarkAllNotificationsReadCommand : ICommand<BaseResponse<string>>;

using System.Collections.Generic;
using Learnexia.Modules.Notifications.Application.Features.Notifications.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Notifications.Queries.List;

/// <summary>
/// Lists the notifications for a recipient, matched on <c>RecipientExternalUserId</c> (the Identity integer
/// user id carried by <c>UserRegisteredIntegrationEvent</c>). Lets api-tester confirm the cross-module
/// delivery path produced a welcome notification. Queries are NOT auto-validated (CONVENTIONS §4) — the
/// handler guards the input.
/// </summary>
public sealed record ListQuery : IQuery<BaseResponse<List<NotificationResponse>>>
{
    public int RecipientUserId { get; init; }
}

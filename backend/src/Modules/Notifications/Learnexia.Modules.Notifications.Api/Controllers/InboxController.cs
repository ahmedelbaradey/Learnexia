using Learnexia.Modules.Notifications.Api.Bases;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkAllNotificationsRead;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.MarkNotificationRead;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.ListMyInbox;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Notifications.Api.Controllers;

/// <summary>
/// Self-scoped in-app notification inbox endpoints (P4-09 B4-4 / AC4).
/// All routes are gated by [Authorize] — any authenticated user can read/manage their own inbox.
/// The recipient user id is ALWAYS resolved from the JWT (never a request parameter) to prevent IDOR.
/// </summary>
[Route("api/Notifications/Inbox")]
[ApiController]
[Authorize]
public sealed class InboxController : AppControllerBase
{
    /// <summary>
    /// Returns the authenticated user's notification inbox, newest-first, paginated.
    /// <paramref name="take"/> is clamped to [1, 100]; <paramref name="skip"/> must be ≥ 0.
    /// </summary>
    [HttpGet("Me")]
    [ProducesResponseType(typeof(BaseResponse<PagedInboxResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MyInbox(
        [FromQuery] int take = 20,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(new ListMyInboxQuery(take, skip), ct));

    /// <summary>
    /// Marks a single notification as read and stamps <c>OpenedAtUtc</c>.
    /// Returns 403 if the notification belongs to a different user (anti-IDOR).
    /// Returns 404 if the notification does not exist.
    /// </summary>
    [HttpPost("{id:guid}/MarkRead")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(
        Guid id,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(new MarkNotificationReadCommand(id), ct));

    /// <summary>
    /// Bulk-marks all unread notifications as read for the authenticated user.
    /// Self-scoped — only the current user's rows are updated.
    /// </summary>
    [HttpPost("MarkAllRead")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
        => NewResult(await Mediator.Send(new MarkAllNotificationsReadCommand(), ct));
}

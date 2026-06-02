using Learnexia.Modules.Notifications.Api.Bases;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Commands.UpdateMyPreferences;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Queries.GetMyPreferences;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.UpdateChildReengagementPreferences;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.GetChildReengagementPreferences;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Notifications.Api.Controllers;

/// <summary>
/// Notification-preference endpoints.
/// — Self-scoped (all roles): manage own per-category preferences (P2-12 BE-1).
/// — Parent-only: manage per-child re-engagement preferences (P4-09 B4-3 / AC3).
/// User ids are always resolved from the JWT inside handlers — never from request body or route
/// for self-scoped endpoints (no IDOR surface).
/// </summary>
[Route("api/Notifications/Preferences")]
[ApiController]
public sealed class PreferencesController : AppControllerBase
{
    // ── Self-scoped preference endpoints (all roles) ──────────────────────────────────────────

    /// <summary>
    /// Returns the authenticated user's notification preferences. Returns sensible defaults for all
    /// 4 categories when no rows have been saved yet — never 404, never persists on read.
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<NotificationPreferencesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPreferences()
        => NewResult(await Mediator.Send(new GetMyNotificationPreferencesQuery()));

    /// <summary>
    /// Upserts the authenticated user's notification preferences. All 4 categories must be supplied;
    /// the handler writes them atomically in an explicit transaction (no Unit of Work, ADR 0001).
    /// </summary>
    [Authorize]
    [HttpPut]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyPreferences([FromBody] UpdateMyNotificationPreferencesCommand command)
        => NewResult(await Mediator.Send(command));

    // ── Parent-only per-child re-engagement preference endpoints (P4-09 B4-3 / AC3) ──────────
    // Both routes are gated to [Authorize(Roles = "Parent")].
    // The handler enforces IsParentOfChildAsync(parentId, childId) — any mismatch returns a generic
    // Forbidden to prevent child-id enumeration (anti-IDOR + anti-enumeration, security-auditor C3/C4).

    /// <summary>
    /// Returns the authenticated parent's re-engagement preferences for the specified child
    /// (3 rows: StreakAtRisk, DailyMissionReminder, LapseWinBack). Synthesises defaults for
    /// any missing rows — nothing is written on read.
    /// </summary>
    [Authorize(Roles = "Parent")]
    [HttpGet("Children/{childId:int}/Reengagement")]
    [ProducesResponseType(typeof(BaseResponse<List<ChildReengagementPreferenceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetChildReengagementPreferences(
        int childId,
        CancellationToken ct)
        => NewResult(await Mediator.Send(new GetChildReengagementPreferencesQuery(childId), ct));

    /// <summary>
    /// Upserts the authenticated parent's re-engagement preferences for the specified child.
    /// Writes all 3 schedulable categories atomically. <c>DailyCap</c> must be [0..10].
    /// Quiet-hours: both Start and End must be supplied together, or both omitted.
    /// </summary>
    [Authorize(Roles = "Parent")]
    [HttpPut("Children/{childId:int}/Reengagement")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateChildReengagementPreferences(
        int childId,
        [FromBody] UpdateChildReengagementPreferencesCommand command,
        CancellationToken ct)
        => NewResult(await Mediator.Send(command with { ChildId = childId }, ct));
}

using Learnexia.Modules.Notifications.Api.Bases;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Commands.UpdateMyPreferences;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Queries.GetMyPreferences;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Notifications.Api.Controllers;

/// <summary>
/// Self-scoped notification-preference endpoints (P2-12 BE-1). The authenticated user's id is resolved
/// inside each handler from the JWT — it is never read from the request body or route (no IDOR surface).
/// </summary>
[Route("api/Notifications/Preferences")]
[ApiController]
public sealed class PreferencesController : AppControllerBase
{
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
}

using Learnexia.Modules.Notifications.Api.Bases;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RegisterDevice;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RevokeDevice;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Notifications.Api.Controllers;

/// <summary>
/// Device push-token registration endpoints (P4-09 B4-4 / AC4).
/// All routes are self-scoped: token ownership is tied to the authenticated user's JWT.
/// </summary>
[Route("api/Notifications/Devices")]
[ApiController]
[Authorize]
public sealed class DevicesController : AppControllerBase
{
    /// <summary>
    /// Registers or re-activates an Expo push token for the authenticated user.
    /// Idempotent: re-registering an existing token updates <c>LastUsedAtUtc</c> and re-activates
    /// if previously revoked. Token rotation (same token, different user) transfers ownership.
    /// </summary>
    [HttpPost("Register")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDeviceCommand command,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(command, ct));

    /// <summary>
    /// Deactivates (revokes) the push token identified by its integer ID.
    /// Self-scoped: only the token owner can revoke. Returns 404 when not found or not owned
    /// (anti-enumeration — no distinction between "not found" and "not yours").
    /// The route uses the integer PK rather than the raw push-token string to prevent token
    /// leakage in reverse-proxy logs and browser history (security fix F-01).
    /// </summary>
    [HttpDelete("{tokenId:int}")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(
        int tokenId,
        CancellationToken ct = default)
        => NewResult(await Mediator.Send(new RevokeDeviceCommand(tokenId), ct));
}

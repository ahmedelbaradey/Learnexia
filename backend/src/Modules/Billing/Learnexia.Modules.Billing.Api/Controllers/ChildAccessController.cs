using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.ChildAccess.Commands.PauseChild;
using Learnexia.Modules.Billing.Application.Features.ChildAccess.Commands.UnpauseChild;
using Learnexia.Modules.Billing.Application.Features.ChildAccess.Queries.GetChildAccessStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Parent-facing child AI-access control endpoints (P10-18-BE-7).
///
/// <para><strong>Parent-only:</strong> all endpoints require a valid parent JWT.
/// Child JWTs are rejected by the <c>[Authorize(Roles = "Parent")]</c> attribute.
/// <c>parentUserId</c> is always resolved from the JWT via <c>ICurrentUserService</c>
/// inside the handler — never from the URL or request body.</para>
///
/// <para><strong>IDOR protection:</strong> the service verifies that the <c>childId</c>
/// from the URL belongs to the authenticated parent's family before any write or read.
/// Cross-family access is rejected with HTTP 403.</para>
///
/// <para><strong>Routes:</strong>
/// <list type="bullet">
///   <item><c>POST api/Billing/Children/{childId}/Pause</c>       — pause a child's AI access.</item>
///   <item><c>POST api/Billing/Children/{childId}/Unpause</c>     — unpause a child's AI access.</item>
///   <item><c>GET  api/Billing/Children/{childId}/AccessStatus</c> — get current access status.</item>
/// </list>
/// </para>
/// </summary>
[Route("api/Billing/Children/{childId:int}")]
[ApiController]
[Authorize(Roles = "Parent")]
public sealed class ChildAccessController : AppControllerBase
{
    /// <summary>
    /// Pauses a child's AI access immediately.
    ///
    /// <para>The pause takes effect immediately. The child's energy balance, seat, and billing
    /// are all untouched. Pausing an already-paused child is a no-op (success).</para>
    /// </summary>
    /// <param name="childId">The child user id to pause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Pause")]
    public async Task<IActionResult> PauseChild(
        [FromRoute] int childId,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new PauseChildCommand(childId), cancellationToken));

    /// <summary>
    /// Unpauses a child's AI access immediately.
    ///
    /// <para>The child regains AI access immediately. Energy balance and seat are unchanged.
    /// Unpausing an already-active child is a no-op (success).</para>
    /// </summary>
    /// <param name="childId">The child user id to unpause.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Unpause")]
    public async Task<IActionResult> UnpauseChild(
        [FromRoute] int childId,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new UnpauseChildCommand(childId), cancellationToken));

    /// <summary>
    /// Returns the current AI access status for a child: seat state, parent-pause state, and
    /// the combined <c>IsAccessAllowed</c> flag.
    /// </summary>
    /// <param name="childId">The child user id to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("AccessStatus")]
    public async Task<IActionResult> GetChildAccessStatus(
        [FromRoute] int childId,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new GetChildAccessStatusQuery(childId), cancellationToken));
}

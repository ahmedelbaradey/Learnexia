using Learnexia.Modules.Moderation.Api.Bases;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Commands.ReviewItem;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Queries.GetItem;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Queries.GetQueue;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Moderation.Api.Controllers;

/// <summary>
/// Admin-only moderation queue API (P7-09).
///
/// Security constraints (P7-09 brief §security):
/// - All endpoints require <see cref="AuthorizationPolicies.AdminOnly"/> — non-admin → 403.
/// - No full-table loads: paged queries only.
/// - Review actor is sourced from JWT (not the request body) — no IDOR via body injection.
/// - v1: no raw-content preview (P3-02 quarantine store deferred — OQ-8).
/// </summary>
[Route("api/Admin/Moderation")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ModerationController : AppControllerBase
{
    /// <summary>
    /// Returns a paginated, filterable list of moderation queue items.
    /// All filtering (status, source, subjectCode, grade, dateFrom, dateTo, search) is DB-side.
    /// Results are ordered newest-first (by DetectedAt).
    /// </summary>
    [HttpGet("Queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetQueue([FromQuery] GetModerationQueueQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Returns the full detail of a single moderation queue item.
    /// v1: no raw-content preview; SafetyVerdict field carries the full safety signal (reason codes, checks).
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItem([FromRoute] int id)
        => NewResult(await Mediator.Send(new GetModerationItemQuery(id)));

    /// <summary>
    /// Reviews a moderation queue item: transitions it to Approved, Rejected, or Flagged.
    /// Reason is required when decision=Rejected.
    /// Only items in Pending or Flagged status may be reviewed; terminal items (Approved/Rejected) → 400.
    /// The review actor is sourced from the JWT — not the request body.
    /// </summary>
    [HttpPost("{id:int}/Review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review([FromRoute] int id, [FromBody] ReviewModerationItemRequest body)
        => NewResult(await Mediator.Send(new ReviewModerationItemCommand(id, body.Decision, body.Reason)));
}

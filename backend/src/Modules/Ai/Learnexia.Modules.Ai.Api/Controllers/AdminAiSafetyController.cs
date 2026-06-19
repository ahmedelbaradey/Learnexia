using Learnexia.Modules.Ai.Api.Bases;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetFlaggedOutputs;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetySignalSummary;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetyTrend;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Ai.Api.Controllers;

/// <summary>
/// Admin-only AI-safety monitoring dashboard (P7-11 buildable slice).
///
/// Security constraints (P7-11 brief §security):
/// - All endpoints require <see cref="AuthorizationPolicies.AdminOnly"/> — anonymous → 401, non-admin → 403.
/// - No full-table loads: paged queries only for flagged outputs; aggregate queries are date-windowed.
/// - No raw prompt/response text exposed — PII-light by design (P3-02 Q5/Q6).
/// - Eval results endpoint is omitted (deferred — blocked on P6-02).
/// - Tutor usage/cost endpoint is omitted (deferred — blocked on AiUsageLogs table decision OQ-2).
/// </summary>
[Route("api/Admin/AiSafety")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminAiSafetyController : AppControllerBase
{
    /// <summary>
    /// Returns aggregated safety-signal counts and breakdowns over a date range.
    /// Breakdowns: by ActionTaken, by ReasonCode, by ModelId, by TaskKind.
    /// Subject/language breakdowns are N/A in this slice (SafetyEvent has no subject/language column).
    /// </summary>
    [HttpGet("signals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSignalSummary([FromQuery] GetSafetySignalSummaryQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Returns a paged, newest-first list of flagged AI outputs for drill-in.
    /// PII-light: id (as content reference), taskKind, actionTaken, reasonCodes, modelId, occurredAt only.
    /// No raw prompt/response text; page size capped at 100.
    /// </summary>
    [HttpGet("flagged")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFlaggedOutputs([FromQuery] GetFlaggedOutputsQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Returns per-day safety event counts over a date range (for a trend chart).
    /// Each bucket shows total events and split by ActionTaken.
    /// </summary>
    [HttpGet("trend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTrend([FromQuery] GetSafetyTrendQuery query)
        => NewResult(await Mediator.Send(query));
}

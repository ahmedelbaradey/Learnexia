using Learnexia.Modules.Ai.Api.Bases;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetEvalResults;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetFlaggedOutputs;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetySignalSummary;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetyTrend;
using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Queries.GetTutorUsage;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Ai.Api.Controllers;

/// <summary>
/// Admin-only AI-safety monitoring dashboard (P7-11 + P6-02).
///
/// Security constraints (P7-11 brief §security):
/// - All endpoints require <see cref="AuthorizationPolicies.AdminOnly"/> — anonymous → 401, non-admin → 403.
/// - No full-table loads: paged queries only for flagged outputs; aggregate queries are date-windowed.
/// - No raw prompt/response text exposed — PII-light by design (P3-02 Q5/Q6).
/// - Eval results: served from the committed artifact (no DB; P6-02 Option 1 §C).
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

    /// <summary>
    /// Returns aggregated AI tutor usage/cost metrics over a date range (P7-11 usage/cost dashboard).
    /// Totals: calls, prompt tokens, completion tokens, estimated cost, avg latency, cache-hit rate.
    /// Breakdowns: by model, by task kind. Trend: per-day buckets.
    /// PII-light: token counts and cost only — no prompt/response text, no student identifiers.
    /// Empty windows return zeroed totals (HTTP 200, never 404).
    /// </summary>
    [HttpGet("usage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTutorUsage([FromQuery] GetTutorUsageQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Returns the latest offline AI-safety eval run result (P6-02 / P7-11-BE-3).
    ///
    /// Served from the committed <c>safety-eval-results.json</c> artifact produced by the
    /// <c>Ai.EvalTests</c> harness (no DB; P6-02 Option 1 §C). When no run has been performed
    /// yet, returns HTTP 200 with a bootstrap sentinel (breached=true, totalCases=0) —
    /// never 404 or 500.
    ///
    /// PII-light: no prompt/response text, no student identifiers — aggregate metrics only.
    ///
    /// <para><strong>CI green ≠ AI safety proven.</strong> The offline tier validates parse/map/fail-closed
    /// logic. Arabic moderation quality is validated by the live tier (Gate B, devops launch gate).</para>
    /// </summary>
    [HttpGet("evals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEvalResults()
        => NewResult(await Mediator.Send(new GetEvalResultsQuery()));
}

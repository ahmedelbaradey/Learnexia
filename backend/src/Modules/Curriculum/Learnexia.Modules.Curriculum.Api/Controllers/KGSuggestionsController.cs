using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.ApproveKGSuggestion;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.BuildKGSuggestions;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.RejectKGSuggestion;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Queries.ListKGSuggestions;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Api.Bases;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Curriculum.Api.Controllers;

/// <summary>
/// Admin-only endpoints for the KG suggestion review queue (BL-03-BE-7/8/9) and the
/// graph inference build trigger (BL-03-BE-3/BE-6).
///
/// <para>Routes:
/// <list type="bullet">
///   <item><c>GET  api/curriculum/kg-suggestions</c> — paged list of KG suggestions
///         (defaults to Pending only; filter by status).</item>
///   <item><c>POST api/curriculum/kg-suggestions/{id}/approve</c> — approve a suggestion,
///         publishing the edge via <see cref="Learnexia.Shared.Contracts.Learning.IKnowledgeEdgeWriter"/>.</item>
///   <item><c>POST api/curriculum/kg-suggestions/{id}/reject</c> — reject a suggestion (no edge written).</item>
///   <item><c>POST api/curriculum/kg-suggestions/build</c> — enqueue an infer_edges job for a subject+grade.</item>
/// </list>
/// </para>
///
/// <para>All endpoints are gated by <c>AuthorizationPolicies.AdminOnly</c>.</para>
/// </summary>
[Route("api/curriculum/kg-suggestions")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class KGSuggestionsController : AppControllerBase
{
    /// <summary>
    /// Returns a paged list of KG suggestions (BL-03-BE-7).
    ///
    /// <para>Defaults to Pending suggestions only. Pass <paramref name="status"/> to override.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] KGSuggestionStatus? status = null,
        [FromQuery] int? subjectCode = null,
        [FromQuery] int? gradeId = null,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new ListKGSuggestionsQuery
            {
                PageNumber  = pageNumber,
                PageSize    = pageSize,
                Status      = status,
                SubjectCode = subjectCode,
                GradeId     = gradeId,
            },
            cancellationToken));

    /// <summary>
    /// Approves a pending KG suggestion, publishing the edge to the learning module (BL-03-BE-8).
    ///
    /// <para>Returns:
    /// <list type="bullet">
    ///   <item>200 OK with the updated suggestion on success.</item>
    ///   <item>404 Not Found when the suggestion does not exist.</item>
    ///   <item>409 Conflict when the suggestion is already resolved.</item>
    ///   <item>422 Unprocessable when the edge would create a cycle or is cross-language.</item>
    ///   <item>401/403 when unauthenticated or non-admin.</item>
    /// </list>
    /// </para>
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] string? reviewNotes = null,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new ApproveKGSuggestionCommand(id, reviewNotes),
            cancellationToken));

    /// <summary>
    /// Rejects a pending KG suggestion (BL-03-BE-9).
    ///
    /// <para>No edge is published. Rejected suggestions are permanently withheld.
    /// Trigger a new <c>POST /build</c> to regenerate suggestions.</para>
    ///
    /// <para>Returns:
    /// <list type="bullet">
    ///   <item>200 OK with the updated suggestion on success.</item>
    ///   <item>404 Not Found when the suggestion does not exist.</item>
    ///   <item>409 Conflict when the suggestion is already resolved.</item>
    ///   <item>401/403 when unauthenticated or non-admin.</item>
    /// </list>
    /// </para>
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        int id,
        [FromBody] string? reviewNotes = null,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new RejectKGSuggestionCommand(id, reviewNotes),
            cancellationToken));

    /// <summary>
    /// Enqueues an <c>infer_edges</c> PipelineJob for the given subject+grade (BL-03-BE-3/BE-6).
    ///
    /// <para>The Python worker polls the job and writes inferred edge candidates to <c>ResultJson</c>.
    /// <see cref="Learnexia.Modules.Curriculum.Infrastructure.Jobs.EdgeInferenceAdvanceService"/> then
    /// claims Done jobs and writes <c>KGSuggestion{Pending}</c> rows.</para>
    ///
    /// <para>Admin-only / system-gated. Rate-limit this endpoint in production.</para>
    /// </summary>
    [HttpPost("build")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Build(
        [FromQuery] int subjectCode,
        [FromQuery] int gradeId,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new BuildKnowledgeGraphSuggestionsCommand(subjectCode, gradeId),
            cancellationToken));
}

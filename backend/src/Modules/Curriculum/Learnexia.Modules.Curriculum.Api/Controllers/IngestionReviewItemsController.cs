using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.ApproveIngestionReviewItem;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.RejectIngestionReviewItem;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Queries.GetIngestionReviewItems;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Api.Bases;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Curriculum.Api.Controllers;

/// <summary>
/// Admin-only endpoints for the ingestion review queue (BL-05-BE-10).
///
/// <para>Routes:
/// <list type="bullet">
///   <item><c>GET  api/curriculum/review-items</c> — paged list of ingestion review items
///         (defaults to Pending only; filter by documentId, status, versionId).</item>
///   <item><c>POST api/curriculum/review-items/{id}/approve</c> — approve an item, committing
///         the hierarchy node via <see cref="Learnexia.Shared.Contracts.Learning.IPedagogicalTreeWriter"/>.</item>
///   <item><c>POST api/curriculum/review-items/{id}/reject</c> — reject an item (withheld permanently).</item>
/// </list>
/// </para>
///
/// <para>All endpoints are gated by <c>AuthorizationPolicies.AdminOnly</c>.</para>
/// </summary>
[Route("api/curriculum/review-items")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class IngestionReviewItemsController : AppControllerBase
{
    /// <summary>
    /// Returns a paged list of ingestion review items (BL-05-BE-7).
    ///
    /// <para>Defaults to Pending items only. Pass <paramref name="status"/> to override.</para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? documentId = null,
        [FromQuery] ReviewStatus? status = null,
        [FromQuery] int? versionId = null,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new GetIngestionReviewItemsQuery
            {
                PageNumber = pageNumber,
                PageSize   = pageSize,
                DocumentId = documentId,
                Status     = status,
                VersionId  = versionId,
            },
            cancellationToken));

    /// <summary>
    /// Approves a pending ingestion review item (BL-05-BE-8).
    ///
    /// <para>The approved hierarchy node is committed to the Learning module via
    /// <see cref="Learnexia.Shared.Contracts.Learning.IPedagogicalTreeWriter"/>.</para>
    ///
    /// <para>Returns:
    /// <list type="bullet">
    ///   <item>200 OK with the updated review item on success.</item>
    ///   <item>404 Not Found when the item does not exist.</item>
    ///   <item>409 Conflict when the item is already resolved (Approved or Rejected).</item>
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
    public async Task<IActionResult> Approve(
        int id,
        [FromBody] string? reviewNotes = null,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new ApproveIngestionReviewItemCommand(id, reviewNotes),
            cancellationToken));

    /// <summary>
    /// Rejects a pending ingestion review item (BL-05-BE-8).
    ///
    /// <para>Rejected items are permanently withheld. Re-ingest the document
    /// (<c>POST api/curriculum/documents/{id}/reingest</c>) to regenerate.</para>
    ///
    /// <para>Returns:
    /// <list type="bullet">
    ///   <item>200 OK with the updated review item on success.</item>
    ///   <item>404 Not Found when the item does not exist.</item>
    ///   <item>409 Conflict when the item is already resolved (Approved or Rejected).</item>
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
            new RejectIngestionReviewItemCommand(id, reviewNotes),
            cancellationToken));
}

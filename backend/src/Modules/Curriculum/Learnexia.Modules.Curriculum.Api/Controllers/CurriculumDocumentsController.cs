using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.UploadCurriculumDocument;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Queries.GetCurriculumDocument;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Queries.ListCurriculumDocuments;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Api.Bases;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Curriculum.Api.Controllers;

/// <summary>
/// Admin-only endpoints for managing curriculum documents (BL-01 BE-8).
/// All endpoints are gated by <c>AuthorizationPolicies.AdminOnly</c>
/// (requires <c>Admin</c> or <c>SuperAdmin</c> role — mirror of P7 admin controllers).
///
/// <para>Routes:
/// <list type="bullet">
///   <item><c>POST  api/curriculum/documents</c> — upload a new curriculum document (multipart). Returns 201.</item>
///   <item><c>GET   api/curriculum/documents</c> — paged list with optional status/grade/subject filters.</item>
///   <item><c>GET   api/curriculum/documents/{id}</c> — full detail; 404 when not found.</item>
/// </list>
/// </para>
///
/// <para><strong>Upload size limit:</strong> the 100 MB cap is enforced at the ROUTE level only
/// via <c>[RequestSizeLimit]</c> and <c>[RequestFormLimits]</c> on the POST action — not globally.
/// Mirrors the plan's requirement to raise the limit per-route without changing global Kestrel limits.</para>
/// </summary>
[Route("api/curriculum/documents")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class CurriculumDocumentsController : AppControllerBase
{
    // 100 MB in bytes — matches CurriculumUploadConfiguration.MaxFileSizeBytes default.
    private const long MaxUploadBytes = 100L * 1024 * 1024;

    /// <summary>
    /// Upload a new curriculum document (PDF, DOCX, or image) with metadata.
    /// Returns 201 Created with the new document id and status = Received.
    ///
    /// The file is validated (type allow-list + magic-byte + size) in the handler.
    /// Invalid files return 422; storage errors return 500.
    ///
    /// The route-only size limit overrides the default Kestrel/IIS limit for this action only.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile file,
        [FromForm] int gradeId,
        [FromForm] int subjectId,
        [FromForm] ContentLanguage language,
        [FromForm] string? country,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(
            new UploadCurriculumDocumentCommand(file, gradeId, subjectId, language, country),
            cancellationToken));

    /// <summary>
    /// Returns a paged list of curriculum documents.
    /// Optionally filtered by <paramref name="status"/>, <paramref name="gradeId"/>,
    /// and/or <paramref name="subjectId"/>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DocumentStatus? status = null,
        [FromQuery] int? gradeId = null,
        [FromQuery] int? subjectId = null,
        CancellationToken cancellationToken = default)
        => NewResult(await Mediator.Send(
            new ListCurriculumDocumentsQuery
            {
                PageNumber = pageNumber,
                PageSize   = pageSize,
                Status     = status,
                GradeId    = gradeId,
                SubjectId  = subjectId,
            },
            cancellationToken));

    /// <summary>
    /// Returns full detail of a single curriculum document.
    /// Returns 404 when not found.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(
            new GetCurriculumDocumentQuery { Id = id },
            cancellationToken));
}

using Learnexia.Modules.Curriculum.Api.Bases;
using Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Curriculum.Api.Controllers;

/// <summary>
/// Dev-only HTTP endpoint exposing <see cref="RetrieveChunksQuery"/> for api-tester integration.
/// Validates: filter correctness (grade/subject/skill + Active version), empty-result path,
/// and latency sanity check.
///
/// <para>This is a development convenience endpoint per OQ-5. In-process callers (<c>RagContextProvider</c>)
/// use <c>RetrieveChunksQuery</c> directly via MediatR — they do NOT call this HTTP endpoint.
/// The endpoint is behind <c>[Authorize]</c> so it never leaks corpus content to anonymous callers.</para>
/// </summary>
[Route("api/Curriculum")]
public sealed class RetrievalController : AppControllerBase
{
    /// <summary>
    /// Retrieves the top-k most semantically similar curriculum chunks for a given text query,
    /// filtered by grade, subject, and optionally skill (weak-area filter).
    /// Returns an empty <c>Chunks</c> list when no chunk clears the similarity floor (AC-4).
    /// </summary>
    [Authorize]
    [HttpGet("Retrieve")]
    public async Task<IActionResult> Retrieve(
        [FromQuery] string text,
        [FromQuery] int gradeId,
        [FromQuery] int subjectId,
        [FromQuery] int? skillId,
        [FromQuery] int topK = 5)
        => NewResult(await Mediator.Send(new RetrieveChunksQuery
        {
            Text            = text ?? string.Empty,
            GradeId         = gradeId,
            SubjectId       = subjectId,
            WeakAreaSkillId = skillId,
            TopK            = topK,
        }));
}

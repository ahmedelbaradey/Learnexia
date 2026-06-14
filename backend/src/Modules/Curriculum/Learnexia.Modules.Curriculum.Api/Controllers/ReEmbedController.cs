using Learnexia.Modules.Curriculum.Api.Bases;
using Learnexia.Modules.Curriculum.Application.Features.ReEmbed.Commands.TriggerReEmbed;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Curriculum.Api.Controllers;

/// <summary>
/// Admin-only endpoint that triggers the <c>ReEmbedCurriculumJob</c> Hangfire job.
///
/// <para><strong>WI-A2 (P3-07-BE-0 buildable side):</strong>
/// Allows an admin to kick off re-embedding of all <c>chunk_embeddings_bge_m3</c> rows whose
/// <c>ModelVersion</c> does not match the currently configured BGE-M3 TEI model version.
/// The job is idempotent — re-running it when all rows are up-to-date is a safe no-op.</para>
///
/// <para><strong>Precondition:</strong>
/// The BGE-M3 TEI endpoint must be configured (<c>Curriculum:Embedding:BaseUrl</c> +
/// <c>Curriculum:Embedding:ModelVersion</c>). If not configured the command returns HTTP 400
/// with a clear error message so the admin knows what to provision first.</para>
///
/// <para><strong>After the job completes, the admin must:</strong>
/// 1. Verify placeholder row count = 0 (query <c>chunk_embeddings_bge_m3</c> for
///    <c>ModelVersion = 'seed-placeholder-v0'</c>).
/// 2. Re-calibrate <c>Curriculum:Retrieval:SimilarityDistanceFloor</c> against the real corpus.
/// 3. Set <c>AiHelper:ContextProvider = "Rag"</c> to activate live grounding.</para>
/// </summary>
[Route("api/Admin/Curriculum")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class ReEmbedController : AppControllerBase
{
    /// <summary>
    /// POST api/Admin/Curriculum/ReEmbed
    ///
    /// Enqueues the <c>ReEmbedCurriculumJob</c> and returns the Hangfire job ID.
    /// The job processes all stale embedding rows in the background without blocking the response.
    /// </summary>
    [HttpPost("ReEmbed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TriggerReEmbed(CancellationToken ct)
        => NewResult(await Mediator.Send(new TriggerReEmbedCommand(), ct));
}

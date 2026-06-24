using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.AddEdge;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Commands.RemoveEdge;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetGraph;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetPrerequisites;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetRelatedConcepts;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetRemediationPath;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetUnlockedBy;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Exposes the skill dependency graph query and admin authoring endpoints.
///
/// Read endpoints (any authenticated user):
///   GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId}
///   GET /api/Learning/KnowledgeGraph/UnlockedBy/{nodeId}
///   GET /api/Learning/KnowledgeGraph/RelatedConcepts/{nodeId}  [BL-03-BE-4]
///   GET /api/Learning/KnowledgeGraph/RemediationPath/{nodeId}  [BL-03-BE-5]
///
/// Admin read endpoints (AdminOnly):
///   GET /api/Learning/KnowledgeGraph/Graph?subjectId={subjectId}
///
/// Admin write endpoints (AdminOnly):
///   POST /api/Learning/KnowledgeGraph/Edges
///   DELETE /api/Learning/KnowledgeGraph/Edges/{edgeId}
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
public class KnowledgeGraphController : AppControllerBase
{
    // ── Read endpoints (any authenticated user) ──────────────────────────────

    /// <summary>
    /// Returns the prerequisite nodes for the given knowledge node
    /// (nodes that must be mastered before <paramref name="nodeId"/>).
    /// </summary>
    [HttpGet("Prerequisites/{nodeId:int}")]
    [Authorize]
    public async Task<IActionResult> GetPrerequisites(int nodeId)
        => NewResult(await Mediator.Send(new GetPrerequisitesQuery(nodeId)));

    /// <summary>
    /// Returns the nodes unlocked by mastering the given knowledge node
    /// (nodes that become accessible once <paramref name="nodeId"/> is mastered).
    /// </summary>
    [HttpGet("UnlockedBy/{nodeId:int}")]
    [Authorize]
    public async Task<IActionResult> GetUnlockedBy(int nodeId)
        => NewResult(await Mediator.Send(new GetUnlockedByQuery(nodeId)));

    /// <summary>
    /// Returns concepts related (via <c>EdgeRelationshipType.Related</c> edges, either direction)
    /// to the given knowledge node. Student-facing — inactive-skill nodes are filtered out.
    /// (BL-03-BE-4)
    ///
    /// GET /api/Learning/KnowledgeGraph/RelatedConcepts/{nodeId}
    /// </summary>
    [HttpGet("RelatedConcepts/{nodeId:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRelatedConcepts(int nodeId)
        => NewResult(await Mediator.Send(new GetRelatedConceptsQuery(nodeId)));

    /// <summary>
    /// Returns the transitive prerequisite chain (remediation path) for a struggling skill.
    /// Given a node id, walks upstream BFS through Prerequisite edges up to
    /// <c>KnowledgeGraph:RemediationMaxDepth</c> levels (default 3). Student-facing.
    /// (BL-03-BE-5)
    ///
    /// GET /api/Learning/KnowledgeGraph/RemediationPath/{nodeId}
    /// </summary>
    [HttpGet("RemediationPath/{nodeId:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRemediationPath(int nodeId)
        => NewResult(await Mediator.Send(new GetRemediationPathQuery(nodeId)));

    // ── Admin read endpoints ──────────────────────────────────────────────────

    /// <summary>
    /// Returns the full skill dependency graph (nodes + edges) for a subject.
    /// Admin view: includes nodes wrapping inactive skills (flagged in the response).
    /// GET /api/Learning/KnowledgeGraph/Graph?subjectId=1
    /// </summary>
    [HttpGet("Graph")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetGraph([FromQuery] int subjectId)
        => NewResult(await Mediator.Send(new GetGraphQuery { SubjectId = subjectId }));

    // ── Admin write endpoints ─────────────────────────────────────────────────

    /// <summary>
    /// Adds a directed edge between two existing knowledge nodes.
    /// Rejects cross-language edges and edges that would create a cycle.
    /// POST /api/Learning/KnowledgeGraph/Edges
    /// </summary>
    [HttpPost("Edges")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> AddEdge([FromBody] AddKnowledgeEdgeCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Soft-deletes a knowledge edge by ID.
    /// DELETE /api/Learning/KnowledgeGraph/Edges/{edgeId}
    /// </summary>
    [HttpDelete("Edges/{edgeId:int}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> RemoveEdge(int edgeId)
        => NewResult(await Mediator.Send(new RemoveKnowledgeEdgeCommand { EdgeId = edgeId }));
}

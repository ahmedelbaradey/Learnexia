using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetPrerequisites;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Queries.GetUnlockedBy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Exposes the skill dependency graph query endpoints (P2-11 BE-5).
/// Routes:
///   GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId}
///   GET /api/Learning/KnowledgeGraph/UnlockedBy/{nodeId}
/// Both endpoints require an authenticated bearer token.
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
public class KnowledgeGraphController : AppControllerBase
{
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
}

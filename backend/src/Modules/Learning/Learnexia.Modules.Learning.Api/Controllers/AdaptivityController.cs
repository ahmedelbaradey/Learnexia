using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Adaptivity.Dtos;
using Learnexia.Modules.Learning.Application.Features.Adaptivity.Queries.GetAdaptivityDecision;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Adaptivity inspection endpoint — student-scoped, IDOR-safe.
/// StudentId is ALWAYS resolved from the authenticated JWT (ICurrentUserService.UserId)
/// and NEVER accepted from route parameters or query strings.
/// Route: api/Learning/Adaptivity
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
[Authorize(Roles = "Student")]
public class AdaptivityController : AppControllerBase
{
    /// <summary>
    /// Returns the current adaptive difficulty decision for the authenticated student on a given skill.
    ///
    /// Cold-start: if the student has no answer history for the skill, returns Medium + isDefault=true.
    /// This endpoint is the AC4 inspection seam (P3-08). P3-11 quiz selection calls
    /// IAdaptivityService.GetTargetDifficulty directly (in-process, no HTTP hop).
    /// </summary>
    /// <param name="skillId">The skill for which the adaptive difficulty is requested.</param>
    [HttpGet("Skill/{skillId:int}")]
    [ProducesResponseType(typeof(BaseResponse<AdaptivityDecisionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAdaptivityDecision([FromRoute] int skillId)
        => NewResult(await Mediator.Send(new GetAdaptivityDecisionQuery { SkillId = skillId }));
}

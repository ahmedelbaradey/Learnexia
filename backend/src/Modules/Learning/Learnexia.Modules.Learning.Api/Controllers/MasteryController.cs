using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Modules.Learning.Application.Features.Mastery.Queries.GetSkillMastery;
using Learnexia.Modules.Learning.Application.Features.Mastery.Queries.GetStudentMastery;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Mastery endpoints — student-scoped, IDOR-safe.
/// StudentId is ALWAYS resolved from the authenticated JWT (ICurrentUserService.UserId)
/// and NEVER accepted from route parameters or query strings.
/// Route: api/Learning/Mastery
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
[Authorize(Roles = "Student")]
public class MasteryController : AppControllerBase
{
    /// <summary>
    /// Returns all per-skill mastery rows for the authenticated student.
    /// An empty list is a valid response (200) — the student has not yet completed any skill attempts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<List<MasteryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStudentMastery()
        => NewResult(await Mediator.Send(new GetStudentMasteryQuery()));

    /// <summary>
    /// Returns the mastery row for a single skill for the authenticated student.
    /// If the student has not yet attempted the skill (or the skillId is unknown),
    /// returns a NotStarted default — never 404 or 500.
    /// </summary>
    /// <param name="skillId">The skill whose mastery is requested.</param>
    [HttpGet("Skill/{skillId:int}")]
    [ProducesResponseType(typeof(BaseResponse<MasteryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSkillMastery([FromRoute] int skillId)
        => NewResult(await Mediator.Send(new GetSkillMasteryQuery { SkillId = skillId }));
}

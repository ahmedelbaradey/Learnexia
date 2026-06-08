using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetSkillStats;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.SetActive;
using Learnexia.Modules.Learning.Application.Features.Skills.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Skills.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class SkillsController : AppControllerBase
{
    /// <summary>
    /// Admin-only flat list of all skills (including inactive ones). Paginated.
    /// Anonymous access is intentionally blocked — this endpoint exposes admin metadata
    /// (MasteryThreshold, IsActive, ConceptId). Students do not use this endpoint directly.
    /// P7-SEC: gated post security-audit (mirrors Subjects/Units/Lessons List contract).
    /// </summary>
    [HttpGet("List")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> List([FromQuery] ListSkillsQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Admin-only: returns a single skill by ID.
    /// P7-SEC: gated to prevent leaking admin skill metadata to anonymous callers.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetSkillQuery { Id = id }));

    [HttpPost("Create")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] AddSkillCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update([FromBody] EditSkillCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteSkillCommand { Id = id }));

    /// <summary>
    /// Activates or deactivates a skill. Inactive skills are hidden from student reads.
    /// PUT /api/learning/Skills/{id}/Active
    /// Body: { skillId: 1, isActive: false }
    /// </summary>
    [HttpPut("{id:int}/Active")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetSkillActiveCommand command)
        => NewResult(await Mediator.Send(command with { SkillId = id }));

    /// <summary>
    /// Returns per-student, per-skill aggregated statistics (accuracy, avg time, hint usage).
    /// StudentId is supplied via query string and cross-checked against the JWT (IDOR guard).
    /// Zero-data case returns zeroed stats (200) — never 404 or 500.
    /// </summary>
    /// <param name="skillId">The skill whose stats are requested.</param>
    /// <param name="studentId">The identity user ID of the student.</param>
    [HttpGet("{skillId}/Stats")]
    [Authorize]
    public async Task<IActionResult> GetSkillStats([FromRoute] int skillId, [FromQuery] int studentId)
        => NewResult(await Mediator.Send(new GetSkillStatsQuery { SkillId = skillId, StudentId = studentId }));
}

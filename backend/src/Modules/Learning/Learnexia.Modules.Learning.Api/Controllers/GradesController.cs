using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Grades.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Grades.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Curriculum grades endpoints.
///
/// Auth:
///   Reads (List, GetById)  — any authenticated user (class-level [Authorize]).
///   Writes (Create, Update, Delete) — Admin or SuperAdmin only
///   ([Authorize(Policy = AuthorizationPolicies.AdminOnly)]).
/// </summary>
[Route("api/learning/[controller]")]
[ApiController]
[Authorize]
public class GradesController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListGradesQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetGradeQuery { Id = id }));

    [HttpPost("Create")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] AddGradeCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update([FromBody] EditGradeCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteGradeCommand { Id = id }));
}

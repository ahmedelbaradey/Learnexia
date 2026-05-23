using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Grades.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Grades.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Grades.Queries.List;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
// AuthZ deliberately omitted for P2-01 (policies generated via Claims but not enforced by default;
// see brief/plan reviewer gate). Add [Authorize] when curriculum-authoring access control ships.
public class GradesController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListGradesQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetGradeQuery { Id = id }));

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] AddGradeCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] EditGradeCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteGradeCommand { Id = id }));
}

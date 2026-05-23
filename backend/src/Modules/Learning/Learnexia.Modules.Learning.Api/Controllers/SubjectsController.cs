using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.List;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class SubjectsController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListSubjectsQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetSubjectQuery { Id = id }));

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] AddSubjectCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] EditSubjectCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteSubjectCommand { Id = id }));
}

using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Lessons.Queries.List;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class LessonsController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListLessonsQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetLessonQuery { Id = id }));

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] AddLessonCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] EditLessonCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteLessonCommand { Id = id }));
}

using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Skills.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Skills.Queries.List;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class SkillsController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListSkillsQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetSkillQuery { Id = id }));

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromBody] AddSkillCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    public async Task<IActionResult> Update([FromBody] EditSkillCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteSkillCommand { Id = id }));
}

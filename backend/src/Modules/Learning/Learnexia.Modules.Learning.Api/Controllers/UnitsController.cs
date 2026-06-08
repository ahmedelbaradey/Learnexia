using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Units.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Units.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class UnitsController : AppControllerBase
{
    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] ListUnitsQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
        => NewResult(await Mediator.Send(new GetUnitQuery { Id = id }));

    [HttpPost("Create")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] AddUnitCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Update([FromBody] EditUnitCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(int id)
        => NewResult(await Mediator.Send(new DeleteUnitCommand { Id = id }));
}

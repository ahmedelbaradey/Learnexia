using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Delete;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.Reorder;
using Learnexia.Modules.Learning.Application.Features.Units.Commands.SetActive;
using Learnexia.Modules.Learning.Application.Features.Units.Queries.Get;
using Learnexia.Modules.Learning.Application.Features.Units.Queries.List;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

[Route("api/learning/[controller]")]
[ApiController]
public class UnitsController : AppControllerBase
{
    [HttpGet("List")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> List([FromQuery] ListUnitsQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
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

    // ── P7-01 Admin management endpoints ─────────────────────────────────────

    /// <summary>
    /// Batch-reorders units within the same Subject.
    /// PUT /api/learning/Units/Reorder
    /// Body: { unitIds: [3, 1, 2] } — position = new SequenceOrder.
    /// </summary>
    [HttpPut("Reorder")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reorder([FromBody] ReorderUnitsCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Activates or deactivates a unit. Inactive units are hidden from student reads.
    /// PUT /api/learning/Units/{id}/Active
    /// Body: { unitId: 1, isActive: false }
    /// </summary>
    [HttpPut("{id:int}/Active")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetUnitActiveCommand command)
        => NewResult(await Mediator.Send(command with { UnitId = id }));
}

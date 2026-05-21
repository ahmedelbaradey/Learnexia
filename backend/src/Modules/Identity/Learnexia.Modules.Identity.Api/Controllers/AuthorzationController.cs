using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.AddRole;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.DeleteRole;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Commands.EditRole;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetClaimList;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetRoleById;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetRoleList;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

[Route("api/Users/[controller]")]
[ApiController]
public class AuthorzationController : AppControllerBase
{
    [HttpGet("RoleList")]
    [ProducesResponseType(typeof(BaseResponse<List<GetRoleListResponse>>), StatusCodes.Status201Created)]
    public async Task<IActionResult> GetRoleList()
        => NewResult(await Mediator.Send(new GetRoleListQuery()));

    [HttpGet]
    public async Task<IActionResult> GetRoleById(int id)
        => NewResult(await Mediator.Send(new GetRoleByIdQuery { Id = id }));

    [HttpGet("CalimList")]
    public async Task<IActionResult> ClaimList()
        => NewResult(await Mediator.Send(new GetClaimListQuery()));

    [HttpPost("Create")]
    public async Task<IActionResult> CreateRole([FromBody] AddRoleCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut("Update")]
    public async Task<IActionResult> EditRole([FromBody] EditRoleCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> DeleteRole(int id)
        => NewResult(await Mediator.Send(new DeleteRoleCommand { Id = id }));
}

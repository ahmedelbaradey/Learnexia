using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.AddChild;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;
using Learnexia.Modules.Identity.Application.Features.Family.Queries.ListMyChildren;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

// Parent-facing family endpoints. Deliberately a SEPARATE controller from UserManagementController:
// that one is class-level AdminOnly, and a parent linking their own child is NOT an admin action.
// This controller is gated to the Parent role (Admin/SuperAdmin may also call, e.g. support flows).
// Role names match the Roles enum's PascalCase, which is what the JWT emits (RoleSeeder standardizes
// to PascalCase and the AdminOnly policy compares ordinally). Unauthenticated callers get 401; an
// authenticated user without one of these roles gets 403. The acting parent is always resolved from
// the JWT inside the handlers (ICurrentUserService) — never from the request body.
[Authorize(Roles = "Parent,Admin,SuperAdmin")]
[Route("api/Users/Parent")]
[ApiController]
public class ParentController : AppControllerBase
{
    // Parent provisions a new child (Student-role) account and is auto-linked to it. The acting
    // parent is resolved from the JWT inside the handler — there is no ParentId/Role on the command.
    [HttpPost("Add-Child")]
    [ProducesResponseType(typeof(BaseResponse<AddedChildResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddChild([FromBody] AddChildCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPost("Link-Child")]
    [ProducesResponseType(typeof(BaseResponse<LinkedChildResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LinkChild([FromBody] LinkChildCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpGet("My-Children")]
    [ProducesResponseType(typeof(BaseResponse<IEnumerable<LinkedChildResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyChildren()
        => NewResult(await Mediator.Send(new ListMyChildrenQuery()));
}

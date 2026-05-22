using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Authentications.Queries.GetMe;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

// Self-scoped current-user endpoint. Authenticated-only (NOT role-gated): both parents and children
// call GET /api/Users/Me to learn their own role/language/onboarding state for the routing guard.
// The user is always resolved from the JWT inside the handler (ICurrentUserService) — there is no
// id parameter, so no IDOR surface. Separate from the AdminOnly UserManagementController by design.
[Route("api/Users")]
[ApiController]
public class UsersController : AppControllerBase
{
    [Authorize]
    [HttpGet("Me")]
    [ProducesResponseType(typeof(BaseResponse<MeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
        => NewResult(await Mediator.Send(new GetMeQuery()));
}

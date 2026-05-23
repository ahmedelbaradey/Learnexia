using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyProfile;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMyProfile;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

// Self-scoped account-profile endpoints (P1-12 BE-1). Authenticated-only ([Authorize]): the user is
// always resolved from the JWT inside the handlers (ICurrentUserService) — there is no id parameter
// on the route or body, so there is no IDOR surface and no mass-assignment of id/email/role.
[Route("api/Users/Account")]
[ApiController]
public class AccountController : AppControllerBase
{
    // Reads the authenticated user's own profile (fullName, phone, country, avatarUrl).
    [Authorize]
    [HttpGet("Profile")]
    [ProducesResponseType(typeof(BaseResponse<AccountProfileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile()
        => NewResult(await Mediator.Send(new GetMyProfileQuery()));

    // Updates the authenticated user's own profile (fullName, phone, country). Returns the updated
    // profile. Avatar is read-only here (set by the avatar-upload endpoint, BE-4).
    [Authorize]
    [HttpPut("Profile")]
    [ProducesResponseType(typeof(BaseResponse<AccountProfileResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateMyProfileCommand command)
        => NewResult(await Mediator.Send(command));
}

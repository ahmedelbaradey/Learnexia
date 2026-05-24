using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.GoogleSignIn;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RegisterParent;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignIn;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.SignOut;
using Learnexia.Modules.Identity.Application.Features.Authentications.Queries.ValidateAccessToken;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

[Route("api/Users/[controller]")]
[ApiController]
public class AuthenticationController : AppControllerBase
{
    [AllowAnonymous]
    [HttpPost("Register-Parent")]
    [ProducesResponseType(typeof(BaseResponse<JwtAuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterParent([FromBody] RegisterParentCommand command)
        => NewResult(await Mediator.Send(command));

    [AllowAnonymous]
    [HttpPost("Sign-In")]
    [ProducesResponseType(typeof(BaseResponse<JwtAuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SignIn([FromBody] SignInCommand command)
        => NewResult(await Mediator.Send(command));

    [AllowAnonymous]
    [HttpPost("Google-SignIn")]
    [ProducesResponseType(typeof(BaseResponse<JwtAuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoogleSignIn([FromBody] GoogleSignInCommand command)
        => NewResult(await Mediator.Send(command));

    [AllowAnonymous]
    [HttpPost("Validate-Token")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateToken([FromBody] AccessTokenQuery query)
        => NewResult(await Mediator.Send(query));

    [AllowAnonymous]
    [HttpPost("Refresh-Token")]
    [ProducesResponseType(typeof(BaseResponse<JwtAuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
        => NewResult(await Mediator.Send(command));

    [Authorize]
    [HttpPost("Sign-Out")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SignOut([FromBody] SignOutCommand command)
        => NewResult(await Mediator.Send(command));
}

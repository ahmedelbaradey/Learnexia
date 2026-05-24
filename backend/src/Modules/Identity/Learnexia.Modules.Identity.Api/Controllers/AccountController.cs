using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Account.Commands.RemoveAvatar;
using Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyProfile;
using Learnexia.Modules.Identity.Application.Features.Account.Commands.UploadAvatar;
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

    // Uploads the authenticated user's avatar (multipart/form-data). The handler validates content-type
    // (png/jpeg/webp), size (<= configurable MaxFileSize), and the actual magic-byte signature, then
    // stores the object in the PRIVATE avatars bucket under a GUID object key (with the detected
    // content-type) and saves THAT KEY in AvatarUrl. Reads presign the key into a short-lived GET URL.
    // Invalid-file rejections return 422 (UnprocessableEntity).
    [Authorize]
    [HttpPost("Avatar")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BaseResponse<AvatarUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<AvatarUploadResponse>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
        => NewResult(await Mediator.Send(new UploadAvatarCommand(file)));

    // Removes the authenticated user's avatar: clears AvatarUrl (the stored object key). The backing
    // object is intentionally LEFT in storage — object DELETE is not part of the MVP storage contract
    // (orphan accepted; follow-up: delete + sweep). Self only.
    [Authorize]
    [HttpDelete("Avatar")]
    [ProducesResponseType(typeof(BaseResponse<AvatarUploadResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAvatar()
        => NewResult(await Mediator.Send(new RemoveAvatarCommand()));
}

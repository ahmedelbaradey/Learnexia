using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.Profile.Queries.GetMyProfile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Exposes the student's XP profile.
/// Route: GET api/Gamification/Profile
///
/// StudentId is resolved exclusively from the JWT (_currentUser.UserId) — no route/query param.
/// IDOR is structurally impossible: there is no client-supplied identity parameter.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class GamificationController : AppControllerBase
{
    /// <summary>
    /// Returns the XP profile for the currently authenticated student.
    /// Brand-new students (no profile yet) receive a zero-state response: XP=0, Level=1.
    /// </summary>
    [HttpGet("Profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
        => NewResult(await Mediator.Send(new GetMyProfileQuery()));
}

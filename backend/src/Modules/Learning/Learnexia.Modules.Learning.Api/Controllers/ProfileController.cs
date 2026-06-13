using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Profile.Dtos;
using Learnexia.Modules.Learning.Application.Features.Profile.Queries.GetStudentProfile;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Behavioral student profile inspection endpoint (P3-13 BE-8 / AC3 / AC5).
///
/// IDOR safety (AC5):
/// - StudentId is ALWAYS resolved from the JWT claim by the query handler via ICurrentUserService.
/// - StudentId is NEVER accepted from a route parameter, query string, or request body.
/// - There is structurally no client-supplied identity parameter on any action here.
///
/// Authorization: [Authorize(Roles = "Student")] — only the owning student may inspect their
/// own profile. Purpose-limited: the profile is for personalization (prompts/adaptivity) and
/// must not be exposed to other children, parents, or roles.
///
/// Route: GET api/Learning/Profile
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
[Authorize(Roles = "Student")]
public class ProfileController : AppControllerBase
{
    /// <summary>
    /// Returns the behavioral learning profile for the JWT-authenticated student.
    ///
    /// DATA MINIMIZATION: the response exposes only the four AC1 derived attributes plus
    /// DataPointCount. No raw answer data, no individual timestamps, no raw session logs.
    ///
    /// Cold-start (AC4): always returns HTTP 200. When DataPointCount == 0 the student has
    /// insufficient data and all attributes are at neutral defaults.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<StudentLearningProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile()
        => NewResult(await Mediator.Send(new GetStudentProfileQuery()));
}

using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Exposes the student's current daily + weekly mission collection.
/// Route: GET api/Gamification/Missions/Me
///
/// StudentId is resolved exclusively from the JWT (_currentUser.UserId) — no route/query param.
/// IDOR is structurally impossible: there is no client-supplied identity parameter.
///
/// The controller sits in the same assembly as <see cref="GamificationController"/>;
/// <c>AddApplicationPart(typeof(GamificationController).Assembly)</c> in <c>GamificationModule</c>
/// already covers this controller — no additional wire-up needed. Mirrors <see cref="BadgesController"/>.
/// </summary>
[Route("api/Gamification/[controller]")]
[ApiController]
[Authorize]
public sealed class MissionsController : AppControllerBase
{
    /// <summary>
    /// Returns the current daily + weekly missions for the authenticated student.
    ///
    /// Daily missions are instantiated lazily on first call per day. Weekly missions are
    /// instantiated lazily on first call per ISO week. Brand-new students (no XP profile yet)
    /// receive empty lists.
    ///
    /// No studentId accepted from the request.
    /// </summary>
    [HttpGet("Me")]
    [ProducesResponseType(typeof(BaseResponse<MyMissionsResponse>), 200)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => NewResult(await Mediator.Send(new GetMyMissionsQuery(), ct));
}

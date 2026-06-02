using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Exposes the student's current weekly league standings.
/// Route: GET api/Gamification/Leagues/Me
///
/// StudentId is resolved exclusively from the JWT (_currentUser.UserId) — no route/query param.
/// IDOR is structurally impossible: there is no client-supplied identity parameter.
///
/// The controller sits in the same assembly as <see cref="GamificationController"/>;
/// <c>AddApplicationPart(typeof(GamificationController).Assembly)</c> in <c>GamificationModule</c>
/// already covers this controller — no additional wire-up needed. Mirrors <see cref="MissionsController"/>.
/// </summary>
[Route("api/Gamification/Leagues")]
[ApiController]
[Authorize]
public sealed class LeaguesController : AppControllerBase
{
    /// <summary>
    /// Returns the full cohort standings for the current week for the authenticated student.
    ///
    /// League membership is lazy-instantiated on first call per week (D12 / AC1). Brand-new
    /// students with no XP profile yet receive an empty standings list.
    ///
    /// Standings are ordered by WeeklyXp DESC, JoinedAtUtc ASC (D4 tiebreak).
    /// Display names are anonymized as "Student #N" (D2 — no PII exposed for child accounts).
    ///
    /// No studentId accepted from the request.
    /// </summary>
    [HttpGet("Me")]
    [ProducesResponseType(typeof(BaseResponse<MyLeagueResponse>), 200)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => NewResult(await Mediator.Send(new GetMyLeagueQuery(), ct));
}

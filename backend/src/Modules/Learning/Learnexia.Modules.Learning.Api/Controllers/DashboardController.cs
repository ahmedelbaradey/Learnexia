using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;
using Learnexia.Modules.Learning.Application.Features.Dashboard.Queries.GetDashboard;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Returns the home dashboard for the JWT-resolved student.
/// Route: GET api/Learning/Dashboard
///
/// XP/Streak are 0 and DailyMission/LeaguePreview are null in Phase 2.
/// Phase-4 wiring: P4-02 (Xp), P4-03 (Streak), P4-06 (DailyMission), P4-07 (LeaguePreview).
/// Continue points at the next Available lesson; may be null for new students with no Available
/// lesson in any Grade-1 subject.
///
/// StudentId is resolved exclusively from the JWT (_currentUser.UserId) — no route/query param.
/// IDOR is structurally impossible: there is no client-supplied identity parameter.
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
public class DashboardController : AppControllerBase
{
    /// <summary>
    /// Returns the home dashboard for the currently authenticated student.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(BaseResponse<DashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get()
        => NewResult(await Mediator.Send(new GetDashboardQuery()));
}

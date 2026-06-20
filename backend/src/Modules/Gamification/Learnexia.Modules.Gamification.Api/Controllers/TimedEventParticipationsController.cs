using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.GetMyTimedEventParticipations;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Student-facing endpoint for timed-event participation progress (P4-12, BE-8).
/// Route: GET api/Gamification/TimedEventParticipations/Me
///
/// StudentId is resolved exclusively from the JWT (<c>ICurrentUserService.UserId</c>) —
/// no route/query param. IDOR is structurally impossible: there is no client-supplied identity parameter.
///
/// The controller sits in the same assembly as <see cref="GamificationController"/>;
/// <c>AddApplicationPart(typeof(GamificationController).Assembly)</c> in <c>GamificationModule</c>
/// already covers this controller — no additional wire-up needed.
/// Mirrors <see cref="MissionsController"/> shape (P4-12).
/// </summary>
[Route("api/Gamification/[controller]")]
[ApiController]
[Authorize]
public sealed class TimedEventParticipationsController : AppControllerBase
{
    /// <summary>
    /// Returns all active timed-event participation snapshots for the authenticated student.
    ///
    /// An "active" participation is one whose <c>EventEndUtc</c> is still in the future.
    /// Returns an empty array for students who have not yet joined any active timed event.
    ///
    /// No studentId accepted from the request.
    /// </summary>
    [HttpGet("Me")]
    [ProducesResponseType(typeof(BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>), 200)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => NewResult(await Mediator.Send(new GetMyTimedEventParticipationsQuery(), ct));
}

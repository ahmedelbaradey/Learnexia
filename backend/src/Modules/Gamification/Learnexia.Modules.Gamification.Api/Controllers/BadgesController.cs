using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Exposes the student's badge collection.
/// Route: GET api/Gamification/Badges/Me
///
/// StudentId is resolved exclusively from the JWT (_currentUser.UserId) — no route/query param.
/// IDOR is structurally impossible: there is no client-supplied identity parameter.
///
/// The controller sits in the same assembly as <see cref="GamificationController"/>;
/// <c>AddApplicationPart(typeof(GamificationController).Assembly)</c> in <c>GamificationModule</c>
/// already covers this controller — no additional wire-up needed.
/// </summary>
[Route("api/Gamification/[controller]")]
[ApiController]
[Authorize]
public sealed class BadgesController : AppControllerBase
{
    /// <summary>
    /// Returns the full badge catalog (all 10 definitions) with earned/locked state for the
    /// currently authenticated student. Ordered by Rarity ASC, SortOrder ASC.
    ///
    /// Earned badges have <c>IsEarned = true</c> and a non-null <c>AwardedAtUtc</c>.
    /// Locked badges have <c>IsEarned = false</c> and <c>AwardedAtUtc = null</c>.
    ///
    /// Brand-new students see all 10 badges as locked. No studentId accepted from the request.
    /// </summary>
    [HttpGet("Me")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => NewResult(await Mediator.Send(new GetMyBadgesQuery(), ct));
}

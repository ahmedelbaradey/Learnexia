using Learnexia.Modules.Gamification.Api.Bases;
using Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.ListTimedEvents;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Gamification.Api.Controllers;

/// <summary>
/// Admin-facing read endpoint for the timed-event catalog (P4-11-B4).
/// Route: GET api/admin/timed-events
///
/// Returns all catalog rows (active + future + past) for admin inspection.
/// No POST/PUT/DELETE — catalog creation is handled via migration/seeder in Phase 3;
/// a write surface is deferred to the P7 admin console.
///
/// Auth: <c>[Authorize(Policy = AuthorizationPolicies.AdminOnly)]</c> — requires Admin or
/// SuperAdmin role. Registered in Identity Infrastructure's <c>AddAuthorization</c>.
/// Fixes P4-11 audit Medium #1 — previously any authenticated user (incl. students) could
/// read the catalog. Response contains no PII but is an internal-only catalog.
///
/// The controller sits in the same assembly as <see cref="GamificationController"/>;
/// <c>AddApplicationPart(typeof(GamificationController).Assembly)</c> in <c>GamificationModule</c>
/// already covers this controller — no additional wire-up needed.
/// </summary>
[Route("api/admin/timed-events")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class TimedEventsController : AppControllerBase
{
    /// <summary>
    /// Lists all timed events (active + future + past), ordered by <c>StartUtc DESC</c>,
    /// capped at 200 rows. Admin-only read endpoint.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => NewResult(await Mediator.Send(new ListTimedEventsQuery(), ct));
}

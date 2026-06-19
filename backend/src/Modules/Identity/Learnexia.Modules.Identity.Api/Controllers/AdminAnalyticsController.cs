using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;
using Learnexia.Modules.Identity.Application.Features.Analytics.Queries.GetPlatformKpis;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

/// <summary>
/// Admin REST surface for the platform-wide KPI dashboard (P7-10).
/// Route: <c>api/Admin/Analytics</c>
/// All endpoints are <c>AdminOnly</c> policy — anonymous → 401; non-admin → 403.
///
/// <para>Module home justification: the Identity module already hosts all other cross-cutting admin
/// read endpoints (<see cref="AdminUsersController"/>). The façade handler lives in
/// <c>Identity.Application</c> and injects only <c>Shared.Contracts</c> seam interfaces —
/// no direct coupling to Learning/Gamification/Billing/Ai projects. This is the least-coupling
/// home for a cross-module admin read façade (no new module required, no new schema).</para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("api/Admin/Analytics")]
[ApiController]
public sealed class AdminAnalyticsController : AppControllerBase
{
    /// <summary>
    /// GET api/Admin/Analytics/kpis?from=&amp;to=
    /// Returns the platform-wide KPI summary over the requested UTC window.
    ///
    /// <para>Query parameters:</para>
    /// <list type="bullet">
    ///   <item><c>from</c> — window start UTC (inclusive). Defaults to 30 days ago when omitted.</item>
    ///   <item><c>to</c>   — window end UTC (exclusive). Defaults to now (UTC) when omitted.</item>
    /// </list>
    ///
    /// <para>KPI facets:</para>
    /// <list type="bullet">
    ///   <item>Learning: <c>lessonsCompleted</c>, <c>totalAttempts</c>, <c>distinctActiveStudents</c> (activity proxy for DAU/WAU/MAU), subject/grade breakdowns.</item>
    ///   <item>Engagement: <c>missionsCompleted</c>, <c>xpEarnedInWindow</c>.</item>
    ///   <item>Subscriptions: <c>totalActiveSubscriptions</c>, tier breakdown. Revenue = N/A (Fake provider).</item>
    ///   <item>AI safety: <c>totalAiSafetyEvents</c>, <c>aiBlockedCount</c>, <c>aiFlaggedCount</c>. AI request volume = N/A (P7-11 AiUsageLogs).</item>
    ///   <item>Retention + session duration = N/A (requires P5-03 analytics events backbone).</item>
    /// </list>
    /// </summary>
    [HttpGet("kpis")]
    [ProducesResponseType(typeof(BaseResponse<PlatformKpiSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetKpis(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
        => NewResult(await Mediator.Send(
            new GetPlatformKpisQuery { FromUtc = from, ToUtc = to }, ct));
}

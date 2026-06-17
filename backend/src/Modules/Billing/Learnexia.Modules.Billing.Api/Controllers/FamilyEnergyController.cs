using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyEnergyOverview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Exposes the parent family-energy wallet read endpoint (P10-13-BE-11).
///
/// <para><strong>Authorization:</strong> <c>[Authorize]</c> — parent JWT only.
/// Child JWTs are rejected at the handler level (IDOR guard: <c>parentUserId</c> resolved
/// from the authenticated user's claim, never from the request).</para>
///
/// <para>Route: <c>GET /api/Billing/FamilyEnergy/Overview</c></para>
/// </summary>
[Route("api/Billing/FamilyEnergy")]
[ApiController]
[Authorize]
public class FamilyEnergyController : AppControllerBase
{
    /// <summary>
    /// Returns the family energy wallet overview for the authenticated parent:
    /// subscription and purchased balances, subscription cycle expiry, and
    /// per-child allocation snapshots (Allocated / Spent / Remaining).
    ///
    /// <para><strong>IDOR:</strong> the handler resolves <c>ParentUserId</c> from the JWT — a parent
    /// can only see their own family wallet. Passing a different id in the query has no effect
    /// (handler ignores any query parameter).</para>
    /// </summary>
    [HttpGet("Overview")]
    public async Task<IActionResult> GetOverview()
        => NewResult(await Mediator.Send(new GetFamilyEnergyOverviewQuery()));
}

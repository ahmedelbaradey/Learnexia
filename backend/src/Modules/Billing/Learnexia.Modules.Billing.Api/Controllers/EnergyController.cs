using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Queries.GetEnergyStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Exposes the child energy-status read seam.
///
/// <para>This is the ONLY backend obligation toward the P10-10 kid energy UI (FE).
/// The FE polls this endpoint to render the energy meter + warning copy.</para>
/// </summary>
[Route("api/Billing/[controller]")]
[ApiController]
public class EnergyController : AppControllerBase
{
    /// <summary>
    /// Returns the energy status for a child: balances, daily usage vs cap, grant expiry,
    /// and a typed <c>WarningState</c>.
    ///
    /// <para><strong>IDOR scoping:</strong> admin = any child; student JWT = own child only;
    /// parent JWT = must be linked via <c>IParentChildQuery</c>.</para>
    /// </summary>
    [HttpGet("{childId:int}/Status")]
    [Authorize]
    public async Task<IActionResult> GetEnergyStatus([FromRoute] int childId)
        => NewResult(await Mediator.Send(new EnergyStatusQuery { ChildId = childId }));
}

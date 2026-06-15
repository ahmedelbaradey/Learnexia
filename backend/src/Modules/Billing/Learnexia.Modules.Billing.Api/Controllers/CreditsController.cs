using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;
using Learnexia.Modules.Billing.Application.Features.Credits.Commands.SpendCredit;
using Learnexia.Modules.Billing.Application.Features.Credits.Queries.GetCreditAccount;
using Learnexia.Modules.Billing.Application.Features.Credits.Queries.ReconcileAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Exposes the credit-account read seam and (admin) ledger operations.
/// The cross-module debit seam (<c>ICreditSpendService</c>) is injected directly —
/// it does NOT go through an HTTP endpoint (intra-process, module isolation).
/// </summary>
[Route("api/Billing/[controller]")]
[ApiController]
public class CreditsController : AppControllerBase
{
    /// <summary>
    /// Returns the current energy balance for a child.
    /// </summary>
    [HttpGet("{childId:int}")]
    [Authorize]
    public async Task<IActionResult> GetBalance([FromRoute] int childId)
        => NewResult(await Mediator.Send(new GetCreditAccountQuery { ChildId = childId }));

    /// <summary>
    /// Admin/ops: reconciles the stored account balance against the ledger sum.
    /// Does NOT auto-heal.
    /// </summary>
    [HttpGet("{childId:int}/Reconcile")]
    [Authorize("Billing.View")]
    public async Task<IActionResult> Reconcile([FromRoute] int childId)
        => NewResult(await Mediator.Send(new ReconcileAccountQuery { ChildId = childId }));

    /// <summary>
    /// Internal: grants monthly energy credits. Called by the P10-02 Hangfire grant job
    /// (not a public HTTP endpoint in production — exposed here for dev/ops tooling).
    /// </summary>
    [HttpPost("Grant")]
    [Authorize("Billing.Create")]
    public async Task<IActionResult> Grant([FromBody] GrantCreditCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Internal: debits energy credits. Primarily called via <c>ICreditSpendService</c>
    /// (intra-process). Exposed for admin tooling / integration tests.
    /// </summary>
    [HttpPost("Spend")]
    [Authorize("Billing.Create")]
    public async Task<IActionResult> Spend([FromBody] SpendCreditCommand command)
        => NewResult(await Mediator.Send(command));
}

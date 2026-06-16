using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;
using Learnexia.Modules.Billing.Application.Features.Credits.Commands.SpendCredit;
using Learnexia.Modules.Billing.Application.Features.Credits.Queries.ReconcileAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Exposes the (admin) family-wallet ledger operations.
///
/// <para><strong>CreditAccount RETIRED:</strong> The per-child <c>GET /Credits/{childId}</c> read
/// (GetCreditAccountQuery), the per-child grant, and all other per-child CreditAccount paths
/// (ApplyPurchase, Adjust, ExpireGrant, per-child Refund) have been removed. Use
/// <c>GET /api/Billing/Energy</c> for the child energy status and
/// <c>GET /api/Billing/FamilyEnergy/Overview</c> for the parent wallet overview.</para>
///
/// <para>The cross-module debit seam (<c>ICreditSpendService</c>) is injected directly —
/// it does NOT go through an HTTP endpoint (intra-process, module isolation).</para>
/// </summary>
[Route("api/Billing/[controller]")]
[ApiController]
public class CreditsController : AppControllerBase
{
    /// <summary>
    /// Admin/ops: reconciles the stored family wallet balances against the ledger sum.
    /// Family-scoped (by parentId). Does NOT auto-heal. Does NOT touch CreditAccount.
    /// </summary>
    [HttpGet("Reconcile/{parentId:int}")]
    [Authorize("Billing.View")]
    public async Task<IActionResult> Reconcile([FromRoute] int parentId)
        => NewResult(await Mediator.Send(new ReconcileAccountQuery { ParentId = parentId }));

    /// <summary>
    /// Admin/ops: deposits energy into the shared family wallet's PurchasedBalance as a permanent
    /// admin comp. Family-scoped (by parentId in the body). Does NOT touch CreditAccount.
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

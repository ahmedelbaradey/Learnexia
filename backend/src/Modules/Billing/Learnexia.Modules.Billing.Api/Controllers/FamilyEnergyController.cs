using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Commands.TransferAllocation;
using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyAllocation;
using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyEnergyOverview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Exposes the parent family-energy wallet endpoints (P10-13-BE-11 + P10-16-BE-6).
///
/// <para><strong>Authorization:</strong> <c>[Authorize(Roles = "Parent")]</c> — parent JWT only.
/// Child (Student) JWTs are rejected at the AUTH layer (403), not just the handler; the IDOR guard
/// (<c>parentUserId</c> resolved from the authenticated user's claim, never from the request) is the
/// second line of defence.</para>
///
/// <para>Routes:
/// <list type="bullet">
///   <item><c>GET /api/Billing/FamilyEnergy/Overview</c> — wallet + per-child allocation overview (P10-13).</item>
///   <item><c>GET /api/Billing/FamilyEnergy/Allocation</c> — per-child movable allocation split, including mid-cycle children (P10-16).</item>
///   <item><c>POST /api/Billing/FamilyEnergy/Transfer</c> — move unspent allocation between own children (P10-16).</item>
/// </list>
/// </para>
/// </summary>
[Route("api/Billing/FamilyEnergy")]
[ApiController]
[Authorize(Roles = "Parent")]
public class FamilyEnergyController : AppControllerBase
{
    // ── P10-13: wallet overview ──────────────────────────────────────────────────────────────

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

    // ── P10-16: redistribution ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the per-child allocation split (movable amount) for ALL active-seat children of
    /// the authenticated parent, including mid-cycle-added children who have zero current
    /// allocation (<c>IsMidCycleNoGrant = true</c>).
    ///
    /// <para><strong>IDOR:</strong> <c>parentUserId</c> resolved from the JWT — never from the request.</para>
    /// </summary>
    [HttpGet("Allocation")]
    public async Task<IActionResult> GetAllocation()
        => NewResult(await Mediator.Send(new GetFamilyAllocationQuery()));

    /// <summary>
    /// Moves unspent allocated energy from one of the authenticated parent's children to another.
    /// Also handles the mid-cycle use case: if the destination child has no current allocation
    /// row (newly activated mid-cycle child), the service creates one before applying the transfer.
    ///
    /// <para><strong>IDOR:</strong> <c>parentUserId</c> resolved from the JWT.
    /// <c>fromChildId</c> and <c>toChildId</c> must both belong to the authenticated parent's family.</para>
    ///
    /// <para><strong>Idempotency:</strong> supply a client-generated <c>idempotencyKey</c>.
    /// Retrying with the same key returns the prior result without double-moving.</para>
    /// </summary>
    [HttpPost("Transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferAllocationRequest request)
        => NewResult(await Mediator.Send(new TransferAllocationCommand(
            request.FromChildId,
            request.ToChildId,
            request.Amount,
            request.IdempotencyKey)));
}

/// <summary>Request body for <c>POST /api/Billing/FamilyEnergy/Transfer</c>.</summary>
public sealed record TransferAllocationRequest(
    int FromChildId,
    int ToChildId,
    long Amount,
    string IdempotencyKey);

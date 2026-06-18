using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Seats.Commands.CancelExtraSeat;
using Learnexia.Modules.Billing.Application.Features.Seats.Commands.ChooseActiveChildren;
using Learnexia.Modules.Billing.Application.Features.Seats.Commands.ReactivateChildSeat;
using Learnexia.Modules.Billing.Application.Features.Seats.Commands.StartSeatCheckout;
using Learnexia.Modules.Billing.Application.Features.Seats.Queries.GetSeatStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Parent-facing seat management endpoints (P10-14-BE-9).
///
/// <para><strong>Parent-only:</strong> all endpoints require a valid parent JWT.
/// <c>ParentUserId</c> is always resolved from the JWT via <c>ICurrentUserService</c> inside
/// the handler — never from the request body.</para>
///
/// <para><strong>Routes:</strong>
/// <list type="bullet">
///   <item><c>GET  api/Billing/Seats/Status</c>  — seat capacity + per-child status.</item>
///   <item><c>POST api/Billing/Seats/Checkout</c> — initiate extra-seat add-on checkout.</item>
///   <item><c>POST api/Billing/Seats/Cancel</c>   — schedule purchased extra seats for cycle-end removal.</item>
/// </list>
/// </para>
/// </summary>
[Route("api/Billing/Seats")]
[ApiController]
[Authorize(Roles = "Parent")]
public sealed class SeatController : AppControllerBase
{
    /// <summary>
    /// Returns the family's seat capacity and per-child occupancy status.
    /// </summary>
    [HttpGet("Status")]
    public async Task<IActionResult> GetSeatStatus(CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new GetSeatStatusQuery(), cancellationToken));

    /// <summary>
    /// Initiates an extra-seat add-on checkout session.
    ///
    /// <para>The price is resolved server-side from <c>GlobalSettings</c> (<c>seats.extra_price_egp</c>)
    /// — the client never supplies the amount. Returns a redirect URL for the parent to complete
    /// payment via the hosted web checkout.</para>
    ///
    /// <para>On <c>payment.succeeded</c> the webhook handler increments
    /// <c>Subscription.PurchasedExtraSeats</c> inline (inside its single transaction), enforcing the
    /// <c>seats.max</c> ceiling and per-payment idempotency.</para>
    /// </summary>
    /// <param name="seatQuantity">Number of extra seats to add (must be &gt; 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Checkout")]
    public async Task<IActionResult> StartSeatCheckout(
        [FromBody] int seatQuantity,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new StartSeatCheckoutCommand(seatQuantity), cancellationToken));

    /// <summary>
    /// Schedules one or more purchased extra seat add-ons for removal at the NEXT renewal boundary
    /// (cycle-end cancel — P10-14-BE-8).
    ///
    /// <para>Records a scheduled-removal marker only: it does NOT start a grace period (grace is the
    /// payment-failure retry window only), does NOT reduce the active-seat count mid-cycle, and does
    /// NOT reclaim energy or delete a child. The seat stays Active for the rest of the current cycle;
    /// P10-15 applies the removal + NoSeat/Locked enforcement at renewal.</para>
    /// </summary>
    /// <param name="seatQuantity">Number of extra seats to cancel at cycle end (must be &gt; 0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Cancel")]
    public async Task<IActionResult> CancelExtraSeat(
        [FromBody] int seatQuantity,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new CancelExtraSeatCommand(seatQuantity), cancellationToken));

    /// <summary>
    /// Explicitly selects which children hold active seats (P10-15-BE-6 / BE-9).
    ///
    /// <para>Locks children NOT in the supplied list (<c>NoSeatLocked</c>). Children already
    /// <c>NoSeatLocked</c> cannot be moved back to Active via this endpoint — they require a
    /// paid reactivation checkout first.</para>
    ///
    /// <para>The paid-active-seat limit guard is enforced server-side. <c>ParentUserId</c> and
    /// family-ownership of all child ids are JWT-resolved — no IDOR.</para>
    /// </summary>
    /// <param name="command">The list of child ids that should hold active seats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("ChooseActive")]
    public async Task<IActionResult> ChooseActiveChildren(
        [FromBody] ChooseActiveChildrenCommand command,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(command, cancellationToken));

    /// <summary>
    /// Initiates a prorated reactivation checkout for a child whose seat is <c>NoSeatLocked</c>
    /// (P10-15-BE-6 / BE-9).
    ///
    /// <para>On webhook confirmation, the child's seat transitions back to <c>Active</c>.
    /// ZERO energy is minted (money-only). The price is prorated by the remaining cycle fraction
    /// and is resolved SERVER-SIDE — never client-supplied.</para>
    ///
    /// <para><c>ParentUserId</c> is JWT-resolved; the child must belong to the parent's family.</para>
    /// </summary>
    /// <param name="command">The child id + idempotency key for the reactivation checkout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Reactivate")]
    public async Task<IActionResult> ReactivateChildSeat(
        [FromBody] ReactivateChildSeatCommand command,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(command, cancellationToken));
}

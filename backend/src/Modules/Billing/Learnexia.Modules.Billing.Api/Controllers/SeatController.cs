using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Seats.Commands.CancelExtraSeat;
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
}

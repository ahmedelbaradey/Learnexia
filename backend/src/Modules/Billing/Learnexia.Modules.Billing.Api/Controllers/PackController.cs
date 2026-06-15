using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartPackCheckout;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Parent-facing energy-pack purchase endpoint (JWT-protected, parent only).
///
/// <para><strong>IDOR safety:</strong> <c>ParentUserId</c> is always resolved from the JWT
/// via <c>ICurrentUserService</c> in the command handler — never from the request body.
/// The family-scope guard (parent owns child) is enforced inside <c>IEnergyPackService</c>.</para>
///
/// <para><strong>Parent-only:</strong> only JWTs with the Parent role may call this endpoint.
/// Child JWTs cannot purchase packs for themselves.</para>
/// </summary>
[Route("api/Billing")]
[ApiController]
[Authorize]
public sealed class PackController : AppControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public PackController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Initiates a one-off energy-pack checkout session for a specific child.
    ///
    /// <para>The pack size and price are resolved server-side from <c>GlobalSettings</c> — the
    /// client supplies NO amount. Returns a redirect URL for the parent to complete payment.</para>
    ///
    /// <para>On payment.succeeded the child's <c>PurchasedBalance</c> is credited atomically
    /// via the webhook handler → <c>IEnergyPackService.CreditPurchasedPackAsync</c>.</para>
    /// </summary>
    /// <param name="childId">The child to receive the energy pack.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("Packs/Checkout")]
    public async Task<IActionResult> StartPackCheckout(
        [FromBody] int childId,
        CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new StartPackCheckoutCommand
        {
            // ParentUserId is set from the JWT in the handler via ICurrentUserService.
            // We do NOT pass it from the request body — IDOR prevention.
            ChildId = childId,
        }, cancellationToken));
}

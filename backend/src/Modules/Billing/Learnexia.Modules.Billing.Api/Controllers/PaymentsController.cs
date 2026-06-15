using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartSubscriptionCheckout;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Parent-facing payment endpoints (JWT-protected).
///
/// <para><strong>IDOR safety:</strong> <c>ParentUserId</c> is always resolved from the JWT
/// via <c>ICurrentUserService</c> — never from the request body.</para>
/// </summary>
[Route("api/Billing")]
[ApiController]
[Authorize]
public class PaymentsController : AppControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public PaymentsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Initiates a subscription checkout session.
    ///
    /// <para>Requires the parent to have a <c>PendingPayment</c> subscription (created via
    /// <c>POST /api/Billing/Subscription/Upgrade</c>).</para>
    ///
    /// <para>Returns a redirect URL to the payment provider's hosted checkout page.
    /// The amount is resolved server-side from the subscription's billing period —
    /// the client supplies NO amount.</para>
    /// </summary>
    [HttpPost("Subscription/Checkout")]
    public async Task<IActionResult> StartSubscriptionCheckout(CancellationToken cancellationToken)
        => NewResult(await Mediator.Send(new StartSubscriptionCheckoutCommand
        {
            ParentUserId = _currentUser.UserId ?? 0,
        }, cancellationToken));
}

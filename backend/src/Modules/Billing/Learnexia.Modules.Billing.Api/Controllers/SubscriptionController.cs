using Learnexia.Modules.Billing.Api.Bases;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.CancelSubscription;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestDowngrade;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestUpgrade;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetCurrentPlan;
using Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetPlanComparison;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Billing.Api.Controllers;

/// <summary>
/// Manages the authenticated parent's subscription plan.
///
/// <para><strong>IDOR safety:</strong> every endpoint resolves <c>ParentUserId</c> from the
/// JWT (<c>ICurrentUserService.UserId</c>) — never from the request body or query string.
/// A parent cannot view or mutate another parent's subscription.</para>
/// </summary>
[Route("api/Billing")]
[ApiController]
[Authorize]
public class SubscriptionController : AppControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public SubscriptionController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Returns the caller's current subscription and resolved plan benefits.
    /// </summary>
    [HttpGet("Subscription/Current")]
    public async Task<IActionResult> GetCurrentPlan()
        => NewResult(await Mediator.Send(new GetCurrentPlanQuery
        {
            ParentUserId = _currentUser.UserId ?? 0,
        }));

    /// <summary>
    /// Returns a Free vs Premium comparison with benefit values and pricing.
    /// All values are sourced from GlobalSettings — never hard-coded.
    /// </summary>
    [HttpGet("Plans/Comparison")]
    public async Task<IActionResult> GetPlanComparison()
        => NewResult(await Mediator.Send(new GetPlanComparisonQuery()));

    /// <summary>
    /// Requests an upgrade to Premium.
    /// Transitions the subscription to <c>PendingPayment</c>.
    /// Actual activation happens when the payment provider confirms (P10-06).
    /// </summary>
    [HttpPost("Subscription/Upgrade")]
    public async Task<IActionResult> RequestUpgrade([FromBody] UpgradeRequestBody body)
        => NewResult(await Mediator.Send(new RequestUpgradeCommand
        {
            ParentUserId  = _currentUser.UserId ?? 0,
            BillingPeriod = body.BillingPeriod,
        }));

    /// <summary>
    /// Schedules a downgrade to Free at the next cycle boundary.
    /// The parent retains Premium access until <c>CurrentCycleEnd</c>.
    /// </summary>
    [HttpPost("Subscription/Downgrade")]
    public async Task<IActionResult> RequestDowngrade()
        => NewResult(await Mediator.Send(new RequestDowngradeCommand
        {
            ParentUserId = _currentUser.UserId ?? 0,
        }));

    /// <summary>
    /// Cancels the subscription. For Premium parents, access continues until <c>CurrentCycleEnd</c>.
    /// </summary>
    [HttpPost("Subscription/Cancel")]
    public async Task<IActionResult> Cancel()
        => NewResult(await Mediator.Send(new CancelSubscriptionCommand
        {
            ParentUserId = _currentUser.UserId ?? 0,
        }));
}

/// <summary>Request body for <c>POST /api/Billing/Subscription/Upgrade</c>.</summary>
public sealed record UpgradeRequestBody(BillingPeriod BillingPeriod);

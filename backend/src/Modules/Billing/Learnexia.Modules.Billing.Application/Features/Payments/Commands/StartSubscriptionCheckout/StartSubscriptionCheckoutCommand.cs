using Learnexia.Modules.Billing.Application.Features.Payments.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Commands.StartSubscriptionCheckout;

/// <summary>
/// Initiates a subscription payment checkout session.
///
/// <para>IDOR safe: <see cref="ParentUserId"/> is always set by the controller from JWT
/// (<c>ICurrentUserService.UserId</c>) — never from the request body.</para>
///
/// <para>Amount is resolved SERVER-SIDE from <c>Subscription.BillingPeriod</c> +
/// <c>IGlobalSettingsProvider</c> keys — the client never supplies the amount.</para>
/// </summary>
public sealed record StartSubscriptionCheckoutCommand : ICommand<BaseResponse<CheckoutResultDto>>
{
    /// <summary>Owning parent — from JWT, never from client payload.</summary>
    public int ParentUserId { get; init; }
}

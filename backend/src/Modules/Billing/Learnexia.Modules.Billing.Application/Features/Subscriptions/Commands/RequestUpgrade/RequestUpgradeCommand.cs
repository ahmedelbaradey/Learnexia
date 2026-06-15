using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestUpgrade;

/// <summary>
/// Transitions the parent's subscription to <c>PendingPayment</c> and records
/// the desired <see cref="BillingPeriod"/>.
///
/// <para>Actual Premium activation happens in P10-06 (<c>HandleProviderWebhookCommand</c>)
/// when the payment provider confirms the charge. This command only records the intent.</para>
/// </summary>
public record RequestUpgradeCommand : ICommand<BaseResponse<SubscriptionDto>>
{
    /// <summary>Owning parent user id — resolved from JWT; never from request body.</summary>
    public int ParentUserId { get; init; }

    /// <summary>Desired billing cadence for Premium.</summary>
    public BillingPeriod BillingPeriod { get; init; }
}

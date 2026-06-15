using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.CancelSubscription;

/// <summary>
/// Cancels the parent's subscription. For Premium parents, access continues until
/// <c>CurrentCycleEnd</c>. For Free parents, the row is marked Canceled immediately.
/// </summary>
public record CancelSubscriptionCommand : ICommand<BaseResponse<SubscriptionDto>>
{
    /// <summary>Owning parent user id — resolved from JWT; never from request body.</summary>
    public int ParentUserId { get; init; }
}

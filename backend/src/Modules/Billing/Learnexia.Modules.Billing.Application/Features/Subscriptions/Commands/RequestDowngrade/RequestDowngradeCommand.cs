using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Commands.RequestDowngrade;

/// <summary>
/// Schedules a downgrade to Free at the next cycle boundary.
/// The parent retains Premium access until <c>CurrentCycleEnd</c>.
/// </summary>
public record RequestDowngradeCommand : ICommand<BaseResponse<SubscriptionDto>>
{
    /// <summary>Owning parent user id — resolved from JWT; never from request body.</summary>
    public int ParentUserId { get; init; }
}

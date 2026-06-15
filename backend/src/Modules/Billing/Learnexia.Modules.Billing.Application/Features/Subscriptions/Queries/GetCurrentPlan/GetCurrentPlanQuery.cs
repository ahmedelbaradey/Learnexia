using Learnexia.Modules.Billing.Application.Features.Subscriptions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Subscriptions.Queries.GetCurrentPlan;

/// <summary>
/// Returns the authenticated parent's current subscription and resolved plan benefits.
/// <para>Query — NOT auto-validated (IQuery, not ICommand).</para>
/// </summary>
public record GetCurrentPlanQuery : IQuery<BaseResponse<SubscriptionDto>>
{
    /// <summary>Owning parent user id — resolved from JWT in the controller; never from request body.</summary>
    public int ParentUserId { get; init; }
}

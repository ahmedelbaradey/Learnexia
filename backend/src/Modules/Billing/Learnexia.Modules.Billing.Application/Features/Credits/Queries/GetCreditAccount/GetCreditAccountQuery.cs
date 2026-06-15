using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.GetCreditAccount;

/// <summary>
/// Returns the current energy balance for a child.
/// No <c>ValidationBehavior</c> runs on queries (rule 4).
/// </summary>
public record GetCreditAccountQuery : IQuery<BaseResponse<CreditAccountDto>>
{
    public int ChildId { get; init; }
}

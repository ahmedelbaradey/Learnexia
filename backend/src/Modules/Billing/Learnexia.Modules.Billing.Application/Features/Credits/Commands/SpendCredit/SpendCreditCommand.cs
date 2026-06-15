using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.SpendCredit;

/// <summary>
/// Atomically debits credits from a child's account (Granted-first, idempotent, never-negative).
/// Used internally by <c>CreditSpendService</c> which is the cross-module seam.
/// </summary>
public record SpendCreditCommand : ICommand<BaseResponse<DebitResultDto>>
{
    public int ChildId { get; init; }
    public int Amount { get; init; }

    /// <summary>String form of <c>CreditReasonCode</c> enum member.</summary>
    public string ReasonCode { get; init; } = null!;

    /// <summary>Caller-supplied per-request unique key for idempotency.</summary>
    public string IdempotencyKey { get; init; } = null!;

    /// <summary>Optional AI action id for correlation.</summary>
    public string? RelatedActionId { get; init; }
}

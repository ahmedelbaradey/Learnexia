using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;

/// <summary>
/// Applies a monthly energy grant to a child's account.
/// Creates the <see cref="Domain.Entities.CreditAccount"/> if it does not exist.
/// Idempotent: same <see cref="IdempotencyKey"/> → no-op.
/// </summary>
public record GrantCreditCommand : ICommand<BaseResponse<DebitResultDto>>
{
    public int ChildId { get; init; }
    public int Amount { get; init; }
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>Typically <c>grant:{childId}:{yyyyMM}</c>.</summary>
    public string IdempotencyKey { get; init; } = null!;

    public bool IsPremium { get; init; }
}

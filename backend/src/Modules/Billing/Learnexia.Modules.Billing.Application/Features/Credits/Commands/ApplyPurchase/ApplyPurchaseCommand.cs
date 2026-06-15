using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.ApplyPurchase;

/// <summary>Credits a purchased energy pack to the child's purchased pool.</summary>
public record ApplyPurchaseCommand : ICommand<BaseResponse<DebitResultDto>>
{
    public int ChildId { get; init; }
    public int Amount { get; init; }
    public string IdempotencyKey { get; init; } = null!;
    public string? RelatedPaymentId { get; init; }
}

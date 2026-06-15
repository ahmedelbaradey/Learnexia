using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.Adjust;

/// <summary>Admin manual balance adjustment. Positive delta credits; negative delta debits (clamped).</summary>
public record AdjustCommand : ICommand<BaseResponse<DebitResultDto>>
{
    public int ChildId { get; init; }
    public int Delta { get; init; }
    public string IdempotencyKey { get; init; } = null!;
    public string? AdminNote { get; init; }
}

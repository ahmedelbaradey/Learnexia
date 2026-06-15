using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.ExpireGrant;

/// <summary>Expires the unused granted balance at cycle end. Idempotent: same key → no-op.</summary>
public record ExpireGrantCommand : ICommand<BaseResponse<DebitResultDto>>
{
    public int ChildId { get; init; }

    /// <summary>Typically <c>expire:{childId}:{yyyyMM}</c>.</summary>
    public string IdempotencyKey { get; init; } = null!;
}

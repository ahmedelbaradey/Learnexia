using Learnexia.Shared.Contracts.Billing;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Dtos;

/// <summary>
/// DTO exposed through <c>BaseResponse&lt;DebitResultDto&gt;</c> from the internal
/// <c>SpendCreditCommandHandler</c>. The cross-module seam uses <see cref="DebitResult"/>
/// (in Shared.Contracts.Billing) directly.
/// </summary>
public record DebitResultDto
{
    public bool Charged { get; init; }
    public int FromGranted { get; init; }
    public int FromPurchased { get; init; }
    public int ResultingTotal { get; init; }
    public DebitOutcome Outcome { get; init; }
}

using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ClearFlag;

/// <summary>
/// Command: clear the quality flag on a question — sets <c>QualityState = Approved</c>,
/// clears <c>FlagReason</c>, and re-enables the question for auto-serve.
///
/// Audited via <c>AdminActionPerformedDomainEvent</c>.
/// Runs inside <c>UnitOfWorkBehavior</c> (ICommand).
/// </summary>
public record ClearFlagCommand : ICommand<BaseResponse<string>>
{
    public int QuestionId { get; init; }
}

using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ConfirmFlag;

/// <summary>
/// Command: confirm the quality flag on a question — admin acknowledges the flag, keeps the question
/// in <c>FlaggedForReview</c> state (excluded from auto-serve), and records the audit.
///
/// The question remains flagged. Use <c>ClearFlagCommand</c> to re-enable serving.
/// Runs inside <c>UnitOfWorkBehavior</c> (ICommand).
/// </summary>
public record ConfirmFlagCommand : ICommand<BaseResponse<string>>
{
    public int QuestionId { get; init; }
}

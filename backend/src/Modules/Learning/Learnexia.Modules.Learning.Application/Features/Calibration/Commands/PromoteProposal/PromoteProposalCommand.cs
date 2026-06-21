using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.PromoteProposal;

/// <summary>
/// Command: promote a recalibration proposal — apply the proposed difficulty to the question.
///
/// This is the ONE and ONLY place the authored <c>QuizQuestion.Difficulty</c> band can change
/// (human-driven, audited, reversible). The job NEVER auto-applies the band.
///
/// <paramref name="ProposalId"/> is taken from the route (injected by the controller).
/// The acting admin UserId is sourced from <c>ICurrentUserService</c> (JWT) — never from the request body.
///
/// Runs inside <c>UnitOfWorkBehavior</c> (ICommand) → atomic + post-commit domain-event dispatch.
/// </summary>
public record PromoteProposalCommand : ICommand<BaseResponse<string>>
{
    public int ProposalId { get; init; }
}

using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.RejectProposal;

/// <summary>
/// Command: reject a recalibration proposal — closes the proposal without changing the authored band.
///
/// Audited via <c>AdminActionPerformedDomainEvent</c>.
/// Runs inside <c>UnitOfWorkBehavior</c> (ICommand).
/// </summary>
public record RejectProposalCommand : ICommand<BaseResponse<string>>
{
    public int ProposalId { get; init; }
}

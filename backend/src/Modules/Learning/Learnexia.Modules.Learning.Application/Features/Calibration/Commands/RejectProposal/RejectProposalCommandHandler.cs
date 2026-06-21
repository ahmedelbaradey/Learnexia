using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.RejectProposal;

/// <summary>
/// Rejects a recalibration proposal:
///   1. Sets <c>proposal.Status = Rejected</c>, stamps <c>ResolvedAtUtc</c> and <c>ResolvedByUserId</c>.
///   2. The authored <c>QuizQuestion.Difficulty</c> is NOT changed (propose-only safety model).
///   3. Raises <see cref="AdminActionPerformedDomainEvent"/> for auditability (AC4/AC7).
///
/// Runs inside UnitOfWorkBehavior → atomic commit + post-commit event dispatch (ADR 0001/0002).
/// </summary>
public class RejectProposalCommandHandler
    : BaseResponseHandler, ICommandHandler<RejectProposalCommand, BaseResponse<string>>
{
    private readonly ICalibrationService _calibrationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RejectProposalCommandHandler(
        ICalibrationService calibrationService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _calibrationService = calibrationService;
        _currentUser        = currentUser;
        _logger             = logger;
        _localizer          = localizer;
    }

    public async Task<BaseResponse<string>> Handle(
        RejectProposalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.ProposalId <= 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            var proposal = await _calibrationService.GetProposalTrackedAsync(
                request.ProposalId, cancellationToken);

            if (proposal is null)
                return NotFound<string>(_localizer[SharedResourcesKey.CalibrationProposalNotFound]);

            if (proposal.Status != ProposalStatus.Pending)
                return BusinessValidation<string>(_localizer[SharedResourcesKey.CalibrationProposalAlreadyResolved]);

            var adminUserId = _currentUser.UserId.GetValueOrDefault();
            var now         = DateTime.UtcNow;

            // ── Resolve proposal (NO authored-band change — propose-only safety model) ──────────
            proposal.Status           = ProposalStatus.Rejected;
            proposal.ResolvedAtUtc    = now;
            proposal.ResolvedByUserId = adminUserId;

            // ── Audit domain event ────────────────────────────────────────────────────────────────
            proposal.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId:      adminUserId,
                Action:           AdminActions.DifficultyProposalRejected,
                TargetEntityType: nameof(QuestionRecalibrationProposal),
                TargetEntityId:   proposal.Id,
                Details:          $"QuestionId={proposal.QuestionId}, ProposedDifficulty={proposal.ProposedDifficulty}"));

            return Success<string>(_localizer[SharedResourcesKey.CalibrationProposalRejectedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RejectProposalCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

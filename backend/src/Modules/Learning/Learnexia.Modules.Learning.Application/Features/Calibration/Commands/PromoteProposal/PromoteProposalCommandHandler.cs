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

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.PromoteProposal;

/// <summary>
/// Promotes a recalibration proposal:
///   1. Applies the <c>ProposedDifficulty</c> to <c>QuizQuestion.Difficulty</c>
///      (the ONE human-driven authored-band change).
///   2. Sets <c>proposal.Status = Promoted</c>, stamps <c>ResolvedAtUtc</c> and <c>ResolvedByUserId</c>.
///   3. Raises <see cref="AdminActionPerformedDomainEvent"/> on the tracked aggregate for auditability (AC4/AC7).
///
/// Runs inside UnitOfWorkBehavior → atomic commit + post-commit event dispatch (ADR 0001/0002).
/// UserId sourced from <see cref="ICurrentUserService"/> (JWT) — never from the request body.
/// </summary>
public class PromoteProposalCommandHandler
    : BaseResponseHandler, ICommandHandler<PromoteProposalCommand, BaseResponse<string>>
{
    private readonly ICalibrationService _calibrationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public PromoteProposalCommandHandler(
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
        PromoteProposalCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.ProposalId <= 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            // Load tracked proposal (includes navigation to Question).
            var proposal = await _calibrationService.GetProposalTrackedAsync(
                request.ProposalId, cancellationToken);

            if (proposal is null)
                return NotFound<string>(_localizer[SharedResourcesKey.CalibrationProposalNotFound]);

            if (proposal.Status != ProposalStatus.Pending)
                return BusinessValidation<string>(_localizer[SharedResourcesKey.CalibrationProposalAlreadyResolved]);

            var question = proposal.Question;
            if (question is null)
                return NotFound<string>(_localizer[SharedResourcesKey.QuizQuestionNotFound]);

            var adminUserId = _currentUser.UserId.GetValueOrDefault();
            var now         = DateTime.UtcNow;

            // ── Apply authored-band change (THE ONLY PLACE this mutation is allowed) ──────────────
            question.Difficulty = proposal.ProposedDifficulty;

            // ── Resolve proposal ─────────────────────────────────────────────────────────────────
            proposal.Status          = ProposalStatus.Promoted;
            proposal.ResolvedAtUtc   = now;
            proposal.ResolvedByUserId = adminUserId;

            // ── Raise domain event on the aggregate (ADR 0002 — dispatched post-commit) ──────────
            proposal.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId:      adminUserId,
                Action:           AdminActions.DifficultyProposalPromoted,
                TargetEntityType: nameof(QuestionRecalibrationProposal),
                TargetEntityId:   proposal.Id,
                Details:          $"QuestionId={proposal.QuestionId}, AuthoredDifficulty={proposal.AuthoredDifficulty}, ProposedDifficulty={proposal.ProposedDifficulty}"));

            return Success<string>(_localizer[SharedResourcesKey.CalibrationProposalPromotedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in PromoteProposalCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

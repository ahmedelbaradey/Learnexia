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

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ConfirmFlag;

/// <summary>
/// Confirms the calibration quality flag on a question:
///   1. The <c>QualityState</c> remains <c>FlaggedForReview</c> — no change to flag state.
///   2. Raises <see cref="AdminActionPerformedDomainEvent"/> so the action is recorded in the
///      audit trail (who confirmed the flag, when, on which question).
///
/// Does NOT change authored content and does NOT clear the flag.
/// Runs inside UnitOfWorkBehavior → atomic commit + post-commit event dispatch (ADR 0001/0002).
/// </summary>
public class ConfirmFlagCommandHandler
    : BaseResponseHandler, ICommandHandler<ConfirmFlagCommand, BaseResponse<string>>
{
    private readonly ICalibrationService _calibrationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ConfirmFlagCommandHandler(
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
        ConfirmFlagCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.QuestionId <= 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyIdValidation]);

            var question = await _calibrationService.GetQuestionTrackedAsync(
                request.QuestionId, cancellationToken);

            if (question is null)
                return NotFound<string>(_localizer[SharedResourcesKey.QuizQuestionNotFound]);

            if (question.QualityState != QuestionQualityState.FlaggedForReview)
                return BusinessValidation<string>(_localizer[SharedResourcesKey.CalibrationQuestionNotFlagged]);

            var adminUserId = _currentUser.UserId.GetValueOrDefault();

            // ── Audit domain event — flag state unchanged, but action recorded ────────────────────
            question.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId:      adminUserId,
                Action:           AdminActions.QuestionFlagConfirmed,
                TargetEntityType: nameof(QuizQuestion),
                TargetEntityId:   question.Id,
                Details:          $"FlagReason={question.FlagReason}"));

            return Success<string>(_localizer[SharedResourcesKey.CalibrationFlagConfirmedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ConfirmFlagCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

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

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ClearFlag;

/// <summary>
/// Clears the calibration quality flag on a question:
///   1. Sets <c>QualityState = Approved</c>, clears <c>FlagReason = null</c>.
///   2. The question is re-added to the auto-serve candidate pool
///      (<c>StartAttemptService.GetPublishedActiveQuestionsAsync</c> checks <c>Approved</c>).
///   3. Raises <see cref="AdminActionPerformedDomainEvent"/> for auditability (AC4).
///
/// Does NOT change authored content (Difficulty/QuestionText/Options/CorrectAnswer).
/// Runs inside UnitOfWorkBehavior → atomic commit + post-commit event dispatch (ADR 0001/0002).
/// </summary>
public class ClearFlagCommandHandler
    : BaseResponseHandler, ICommandHandler<ClearFlagCommand, BaseResponse<string>>
{
    private readonly ICalibrationService _calibrationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ClearFlagCommandHandler(
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
        ClearFlagCommand request, CancellationToken cancellationToken)
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

            // ── Clear flag — re-enables auto-serve ───────────────────────────────────────────────
            question.QualityState = QuestionQualityState.Approved;
            question.FlagReason   = null;

            // ── Audit domain event (on AggregateRoot QuizQuestion) ───────────────────────────────
            question.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId:      adminUserId,
                Action:           AdminActions.QuestionUnflagged,
                TargetEntityType: nameof(QuizQuestion),
                TargetEntityId:   question.Id,
                Details:          $"QualityState cleared to Approved"));

            return Success<string>(_localizer[SharedResourcesKey.CalibrationFlagClearedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ClearFlagCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Calibration.Dtos;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Queries.ListFlagged;

/// <summary>
/// Handles <see cref="ListFlaggedQuery"/>.
///
/// Returns a paged list of flagged questions with localized flag-reason strings.
/// AdminOnly (enforced on the controller action).
///
/// Reason localization (OQ-6): the stored <c>FlagReason</c> is a stable code.
/// Mapped to a localized string at read time.
/// </summary>
public class ListFlaggedQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListFlaggedQuery, BaseResponse<PaginatedResult<FlaggedQuestionDto>>>
{
    private readonly ICalibrationService _calibrationService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListFlaggedQueryHandler(
        ICalibrationService calibrationService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _calibrationService = calibrationService;
        _logger             = logger;
        _localizer          = localizer;
    }

    public async Task<BaseResponse<PaginatedResult<FlaggedQuestionDto>>> Handle(
        ListFlaggedQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var page     = request.Page < 1    ? 1  : request.Page;
            var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, 100);

            var paged = await _calibrationService.GetFlaggedQuestionsPagedAsync(
                page, pageSize, cancellationToken);

            // paged.Data is List<FlaggedQuestionRow> (PaginatedResult<T> : BaseResponse<List<T>>).
            var dtos = (paged.Data ?? new()).Select(row => new FlaggedQuestionDto
            {
                QuestionId          = row.Question.Id,
                QuestionText        = row.Question.QuestionText,
                LessonId            = row.Question.LessonId,
                GeneratedBy         = row.Question.GeneratedBy.ToString(),
                EvidencePValue      = row.Stats?.PValue,
                EvidenceSampleSize  = row.Stats?.SampleSize,
                FlagReason          = row.Question.FlagReason ?? string.Empty,
                FlagReasonLocalized = LocalizeReasonCode(row.Question.FlagReason),
                QualityState        = row.Question.QualityState.ToString(),
            }).ToList();

            var result = new PaginatedResult<FlaggedQuestionDto>(
                dtos, paged.TotalCount, page, pageSize,
                _localizer[SharedResourcesKey.CalibrationFlaggedQuestionsRetrievedSuccessfully]);

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListFlaggedQuery");
            return ServerError<PaginatedResult<FlaggedQuestionDto>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private string LocalizeReasonCode(string? reasonCode) => reasonCode switch
    {
        CalibrationEngine.ReasonDifficultyDiverged   => _localizer[SharedResourcesKey.CalibrationReasonDifficultyDiverged],
        CalibrationEngine.ReasonAiPValueExtremeHigh  => _localizer[SharedResourcesKey.CalibrationReasonAiPValueExtremeHigh],
        CalibrationEngine.ReasonAiPValueExtremeLow   => _localizer[SharedResourcesKey.CalibrationReasonAiPValueExtremeLow],
        _                                             => reasonCode ?? string.Empty,
    };
}

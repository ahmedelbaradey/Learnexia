using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Calibration.Dtos;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Calibration.Queries.ListProposals;

/// <summary>
/// Handles <see cref="ListProposalsQuery"/>.
///
/// Returns a paged list of recalibration proposals with localized reason strings.
/// AdminOnly (enforced on the controller action — CLAUDE §6 pattern).
///
/// Reason localization (OQ-6): the stored <c>ReasonCode</c> is a stable machine-generated code.
/// This handler maps it to a localized string via <see cref="IStringLocalizer{T}"/> at read time.
/// The raw code is also returned in the DTO so admin tooling can filter/group by code.
///
/// Queries are NOT auto-validated — inputs are validated in-handler (CLAUDE rule §4/§13).
/// </summary>
public class ListProposalsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListProposalsQuery, BaseResponse<PaginatedResult<CalibrationProposalDto>>>
{
    private readonly ICalibrationService _calibrationService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListProposalsQueryHandler(
        ICalibrationService calibrationService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _calibrationService = calibrationService;
        _logger             = logger;
        _localizer          = localizer;
    }

    public async Task<BaseResponse<PaginatedResult<CalibrationProposalDto>>> Handle(
        ListProposalsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate pagination in-handler (queries are NOT auto-validated).
            var page     = request.Page < 1     ? 1  : request.Page;
            var pageSize = request.PageSize < 1  ? 20 : Math.Min(request.PageSize, 100);

            // Validate status filter: if provided, must be a valid ProposalStatus member.
            int? statusFilter = null;
            if (request.Status.HasValue)
            {
                var val = request.Status.Value;
                if (val == (int)ProposalStatus.Pending
                    || val == (int)ProposalStatus.Promoted
                    || val == (int)ProposalStatus.Rejected)
                    statusFilter = val;
                // Out-of-range → ignore filter (return all).
            }

            var paged = await _calibrationService.GetProposalsPagedAsync(
                statusFilter, page, pageSize, cancellationToken);

            // paged.Data is List<QuestionRecalibrationProposal> (PaginatedResult<T> : BaseResponse<List<T>>).
            var dtos = (paged.Data ?? new()).Select(p => new CalibrationProposalDto
            {
                Id                 = p.Id,
                QuestionId         = p.QuestionId,
                QuestionText       = p.Question?.QuestionText ?? string.Empty,
                AuthoredDifficulty = p.AuthoredDifficulty.ToString(),
                ProposedDifficulty = p.ProposedDifficulty.ToString(),
                EvidencePValue     = p.EvidencePValue,
                EvidenceSampleSize = p.EvidenceSampleSize,
                ReasonCode         = p.ReasonCode,
                ReasonLocalized    = LocalizeReasonCode(p.ReasonCode),
                Status             = p.Status.ToString(),
                ResolvedAtUtc      = p.ResolvedAtUtc,
                ResolvedByUserId   = p.ResolvedByUserId,
                CreatedAt          = p.CreatedAt,
            }).ToList();

            var result = new PaginatedResult<CalibrationProposalDto>(
                dtos, paged.TotalCount, page, pageSize,
                _localizer[SharedResourcesKey.CalibrationProposalsRetrievedSuccessfully]);

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListProposalsQuery");
            return ServerError<PaginatedResult<CalibrationProposalDto>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private string LocalizeReasonCode(string reasonCode) => reasonCode switch
    {
        CalibrationEngine.ReasonDifficultyDiverged    => _localizer[SharedResourcesKey.CalibrationReasonDifficultyDiverged],
        CalibrationEngine.ReasonAiPValueExtremeHigh   => _localizer[SharedResourcesKey.CalibrationReasonAiPValueExtremeHigh],
        CalibrationEngine.ReasonAiPValueExtremeLow    => _localizer[SharedResourcesKey.CalibrationReasonAiPValueExtremeLow],
        _                                              => reasonCode,
    };
}

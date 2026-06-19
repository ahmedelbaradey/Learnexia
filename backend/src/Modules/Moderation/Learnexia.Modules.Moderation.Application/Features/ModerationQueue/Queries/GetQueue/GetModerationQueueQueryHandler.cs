using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Queries.GetQueue;

/// <summary>
/// Handles <see cref="GetModerationQueueQuery"/>. Thin handler — clamps page inputs, then
/// delegates all EF/query/pagination/UTC-normalization logic to <see cref="IModerationQueryService"/>
/// (Option C: no DbContext or EF types in this handler).
///
/// Results are ordered newest-first (by <c>DetectedAt</c>). Queries are NOT auto-validated;
/// page inputs are clamped in this handler.
/// </summary>
public class GetModerationQueueQueryHandler
    : BaseResponseHandler, IQueryHandler<GetModerationQueueQuery, BaseResponse<PaginatedResult<ModerationItemDto>>>
{
    private readonly IModerationQueryService _queryService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetModerationQueueQueryHandler(
        IModerationQueryService queryService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _queryService = queryService;
        _logger       = logger;
        _localizer    = localizer;
    }

    public async Task<BaseResponse<PaginatedResult<ModerationItemDto>>> Handle(
        GetModerationQueueQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Clamp page size to prevent full-table loads (security: no unbounded queries).
            var pageSize   = Math.Clamp(request.PageSize, 1, 100);
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var result = await _queryService.GetQueuePagedAsync(
                status:            request.Status,
                source:            request.Source,
                subjectCode:       request.SubjectCode,
                grade:             request.Grade,
                dateFrom:          request.DateFrom,
                dateTo:            request.DateTo,
                search:            request.Search,
                pageNumber:        pageNumber,
                pageSize:          pageSize,
                cancellationToken: cancellationToken);

            if (result.TotalCount == 0)
                return EmptyCollection(PaginatedResult<ModerationItemDto>.Success(
                    new List<ModerationItemDto>(), 0, pageNumber, pageSize));

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetModerationQueueQuery");
            return ServerError<PaginatedResult<ModerationItemDto>>();
        }
    }
}

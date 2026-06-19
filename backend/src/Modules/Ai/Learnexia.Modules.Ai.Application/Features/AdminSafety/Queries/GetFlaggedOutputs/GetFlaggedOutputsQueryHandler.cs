using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetFlaggedOutputs;

/// <summary>
/// Handles <see cref="GetFlaggedOutputsQuery"/>.
///
/// Thin handler: clamps page inputs (security — no unbounded queries), then delegates all
/// EF/pagination work to <see cref="IAiSafetyDashboardService"/> (Option C — no DbContext here).
/// Empty results produce HTTP 200 with an empty page, never 404.
/// </summary>
public sealed class GetFlaggedOutputsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetFlaggedOutputsQuery, BaseResponse<PaginatedResult<FlaggedOutputDto>>>
{
    private readonly IAiSafetyDashboardService _dashboardService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetFlaggedOutputsQueryHandler(
        IAiSafetyDashboardService dashboardService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _dashboardService = dashboardService;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<PaginatedResult<FlaggedOutputDto>>> Handle(
        GetFlaggedOutputsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Clamp page inputs — prevent full-table loads (security: no unbounded queries).
            var pageSize   = Math.Clamp(request.PageSize, 1, 100);
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var result = await _dashboardService.GetFlaggedOutputsPagedAsync(
                action:            request.Action,
                reasonCode:        request.ReasonCode,
                taskKind:          request.TaskKind,
                from:              request.From,
                to:                request.To,
                pageNumber:        pageNumber,
                pageSize:          pageSize,
                cancellationToken: cancellationToken);

            if (result.TotalCount == 0)
                return EmptyCollection(PaginatedResult<FlaggedOutputDto>.Success(
                    new List<FlaggedOutputDto>(), 0, pageNumber, pageSize));

            var response = Success(result);
            response.Message = _localizer[SharedResourcesKey.AiSafetyFlaggedOutputsRetrievedSuccessfully].Value;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetFlaggedOutputsQuery");
            return ServerError<PaginatedResult<FlaggedOutputDto>>(ex.Message);
        }
    }
}

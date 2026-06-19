using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetySignalSummary;

/// <summary>
/// Handles <see cref="GetSafetySignalSummaryQuery"/>.
///
/// Thin handler: resolves the date range, validates it, then delegates all EF/aggregation
/// work to <see cref="IAiSafetyDashboardService"/> (Option C — no DbContext or EF types here).
/// Empty range produces a zeroed summary (HTTP 200), never 404.
/// </summary>
public sealed class GetSafetySignalSummaryQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSafetySignalSummaryQuery, BaseResponse<SafetySignalSummaryDto>>
{
    private readonly IAiSafetyDashboardService _dashboardService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSafetySignalSummaryQueryHandler(
        IAiSafetyDashboardService dashboardService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _dashboardService = dashboardService;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<SafetySignalSummaryDto>> Handle(
        GetSafetySignalSummaryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var to   = request.To   ?? DateTime.UtcNow;
            var from = request.From ?? to.AddDays(-30);

            if (from >= to)
                return BadRequest<SafetySignalSummaryDto>(
                    _localizer[SharedResourcesKey.AiSafetyInvalidDateRange].Value);

            var summary = await _dashboardService.GetSummaryAsync(from, to, cancellationToken);

            var response = Success(summary);
            response.Message = _localizer[SharedResourcesKey.AiSafetySignalSummaryRetrievedSuccessfully].Value;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSafetySignalSummaryQuery");
            return ServerError<SafetySignalSummaryDto>(ex.Message);
        }
    }
}

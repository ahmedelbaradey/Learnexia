using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetSafetyTrend;

/// <summary>
/// Handles <see cref="GetSafetyTrendQuery"/>.
///
/// Thin handler: resolves the date range, validates it, then delegates all EF/bucketing
/// work to <see cref="IAiSafetyDashboardService"/> (Option C — no DbContext here).
/// Empty range produces an empty list (HTTP 200), never 404.
/// </summary>
public sealed class GetSafetyTrendQueryHandler
    : BaseResponseHandler, IQueryHandler<GetSafetyTrendQuery, BaseResponse<IReadOnlyList<SafetyTrendBucketDto>>>
{
    private readonly IAiSafetyDashboardService _dashboardService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetSafetyTrendQueryHandler(
        IAiSafetyDashboardService dashboardService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _dashboardService = dashboardService;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<IReadOnlyList<SafetyTrendBucketDto>>> Handle(
        GetSafetyTrendQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var to   = request.To   ?? DateTime.UtcNow;
            var from = request.From ?? to.AddDays(-30);

            if (from >= to)
                return BadRequest<IReadOnlyList<SafetyTrendBucketDto>>(
                    _localizer[SharedResourcesKey.AiSafetyInvalidDateRange].Value);

            var buckets = await _dashboardService.GetTrendAsync(from, to, cancellationToken);

            if (buckets.Count == 0)
                return EmptyCollection<IReadOnlyList<SafetyTrendBucketDto>>(
                    new List<SafetyTrendBucketDto>());

            var response = Success(buckets);
            response.Message = _localizer[SharedResourcesKey.AiSafetyTrendRetrievedSuccessfully].Value;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetSafetyTrendQuery");
            return ServerError<IReadOnlyList<SafetyTrendBucketDto>>();
        }
    }
}

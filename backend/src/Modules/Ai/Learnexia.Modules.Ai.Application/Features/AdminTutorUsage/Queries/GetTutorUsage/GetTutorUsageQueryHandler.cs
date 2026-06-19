using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Queries.GetTutorUsage;

/// <summary>
/// Handles <see cref="GetTutorUsageQuery"/>.
///
/// Thin handler: resolves the date range, validates it, then delegates all EF/aggregation
/// work to <see cref="IAiTutorUsageService"/> (Option C — no DbContext or EF types here).
/// Empty range produces a zeroed summary (HTTP 200), never 404.
/// </summary>
public sealed class GetTutorUsageQueryHandler
    : BaseResponseHandler, IQueryHandler<GetTutorUsageQuery, BaseResponse<TutorUsageDto>>
{
    // Caps in-memory aggregation as the table grows (mirrors the P7-10 analytics window cap).
    private static readonly TimeSpan MaxWindow = TimeSpan.FromDays(366);

    private readonly IAiTutorUsageService _tutorUsageService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetTutorUsageQueryHandler(
        IAiTutorUsageService tutorUsageService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _tutorUsageService = tutorUsageService;
        _logger            = logger;
        _localizer         = localizer;
    }

    public async Task<BaseResponse<TutorUsageDto>> Handle(
        GetTutorUsageQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var to   = request.To   ?? DateTime.UtcNow;
            var from = request.From ?? to.AddDays(-30);

            if (from >= to)
                return BadRequest<TutorUsageDto>(
                    _localizer[SharedResourcesKey.AiSafetyInvalidDateRange].Value);

            if ((to - from) > MaxWindow)
                return BadRequest<TutorUsageDto>(
                    _localizer[SharedResourcesKey.AiSafetyDateRangeTooLarge].Value);

            var summary = await _tutorUsageService.GetUsageSummaryAsync(from, to, cancellationToken);

            var response = Success(summary);
            response.Message = _localizer[SharedResourcesKey.AiTutorUsageRetrievedSuccessfully].Value;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetTutorUsageQuery");
            return ServerError<TutorUsageDto>();
        }
    }
}

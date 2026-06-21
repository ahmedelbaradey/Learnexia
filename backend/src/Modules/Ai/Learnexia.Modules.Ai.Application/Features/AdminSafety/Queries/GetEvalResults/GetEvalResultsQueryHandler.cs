using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetEvalResults;

/// <summary>
/// Handles <see cref="GetEvalResultsQuery"/> (P6-02-BE-6 / P7-11-BE-3).
///
/// <para>Thin handler: delegates all artifact reading to <see cref="IAiSafetyEvalResultsQuery"/>
/// (Option C — no DbContext or EF types here). Returns HTTP 200 with the bootstrap sentinel
/// when no artifact is present (never 404 or 500).</para>
///
/// <para>Module isolation: <see cref="IAiSafetyEvalResultsQuery"/> is a <c>Shared.Contracts</c>
/// seam; the admin module reads via this interface, never referencing Ai infrastructure directly.</para>
/// </summary>
public sealed class GetEvalResultsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetEvalResultsQuery, BaseResponse<EvalResultsDto>>
{
    private readonly IAiSafetyEvalResultsQuery _evalResultsQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetEvalResultsQueryHandler(
        IAiSafetyEvalResultsQuery evalResultsQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _evalResultsQuery = evalResultsQuery;
        _logger           = logger;
        _localizer        = localizer;
    }

    public async Task<BaseResponse<EvalResultsDto>> Handle(
        GetEvalResultsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _evalResultsQuery.GetLatestAsync(cancellationToken);
            var dto    = MapToDto(result);

            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.AiSafetyEvalResultsRetrievedSuccessfully].Value;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetEvalResultsQuery");
            return ServerError<EvalResultsDto>();
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static EvalResultsDto MapToDto(SafetyEvalRunResult result)
    {
        return new EvalResultsDto
        {
            RunId            = result.RunId,
            RanAt            = result.RanAtUtc,
            TotalCases       = result.TotalCases,
            PassedCases      = result.PassedCases,
            FailedCases      = result.FailedCases,
            PassRate         = result.PassRate,
            FailRate         = result.FailRate,
            ThresholdPercent = result.ThresholdPercent,
            Breached         = result.Breached,
            Tier             = result.Tier,
            Note             = result.Note,
            ByCheck          = MapBreakdowns(result.ByCheck),
            BySubject        = MapBreakdowns(result.BySubject),
            ByLanguage       = MapBreakdowns(result.ByLanguage),
        };
    }

    private static IReadOnlyDictionary<string, EvalCheckBreakdownDto> MapBreakdowns(
        IReadOnlyDictionary<string, EvalCheckBreakdown> source)
    {
        if (source.Count == 0)
            return new Dictionary<string, EvalCheckBreakdownDto>();

        return source.ToDictionary(
            kv => kv.Key,
            kv => new EvalCheckBreakdownDto(kv.Value.Passed, kv.Value.Total, kv.Value.PassRate));
    }
}

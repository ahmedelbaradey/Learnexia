using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;
using System.Text.Json;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetWeeklyReport;

/// <summary>
/// E9 — GET /Parent/Children/{id}/WeeklyReport (optional ?week=yyyy-MM-dd)
///
/// <para>IDOR-guarded: resolves parentId from JWT, calls <see cref="IParentChildQuery.IsParentOfChildAsync"/>
/// before any data access. Returns generic 403 on failure — no distinction between
/// "child not found" and "not your child" (anti-IDOR contract).</para>
///
/// <para>No-report state: when no <c>WeeklyReport</c> row exists yet, returns 200 OK with
/// <see cref="WeeklyReportDto.ReportFound"/> = false and zeroed fields — never 404 (AC3).</para>
///
/// <para>EF-free: reads via <see cref="IWeeklyReportQueryService"/> (Option-C).</para>
/// </summary>
public class GetWeeklyReportQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetWeeklyReportQuery, BaseResponse<WeeklyReportDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IWeeklyReportQueryService _reportQuery;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    // Shared JsonSerializerOptions — re-used across deserialization calls on the hot path.
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public GetWeeklyReportQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IWeeklyReportQueryService reportQuery,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _reportQuery = reportQuery;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<WeeklyReportDto>> Handle(
        GetWeeklyReportQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<WeeklyReportDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<WeeklyReportDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── IDOR gate — always check before any data read ──
            var isOwned = await _parentChildQuery.IsParentOfChildAsync(
                parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<WeeklyReportDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            // ── Fetch the report row (EF-free via service) ──
            WeeklyReportReadDto? row = request.WeekStartUtc.HasValue
                ? await _reportQuery.GetByWeekAsync(request.ChildId, request.WeekStartUtc.Value, cancellationToken)
                : await _reportQuery.GetLatestAsync(request.ChildId, cancellationToken);

            // ── No-report zero-state (AC3) ──
            if (row is null)
            {
                _logger.LogInfo($"No weekly report found for childId={request.ChildId} by parentId={parentId}");
                var empty = new WeeklyReportDto { ReportFound = false };
                var emptyResponse = Success(empty);
                emptyResponse.Message = _localizer[SharedResourcesKey.WeeklyReportNotYetGenerated];
                return emptyResponse;
            }

            // ── Deserialise JSON snapshots — degrade gracefully on parse failure ──
            IReadOnlyList<WeakAreaSnapshotItem> weakAreas = [];
            try
            {
                weakAreas = JsonSerializer.Deserialize<List<WeakAreaSnapshotItem>>(
                    row.WeakAreasJson, JsonOpts) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Failed to deserialise WeakAreasJson for childId={request.ChildId}");
            }

            IReadOnlyList<string> recommendations = [];
            try
            {
                recommendations = JsonSerializer.Deserialize<List<string>>(
                    row.RecommendationsJson, JsonOpts) ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Failed to deserialise RecommendationsJson for childId={request.ChildId}");
            }

            var dto = new WeeklyReportDto
            {
                ReportFound     = true,
                WeekStartUtc    = row.WeekStartUtc,
                XpEarned        = row.XpEarned,
                SkillsImproved  = row.SkillsImproved,
                WeakAreas       = weakAreas,
                Recommendations = recommendations,
                GeneratedAtUtc  = row.GeneratedAtUtc,
            };

            _logger.LogInfo($"Weekly report retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.WeeklyReportRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetWeeklyReportQueryHandler");
            return ServerError<WeeklyReportDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

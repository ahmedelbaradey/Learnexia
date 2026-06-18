using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildReports;

/// <summary>
/// E6 — GET /Parent/Children/{id}/Reports
/// </summary>
public class GetChildReportsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildReportsQuery, BaseResponse<ChildReportsDto>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStudentXpTimeSeriesQuery _xpTimeSeries;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetChildReportsQueryHandler(
        ICurrentUserService currentUser,
        IParentChildQuery parentChildQuery,
        IStudentXpTimeSeriesQuery xpTimeSeries,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _currentUser = currentUser;
        _parentChildQuery = parentChildQuery;
        _xpTimeSeries = xpTimeSeries;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<ChildReportsDto>> Handle(
        GetChildReportsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.ChildId <= 0)
                return BadRequest<ChildReportsDto>(_localizer[SharedResourcesKey.ChildIdMustBePositive]);

            var parentId = _currentUser.UserId;
            if (parentId is null)
                return Unauthorized<ChildReportsDto>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var isOwned = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isOwned)
                return Forbidden<ChildReportsDto>(_localizer[SharedResourcesKey.ParentChildNotAuthorized]);

            var today    = DateOnly.FromDateTime(DateTime.UtcNow);
            var week7    = today.AddDays(-6); // rolling 7 days inclusive [today-6, today]

            IReadOnlyList<DailyXpEntryDto> dailySeries = [];
            IReadOnlyList<DailyXpEntryDto> trend20     = [];
            IReadOnlyList<TimeOfDayBucketDto> buckets  = [];

            try
            {
                var raw = await _xpTimeSeries.GetDailySeriesAsync(request.ChildId, week7, today, cancellationToken);
                dailySeries = raw.Select(e => new DailyXpEntryDto
                {
                    Day = e.Day.ToString("yyyy-MM-dd"),
                    Xp  = e.Xp,
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery.GetDailySeriesAsync failed for childId={request.ChildId}");
            }

            try
            {
                var raw = await _xpTimeSeries.GetTwentyDayTrendAsync(request.ChildId, today, cancellationToken);
                trend20 = raw.Select(e => new DailyXpEntryDto
                {
                    Day = e.Day.ToString("yyyy-MM-dd"),
                    Xp  = e.Xp,
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery.GetTwentyDayTrendAsync failed for childId={request.ChildId}");
            }

            try
            {
                var raw = await _xpTimeSeries.GetTimeOfDayBucketsAsync(request.ChildId, week7, today, cancellationToken);
                buckets = raw.Select(b => new TimeOfDayBucketDto
                {
                    Bucket  = b.Bucket,
                    TotalXp = b.TotalXp,
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"IStudentXpTimeSeriesQuery.GetTimeOfDayBucketsAsync failed for childId={request.ChildId}");
            }

            var dto = new ChildReportsDto
            {
                DailyXpSeries    = dailySeries,
                XpTrend20Day     = trend20,
                TimeOfDayBuckets = buckets,
            };

            _logger.LogInfo($"Report charts retrieved for childId={request.ChildId} by parentId={parentId}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.ReportChartsRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetChildReportsQueryHandler");
            return ServerError<ChildReportsDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

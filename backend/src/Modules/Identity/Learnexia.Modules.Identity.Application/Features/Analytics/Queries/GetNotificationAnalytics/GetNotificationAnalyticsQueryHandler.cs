using Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;
using Learnexia.Shared.Contracts.Analytics;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Analytics.Queries.GetNotificationAnalytics;

/// <summary>
/// P9-11. Façade handler for <see cref="GetNotificationAnalyticsQuery"/>.
/// Delegates to <see cref="IPlatformAnalyticsQuery.GetNotificationsAsync"/> and maps the
/// result to <see cref="NotificationAnalyticsDto"/>.
///
/// <para>Module isolation: the handler injects ONLY the <c>Shared.Contracts</c> seam interface
/// <see cref="IPlatformAnalyticsQuery"/> — never any concrete Analytics types.</para>
///
/// <para>Persistence architecture: this is a query handler (no write). It calls one seam interface
/// — permissible per CONVENTIONS §7. No repo, no DbContext, no EF here.</para>
///
/// <para>Input defaults: null <c>FromUtc</c> defaults to 30 days ago; null <c>ToUtc</c> defaults
/// to now (UTC). Max window = 365 days. <c>from &gt;= to</c> → 400 (reuses KPI validation keys).</para>
///
/// <para><strong>v1 suppression scope:</strong> the response DTO documents that suppressed counts
/// = push-rationing only (P9-07 arbiter). Pre-dispatch suppressions are a deferred follow-up.
/// Open-rate undercounts until P9-02 FE (push-tap) and mark-all-read emission ship.</para>
/// </summary>
public sealed class GetNotificationAnalyticsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetNotificationAnalyticsQuery, BaseResponse<NotificationAnalyticsDto>>
{
    private readonly IPlatformAnalyticsQuery           _analyticsQuery;
    private readonly ILoggerManager                    _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    private static readonly TimeSpan DefaultWindowDays = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxWindow         = TimeSpan.FromDays(365);

    public GetNotificationAnalyticsQueryHandler(
        IPlatformAnalyticsQuery           analyticsQuery,
        ILoggerManager                    logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _analyticsQuery = analyticsQuery;
        _logger         = logger;
        _localizer      = localizer;
    }

    public async Task<BaseResponse<NotificationAnalyticsDto>> Handle(
        GetNotificationAnalyticsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── Input defaults and validation (query — not auto-validated) ────────────────────────
            var now     = DateTime.UtcNow;
            var fromUtc = request.FromUtc ?? now.Subtract(DefaultWindowDays);
            var toUtc   = request.ToUtc   ?? now;

            if (fromUtc >= toUtc)
                return BadRequest<NotificationAnalyticsDto>(
                    _localizer[SharedResourcesKey.PlatformKpisFromMustBeBeforeTo]);

            if ((toUtc - fromUtc) > MaxWindow)
                return BadRequest<NotificationAnalyticsDto>(
                    _localizer[SharedResourcesKey.PlatformKpisWindowTooLarge]);

            // ── Delegate to the Analytics seam ───────────────────────────────────────────────────
            var stats = await _analyticsQuery.GetNotificationsAsync(
                fromUtc, toUtc, request.Category, cancellationToken);

            // ── Map seam record → Identity-side DTO ──────────────────────────────────────────────
            var dto = new NotificationAnalyticsDto
            {
                FromUtc         = fromUtc,
                ToUtc           = toUtc,
                TotalDispatched = stats.TotalDispatched,
                TotalSuppressed = stats.TotalSuppressed,
                TotalOpened     = stats.TotalOpened,
                OpenRate        = stats.OpenRate,
                ByCode = stats.ByCode
                    .Select(s => new NotificationCodeStatDto(
                        s.Code, s.Dispatched, s.Suppressed, s.Opened, s.OpenRate))
                    .ToList(),
                ByCategory = stats.ByCategory
                    .Select(s => new NotificationCategoryStatDto(
                        s.Category, s.Dispatched, s.Suppressed, s.Opened, s.OpenRate))
                    .ToList(),
                SuppressionReasons = stats.SuppressionReasons
                    .Select(s => new NotificationReasonStatDto(s.Reason, s.Count))
                    .ToList(),
            };

            _logger.LogInfo(
                $"GetNotificationAnalyticsQuery: assembled for [{fromUtc:u}, {toUtc:u}) " +
                $"category={request.Category?.ToString() ?? "all"} — " +
                $"dispatched={dto.TotalDispatched}, suppressed={dto.TotalSuppressed}, " +
                $"opened={dto.TotalOpened}, openRate={dto.OpenRate:P1}");

            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.NotificationAnalyticsRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetNotificationAnalyticsQueryHandler");
            return ServerError<NotificationAnalyticsDto>(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}

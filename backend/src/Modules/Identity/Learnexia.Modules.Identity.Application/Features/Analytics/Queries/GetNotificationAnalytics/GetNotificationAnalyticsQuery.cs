using Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Analytics.Queries.GetNotificationAnalytics;

/// <summary>
/// P9-11. Query: return the notification lifecycle analytics aggregate over a UTC date range.
/// <c>GET /api/Admin/Analytics/notifications?from=&amp;to=&amp;category=</c>.
///
/// <para>Implements <c>IQuery&lt;T&gt;</c> — NOT auto-validated (queries bypass
/// <c>ValidationBehavior</c>). All input validation (range, guard checks) runs in the handler.</para>
///
/// <para>Optional <see cref="Category"/> filter: the <c>NotificationCategory</c> int value (0–9).
/// <c>null</c> = all categories.</para>
/// </summary>
public sealed record GetNotificationAnalyticsQuery : IQuery<BaseResponse<NotificationAnalyticsDto>>
{
    /// <summary>Window start, UTC (inclusive). Defaults to 30 days ago when null.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Window end, UTC (exclusive). Defaults to now (UTC) when null.</summary>
    public DateTime? ToUtc { get; init; }

    /// <summary>
    /// Optional <c>NotificationCategory</c> int filter (0–9). <c>null</c> = all categories.
    /// </summary>
    public int? Category { get; init; }
}

using Learnexia.Shared.Contracts.Analytics;

namespace Learnexia.Modules.Analytics.Application.Abstractions;

/// <summary>
/// P9-11. Read-time aggregation service over the notification-lifecycle rows in
/// <c>analytics.ActivityEvents</c> (EventType IN NotificationDispatched / NotificationSuppressed /
/// NotificationOpened).
///
/// <para>Option C: the implementation lives in <c>Analytics.Infrastructure</c> and has direct
/// access to <c>AnalyticsDbContext</c>; the Application layer only references this interface.</para>
///
/// <para>All results are sentinel-safe: an empty window or a table with no notification rows
/// returns a zeroed <see cref="NotificationAnalyticsStats"/> — never <c>null</c>, never throws.</para>
///
/// <para>v1 suppression scope: suppression counts capture only push-channel rationing via the
/// P9-07 arbiter. Pre-dispatch suppressions are a deferred follow-up.</para>
/// </summary>
public interface INotificationAnalyticsService
{
    /// <summary>
    /// Returns notification lifecycle aggregate statistics over
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>), optionally filtered by
    /// <paramref name="category"/> (<c>NotificationCategory</c> int value, 0–9).
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="category">
    /// Optional <c>NotificationCategory</c> int filter. <c>null</c> = all categories.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="NotificationAnalyticsStats"/> — never <c>null</c>.
    /// Zeroed when no notification events exist in the window.
    /// </returns>
    Task<NotificationAnalyticsStats> GetStatsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int?     category,
        CancellationToken ct = default);
}

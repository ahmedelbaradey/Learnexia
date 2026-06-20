using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Analytics;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Analytics.Infrastructure.Services;

/// <summary>
/// P9-11. Infrastructure implementation of <see cref="INotificationAnalyticsService"/>.
/// Queries <c>analytics.ActivityEvents</c> with <c>AsNoTracking</c> + a date window filtered
/// to notification EventTypes, then groups and aggregates server-side (EF LINQ → SQL group-by).
///
/// <para>Option C: all EF access is here, in Infrastructure. The Application layer
/// only references <see cref="INotificationAnalyticsService"/>.</para>
///
/// <para>v1 suppression scope: suppression counts capture only push-channel rationing via
/// the P9-07 arbiter (GlobalBudgetExhausted, PriorityLost, Cooldown). Pre-dispatch
/// suppressions are deferred.</para>
///
/// <para>Sentinel-safe: returns a zeroed <see cref="NotificationAnalyticsStats"/> on an empty
/// window or on exception — never null, never throws.</para>
///
/// <para>Performance note: this is admin-only, low-traffic. Group-by runs server-side against
/// the composite index <c>(EventType, OccurredAtUtc)</c> added in migration
/// <c>P9_11_AddNotificationFacetsToActivityEvent</c>. No materialised rollup for v1.</para>
/// </summary>
internal sealed class NotificationAnalyticsService : INotificationAnalyticsService
{
    private static readonly string[] NotificationEventTypes =
    [
        "NotificationDispatched",
        "NotificationSuppressed",
        "NotificationOpened"
    ];

    private readonly AnalyticsDbContext _db;
    private readonly ILoggerManager _logger;

    public NotificationAnalyticsService(AnalyticsDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationAnalyticsStats> GetStatsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        int?     category,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Pull the windowed notification-event slice (lightweight projection) ───────────
            // Composite index (EventType, OccurredAtUtc) drives the initial filter.
            var query = _db.ActivityEvents
                .AsNoTracking()
                .Where(e =>
                    NotificationEventTypes.Contains(e.EventType) &&
                    e.OccurredAtUtc >= fromUtc &&
                    e.OccurredAtUtc < toUtc);

            // Optional category filter (applied after EventType pre-filter so index is used).
            if (category.HasValue)
                query = query.Where(e => e.NotificationCategory == category.Value);

            var events = await query
                .Select(e => new
                {
                    e.EventType,
                    e.NotificationCode,
                    e.NotificationCategory,
                    e.NotificationChannels,
                    e.SuppressionReason,
                })
                .ToListAsync(ct);

            if (events.Count == 0)
            {
                _logger.LogInfo(
                    $"Analytics: NotificationAnalyticsService — no events in " +
                    $"[{fromUtc:u}, {toUtc:u}) category={category?.ToString() ?? "all"}.");
                return ZeroedStats();
            }

            // ── 2. Aggregate totals ──────────────────────────────────────────────────────────────
            int totalDispatched = events.Count(e => e.EventType == "NotificationDispatched");
            int totalSuppressed = events.Count(e => e.EventType == "NotificationSuppressed");
            int totalOpened     = events.Count(e => e.EventType == "NotificationOpened");
            double overallRate  = totalDispatched > 0
                ? (double)totalOpened / totalDispatched
                : 0.0;

            // ── 3. Group by NotificationCode ─────────────────────────────────────────────────────
            var byCodeGroups = events
                .Where(e => e.NotificationCode is not null)
                .GroupBy(e => e.NotificationCode!)
                .Select(g =>
                {
                    int d = g.Count(e => e.EventType == "NotificationDispatched");
                    int s = g.Count(e => e.EventType == "NotificationSuppressed");
                    int o = g.Count(e => e.EventType == "NotificationOpened");
                    return new NotificationCodeStat(
                        Code:       g.Key,
                        Dispatched: d,
                        Suppressed: s,
                        Opened:     o,
                        OpenRate:   d > 0 ? (double)o / d : 0.0);
                })
                .OrderByDescending(x => x.Dispatched)
                .ToList();

            // ── 4. Group by NotificationCategory ────────────────────────────────────────────────
            var byCategoryGroups = events
                .Where(e => e.NotificationCategory.HasValue)
                .GroupBy(e => e.NotificationCategory!.Value)
                .Select(g =>
                {
                    int d = g.Count(e => e.EventType == "NotificationDispatched");
                    int s = g.Count(e => e.EventType == "NotificationSuppressed");
                    int o = g.Count(e => e.EventType == "NotificationOpened");
                    return new NotificationCategoryStat(
                        Category:   g.Key,
                        Dispatched: d,
                        Suppressed: s,
                        Opened:     o,
                        OpenRate:   d > 0 ? (double)o / d : 0.0);
                })
                .OrderBy(x => x.Category)
                .ToList();

            // ── 5. Group suppression reasons ────────────────────────────────────────────────────
            var suppressionReasons = events
                .Where(e => e.EventType == "NotificationSuppressed" && e.SuppressionReason is not null)
                .GroupBy(e => e.SuppressionReason!)
                .Select(g => new NotificationReasonStat(Reason: g.Key, Count: g.Count()))
                .OrderByDescending(x => x.Count)
                .ToList();

            _logger.LogInfo(
                $"Analytics: NotificationAnalyticsService.GetStatsAsync " +
                $"[{fromUtc:u}, {toUtc:u}) category={category?.ToString() ?? "all"} — " +
                $"dispatched={totalDispatched}, suppressed={totalSuppressed}, " +
                $"opened={totalOpened}, openRate={overallRate:P1}.");

            return new NotificationAnalyticsStats(
                TotalDispatched:    totalDispatched,
                TotalSuppressed:    totalSuppressed,
                TotalOpened:        totalOpened,
                OpenRate:           overallRate,
                ByCode:             byCodeGroups,
                ByCategory:         byCategoryGroups,
                SuppressionReasons: suppressionReasons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Analytics: NotificationAnalyticsService error for window [{fromUtc:u}, {toUtc:u}).");
            // Sentinel-safe: return zeroed on error rather than propagating.
            return ZeroedStats();
        }
    }

    private static NotificationAnalyticsStats ZeroedStats() =>
        new(
            TotalDispatched:    0,
            TotalSuppressed:    0,
            TotalOpened:        0,
            OpenRate:           0.0,
            ByCode:             [],
            ByCategory:         [],
            SuppressionReasons: []);
}

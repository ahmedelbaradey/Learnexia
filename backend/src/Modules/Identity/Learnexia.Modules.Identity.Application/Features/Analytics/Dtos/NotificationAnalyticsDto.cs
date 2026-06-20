namespace Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;

/// <summary>
/// P9-11. Notification lifecycle analytics aggregate returned by
/// <c>GET /api/Admin/Analytics/notifications?from=&amp;to=&amp;category=</c>.
///
/// <para>Dispatched / suppressed / opened counts + open-rate per code and per category,
/// plus suppression-reason buckets, over a UTC date window.</para>
///
/// <para><strong>v1 suppression scope:</strong> <see cref="TotalSuppressed"/> and per-bucket
/// suppressed counts capture only push-channel rationing via the P9-07 arbiter
/// (GlobalBudgetExhausted, PriorityLost, Cooldown). Pre-dispatch suppressions (parent-disabled,
/// quiet-hours, per-category daily-cap) are captured only as log entries today and are a deferred
/// follow-up — so the admin should interpret "suppressed" as "push rationed" not "nudge dropped".</para>
///
/// <para><strong>Open-rate undercount (v1):</strong> opened events are emitted only on inbox
/// mark-read (single notification). Push-tap opens are deferred to P9-02 FE, and mark-all-read
/// bulk-opened events are also deferred. Open-rate = Opened / Dispatched.</para>
/// </summary>
public sealed record NotificationAnalyticsDto
{
    // ── Date range for which the analytics were computed ───────────────────────────────────────
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc   { get; init; }

    // ── Platform totals ────────────────────────────────────────────────────────────────────────
    /// <summary>Total notifications dispatched (in-app row written) in the window.</summary>
    public int TotalDispatched { get; init; }

    /// <summary>
    /// Total push-channel suppressions in the window. v1 = P9-07 arbiter reasons only.
    /// See class-level note for suppression scope.
    /// </summary>
    public int TotalSuppressed { get; init; }

    /// <summary>
    /// Total notifications opened (inbox mark-read, single-read path only in v1).
    /// Push-tap and mark-all-read opens are deferred.
    /// </summary>
    public int TotalOpened { get; init; }

    /// <summary>
    /// Platform-wide open-rate = TotalOpened / TotalDispatched. 0.0 when no dispatches.
    /// </summary>
    public double OpenRate { get; init; }

    // ── Per-code breakdown ────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Dispatched / suppressed / opened counts + open-rate per notification template code,
    /// ordered by dispatched count descending.
    /// </summary>
    public IReadOnlyList<NotificationCodeStatDto> ByCode { get; init; } = [];

    // ── Per-category breakdown ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Dispatched / suppressed / opened counts + open-rate per <c>NotificationCategory</c> int
    /// value (0–9), ordered by category value ascending.
    /// Admin layer maps int to label (0=WeeklyReport, 1=StreakAtRisk, … 9=TimedEvent).
    /// </summary>
    public IReadOnlyList<NotificationCategoryStatDto> ByCategory { get; init; } = [];

    // ── Suppression-reason breakdown ──────────────────────────────────────────────────────────
    /// <summary>
    /// Count of push-suppression events per reason string, ordered by count descending.
    /// v1 values: GlobalBudgetExhausted, PriorityLost, Cooldown.
    /// </summary>
    public IReadOnlyList<NotificationReasonStatDto> SuppressionReasons { get; init; } = [];
}

/// <summary>
/// P9-11. Notification analytics per template code.
/// </summary>
public sealed record NotificationCodeStatDto(
    string Code,
    int    Dispatched,
    int    Suppressed,
    int    Opened,
    double OpenRate);

/// <summary>
/// P9-11. Notification analytics per category.
/// <see cref="Category"/> is the <c>NotificationCategory</c> int value (0–9).
/// </summary>
public sealed record NotificationCategoryStatDto(
    int    Category,
    int    Dispatched,
    int    Suppressed,
    int    Opened,
    double OpenRate);

/// <summary>
/// P9-11. Push suppression count per reason string (v1: arbiter reasons only).
/// </summary>
public sealed record NotificationReasonStatDto(
    string Reason,
    int    Count);

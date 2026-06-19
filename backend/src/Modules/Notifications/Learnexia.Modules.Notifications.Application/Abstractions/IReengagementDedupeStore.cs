using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Atomic Redis SETNX dedupe store for reengagement nudges (F-04).
/// Key: <c>nudge:{studentId}:{categoryInt}:{yyyyMMdd}</c>; TTL=36h.
/// Fail-open: when Redis is unavailable, implementations should log a WARN and return <c>true</c>
/// (allow send; duplicate is preferable to a silently dropped nudge per D4).
/// </summary>
public interface IReengagementDedupeStore
{
    /// <summary>
    /// Attempts to acquire the dedupe lock for (<paramref name="studentId"/>, <paramref name="category"/>,
    /// <paramref name="eventDay"/>).
    /// Returns <c>true</c> when the lock was set (first caller — proceed to send);
    /// <c>false</c> when it already exists (duplicate — skip send).
    /// </summary>
    Task<bool> TryAcquireAsync(
        int studentId,
        NotificationCategory category,
        DateTime eventDay,
        CancellationToken ct = default);

    /// <summary>
    /// P9-08: Per-lapse-episode dedupe for the comeback escalation ladder.
    /// Key: <c>nudge-tier:{studentId}:{tierCode}</c>; TTL = <paramref name="ttl"/> (episode-length, not per-day).
    /// Each tier code fires at most once per lapse episode — the key is set without a day component
    /// so repeated daily lapse events do not re-fire the same tier within the episode window.
    /// Fail-open: Redis unavailable → log WARN + return <c>true</c> (allow send).
    /// </summary>
    /// <param name="studentId">The student id.</param>
    /// <param name="tierCode">Stable tier code, e.g. "LAPSE_WIN_BACK_GENTLE".</param>
    /// <param name="ttl">Episode-length TTL (how long the tier is blocked from re-firing).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAcquireTierAsync(
        int studentId,
        string tierCode,
        TimeSpan ttl,
        CancellationToken ct = default);
}

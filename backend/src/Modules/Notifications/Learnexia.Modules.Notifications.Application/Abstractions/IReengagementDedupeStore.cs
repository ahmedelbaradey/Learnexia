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
}

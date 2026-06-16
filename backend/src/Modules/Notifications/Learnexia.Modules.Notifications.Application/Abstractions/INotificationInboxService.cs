using Learnexia.Modules.Notifications.Application.Features.Notifications.Dtos;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Owns all persistence for the <c>Notification</c> inbox rows:
/// idempotent welcome-write, paginated inbox list, mark-read (single + bulk),
/// notification listing for admin, and counts for reengagement daily-cap.
/// Implemented in Infrastructure; injected only into Application handlers/helpers.
/// </summary>
public interface INotificationInboxService
{
    /// <summary>
    /// Idempotent welcome-notification write for a newly registered user.
    /// Writes the row only when no notification already exists for <paramref name="userId"/>.
    /// Returns true when the row was written; false when it already existed (skipped).
    /// </summary>
    Task<bool> WriteWelcomeIfAbsentAsync(
        int userId,
        string title,
        string body,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated, newest-first inbox for <paramref name="recipientUserId"/>.
    /// </summary>
    Task<PagedInboxResult> ListInboxAsync(
        int recipientUserId,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a single notification as read.
    /// Returns the notification if found (and owned by <paramref name="recipientUserId"/>), null otherwise.
    /// </summary>
    Task<Notification?> MarkReadAsync(
        Guid notificationId,
        int recipientUserId,
        DateTime readAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-marks all unread notifications for <paramref name="recipientUserId"/> as read.
    /// Returns the count of rows updated.
    /// </summary>
    Task<int> MarkAllReadAsync(int recipientUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns all notifications for <paramref name="recipientUserId"/> (admin list).
    /// </summary>
    Task<List<NotificationResponse>> ListByRecipientAsync(
        int recipientUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Counts how many notifications were dispatched for (<paramref name="childId"/>, <paramref name="category"/>)
    /// on the UTC day containing <paramref name="nowUtc"/>.
    /// Used by reengagement handlers to enforce the daily-cap gate.
    /// </summary>
    Task<int> CountSentTodayAsync(
        int childId,
        NotificationCategory category,
        DateTime nowUtc,
        CancellationToken ct = default);
}

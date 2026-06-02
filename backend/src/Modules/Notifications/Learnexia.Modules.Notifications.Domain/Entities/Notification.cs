using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Notifications.Domain.Entities;

public sealed class Notification : AuditableEntity<Guid>
{
    private Notification() { }

    public Guid RecipientUserId { get; private set; }

    /// <summary>
    /// Originating user's integer id, as carried by cross-module integration events (e.g.
    /// <c>UserRegisteredIntegrationEvent.UserId</c>). Stored as a plain int — NOT a cross-module FK
    /// (CONVENTIONS §12) — so the consumer's notifications are query-able by the producer's id.
    /// </summary>
    public int? RecipientExternalUserId { get; private set; }

    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;

    /// <summary>
    /// The notification's category, sourced from the <see cref="NotificationType"/> domain enum — no FK to a
    /// seeded lookup table, so a system-generated notification has no seed-data dependency.
    /// </summary>
    public NotificationType Type { get; private set; }

    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }

    // -------------------------------------------------------------------------
    // P4-09 re-engagement fields
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parent-side opt-in category that this notification belongs to. Defaults to
    /// <see cref="NotificationCategory.System"/> (= 6) for all rows created before P4-09 (backfill).
    /// </summary>
    public NotificationCategory Category { get; private set; } = NotificationCategory.System;

    /// <summary>
    /// Stable code identifying the notification template, e.g. "BADGE_EARNED", "STREAK_BROKEN".
    /// Used by the FE to look up localised copy and render the correct icon.
    /// </summary>
    public string Code { get; private set; } = null!;

    /// <summary>
    /// JSON payload carrying per-nudge data (badge code, mission code, streak length, etc.).
    /// Stored as <c>jsonb</c> in Postgres. Null for system notifications that predate P4-09.
    /// </summary>
    public string Data { get; private set; } = "{}";

    /// <summary>
    /// Bitmask recording which channels successfully delivered the notification:
    /// 1 = email, 2 = push, 4 = inApp. Default 0 (not yet dispatched / pre-P4-09 rows).
    /// </summary>
    public int DeliveredChannels { get; private set; } = 0;

    /// <summary>
    /// UTC time when the notification was dispatched to at least one channel.
    /// Null for rows created before P4-09.
    /// </summary>
    public DateTime? SentAtUtc { get; private set; }

    // -------------------------------------------------------------------------
    // Factories
    // -------------------------------------------------------------------------

    /// <summary>
    /// Factory for a system-generated welcome notification raised by a cross-module integration-event
    /// consumer. Sets <see cref="Type"/> to <see cref="NotificationType.Welcome"/> from the domain enum, so
    /// it has no dependency on seed data.
    /// </summary>
    public static Notification CreateWelcome(int externalUserId, string title, string body)
        => new()
        {
            Id = Guid.NewGuid(),
            RecipientUserId = Guid.Empty,
            RecipientExternalUserId = externalUserId,
            Title = title,
            Body = body,
            Type = NotificationType.Welcome,
            Category = NotificationCategory.System,
            Code = "WELCOME",
            Data = "{}",
            DeliveredChannels = 4, // InApp
            SentAtUtc = DateTime.UtcNow,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system:UserRegistered",
        };

    /// <summary>
    /// Factory for a P4-09 re-engagement notification written by the nudge dispatcher.
    /// <paramref name="code"/> is a stable template key, e.g. "STREAK_BROKEN".
    /// <paramref name="dataJson"/> is an optional JSON string (defaults to "{}").
    /// </summary>
    public static Notification CreateReengagement(
        int recipientExternalUserId,
        NotificationCategory category,
        string code,
        string title,
        string body,
        string? dataJson = null)
        => new()
        {
            Id = Guid.NewGuid(),
            RecipientUserId = Guid.Empty,
            RecipientExternalUserId = recipientExternalUserId,
            Title = title,
            Body = body,
            Type = NotificationType.Reminder,
            Category = category,
            Code = code,
            Data = dataJson ?? "{}",
            DeliveredChannels = 0,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

    // -------------------------------------------------------------------------
    // Mutations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Records a successful dispatch: stamps <see cref="SentAtUtc"/> and sets
    /// <see cref="DeliveredChannels"/> bitmask. Called by <c>NudgeDispatcher</c> (Batch 4).
    /// </summary>
    public void RecordSent(int deliveredChannels, DateTime sentAtUtc)
    {
        DeliveredChannels = deliveredChannels;
        SentAtUtc = sentAtUtc;
    }

    /// <summary>Marks the notification as read and stamps the opened timestamp.</summary>
    public void MarkRead(DateTime openedAtUtc)
    {
        IsRead = true;
        ReadAtUtc = openedAtUtc;
    }
}

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
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system:UserRegistered",
        };
}

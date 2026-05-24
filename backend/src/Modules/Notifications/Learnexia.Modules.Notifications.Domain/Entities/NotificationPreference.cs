using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Notifications.Domain.Entities;

/// <summary>
/// A single user's opt-in/opt-out toggle for one <see cref="NotificationCategory"/> (P2-12). One row per
/// (UserId, Category) — composite PK. <see cref="UserId"/> is the Identity integer user id carried by the
/// JWT, stored as a plain int — NOT a cross-module FK (CONVENTIONS §12) — so a preference row has no
/// dependency on the Identity schema. Audit fields are stamped by the DbContext, never by hand.
/// </summary>
public sealed class NotificationPreference : AuditableEntity<int>
{
    private NotificationPreference() { }

    /// <summary>Identity integer user id (loose ref, no FK). Part of the composite PK.</summary>
    public int UserId { get; private set; }

    /// <summary>The preference category. Part of the composite PK.</summary>
    public NotificationCategory Category { get; private set; }

    public bool EmailEnabled { get; private set; }
    public bool PushEnabled { get; private set; }

    public static NotificationPreference Create(int userId, NotificationCategory category, bool emailEnabled, bool pushEnabled)
        => new()
        {
            UserId = userId,
            Category = category,
            EmailEnabled = emailEnabled,
            PushEnabled = pushEnabled,
        };

    public void Update(bool emailEnabled, bool pushEnabled)
    {
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
    }
}

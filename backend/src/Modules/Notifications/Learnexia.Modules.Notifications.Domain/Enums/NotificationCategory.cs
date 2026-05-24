namespace Learnexia.Modules.Notifications.Domain.Enums;

// User-facing notification preference categories (P2-12). Intentionally DECOUPLED from the delivery-side
// NotificationType enum: NotificationType classifies how/why a notification row is produced, whereas
// NotificationCategory is the set of opt-in/opt-out toggles a user manages from account settings. Explicit
// numeric values (mirrors NotificationType / the Gamification enum style) so the stored value is stable.
public enum NotificationCategory
{
    WeeklyReport = 0,
    StreakAtRisk = 1,
    ProductAnnouncement = 2,
    Achievement = 3,
}

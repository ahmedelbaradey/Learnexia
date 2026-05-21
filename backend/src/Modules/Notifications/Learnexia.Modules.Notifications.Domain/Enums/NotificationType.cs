namespace Learnexia.Modules.Notifications.Domain.Enums;

// Category of a notification. Replaces the nullable NotificationTypeId FK / seeded NotificationTypes row
// for the welcome flow — a system-generated notification carries its type from this enum, with no seed
// data dependency. Mirrors the Gamification module's enum style (explicit numeric values).
public enum NotificationType
{
    System = 1,
    Welcome = 2,
    Reminder = 3,
}

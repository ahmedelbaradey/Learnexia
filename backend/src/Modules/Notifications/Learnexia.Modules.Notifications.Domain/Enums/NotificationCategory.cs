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
    // P4-09 re-engagement categories
    DailyMissionReminder = 4,
    LapseWinBack = 5,
    // Used as backfill default for the new Category column on existing Notification rows
    System = 6,
    // P9-09: spaced-repetition review reminder — distinct category so the parent can toggle it
    // independently of Achievement/WeeklyReport once P9-04 FE per-type toggles ship.
    // Inbox-only in v1 (not in ReengagementCategories push set); push falls out by prefs default.
    ReviewReminder = 7,
    // P9-06: weekly-challenge ending-soon reminder — distinct category so the parent can toggle it
    // independently of DailyMissionReminder once P9-04 FE per-type toggles ship.
    // Inbox-only in v1 (not in ReengagementCategories push set); push falls out by prefs default.
    // No migration: stored as int with HasSentinel(-1); value 8 != 0 != -1, stores correctly.
    WeeklyChallenge = 8,
    // P9-12: limited-time timed-event nudges — four phases: live (join fan-out), progress (halfway),
    // ending-soon, and completion. Distinct category so the parent can toggle it independently
    // once P9-04 FE per-type toggles ship. Listed in the P9-04 parent catalog.
    // Inbox-only in v1 (not in ReengagementCategories push set); push falls out by prefs default.
    // No migration: stored as int with HasSentinel(-1); value 9 != 0 != -1, stores correctly.
    TimedEvent = 9,
}

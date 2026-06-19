namespace Learnexia.Modules.Notifications.Domain.Entities;

using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

/// <summary>
/// Parent-controlled per-child notification preference (P4-09).
/// SRS §6: prefs are "per ParentStudent" — the parent JWT writes; child JWT cannot.
/// Unique on (ParentId, ChildId, Category): each (parent, child) pair has at most one row per category.
/// Derives from <see cref="FullAuditedEntity"/> so that the DbContext stamps audit fields automatically
/// and the record carries soft-delete support consistent with the module pattern.
/// </summary>
public sealed class ChildReengagementPreference : FullAuditedEntity
{
    public int ParentId { get; private set; }              // soft-ref to Identity.User
    public int ChildId { get; private set; }               // soft-ref to Identity.User
    public NotificationCategory Category { get; private set; }
    public bool Email { get; private set; }
    public bool Push { get; private set; }
    public bool InApp { get; private set; }
    public TimeOnly QuietHoursStartLocal { get; private set; }
    public TimeOnly QuietHoursEndLocal { get; private set; }
    public string TimeZoneId { get; private set; } = null!;

    /// <summary>Maximum number of nudges delivered across all channels for this category per day.</summary>
    public int DailyCap { get; private set; } = 3;

    /// <summary>
    /// Parent-configurable global cross-category daily PUSH budget for this child.
    /// When <c>null</c>, the platform default applies (sourced from
    /// <c>IGlobalSettingsProvider.GetInt("Notifications:GlobalDailyPushBudget", 4)</c>
    /// by the arbiter at runtime).  A non-null value is this parent's explicit per-child cap.
    /// In-app inbox delivery is never rationed by this budget — only push channel sends are counted.
    /// </summary>
    public int? GlobalDailyPushBudget { get; private set; }

    private ChildReengagementPreference() { }

    public static ChildReengagementPreference CreateDefault(int parentId, int childId, NotificationCategory category)
        => new()
        {
            ParentId = parentId,
            ChildId = childId,
            Category = category,
            Email = false,            // Conservative default: push disabled until parent opts in (AC3).
            Push = false,
            InApp = true,
            QuietHoursStartLocal = new TimeOnly(22, 0),
            QuietHoursEndLocal = new TimeOnly(8, 0),
            TimeZoneId = "Africa/Cairo",
            DailyCap = 3,
        };

    public void UpdateChannels(bool email, bool push, bool inApp)
    {
        Email = email; Push = push; InApp = inApp;
    }

    public void UpdateQuietHours(TimeOnly start, TimeOnly end, string timeZoneId)
    {
        QuietHoursStartLocal = start;
        QuietHoursEndLocal = end;
        TimeZoneId = timeZoneId;
    }

    public void UpdateDailyCap(int dailyCap)
    {
        DailyCap = dailyCap;
    }
}

using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;

/// <summary>
/// Response DTO for one (parent, child, category) preference row (P4-09 B4-3).
/// Synthesised from the domain entity or from defaults if no row has been persisted yet.
/// </summary>
public sealed class ChildReengagementPreferenceDto
{
    public NotificationCategory Category { get; set; }
    public bool Email { get; set; }
    public bool Push { get; set; }
    public bool InApp { get; set; }

    /// <summary>Quiet-hours window start in the child's local timezone. HH:mm format.</summary>
    public string QuietHoursStartLocal { get; set; } = "22:00";

    /// <summary>Quiet-hours window end in the child's local timezone. HH:mm format.</summary>
    public string QuietHoursEndLocal { get; set; } = "08:00";

    /// <summary>IANA timezone identifier for the quiet-hours window (e.g. "Africa/Cairo").</summary>
    public string TimeZoneId { get; set; } = "Africa/Cairo";

    /// <summary>Maximum nudges per category per day across all channels.</summary>
    public int DailyCap { get; set; } = 3;
}

/// <summary>
/// Per-category item within an <see cref="UpdateChildReengagementPreferencesCommand"/> request.
/// </summary>
public sealed class CategoryPrefItem
{
    public NotificationCategory Category { get; set; }
    public bool Email { get; set; }
    public bool Push { get; set; }
    public bool InApp { get; set; }
}

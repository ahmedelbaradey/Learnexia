using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.UpdateChildReengagementPreferences;

/// <summary>
/// Upserts the parent's per-child re-engagement preferences (P4-09 B4-3 / AC3).
/// Requires a parent JWT; handler enforces <c>IsParentOfChildAsync</c> check.
/// <para>All 3 schedulable categories must be supplied; the handler writes them in an explicit
/// transaction (no Unit of Work, ADR 0001).</para>
/// </summary>
public sealed record UpdateChildReengagementPreferencesCommand : ICommand<BaseResponse<string>>
{
    /// <summary>Child whose prefs are being updated (from route parameter — set by controller).</summary>
    public int ChildId { get; init; }

    /// <summary>Category-level channel toggles. Must include all 3 schedulable categories.</summary>
    public List<CategoryPrefItem> Items { get; init; } = new();

    /// <summary>Quiet-hours start in the child's local timezone (e.g. "22:00"). Null = use existing.</summary>
    public TimeOnly? QuietHoursStartLocal { get; init; }

    /// <summary>Quiet-hours end in the child's local timezone (e.g. "08:00"). Null = use existing.</summary>
    public TimeOnly? QuietHoursEndLocal { get; init; }

    /// <summary>IANA timezone identifier for quiet hours (e.g. "Africa/Cairo"). Null = use existing.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>Max nudges per category per day [0..10]. Null = use existing (default 3).</summary>
    public int? DailyCap { get; init; }
}

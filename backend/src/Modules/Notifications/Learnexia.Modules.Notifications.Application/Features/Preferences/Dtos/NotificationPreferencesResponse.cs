using System.Collections.Generic;
using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;

/// <summary>
/// The authenticated user's notification preferences (P2-12) — one item per
/// <see cref="NotificationCategory"/>. Always returns all categories: defaults are synthesised on first
/// read when no rows exist yet (no 404, nothing persisted on read).
/// </summary>
public sealed class NotificationPreferencesResponse
{
    public List<NotificationPreferenceItemDto> Preferences { get; set; } = new();
}

public sealed class NotificationPreferenceItemDto
{
    public NotificationCategory Category { get; set; }
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
}

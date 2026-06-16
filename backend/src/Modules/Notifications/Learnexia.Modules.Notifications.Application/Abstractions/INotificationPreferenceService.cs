using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Owns all persistence for user-level <c>NotificationPreference</c> rows.
/// Default synthesis (WeeklyReport=email-on; all others off) is performed inside the implementation
/// so handlers remain EF-free.
/// </summary>
public interface INotificationPreferenceService
{
    /// <summary>
    /// Returns the full set of user-visible preferences for <paramref name="userId"/>,
    /// synthesising in-memory defaults for any category not yet persisted (no side effects on read).
    /// </summary>
    Task<NotificationPreferencesResponse> GetPreferencesAsync(
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts all preferences in <paramref name="preferences"/> for <paramref name="userId"/>
    /// inside an explicit transaction — all rows commit atomically or none do.
    /// </summary>
    Task UpsertPreferencesAsync(
        int userId,
        IReadOnlyList<NotificationPreferenceItemDto> preferences,
        CancellationToken ct = default);
}

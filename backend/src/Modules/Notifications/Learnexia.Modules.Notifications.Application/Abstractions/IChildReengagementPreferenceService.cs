using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Owns all persistence for parent-controlled per-child re-engagement preference rows
/// (<c>ChildReengagementPreference</c>). The 3 schedulable categories
/// (StreakAtRisk, DailyMissionReminder, LapseWinBack) each get a DTO with in-memory defaults
/// when no row has been persisted yet.
/// </summary>
public interface IChildReengagementPreferenceService
{
    /// <summary>
    /// Returns the 3 schedulable-category preferences for (<paramref name="parentId"/>, <paramref name="childId"/>),
    /// synthesising conservative defaults (Push=false, InApp=true, DailyCap=3) for any missing row.
    /// Nothing is persisted on read.
    /// </summary>
    Task<List<ChildReengagementPreferenceDto>> GetForChildAsync(
        int parentId,
        int childId,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts all 3 schedulable-category rows for (<paramref name="parentId"/>, <paramref name="childId"/>)
    /// inside an explicit transaction.
    /// </summary>
    Task UpsertAsync(
        int parentId,
        int childId,
        IReadOnlyList<CategoryPrefItem> items,
        TimeOnly? quietHoursStartLocal,
        TimeOnly? quietHoursEndLocal,
        string? timeZoneId,
        int? dailyCap,
        int? globalDailyPushBudget = null,
        CancellationToken ct = default);

    /// <summary>
    /// Loads a single preference row for (<paramref name="parentId"/>, <paramref name="childId"/>, <paramref name="category"/>).
    /// Returns a conservative in-memory default when no row exists — nothing is persisted.
    /// Used by reengagement integration-event handlers.
    /// </summary>
    Task<ChildReengagementPreference> GetOrDefaultAsync(
        int parentId,
        int childId,
        NotificationCategory category,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the parent-configured global daily push budget for (<paramref name="parentId"/>, <paramref name="childId"/>),
    /// or <c>null</c> if no explicit budget has been set (caller should fall back to the platform config default).
    ///
    /// <para>Since <see cref="UpsertAsync"/> syncs <c>GlobalDailyPushBudget</c> across all 3 category rows for a
    /// child, any non-null row value is authoritative. The implementation returns the first non-null value found
    /// across the child's rows.</para>
    ///
    /// Used by <see cref="INudgeDispatcher"/> to resolve the effective budget for EVERY nudge uniformly,
    /// so that legacy handlers (which don't pre-compute the budget) are gated by the parent's actual setting.
    /// </summary>
    Task<int?> GetGlobalDailyPushBudgetAsync(
        int parentId,
        int childId,
        CancellationToken ct = default);
}

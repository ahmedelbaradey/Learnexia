using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IChildReengagementPreferenceService"/>.
/// Conservative defaults (Push=false, InApp=true, DailyCap=3) are synthesised in memory for
/// missing rows — nothing is persisted on read. Multi-row upserts run in an explicit transaction.
/// </summary>
internal sealed class ChildReengagementPreferenceService : IChildReengagementPreferenceService
{
    private static readonly NotificationCategory[] ReengagementCategories =
    [
        NotificationCategory.StreakAtRisk,
        NotificationCategory.DailyMissionReminder,
        NotificationCategory.LapseWinBack,
    ];

    private readonly NotificationsDbContext _db;

    public ChildReengagementPreferenceService(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task<List<ChildReengagementPreferenceDto>> GetForChildAsync(
        int parentId,
        int childId,
        CancellationToken ct = default)
    {
        var stored = await _db.ChildReengagementPreferences
            .AsNoTracking()
            .Where(p => p.ParentId == parentId && p.ChildId == childId)
            .ToDictionaryAsync(p => p.Category, ct);

        var result = new List<ChildReengagementPreferenceDto>(ReengagementCategories.Length);

        foreach (var category in ReengagementCategories)
        {
            if (stored.TryGetValue(category, out var row))
                result.Add(ToDto(row));
            else
                result.Add(BuildDefault(category));
        }

        return result;
    }

    public async Task UpsertAsync(
        int parentId,
        int childId,
        IReadOnlyList<CategoryPrefItem> items,
        TimeOnly? quietHoursStartLocal,
        TimeOnly? quietHoursEndLocal,
        string? timeZoneId,
        int? dailyCap,
        int? globalDailyPushBudget = null,
        CancellationToken ct = default)
    {
        var existing = await _db.ChildReengagementPreferences
            .Where(p => p.ParentId == parentId && p.ChildId == childId)
            .ToDictionaryAsync(p => p.Category, ct);

        var incoming = (items ?? []).ToDictionary(i => i.Category);

        // Explicit transaction: all rows must commit atomically (ADR 0001).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var category in ReengagementCategories)
        {
            if (existing.TryGetValue(category, out var row))
            {
                if (incoming.TryGetValue(category, out var item))
                    row.UpdateChannels(item.Email, item.Push, item.InApp);

                if (quietHoursStartLocal.HasValue && quietHoursEndLocal.HasValue)
                    row.UpdateQuietHours(quietHoursStartLocal.Value, quietHoursEndLocal.Value,
                        timeZoneId ?? row.TimeZoneId);

                if (dailyCap.HasValue)
                    row.UpdateDailyCap(dailyCap.Value);

                // P9-07: parent-configurable global push budget (applied to all rows for this child).
                if (globalDailyPushBudget.HasValue)
                    row.UpdateGlobalDailyPushBudget(globalDailyPushBudget.Value);
            }
            else
            {
                var newRow = ChildReengagementPreference.CreateDefault(parentId, childId, category);

                if (incoming.TryGetValue(category, out var item))
                    newRow.UpdateChannels(item.Email, item.Push, item.InApp);

                if (quietHoursStartLocal.HasValue && quietHoursEndLocal.HasValue)
                    newRow.UpdateQuietHours(quietHoursStartLocal.Value, quietHoursEndLocal.Value,
                        timeZoneId ?? newRow.TimeZoneId);

                if (dailyCap.HasValue)
                    newRow.UpdateDailyCap(dailyCap.Value);

                // P9-07: parent-configurable global push budget.
                if (globalDailyPushBudget.HasValue)
                    newRow.UpdateGlobalDailyPushBudget(globalDailyPushBudget.Value);

                await _db.ChildReengagementPreferences.AddAsync(newRow, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<ChildReengagementPreference> GetOrDefaultAsync(
        int parentId,
        int childId,
        NotificationCategory category,
        CancellationToken ct = default)
    {
        var row = await _db.ChildReengagementPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ParentId == parentId && p.ChildId == childId && p.Category == category,
                ct);

        return row ?? ChildReengagementPreference.CreateDefault(parentId, childId, category);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static ChildReengagementPreferenceDto ToDto(ChildReengagementPreference row)
        => new()
        {
            Category             = row.Category,
            Email                = row.Email,
            Push                 = row.Push,
            InApp                = row.InApp,
            QuietHoursStartLocal = row.QuietHoursStartLocal.ToString("HH:mm"),
            QuietHoursEndLocal   = row.QuietHoursEndLocal.ToString("HH:mm"),
            TimeZoneId           = row.TimeZoneId,
            DailyCap             = row.DailyCap,
        };

    private static ChildReengagementPreferenceDto BuildDefault(NotificationCategory category)
        => new()
        {
            Category             = category,
            Email                = false,
            Push                 = false,   // Conservative default — parent must opt-in (AC3).
            InApp                = true,
            QuietHoursStartLocal = "22:00",
            QuietHoursEndLocal   = "08:00",
            TimeZoneId           = "Africa/Cairo",
            DailyCap             = 3,
        };
}

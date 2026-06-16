using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="INotificationPreferenceService"/>.
/// The 4 user-visible categories are synthesised with defaults on read (no write side-effect).
/// Multi-row upserts run inside an explicit transaction — no Unit of Work (ADR 0001).
/// </summary>
internal sealed class NotificationPreferenceService : INotificationPreferenceService
{
    // P4-09 guard: only the 4 original user-visible categories belong to the self-preferences endpoint.
    // Categories DailyMissionReminder, LapseWinBack, and System are parent-controlled per-child
    // or internal sentinels — they must NOT be surfaced here.
    private static readonly NotificationCategory[] UserFacingCategories =
    [
        NotificationCategory.WeeklyReport,
        NotificationCategory.StreakAtRisk,
        NotificationCategory.ProductAnnouncement,
        NotificationCategory.Achievement,
    ];

    private readonly NotificationsDbContext _db;

    public NotificationPreferenceService(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationPreferencesResponse> GetPreferencesAsync(
        int userId,
        CancellationToken ct = default)
    {
        var rows = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        var stored = rows.ToDictionary(r => r.Category);

        var items = new List<NotificationPreferenceItemDto>(UserFacingCategories.Length);

        foreach (var category in UserFacingCategories)
        {
            if (stored.TryGetValue(category, out var row))
            {
                items.Add(new NotificationPreferenceItemDto
                {
                    Category     = row.Category,
                    EmailEnabled = row.EmailEnabled,
                    PushEnabled  = row.PushEnabled,
                });
            }
            else
            {
                // Default values: WeeklyReport gets email on; all others default off.
                // Defaults are synthesised in memory — NOT persisted (no side effects on read).
                items.Add(new NotificationPreferenceItemDto
                {
                    Category     = category,
                    EmailEnabled = category == NotificationCategory.WeeklyReport,
                    PushEnabled  = false,
                });
            }
        }

        return new NotificationPreferencesResponse { Preferences = items };
    }

    public async Task UpsertPreferencesAsync(
        int userId,
        IReadOnlyList<NotificationPreferenceItemDto> preferences,
        CancellationToken ct = default)
    {
        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.Category, ct);

        // Explicit transaction: multiple rows must commit atomically (no Unit of Work, ADR 0001).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var dto in preferences)
        {
            if (existing.TryGetValue(dto.Category, out var row))
            {
                // Row exists — update in place.
                row.Update(dto.EmailEnabled, dto.PushEnabled);
            }
            else
            {
                // New row — create and add to the context.
                var newRow = NotificationPreference.Create(
                    userId, dto.Category, dto.EmailEnabled, dto.PushEnabled);
                await _db.NotificationPreferences.AddAsync(newRow, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

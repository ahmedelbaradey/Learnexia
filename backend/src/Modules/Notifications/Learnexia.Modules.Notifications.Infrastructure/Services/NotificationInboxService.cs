using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Notifications.Dtos;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="INotificationInboxService"/>.
/// Owns all EF/SQL for the <c>Notification</c> table; returns materialized results — no IQueryable leaks.
/// </summary>
internal sealed class NotificationInboxService : INotificationInboxService
{
    private readonly NotificationsDbContext _db;

    public NotificationInboxService(NotificationsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> WriteWelcomeIfAbsentAsync(
        int userId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        var alreadyExists = await _db.Notifications
            .AnyAsync(n => n.RecipientExternalUserId == userId, ct);

        if (alreadyExists)
            return false;

        var welcome = Notification.CreateWelcome(
            externalUserId: userId,
            title: title,
            body: body);

        _db.Notifications.Add(welcome);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PagedInboxResult> ListInboxAsync(
        int recipientUserId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var baseQuery = _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientExternalUserId == recipientUserId);

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(n => new InboxItemDto
            {
                Id           = n.Id,
                Title        = n.Title,
                Body         = n.Body,
                Category     = n.Category,
                Code         = n.Code,
                Data         = n.Data,
                IsRead       = n.IsRead,
                CreatedAtUtc = n.CreatedAtUtc,
                OpenedAtUtc  = n.ReadAtUtc,
            })
            .ToListAsync(ct);

        return new PagedInboxResult
        {
            Items      = items,
            TotalCount = total,
            Skip       = skip,
            Take       = take,
        };
    }

    public async Task<Notification?> MarkReadAsync(
        Guid notificationId,
        int recipientUserId,
        DateTime readAtUtc,
        CancellationToken ct = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, ct);

        if (notification is null)
            return null;

        if (notification.RecipientExternalUserId != recipientUserId)
        {
            // Return a sentinel: we return the notification but the handler checks ownership.
            // Returning null here would conflate "not found" with "not owned".
            // We return the entity so the handler can distinguish the two cases.
            return notification;
        }

        if (!notification.IsRead)
        {
            notification.MarkRead(readAtUtc);
            await _db.SaveChangesAsync(ct);
        }

        return notification;
    }

    public async Task<int> MarkAllReadAsync(int recipientUserId, CancellationToken ct = default)
    {
        // EF 7+ ExecuteUpdateAsync — single SQL UPDATE, no change tracker, IDOR-safe by WHERE.
        return await _db.Notifications
            .Where(n => n.RecipientExternalUserId == recipientUserId && !n.IsRead)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.IsRead, true),
                ct);
    }

    public async Task<List<NotificationResponse>> ListByRecipientAsync(
        int recipientUserId,
        CancellationToken ct = default)
    {
        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientExternalUserId == recipientUserId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationResponse
            {
                Id           = n.Id,
                Title        = n.Title,
                Body         = n.Body,
                Type         = n.Type,
                IsRead       = n.IsRead,
                CreatedAtUtc = n.CreatedAtUtc,
            })
            .ToListAsync(ct);
    }

    public async Task<int> CountSentTodayAsync(
        int childId,
        NotificationCategory category,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var todayStart    = nowUtc.Date;
        var tomorrowStart = todayStart.AddDays(1);

        return await _db.Notifications
            .AsNoTracking()
            .CountAsync(n =>
                n.RecipientExternalUserId == childId
                && n.Category == category
                && n.SentAtUtc >= todayStart
                && n.SentAtUtc < tomorrowStart,
                ct);
    }

    /// <summary>
    /// P9-07: Cross-category push count for the global daily push budget.
    /// Push rows are identified by <c>DeliveredChannels &amp; 2 != 0</c> (Push bitmask = 2).
    /// This is the durable DB-backed budget authority — enforced even when Redis is unavailable.
    /// </summary>
    public async Task<int> CountPushesSentTodayAsync(
        int childId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var todayStart    = nowUtc.Date;
        var tomorrowStart = todayStart.AddDays(1);

        // DeliveredChannels & 2 != 0 selects rows where the push bit was set.
        return await _db.Notifications
            .AsNoTracking()
            .CountAsync(n =>
                n.RecipientExternalUserId == childId
                && (n.DeliveredChannels & 2) != 0
                && n.SentAtUtc >= todayStart
                && n.SentAtUtc < tomorrowStart,
                ct);
    }
}

using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Modules.Notifications.Domain.Templates;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;

/// <summary>
/// Shared procedural helpers for re-engagement integration-event handlers (P4-09 B4-5).
/// This is a plain static utility class — NOT a design pattern (rule 8). It extracts
/// repeated code (DB prefs lookup, Redis dedupe, locale lookup) that would otherwise
/// duplicate verbatim across 8 handlers.
/// </summary>
internal static class ReengagementHandlerHelper
{
    private const string DefaultLocale = "ar-EG";
    private const int DedupeTtlHours   = 36;

    // -------------------------------------------------------------------------
    // Parent-child resolve
    // -------------------------------------------------------------------------

    // (No helper needed — callers call IParentChildQuery.FindParentForChildAsync directly.)

    // -------------------------------------------------------------------------
    // Preference lookup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the preference row for <c>(parentId, childId, category)</c> from the DB.
    /// Returns a default (conservative) in-memory instance when no row exists — nothing is persisted.
    /// </summary>
    public static async Task<ChildReengagementPreference> GetOrDefaultPrefsAsync(
        INotificationsDbContext db,
        int parentId,
        int childId,
        NotificationCategory category,
        CancellationToken ct)
    {
        var row = await db.ChildReengagementPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ParentId == parentId && p.ChildId == childId && p.Category == category,
                ct);

        return row ?? ChildReengagementPreference.CreateDefault(parentId, childId, category);
    }

    // -------------------------------------------------------------------------
    // Daily count (for DailyCap check)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Counts how many notifications have been dispatched for (childId, category) today (UTC).
    /// Used as the <c>sentsToday</c> input to <see cref="ReengagementEvaluator.Evaluate"/>.
    /// </summary>
    public static async Task<int> CountSentTodayAsync(
        INotificationsDbContext db,
        int childId,
        NotificationCategory category,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var todayStart = nowUtc.Date;
        var tomorrowStart = todayStart.AddDays(1);

        return await db.Notifications
            .AsNoTracking()
            .CountAsync(n =>
                n.RecipientExternalUserId == childId
                && n.Category == category
                && n.SentAtUtc >= todayStart
                && n.SentAtUtc < tomorrowStart,
                ct);
    }

    // -------------------------------------------------------------------------
    // Redis SETNX dedupe
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to acquire the dedupe lock for (studentId, category, eventDay).
    /// Key: <c>nudge:{studentId}:{categoryInt}:{yyyyMMdd}</c>.
    /// TTL: 36h (covers late-delivery + redelivery scenarios per D4).
    ///
    /// F-04 security fix: when <paramref name="redisMultiplexer"/> is available, uses atomic SETNX
    /// (<c>StringSetAsync(key, value, ttl, When.NotExists)</c>) to eliminate the TOCTOU race between
    /// a GET and a SET that existed with the <see cref="IDistributedCache"/> fallback.
    /// When the multiplexer is null (Redis not wired — local dev / tests), falls back to the
    /// IDistributedCache approach which is race-tolerant but not race-free.
    ///
    /// Fail-open: if Redis throws (connection error, timeout) → logs WARN with a safe static message
    /// (no infrastructure details — F-09) and returns <c>true</c>
    /// (allow send; duplicate is preferable to silently dropped nudge per D4).
    /// </summary>
    public static async Task<bool> TryAcquireDedupeAsync(
        IDistributedCache cache,
        IConnectionMultiplexer? redisMultiplexer,
        ILoggerManager logger,
        int studentId,
        NotificationCategory category,
        DateTime eventDay,
        CancellationToken ct)
    {
        var key = $"nudge:{studentId}:{(int)category}:{eventDay.Date:yyyyMMdd}";
        var ttl = TimeSpan.FromHours(DedupeTtlHours);
        try
        {
            if (redisMultiplexer is not null)
            {
                // F-04: atomic SETNX — sets the key only if it does not already exist.
                // Returns true when the key was set (first caller wins); false when it already existed.
                var db = redisMultiplexer.GetDatabase();
                return await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
            }

            // Fallback: IDistributedCache (best-effort, race-tolerant but not race-free).
            var existing = await cache.GetStringAsync(key, ct);
            if (existing is not null)
                return false; // Already sent for this (student, category, day).

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
            };
            await cache.SetStringAsync(key, "1", options, ct);
            return true;
        }
        catch (Exception ex)
        {
            // F-09: log only a safe static message — do not expose ex.Message which may contain
            // Redis connection endpoint / socket details.
            logger.LogWarn($"P4-09: Redis dedupe unavailable for key={key} — fail-open, nudge may duplicate.");
            _ = ex; // suppress unused-variable warning
            return true;
        }
    }

    // -------------------------------------------------------------------------
    // Locale resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves the recipient's preferred locale for copy-template selection.
    /// Falls back to "ar-EG" (platform default) when the lookup fails or returns null.
    /// </summary>
    public static async Task<string> GetLocaleAsync(
        IUserLookup? userLookup,
        int userId,
        CancellationToken ct)
    {
        if (userLookup is null)
            return DefaultLocale;

        try
        {
            var user = await userLookup.FindByIdAsync(userId, ct);
            return string.IsNullOrWhiteSpace(user?.PreferredLanguage) ? DefaultLocale : user.PreferredLanguage;
        }
        catch
        {
            return DefaultLocale;
        }
    }

    // -------------------------------------------------------------------------
    // NudgeMessage builder
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="NudgeMessage"/> from the resolved prefs + rendered copy.
    /// </summary>
    public static NudgeMessage BuildMessage(
        int childId,
        int parentId,
        NotificationCategory category,
        string code,
        ChildReengagementPreference prefs,
        string locale,
        params (string Name, string Value)[] placeholders)
    {
        var (title, body) = ReengagementCopyTemplates.Render(category, code, locale, placeholders);
        return new NudgeMessage(
            RecipientChildUserId: childId,
            ParentId:             parentId,
            Category:             category,
            Code:                 code,
            Title:                title,
            Body:                 body,
            DataJson:             null,
            ShouldPush:           prefs.Push,
            ShouldInApp:          prefs.InApp);
    }
}

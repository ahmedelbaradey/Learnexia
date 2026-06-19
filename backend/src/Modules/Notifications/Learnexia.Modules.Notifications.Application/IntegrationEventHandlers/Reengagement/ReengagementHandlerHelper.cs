using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Modules.Notifications.Domain.Templates;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Notifications.Application.IntegrationEventHandlers.Reengagement;

/// <summary>
/// Shared procedural helpers for re-engagement integration-event handlers (P4-09 B4-5).
/// This is a plain static utility class — NOT a design pattern (rule 8). It extracts
/// repeated code (prefs lookup, Redis dedupe, locale lookup) that would otherwise
/// duplicate verbatim across 8 handlers.
/// EF/persistence access has been lifted out into the service interfaces (Option-C rule).
/// </summary>
internal static class ReengagementHandlerHelper
{
    private const string DefaultLocale = "ar-EG";

    // -------------------------------------------------------------------------
    // Preference lookup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the preference row for <c>(parentId, childId, category)</c> via the service.
    /// Returns a default (conservative) in-memory instance when no row exists — nothing is persisted.
    /// </summary>
    public static Task<ChildReengagementPreference> GetOrDefaultPrefsAsync(
        IChildReengagementPreferenceService preferenceService,
        int parentId,
        int childId,
        NotificationCategory category,
        CancellationToken ct)
        => preferenceService.GetOrDefaultAsync(parentId, childId, category, ct);

    // -------------------------------------------------------------------------
    // Daily count (for DailyCap check)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Counts how many notifications have been dispatched for (childId, category) today (UTC).
    /// Used as the <c>sentsToday</c> input to <see cref="ReengagementEvaluator.Evaluate"/>.
    /// </summary>
    public static Task<int> CountSentTodayAsync(
        INotificationInboxService inboxService,
        int childId,
        NotificationCategory category,
        DateTime nowUtc,
        CancellationToken ct)
        => inboxService.CountSentTodayAsync(childId, category, nowUtc, ct);

    // -------------------------------------------------------------------------
    // Redis SETNX dedupe
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to acquire the dedupe lock for (studentId, category, eventDay) via the store.
    /// Fail-open: if the store throws → logs WARN + returns <c>true</c> (allow send; duplicate
    /// is preferable to a silently dropped nudge per D4).
    /// </summary>
    public static Task<bool> TryAcquireDedupeAsync(
        IReengagementDedupeStore dedupeStore,
        ILoggerManager logger,
        int studentId,
        NotificationCategory category,
        DateTime eventDay,
        CancellationToken ct)
        => dedupeStore.TryAcquireAsync(studentId, category, eventDay, ct);

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
    /// <c>ShouldPush</c> is set to <c>prefs.Push</c> (parent pref only). The dispatcher applies
    /// the global daily budget + per-type cooldown gate via <see cref="INudgeArbiter"/>.
    /// Budget resolution is owned entirely by <see cref="INudgeDispatcher"/> — handlers must NOT
    /// pre-compute or pass a budget value; the dispatcher looks it up uniformly for every nudge.
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
            RecipientChildUserId:  childId,
            ParentId:              parentId,
            Category:              category,
            Code:                  code,
            Title:                 title,
            Body:                  body,
            DataJson:              null,
            ShouldPush:            prefs.Push,
            ShouldInApp:           prefs.InApp);
    }
}

using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Domain.Templates;

/// <summary>
/// Pure-static lookup of notification copy templates for re-engagement nudges (P4-09 B4-1 / AC5).
/// No DI, no DB — fully unit-testable.
///
/// <para>Keys are returned in two languages: <c>ar-EG</c> (primary, Arabic-first per FR-GM-8) and
/// <c>en-US</c> (fallback). Unknown locales fall back to <c>en-US</c>. Named placeholders such as
/// <c>{streakLength}</c>, <c>{daysIdle}</c>, and <c>{badgeCode}</c> are substituted by callers via
/// <see cref="Render"/>.</para>
///
/// <para>Child-safety principle (BRD §8): copy is encouraging, never shaming. No "you broke your
/// streak" or failure framing. All templates reviewed for COPPA-appropriateness.</para>
/// </summary>
public static class ReengagementCopyTemplates
{
    private const string ArEg = "ar-EG";
    private const string EnUs = "en-US";

    // Template key structure: (category, code, locale) → (title, body)
    // Named placeholders use {name} syntax; callers replace with Render().
    private static readonly Dictionary<string, (string Title, string Body)> Templates = new()
    {
        // ── WeeklyReport ─────────────────────────────────────────────────────────────────────
        // P9-06: Weekly recap — once/week personalized summary, celebration > guilt, never-shaming.
        // Category = WeeklyReport; inbox-only in v1 until P9-04 FE per-type toggles ship.
        // Zero-activity weeks are suppressed at the producer — these templates never appear for them.
        // Placeholders: {xp} = XpEarned, {skills} = SkillsImproved.
        [$"WeeklyReport:WEEKLY_RECAP:{ArEg}"] = (
            "إنجازك الأسبوعي!",
            "🌟 إنجازك الأسبوع ده: {xp} XP و {skills} مهارات — رائع! استمر في التقدم"),
        [$"WeeklyReport:WEEKLY_RECAP:{EnUs}"] = (
            "Your weekly achievement!",
            "🌟 This week: {xp} XP and {skills} skills — great job! Keep up the momentum"),

        // ── StreakAtRisk ──────────────────────────────────────────────────────────────────────
        [$"StreakAtRisk:STREAK_AT_RISK:{ArEg}"] = (
            "سلسلتك في خطر!",
            "بقاءلك 6 ساعات! درس واحد بس وتحافظ على سلسلة الـ{streakLength} يوم 🔥"),
        [$"StreakAtRisk:STREAK_AT_RISK:{EnUs}"] = (
            "Streak at risk!",
            "6 hours left! One lesson keeps your {streakLength}-day streak alive 🔥"),

        [$"StreakAtRisk:STREAK_BROKEN:{ArEg}"] = (
            "ابدأ سلسلة جديدة!",
            "انتهت السلسلة، لكن كل بطل يبدأ من جديد! 💪 اكمل درساً اليوم وابدأ سلسلة جديدة"),
        [$"StreakAtRisk:STREAK_BROKEN:{EnUs}"] = (
            "Start a new streak!",
            "Streak ended, but every champion starts fresh! 💪 Complete a lesson today to begin a new streak"),

        [$"StreakAtRisk:HEARTS_DEPLETED:{ArEg}"] = (
            "نفدت القلوب!",
            "نفدت قلوبك، لكن مش مشكلة — ستعود كاملة قريباً! 💙 استرح قليلاً ثم عد"),
        [$"StreakAtRisk:HEARTS_DEPLETED:{EnUs}"] = (
            "Hearts are empty!",
            "Your hearts are empty, but don't worry — they'll refill soon! 💙 Rest a bit and come back"),

        // ── Achievement ───────────────────────────────────────────────────────────────────────
        [$"Achievement:HEARTS_REFILLED:{ArEg}"] = (
            "القلوب امتلأت!",
            "قلوبك مليانة من جديد ❤️ حان وقت المغامرة — اكمل درساً الآن!"),
        [$"Achievement:HEARTS_REFILLED:{EnUs}"] = (
            "Hearts are full!",
            "Your hearts are full again ❤️ Time for adventure — complete a lesson now!"),

        [$"Achievement:BADGE_EARNED:{ArEg}"] = (
            "شارة جديدة!",
            "🎉 حصلت على شارة {badgeCode}! استمر في التعلم لفتح المزيد"),
        [$"Achievement:BADGE_EARNED:{EnUs}"] = (
            "New badge!",
            "🎉 You earned the {badgeCode} badge! Keep learning to unlock more"),

        [$"Achievement:MISSION_COMPLETED:{ArEg}"] = (
            "أحسنت!",
            "أتممت مهمتك اليومية ✨ +{rewardXp} XP — استمر في التقدم!"),
        [$"Achievement:MISSION_COMPLETED:{EnUs}"] = (
            "Well done!",
            "You completed your daily mission ✨ +{rewardXp} XP — keep going!"),

        [$"Achievement:LEVELED_UP:{ArEg}"] = (
            "ارتقيت لمستوى جديد!",
            "🌟 وصلت لمستوى جديد! مغامراتك التعليمية تزداد إثارة"),
        [$"Achievement:LEVELED_UP:{EnUs}"] = (
            "Level up!",
            "🌟 You reached a new level! Your learning adventures are getting more exciting"),

        [$"Achievement:LEAGUE_TIER_CHANGED:{ArEg}"] = (
            "دوري جديد!",
            "🏆 انتقلت لدوري جديد! واصل التعلم وحافظ على مكانتك"),
        [$"Achievement:LEAGUE_TIER_CHANGED:{EnUs}"] = (
            "New league!",
            "🏆 You moved to a new league! Keep learning and hold your spot"),

        // P9-05: League promotion variant (shown when Status == "Promoted")
        [$"Achievement:LEAGUE_TIER_CHANGED_PROMOTED:{ArEg}"] = (
            "ترقيت لدوري أعلى!",
            "🏆 ارتقيت لدوري أعلى! واصل التعلم وحافظ على مكانتك الجديدة"),
        [$"Achievement:LEAGUE_TIER_CHANGED_PROMOTED:{EnUs}"] = (
            "You've been promoted!",
            "🏆 You moved up to a higher league! Keep learning and hold your new spot"),

        // P9-05: Streak freeze consumed (Achievement v1 per OQ-8 locked decision)
        [$"Achievement:STREAK_FREEZE_CONSUMED:{ArEg}"] = (
            "سلسلتك محفوظة!",
            "❄️ أنقذنا سلسلتك اليوم! سلسلتك لا تزال {streakLength} يوم — ارجع غداً تكمّل"),
        [$"Achievement:STREAK_FREEZE_CONSUMED:{EnUs}"] = (
            "Streak saved!",
            "❄️ Your freeze saved your {streakLength}-day streak today! Come back tomorrow to keep it going"),

        // ── DailyMissionReminder ──────────────────────────────────────────────────────────────
        [$"DailyMissionReminder:DAILY_MISSION_REMINDER:{ArEg}"] = (
            "مهمتك اليومية!",
            "مهمتك اليومية هتنتهي قريب — اكمل لتربح 30 XP إضافي ⭐"),
        [$"DailyMissionReminder:DAILY_MISSION_REMINDER:{EnUs}"] = (
            "Daily mission!",
            "Your daily mission ends soon — finish it for +30 XP ⭐"),

        // ── LapseWinBack ─────────────────────────────────────────────────────────────────────
        // F-Spot: removed {daysIdle} from the body — DaysSinceLastActivity is a days-idle count,
        // not a mission count, so "14 missions await" for a 14-day-idle child was semantically wrong.
        // Body is now timeless and avoids the ambiguity entirely.
        [$"LapseWinBack:LAPSE_WIN_BACK:{ArEg}"] = (
            "وحشتنا!",
            "وحشتنا! ارجع اليوم لتكمل رحلتك 🌟"),
        [$"LapseWinBack:LAPSE_WIN_BACK:{EnUs}"] = (
            "We miss you!",
            "We miss you! Come back today to continue your journey 🌟"),

        // ── ReviewReminder ────────────────────────────────────────────────────────────────────
        // P9-09: Spaced-repetition review-due nudge — once per SR sweep, top due skill as headline.
        // Category = ReviewReminder; inbox-only in v1 (not in ReengagementCategories push set until P9-04 FE).
        // Placeholders: {skill} = TopSkills[0].SkillName, {minutes} = TopSkills[0].EstimatedTimeMinutes.
        // Arabic minutes phrasing uses fixed "دقائق بس" — no CLDR plural agreement (v1 accepted simplification,
        // consistent with existing "{streakLength} يوم" templates).
        [$"ReviewReminder:REVIEW_DUE:{ArEg}"] = (
            "وقت المراجعة! 🧠",
            "🧠 وقت مراجعة سريعة لمهارة {skill} ({minutes} دقائق بس) — تعالى نثبّتها!"),
        [$"ReviewReminder:REVIEW_DUE:{EnUs}"] = (
            "Time to review! 🧠",
            "🧠 Quick review time for {skill} (just {minutes} min) — let's lock it in!"),

        // P9-08: Comeback escalation ladder (3 tiers: gentle ~2d / repair ~5d / fresh-start ~14d)
        // Copy-only — streak-repair action is out of scope (economy-dependent, Gap C).
        // Never shaming; celebration > guilt (BRD §8 child-safety principle).

        // Tier 1 — gentle (~2 days idle): warm, encouraging, low pressure
        [$"LapseWinBack:LAPSE_WIN_BACK_GENTLE:{ArEg}"] = (
            "وحشتنا!",
            "يومين وانت مش معانا — رحلتك بتستناك! 🌟 ارجع النهارده وكمّل من حيث وقفت"),
        [$"LapseWinBack:LAPSE_WIN_BACK_GENTLE:{EnUs}"] = (
            "We miss you!",
            "Two days away — your journey is waiting! 🌟 Come back today and pick up where you left off"),

        // Tier 2 — repair framing (~5 days idle): streak-repair framing (copy only; no action)
        [$"LapseWinBack:LAPSE_WIN_BACK_REPAIR:{ArEg}"] = (
            "سلسلتك تستناك!",
            "⭐ رحلتك مش هتاخدك كتير — درس واحد النهارده يفرق. عد وابدأ من جديد!"),
        [$"LapseWinBack:LAPSE_WIN_BACK_REPAIR:{EnUs}"] = (
            "Your journey awaits!",
            "⭐ Getting back won't take long — just one lesson today makes a difference. Come back and restart!"),

        // Tier 3 — fresh start (~14 days idle): new-beginning framing, zero pressure
        [$"LapseWinBack:LAPSE_WIN_BACK_FRESH_START:{ArEg}"] = (
            "بداية جديدة!",
            "🌱 كل بطل بيبدأ من جديد في أي وقت! رحلتك بتستناك — ارجع وابدأ صفحة جديدة"),
        [$"LapseWinBack:LAPSE_WIN_BACK_FRESH_START:{EnUs}"] = (
            "Fresh start!",
            "🌱 Every champion can start fresh anytime! Your journey is waiting — come back and begin a new chapter"),
    };

    /// <summary>
    /// Returns the <c>(title, body)</c> template pair for the given category, notification code,
    /// and locale. Unknown locales fall back to <c>en-US</c>; unknown codes return generic fallback copy.
    /// </summary>
    public static (string Title, string Body) GetTemplate(
        NotificationCategory category,
        string code,
        string? locale)
    {
        var resolvedLocale = ResolveLocale(locale);
        var key = $"{category}:{code}:{resolvedLocale}";

        if (Templates.TryGetValue(key, out var entry))
            return entry;

        // Fallback 1: try en-US for same code
        var enKey = $"{category}:{code}:{EnUs}";
        if (Templates.TryGetValue(enKey, out var enEntry))
            return enEntry;

        // Fallback 2: generic fallback
        return ("New notification", "You have a new notification.");
    }

    /// <summary>
    /// Returns the rendered <c>(title, body)</c> pair with named placeholders replaced.
    /// <paramref name="placeholders"/> is a sequence of <c>(name, value)</c> pairs;
    /// <c>{name}</c> in the template is replaced with <c>value</c>.
    /// </summary>
    public static (string Title, string Body) Render(
        NotificationCategory category,
        string code,
        string? locale,
        params (string Name, string Value)[] placeholders)
    {
        var (title, body) = GetTemplate(category, code, locale);

        foreach (var (name, value) in placeholders)
        {
            title = title.Replace($"{{{name}}}", value, StringComparison.OrdinalIgnoreCase);
            body = body.Replace($"{{{name}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return (title, body);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string ResolveLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return ArEg; // Platform default is Arabic

        return locale.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? ArEg : EnUs;
    }
}

using FluentAssertions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Templates;
using Xunit;

namespace Modules.Notifications.UnitTests;

/// <summary>
/// Unit tests for <see cref="ReengagementCopyTemplates"/> (P4-09 B5-1).
///
/// Pure static lookup — no DI required.
/// Covers every category × both locales, placeholder substitution, unknown-code fallback,
/// and unknown-locale fallback.
///
/// Coverage map:
///   C1   StreakAtRisk  STREAK_AT_RISK  ar-EG → non-empty title + body
///   C2   StreakAtRisk  STREAK_AT_RISK  en-US → non-empty title + body
///   C3   StreakAtRisk  STREAK_BROKEN   ar-EG → non-empty
///   C4   StreakAtRisk  HEARTS_DEPLETED ar-EG → non-empty
///   C5   Achievement   BADGE_EARNED   ar-EG → body contains placeholder slot or resolved value
///   C6   Achievement   BADGE_EARNED   en-US → non-empty
///   C7   Achievement   MISSION_COMPLETED ar-EG → non-empty
///   C8   Achievement   HEARTS_REFILLED  en-US → non-empty
///   C9   DailyMissionReminder DAILY_MISSION_REMINDER ar-EG → non-empty
///   C10  LapseWinBack  LAPSE_WIN_BACK  en-US → non-empty
///   C11  Unknown code → returns generic fallback (non-empty, does not throw)
///   C12  Unknown locale (e.g. "fr-FR") → falls back to en-US template
///   C13  Render: {badgeCode} placeholder substituted correctly
///   C14  Render: {streakLength} placeholder substituted correctly
///   C15  Render: {daysIdle} placeholder substituted correctly
///   C16  ar-EG keys are Arabic text (non-ASCII)
///   C17  NotificationCategory enum has exactly 7 values (drift guard)
/// </summary>
public sealed class ReengagementCopyTemplatesTests
{
    // =========================================================================
    // C1 — StreakAtRisk / STREAK_AT_RISK / ar-EG
    // =========================================================================

    [Fact(DisplayName = "P409-C1 StreakAtRisk STREAK_AT_RISK ar-EG returns non-empty title and body")]
    public void StreakAtRisk_StreakAtRisk_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "STREAK_AT_RISK", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C2 — StreakAtRisk / STREAK_AT_RISK / en-US
    // =========================================================================

    [Fact(DisplayName = "P409-C2 StreakAtRisk STREAK_AT_RISK en-US returns non-empty title and body")]
    public void StreakAtRisk_StreakAtRisk_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "STREAK_AT_RISK", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C3 — StreakAtRisk / STREAK_BROKEN / ar-EG
    // =========================================================================

    [Fact(DisplayName = "P409-C3 StreakAtRisk STREAK_BROKEN ar-EG returns non-empty")]
    public void StreakAtRisk_StreakBroken_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "STREAK_BROKEN", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C4 — StreakAtRisk / HEARTS_DEPLETED / ar-EG
    // =========================================================================

    [Fact(DisplayName = "P409-C4 StreakAtRisk HEARTS_DEPLETED ar-EG returns non-empty")]
    public void StreakAtRisk_HeartsDepleted_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "HEARTS_DEPLETED", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C5 — Achievement / BADGE_EARNED / ar-EG
    // =========================================================================

    [Fact(DisplayName = "P409-C5 Achievement BADGE_EARNED ar-EG returns non-empty")]
    public void Achievement_BadgeEarned_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "BADGE_EARNED", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C6 — Achievement / BADGE_EARNED / en-US
    // =========================================================================

    [Fact(DisplayName = "P409-C6 Achievement BADGE_EARNED en-US returns non-empty")]
    public void Achievement_BadgeEarned_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "BADGE_EARNED", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C7 — Achievement / MISSION_COMPLETED / ar-EG
    // =========================================================================

    [Fact(DisplayName = "P409-C7 Achievement MISSION_COMPLETED ar-EG returns non-empty")]
    public void Achievement_MissionCompleted_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "MISSION_COMPLETED", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C8 — Achievement / HEARTS_REFILLED / en-US
    // =========================================================================

    [Fact(DisplayName = "P409-C8 Achievement HEARTS_REFILLED en-US returns non-empty")]
    public void Achievement_HeartsRefilled_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "HEARTS_REFILLED", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C9 — DailyMissionReminder / DAILY_MISSION_REMINDER / ar-EG
    // =========================================================================

    [Fact(DisplayName = "P409-C9 DailyMissionReminder DAILY_MISSION_REMINDER ar-EG returns non-empty")]
    public void DailyMissionReminder_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.DailyMissionReminder, "DAILY_MISSION_REMINDER", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C10 — LapseWinBack / LAPSE_WIN_BACK / en-US
    // =========================================================================

    [Fact(DisplayName = "P409-C10 LapseWinBack LAPSE_WIN_BACK en-US returns non-empty")]
    public void LapseWinBack_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.LapseWinBack, "LAPSE_WIN_BACK", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // =========================================================================
    // C11 — Unknown code → generic fallback, no exception
    // =========================================================================

    [Fact(DisplayName = "P409-C11 Unknown code returns generic fallback without throwing")]
    public void UnknownCode_ReturnsFallback_NoException()
    {
        var act = () => ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "NONEXISTENT_CODE_XYZ", "ar-EG");
        act.Should().NotThrow("unknown codes fall back gracefully");

        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "NONEXISTENT_CODE_XYZ", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace("fallback returns a non-empty title");
        body.Should().NotBeNullOrWhiteSpace("fallback returns a non-empty body");
    }

    // =========================================================================
    // C12 — Unknown locale (fr-FR) → falls back to en-US
    // =========================================================================

    [Fact(DisplayName = "P409-C12 Unknown locale fr-FR falls back to en-US template")]
    public void UnknownLocale_FallsBackToEnUs()
    {
        var (titleFr, bodyFr) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "STREAK_AT_RISK", "fr-FR");
        var (titleEn, bodyEn) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "STREAK_AT_RISK", "en-US");

        // The ResolveLocale logic maps non-Arabic to en-US fallback
        titleFr.Should().Be(titleEn, "fr-FR should fall back to en-US title");
        bodyFr.Should().Be(bodyEn, "fr-FR should fall back to en-US body");
    }

    // =========================================================================
    // C13 — Render: {badgeCode} substituted
    // =========================================================================

    [Fact(DisplayName = "P409-C13 Render substitutes {badgeCode} placeholder correctly")]
    public void Render_SubstitutesBadgeCode()
    {
        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.Achievement, "BADGE_EARNED", "en-US",
            ("badgeCode", "STREAK_3"));

        body.Should().Contain("STREAK_3",
            "{badgeCode} placeholder should be replaced with the actual badge code");
        body.Should().NotContain("{badgeCode}",
            "the literal placeholder should not remain after substitution");
    }

    // =========================================================================
    // C14 — Render: {streakLength} substituted
    // =========================================================================

    [Fact(DisplayName = "P409-C14 Render substitutes {streakLength} placeholder correctly")]
    public void Render_SubstitutesStreakLength()
    {
        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.StreakAtRisk, "STREAK_AT_RISK", "en-US",
            ("streakLength", "7"));

        body.Should().Contain("7", "{streakLength} should be replaced with '7'");
        body.Should().NotContain("{streakLength}", "literal placeholder must be replaced");
    }

    // =========================================================================
    // C15 — LapseWinBack body no longer contains {daysIdle} (F-Spot fix)
    // =========================================================================

    [Fact(DisplayName = "P409-C15 LapseWinBack body has no {daysIdle} placeholder after F-Spot fix")]
    public void Render_LapseWinBack_HasNoPlaceholderAfterFSpot()
    {
        // F-Spot: {daysIdle} was removed from the LAPSE_WIN_BACK body because DaysSinceLastActivity
        // is a days-idle count, not a mission count — "14 missions await" for a 14-day-idle child
        // was semantically wrong. The body is now timeless.
        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.LapseWinBack, "LAPSE_WIN_BACK", "en-US",
            ("daysIdle", "5"));

        body.Should().NotContain("{daysIdle}", "the template must not contain an unreplaced placeholder");
        body.Should().Contain("journey", "the new body copy must be present");
    }

    // =========================================================================
    // C16 — Arabic templates are Arabic text (non-ASCII chars present)
    // =========================================================================

    [Fact(DisplayName = "P409-C16 ar-EG templates contain Arabic characters (non-ASCII, child-safe)")]
    public void ArabicTemplate_ContainsArabicCharacters()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.StreakAtRisk, "STREAK_AT_RISK", "ar-EG");

        // Arabic Unicode range: U+0600–U+06FF
        var hasArabic = (title + body).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeTrue("ar-EG templates must contain Arabic-script characters");
    }

    // =========================================================================
    // C17 — NotificationCategory enum drift guard (7 values)
    // =========================================================================

    [Fact(DisplayName = "P409-C17 NotificationCategory has exactly 7 values — drift guard")]
    public void NotificationCategory_HasExactlySevenValues()
    {
        var values = Enum.GetValues<NotificationCategory>();
        values.Should().HaveCount(7,
            "NotificationCategory must have exactly 7 values: " +
            "WeeklyReport=0, StreakAtRisk=1, ProductAnnouncement=2, Achievement=3, " +
            "DailyMissionReminder=4, LapseWinBack=5, System=6. " +
            "If you add a value, update this test and the ReengagementCopyTemplates lookup.");
    }

    [Fact(DisplayName = "P409-C17b NotificationCategory integer values match spec")]
    public void NotificationCategory_IntegerValues_MatchSpec()
    {
        ((int)NotificationCategory.WeeklyReport).Should().Be(0);
        ((int)NotificationCategory.StreakAtRisk).Should().Be(1);
        ((int)NotificationCategory.ProductAnnouncement).Should().Be(2);
        ((int)NotificationCategory.Achievement).Should().Be(3);
        ((int)NotificationCategory.DailyMissionReminder).Should().Be(4);
        ((int)NotificationCategory.LapseWinBack).Should().Be(5);
        ((int)NotificationCategory.System).Should().Be(6);
    }

    // =========================================================================
    // P9-06 new templates — STREAK_MILESTONE + WEEKLY_RECAP
    // =========================================================================

    // C18 — Achievement / STREAK_MILESTONE / ar-EG → non-empty
    [Fact(DisplayName = "P906-C18 Achievement STREAK_MILESTONE ar-EG returns non-empty title and body")]
    public void Achievement_StreakMilestone_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "STREAK_MILESTONE", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // C19 — Achievement / STREAK_MILESTONE / en-US → non-empty
    [Fact(DisplayName = "P906-C19 Achievement STREAK_MILESTONE en-US returns non-empty title and body")]
    public void Achievement_StreakMilestone_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "STREAK_MILESTONE", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // C20 — STREAK_MILESTONE body contains {streakLength} placeholder before Render
    [Fact(DisplayName = "P906-C20 STREAK_MILESTONE raw body contains {streakLength} placeholder")]
    public void Achievement_StreakMilestone_BodyContainsPlaceholder()
    {
        var (_, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "STREAK_MILESTONE", "en-US");

        body.Should().Contain("{streakLength}", "the template must carry the {streakLength} placeholder");
    }

    // C21 — Render STREAK_MILESTONE substitutes {streakLength}
    [Fact(DisplayName = "P906-C21 Render STREAK_MILESTONE substitutes {streakLength} correctly")]
    public void Render_StreakMilestone_SubstitutesStreakLength()
    {
        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.Achievement, "STREAK_MILESTONE", "en-US",
            ("streakLength", "7"));

        body.Should().Contain("7", "{streakLength} should be replaced with '7'");
        body.Should().NotContain("{streakLength}", "literal placeholder must be removed after render");
    }

    // C22 — WeeklyReport / WEEKLY_RECAP / ar-EG → non-empty
    [Fact(DisplayName = "P906-C22 WeeklyReport WEEKLY_RECAP ar-EG returns non-empty title and body")]
    public void WeeklyReport_WeeklyRecap_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.WeeklyReport, "WEEKLY_RECAP", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // C23 — WeeklyReport / WEEKLY_RECAP / en-US → non-empty
    [Fact(DisplayName = "P906-C23 WeeklyReport WEEKLY_RECAP en-US returns non-empty title and body")]
    public void WeeklyReport_WeeklyRecap_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.WeeklyReport, "WEEKLY_RECAP", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // C24 — Render WEEKLY_RECAP substitutes {xp} and {skills}
    [Fact(DisplayName = "P906-C24 Render WEEKLY_RECAP substitutes {xp} and {skills} correctly")]
    public void Render_WeeklyRecap_SubstitutesXpAndSkills()
    {
        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.WeeklyReport, "WEEKLY_RECAP", "en-US",
            ("xp", "350"),
            ("skills", "4"));

        body.Should().Contain("350", "{xp} should be replaced with '350'");
        body.Should().Contain("4",   "{skills} should be replaced with '4'");
        body.Should().NotContain("{xp}",     "literal {xp} placeholder must be removed");
        body.Should().NotContain("{skills}", "literal {skills} placeholder must be removed");
    }

    // C25 — WEEKLY_RECAP ar-EG template contains Arabic characters
    [Fact(DisplayName = "P906-C25 WEEKLY_RECAP ar-EG template contains Arabic characters")]
    public void WeeklyRecap_Arabic_ContainsArabicCharacters()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.WeeklyReport, "WEEKLY_RECAP", "ar-EG");

        // Arabic Unicode range: U+0600–U+06FF
        var hasArabic = (title + body).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeTrue("ar-EG weekly recap template must contain Arabic-script characters");
    }

    // C26 — STREAK_MILESTONE ar-EG template contains Arabic characters
    [Fact(DisplayName = "P906-C26 STREAK_MILESTONE ar-EG template contains Arabic characters")]
    public void StreakMilestone_Arabic_ContainsArabicCharacters()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.Achievement, "STREAK_MILESTONE", "ar-EG");

        var hasArabic = (title + body).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeTrue("ar-EG streak milestone template must contain Arabic-script characters");
    }
}

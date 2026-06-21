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
    // C17 — NotificationCategory enum drift guard (8 values as of P9-09 + P9-10)
    // =========================================================================

    [Fact(DisplayName = "P409-C17 NotificationCategory has exactly 10 values — drift guard (updated for P9-12 + P9-06 additions)")]
    public void NotificationCategory_HasExactlyTenValues()
    {
        var values = Enum.GetValues<NotificationCategory>();
        values.Should().HaveCount(10,
            "NotificationCategory must have exactly 10 values: " +
            "WeeklyReport=0, StreakAtRisk=1, ProductAnnouncement=2, Achievement=3, " +
            "DailyMissionReminder=4, LapseWinBack=5, System=6, ReviewReminder=7, " +
            "WeeklyChallenge=8 (P9-06), TimedEvent=9 (P9-12). " +
            "If you add a value, update this test and the ReengagementCopyTemplates lookup.");
    }

    [Fact(DisplayName = "P409-C17b NotificationCategory integer values match spec (including P9-06 WeeklyChallenge + P9-12 TimedEvent)")]
    public void NotificationCategory_IntegerValues_MatchSpec()
    {
        ((int)NotificationCategory.WeeklyReport).Should().Be(0);
        ((int)NotificationCategory.StreakAtRisk).Should().Be(1);
        ((int)NotificationCategory.ProductAnnouncement).Should().Be(2);
        ((int)NotificationCategory.Achievement).Should().Be(3);
        ((int)NotificationCategory.DailyMissionReminder).Should().Be(4);
        ((int)NotificationCategory.LapseWinBack).Should().Be(5);
        ((int)NotificationCategory.System).Should().Be(6);
        ((int)NotificationCategory.ReviewReminder).Should().Be(7);
        ((int)NotificationCategory.WeeklyChallenge).Should().Be(8, "P9-06 WeeklyChallenge must be 8");
        ((int)NotificationCategory.TimedEvent).Should().Be(9, "P9-12 TimedEvent must be 9");
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

    // =========================================================================
    // P9-10 WELCOME template tests (BE-6)
    // =========================================================================

    // C26 — System / WELCOME / ar-EG returns non-empty title and body
    [Fact(DisplayName = "P910-C26 System WELCOME ar-EG returns non-empty title and body")]
    public void System_Welcome_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "WELCOME", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // C27 — System / WELCOME / en-US returns non-empty title and body
    [Fact(DisplayName = "P910-C27 System WELCOME en-US returns non-empty title and body")]
    public void System_Welcome_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "WELCOME", "en-US");

        title.Should().NotBeNullOrWhiteSpace();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // C28 — Render WELCOME ar-EG substitutes {userName} and leaves no literal placeholder
    [Fact(DisplayName = "P910-C28 Render WELCOME ar-EG substitutes {userName} placeholder correctly")]
    public void Render_Welcome_Arabic_SubstitutesUserName()
    {
        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "WELCOME", "ar-EG",
            ("userName", "أحمد"));

        body.Should().Contain("أحمد", "{userName} should be replaced with the actual user name");
        body.Should().NotContain("{userName}", "literal placeholder must not remain after substitution");
        title.Should().NotBeNullOrWhiteSpace();
    }

    // C29 — Render WELCOME en-US substitutes {userName} and leaves no literal placeholder
    [Fact(DisplayName = "P910-C29 Render WELCOME en-US substitutes {userName} placeholder correctly")]
    public void Render_Welcome_English_SubstitutesUserName()
    {
        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "WELCOME", "en-US",
            ("userName", "Ahmed"));

        body.Should().Contain("Ahmed", "{userName} should be replaced with 'Ahmed'");
        body.Should().NotContain("{userName}", "literal placeholder must not remain after substitution");
        title.Should().NotBeNullOrWhiteSpace();
    }

    // C30 — WELCOME unknown locale (fr-FR) falls back to en-US
    [Fact(DisplayName = "P910-C30 WELCOME unknown locale fr-FR falls back to en-US template")]
    public void Welcome_UnknownLocale_FallsBackToEnUs()
    {
        var (titleFr, bodyFr) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "WELCOME", "fr-FR");
        var (titleEn, bodyEn) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "WELCOME", "en-US");

        titleFr.Should().Be(titleEn, "fr-FR should fall back to en-US title for WELCOME");
        bodyFr.Should().Be(bodyEn, "fr-FR should fall back to en-US body for WELCOME");
    }

    // C31 — WELCOME ar-EG title and body contain Arabic characters (ar-first principle)
    [Fact(DisplayName = "P910-C31 WELCOME ar-EG template contains Arabic characters")]
    public void Welcome_Arabic_ContainsArabicCharacters()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "WELCOME", "ar-EG");

        // Arabic Unicode range: U+0600–U+06FF
        var hasArabic = (title + body).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeTrue("ar-EG WELCOME template must contain Arabic-script characters (ar-first)");
    }

    // C32 — Missing code (UNKNOWN_CODE) → generic fallback, non-empty, no key-leaking string
    [Fact(DisplayName = "P910-C32 Unknown code for System category returns non-empty generic fallback")]
    public void System_UnknownCode_ReturnsGenericFallback()
    {
        var act = () => ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "UNKNOWN_CODE_XYZ", "ar-EG");
        act.Should().NotThrow("unknown codes for System category must fall back gracefully");

        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "UNKNOWN_CODE_XYZ", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace("generic fallback title must not be empty");
        body.Should().NotBeNullOrWhiteSpace("generic fallback body must not be empty");
        // Guard: fallback must not echo a key-like pattern (e.g. "System:UNKNOWN_CODE_XYZ:ar-EG")
        title.Should().NotContain(":", "generic fallback must not contain a key-leaking colon pattern");
    }

    // =========================================================================
    // P6-06 BE-2 PASSWORD_RESET template tests
    //
    // Coverage map:
    //   C33  System / PASSWORD_RESET / ar-EG returns non-empty title and body
    //   C34  System / PASSWORD_RESET / en-US returns non-empty title and body
    //   C35  ar-EG title + body contain Arabic characters (ar-first principle)
    //   C36  en-US title + body do NOT contain Arabic characters
    //   C37  Render ar-EG substitutes {userName} — no literal placeholder remains
    //   C38  Render en-US substitutes {userName} — no literal placeholder remains
    //   C39  Render ar-EG substitutes {resetUrl} — no literal placeholder remains
    //   C40  Render en-US substitutes {resetUrl} — no literal placeholder remains
    //   C41  Render ar-EG: both placeholders substituted simultaneously
    //   C42  Render en-US: both placeholders substituted simultaneously
    //   C43  ar-EG body contains <a href link pattern after {resetUrl} substitution
    //   C44  en-US body contains <a href link pattern after {resetUrl} substitution
    //   C45  Unknown locale (fr-FR) falls back to en-US for PASSWORD_RESET
    //   C46  ar-EG title and en-US title are DIFFERENT (locale branching confirmed)
    // =========================================================================

    // C33 — System / PASSWORD_RESET / ar-EG returns non-empty title and body
    [Fact(DisplayName = "P606-C33 System PASSWORD_RESET ar-EG returns non-empty title and body")]
    public void PasswordReset_Arabic_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG");

        title.Should().NotBeNullOrWhiteSpace("ar-EG PASSWORD_RESET title must not be empty");
        body.Should().NotBeNullOrWhiteSpace("ar-EG PASSWORD_RESET body must not be empty");
    }

    // C34 — System / PASSWORD_RESET / en-US returns non-empty title and body
    [Fact(DisplayName = "P606-C34 System PASSWORD_RESET en-US returns non-empty title and body")]
    public void PasswordReset_English_ReturnsContent()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "en-US");

        title.Should().NotBeNullOrWhiteSpace("en-US PASSWORD_RESET title must not be empty");
        body.Should().NotBeNullOrWhiteSpace("en-US PASSWORD_RESET body must not be empty");
    }

    // C35 — ar-EG PASSWORD_RESET title + body contain Arabic characters
    [Fact(DisplayName = "P606-C35 PASSWORD_RESET ar-EG template contains Arabic characters (ar-first principle)")]
    public void PasswordReset_Arabic_ContainsArabicCharacters()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG");

        // Arabic Unicode range: U+0600–U+06FF
        var hasArabic = (title + body).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeTrue(
            "ar-EG PASSWORD_RESET template must contain Arabic-script characters (Arabic-first platform promise, BRD §8). " +
            "Title: '{0}'", title);
    }

    // C36 — en-US PASSWORD_RESET does NOT contain Arabic characters
    [Fact(DisplayName = "P606-C36 PASSWORD_RESET en-US template does NOT contain Arabic characters")]
    public void PasswordReset_English_NoArabicCharacters()
    {
        var (title, body) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "en-US");

        var hasArabic = (title + body).Any(c => c >= '؀' && c <= 'ۿ');
        hasArabic.Should().BeFalse(
            "en-US PASSWORD_RESET template must NOT contain Arabic characters. Title: '{0}'", title);
    }

    // C37 — Render ar-EG substitutes {userName}; no literal placeholder remains
    [Fact(DisplayName = "P606-C37 Render PASSWORD_RESET ar-EG substitutes {userName} placeholder correctly")]
    public void Render_PasswordReset_Arabic_SubstitutesUserName()
    {
        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG",
            ("userName", "أحمد"),
            ("resetUrl", "http://test.example/reset"));

        body.Should().Contain("أحمد",
            "P6-06 BE-2: {userName} must be replaced with the actual Arabic name in ar-EG body");
        body.Should().NotContain("{userName}",
            "P6-06 BE-2: literal {userName} placeholder must not remain after substitution in ar-EG body");
        title.Should().NotContain("{userName}",
            "P6-06 BE-2: literal {userName} placeholder must not remain in ar-EG title either");
    }

    // C38 — Render en-US substitutes {userName}; no literal placeholder remains
    [Fact(DisplayName = "P606-C38 Render PASSWORD_RESET en-US substitutes {userName} placeholder correctly")]
    public void Render_PasswordReset_English_SubstitutesUserName()
    {
        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "en-US",
            ("userName", "Ahmed"),
            ("resetUrl", "http://test.example/reset"));

        body.Should().Contain("Ahmed",
            "P6-06 BE-2: {userName} must be replaced with 'Ahmed' in en-US body");
        body.Should().NotContain("{userName}",
            "P6-06 BE-2: literal {userName} placeholder must not remain in en-US body");
    }

    // C39 — Render ar-EG substitutes {resetUrl}; no literal placeholder remains
    [Fact(DisplayName = "P606-C39 Render PASSWORD_RESET ar-EG substitutes {resetUrl} placeholder correctly")]
    public void Render_PasswordReset_Arabic_SubstitutesResetUrl()
    {
        const string testUrl = "http://localhost:3000/reset-password?email=test%40test.com&token=abc123";

        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG",
            ("userName", "أحمد"),
            ("resetUrl", testUrl));

        body.Should().Contain(testUrl,
            "P6-06 BE-2: {resetUrl} must be replaced with the actual reset URL in ar-EG body");
        body.Should().NotContain("{resetUrl}",
            "P6-06 BE-2: literal {resetUrl} placeholder must not remain in ar-EG body");
    }

    // C40 — Render en-US substitutes {resetUrl}; no literal placeholder remains
    [Fact(DisplayName = "P606-C40 Render PASSWORD_RESET en-US substitutes {resetUrl} placeholder correctly")]
    public void Render_PasswordReset_English_SubstitutesResetUrl()
    {
        const string testUrl = "https://app.learnexia.com/reset-password?email=user%40mail.com&token=xyz";

        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "en-US",
            ("userName", "TestUser"),
            ("resetUrl", testUrl));

        body.Should().Contain(testUrl,
            "P6-06 BE-2: {resetUrl} must be replaced with the actual reset URL in en-US body");
        body.Should().NotContain("{resetUrl}",
            "P6-06 BE-2: literal {resetUrl} placeholder must not remain in en-US body");
    }

    // C41 — Render ar-EG: both {userName} and {resetUrl} substituted simultaneously;
    //         no unresolved placeholders remain.
    [Fact(DisplayName = "P606-C41 Render PASSWORD_RESET ar-EG substitutes BOTH placeholders with no leftovers")]
    public void Render_PasswordReset_Arabic_BothPlaceholderssSubstituted_NoLeftovers()
    {
        const string testUrl = "http://example.com/r?token=tok123";

        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG",
            ("userName", "علي"),
            ("resetUrl", testUrl));

        body.Should().Contain("علي", "userName substituted in ar-EG body");
        body.Should().Contain(testUrl, "resetUrl substituted in ar-EG body");

        // Critical: no raw placeholder must survive in title or body
        body.Should().NotContain("{userName}", "no {userName} placeholder in ar-EG body after Render");
        body.Should().NotContain("{resetUrl}", "no {resetUrl} placeholder in ar-EG body after Render");
        title.Should().NotContain("{userName}", "no {userName} placeholder in ar-EG title");
        title.Should().NotContain("{resetUrl}", "no {resetUrl} placeholder in ar-EG title");
    }

    // C42 — Render en-US: both {userName} and {resetUrl} substituted simultaneously;
    //         no unresolved placeholders remain.
    [Fact(DisplayName = "P606-C42 Render PASSWORD_RESET en-US substitutes BOTH placeholders with no leftovers")]
    public void Render_PasswordReset_English_BothPlaceholdersSubstituted_NoLeftovers()
    {
        const string testUrl = "https://app.example.com/reset?token=en-tok-456";

        var (title, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "en-US",
            ("userName", "Sara"),
            ("resetUrl", testUrl));

        body.Should().Contain("Sara", "userName substituted in en-US body");
        body.Should().Contain(testUrl, "resetUrl substituted in en-US body");

        body.Should().NotContain("{userName}", "no {userName} placeholder in en-US body");
        body.Should().NotContain("{resetUrl}", "no {resetUrl} placeholder in en-US body");
        title.Should().NotContain("{userName}", "no {userName} placeholder in en-US title");
        title.Should().NotContain("{resetUrl}", "no {resetUrl} placeholder in en-US title");
    }

    // C43 — ar-EG body contains an <a href= link after {resetUrl} substitution
    //         (the template uses <a href="{resetUrl}">...</a> shape per the brief)
    [Fact(DisplayName = "P606-C43 PASSWORD_RESET ar-EG rendered body contains <a href link (reset URL is linked)")]
    public void Render_PasswordReset_Arabic_BodyContainsHrefLink()
    {
        const string testUrl = "http://localhost:3000/reset-password?email=t%40t.com&token=TOKEN";

        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG",
            ("userName", "مستخدم"),
            ("resetUrl", testUrl));

        // The template must embed the link in an <a href="..."> element
        body.Should().Contain("<a href=",
            "P6-06 BE-2: ar-EG PASSWORD_RESET body must contain an <a href link for the reset URL " +
            "(template uses <a href=\"{resetUrl}\">...</a> per the brief). Actual body: {0}", body);
        body.Should().Contain(testUrl,
            "P6-06 BE-2: the reset URL must appear in the ar-EG body link; body: {0}", body);
    }

    // C44 — en-US body contains an <a href= link after {resetUrl} substitution
    [Fact(DisplayName = "P606-C44 PASSWORD_RESET en-US rendered body contains <a href link (reset URL is linked)")]
    public void Render_PasswordReset_English_BodyContainsHrefLink()
    {
        const string testUrl = "https://app.learnexia.com/reset-password?email=e%40e.com&token=TOKEN";

        var (_, body) = ReengagementCopyTemplates.Render(
            NotificationCategory.System, "PASSWORD_RESET", "en-US",
            ("userName", "User"),
            ("resetUrl", testUrl));

        body.Should().Contain("<a href=",
            "P6-06 BE-2: en-US PASSWORD_RESET body must contain an <a href link. Actual body: {0}", body);
        body.Should().Contain(testUrl,
            "P6-06 BE-2: the reset URL must appear in the en-US body link; body: {0}", body);
    }

    // C45 — Unknown locale (fr-FR) falls back to en-US for PASSWORD_RESET
    [Fact(DisplayName = "P606-C45 PASSWORD_RESET unknown locale fr-FR falls back to en-US template")]
    public void PasswordReset_UnknownLocale_FallsBackToEnUs()
    {
        var (titleFr, bodyFr) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "fr-FR");
        var (titleEn, bodyEn) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "en-US");

        titleFr.Should().Be(titleEn,
            "P6-06 BE-2: fr-FR must fall back to en-US title for PASSWORD_RESET (unknown locale falls back per ResolveLocale)");
        bodyFr.Should().Be(bodyEn,
            "P6-06 BE-2: fr-FR must fall back to en-US body for PASSWORD_RESET");
    }

    // C46 — ar-EG and en-US PASSWORD_RESET titles are DIFFERENT (locale branching)
    [Fact(DisplayName = "P606-C46 PASSWORD_RESET ar-EG and en-US titles are DIFFERENT (locale branching confirmed)")]
    public void PasswordReset_ArEgAndEnUs_HaveDifferentTitles()
    {
        var (titleAr, _) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "ar-EG");
        var (titleEn, _) = ReengagementCopyTemplates.GetTemplate(
            NotificationCategory.System, "PASSWORD_RESET", "en-US");

        titleAr.Should().NotBe(titleEn,
            "P6-06 BE-2: ar-EG and en-US PASSWORD_RESET titles must be DIFFERENT, confirming the " +
            "locale branching in ReengagementCopyTemplates.GetTemplate actually routes to distinct entries. " +
            "ar: '{0}'; en: '{1}'", titleAr, titleEn);
    }

}

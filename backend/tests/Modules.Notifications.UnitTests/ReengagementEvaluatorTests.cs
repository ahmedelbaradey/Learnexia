using FluentAssertions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Xunit;

namespace Modules.Notifications.UnitTests;

/// <summary>
/// Unit tests for <see cref="ReengagementEvaluator"/> (P4-09 B5-1).
///
/// Pure static helpers — no DbContext, no DI required.
/// Every branch of <c>Evaluate</c> is covered: channel-disabled, daily-cap, quiet-hours
/// (same-day window, cross-midnight window, Africa/Cairo TZ, invalid TZ fallback).
///
/// Coverage map:
///   E1  Null pref replaced by default: uses conservative defaults (Push=false, InApp=true, DailyCap=3)
///   E2  Both channels OFF → DisabledByParent
///   E3  Outside quiet window → ShouldPush stays true (Eligible)
///   E4  Inside quiet window (22:00–08:00), now=23:00 → QuietHours
///   E5  Wrap-around midnight, now=00:30 (between midnight and 08:00) → QuietHours
///   E6  Africa/Cairo TZ: UTC 20:00 = Cairo 23:00 → inside 22:00–08:00 window
///   E7  Invalid TimeZoneId → falls back to UTC (no exception, evaluates in UTC)
///   E8  now exactly at quiet-start (22:00:00) → inside window → QuietHours
///   E9  now exactly at quiet-end (08:00:00) → NOT inside window (exclusive end) → Eligible
///   E10 DailyCap=0 → DailyCapReached even with sentsToday=0
///   E11 DailyCap=3, sentsToday=2 → Eligible
///   E12 DailyCap=3, sentsToday=3 → DailyCapReached
///   E13 InApp=true, Push=false → Eligible (not disabled; quiet-hours skipped for push-off)
/// </summary>
public sealed class ReengagementEvaluatorTests
{
    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Builds a minimal <see cref="ChildReengagementPreference"/> using the static factory.
    /// We then mutate it via the public Update* methods to set test-specific values.
    /// </summary>
    private static ChildReengagementPreference MakePref(
        bool push, bool inApp,
        TimeOnly quietStart, TimeOnly quietEnd,
        string tzId = "UTC",
        int dailyCap = 3)
    {
        // CreateDefault sets conservative values; override with test values.
        var pref = ChildReengagementPreference.CreateDefault(1, 2, NotificationCategory.StreakAtRisk);
        pref.UpdateChannels(email: false, push: push, inApp: inApp);
        pref.UpdateQuietHours(quietStart, quietEnd, tzId);
        pref.UpdateDailyCap(dailyCap);
        return pref;
    }

    // =========================================================================
    // E1 — Conservative defaults (CreateDefault: Push=false, InApp=true, DailyCap=3)
    // =========================================================================

    [Fact(DisplayName = "P409-E1 CreateDefault prefs: Push=false → Eligible (inApp path, not disabled)")]
    public void DefaultPrefs_PushFalseInAppTrue_IsEligible()
    {
        var pref    = ChildReengagementPreference.CreateDefault(1, 2, NotificationCategory.StreakAtRisk);
        var nowUtc  = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result  = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        // Push=false means quiet-hours check is skipped; InApp=true so not DisabledByParent
        result.Eligible.Should().BeTrue("default prefs have InApp=true, so nudge is eligible via in-app channel");
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // E2 — Both channels OFF → DisabledByParent
    // =========================================================================

    [Fact(DisplayName = "P409-E2 Both Push=false InApp=false → DisabledByParent")]
    public void BothChannelsOff_ReturnsDisabledByParent()
    {
        var pref    = MakePref(push: false, inApp: false, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc  = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result  = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeFalse();
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.DisabledByParent);
    }

    // =========================================================================
    // E3 — Outside quiet window → still eligible (push allowed)
    // =========================================================================

    [Fact(DisplayName = "P409-E3 now=14:00 UTC outside 22:00–08:00 UTC → Eligible")]
    public void OutsideQuietWindow_IsEligible()
    {
        var pref    = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc  = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc); // 14:00 UTC — well outside 22–08
        var result  = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeTrue("14:00 UTC is not within the 22:00–08:00 quiet window");
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // E4 — Inside quiet window, evening side (23:00 UTC, 22:00–08:00 UTC) → QuietHours
    // =========================================================================

    [Fact(DisplayName = "P409-E4 now=23:00 UTC inside 22:00–08:00 UTC → QuietHours")]
    public void InsideQuietWindow_Evening_ReturnsQuietHours()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc = new DateTime(2026, 6, 1, 23, 0, 0, DateTimeKind.Utc);
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeFalse();
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.QuietHours);
    }

    // =========================================================================
    // E5 — Cross-midnight wrap, now=00:30 UTC (between midnight and 08:00) → QuietHours
    // =========================================================================

    [Fact(DisplayName = "P409-E5 now=00:30 UTC inside wrap-around 22:00–08:00 UTC → QuietHours")]
    public void InsideQuietWindow_MidnightSide_ReturnsQuietHours()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc = new DateTime(2026, 6, 2, 0, 30, 0, DateTimeKind.Utc); // 00:30 — within 00:00..08:00
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeFalse();
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.QuietHours);
    }

    // =========================================================================
    // E6 — Africa/Cairo TZ: UTC 20:00 = Cairo 23:00 (UTC+3, no DST in 2026)
    //       → inside 22:00–08:00 Cairo window → QuietHours
    // =========================================================================

    [Fact(DisplayName = "P409-E6 Africa/Cairo: nowUtc=20:00 → 23:00 Cairo → QuietHours")]
    public void CairoTz_UtcEveningMapsToLocalNight_QuietHours()
    {
        // Skip if Africa/Cairo TZ is not available on this OS (CI guard).
        if (!TryFindTimeZone("Africa/Cairo"))
            return;

        var pref = MakePref(push: true, inApp: true,
            new TimeOnly(22, 0), new TimeOnly(8, 0), tzId: "Africa/Cairo");
        var nowUtc = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc); // UTC 20:00 = Cairo 23:00 (UTC+3)
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeFalse(
            "UTC 20:00 = Cairo 23:00 which is inside the 22:00–08:00 Cairo quiet window");
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.QuietHours);
    }

    // =========================================================================
    // E7 — Invalid TimeZoneId → fallback to UTC (no exception thrown)
    // =========================================================================

    [Fact(DisplayName = "P409-E7 Invalid TimeZoneId falls back to UTC — no exception")]
    public void InvalidTimeZoneId_FallsBackToUtc_NoException()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0),
                              tzId: "Not/A/Real/TZ");
        var nowUtc = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc); // 14:00 UTC — outside 22–08

        // Must not throw; falls back to UTC and evaluates in UTC → outside window → Eligible
        var act = () => ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);
        act.Should().NotThrow("invalid TZ falls back to UTC without exception");

        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);
        result.Eligible.Should().BeTrue("14:00 UTC is outside the 22:00–08:00 window after UTC fallback");
    }

    // =========================================================================
    // E8 — Edge: now exactly at quiet-start (22:00:00) → INSIDE window
    // =========================================================================

    [Fact(DisplayName = "P409-E8 now exactly 22:00:00 UTC (quiet-start boundary) → QuietHours")]
    public void AtQuietStart_IsInsideWindow()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc = new DateTime(2026, 6, 1, 22, 0, 0, DateTimeKind.Utc); // exactly 22:00
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeFalse("22:00 is the start of the quiet window — it is blocked (>=)");
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.QuietHours);
    }

    // =========================================================================
    // E9 — Edge: now exactly at quiet-end (08:00:00) → OUTSIDE window (exclusive end)
    // =========================================================================

    [Fact(DisplayName = "P409-E9 now exactly 08:00:00 UTC (quiet-end boundary) → Eligible (exclusive end)")]
    public void AtQuietEnd_IsOutsideWindow()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc); // exactly 08:00
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        // The implementation uses: localTime < endLocal (not <=), so 08:00 is NOT inside.
        result.Eligible.Should().BeTrue("08:00 is the exclusive end of the quiet window — it is no longer blocked");
    }

    // =========================================================================
    // E10 — DailyCap=0 blocks all nudges
    // =========================================================================

    [Fact(DisplayName = "P409-E10 DailyCap=0 → DailyCapReached even with sentsToday=0")]
    public void DailyCap_Zero_BlocksAll()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0), dailyCap: 0);
        var nowUtc = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeFalse();
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.DailyCapReached,
            "cap of 0 means no nudges allowed at all; sentsToday=0 >= cap=0");
    }

    // =========================================================================
    // E11 — DailyCap=3, sentsToday=2 → Eligible (under cap)
    // =========================================================================

    [Fact(DisplayName = "P409-E11 DailyCap=3, sentsToday=2 → Eligible")]
    public void UnderDailyCap_IsEligible()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0), dailyCap: 3);
        var nowUtc = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 2);

        result.Eligible.Should().BeTrue("2 < 3, still under cap");
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // E12 — DailyCap=3, sentsToday=3 → DailyCapReached
    // =========================================================================

    [Fact(DisplayName = "P409-E12 DailyCap=3, sentsToday=3 → DailyCapReached")]
    public void AtDailyCap_IsCapReached()
    {
        var pref   = MakePref(push: true, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0), dailyCap: 3);
        var nowUtc = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 3);

        result.Eligible.Should().BeFalse();
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.DailyCapReached);
    }

    // =========================================================================
    // E13 — Push=false, InApp=true → Eligible (quiet-hours skipped for push-off prefs)
    // =========================================================================

    [Fact(DisplayName = "P409-E13 Push=false InApp=true during quiet hours → Eligible (push off, quiet-hours skipped)")]
    public void PushOffInAppOn_DuringQuietHours_IsEligible()
    {
        // Push=false means quiet-hours suppression only applies to push channel, which is disabled.
        // InApp=true means the nudge can still go to in-app, so not DisabledByParent.
        var pref   = MakePref(push: false, inApp: true, new TimeOnly(22, 0), new TimeOnly(8, 0));
        var nowUtc = new DateTime(2026, 6, 1, 23, 0, 0, DateTimeKind.Utc); // 23:00 = inside quiet window
        var result = ReengagementEvaluator.Evaluate(pref, nowUtc, sentsToday: 0);

        result.Eligible.Should().BeTrue(
            "Push is disabled so quiet-hours check is skipped; InApp=true means eligible");
        result.Reason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static bool TryFindTimeZone(string id)
    {
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch { return false; }
    }
}

using Learnexia.Modules.Notifications.Domain.Entities;

namespace Learnexia.Modules.Notifications.Domain.Services;

/// <summary>
/// Pure-static eligibility evaluator for re-engagement nudges (P4-09 B4-1 / AC3).
/// No DB access, no DI — fully unit-testable.
///
/// Decision order (short-circuits on first ineligible condition):
/// 1. Both channels disabled → <see cref="NotEligibleReason.DisabledByParent"/>.
/// 2. <c>sentsToday &gt;= prefs.DailyCap</c> → <see cref="NotEligibleReason.DailyCapReached"/>.
/// 3. <c>nowUtc</c> falls inside the quiet-hours window (push only) →
///    <see cref="NotEligibleReason.QuietHours"/> (still writes in-app inbox; only push is suppressed).
///
/// Quiet-hours logic handles cross-midnight windows (e.g. 22:00–08:00):
/// if <c>startLocal &gt; endLocal</c> the window wraps midnight →
/// blocked when <c>localTime &gt;= startLocal OR localTime &lt; endLocal</c>.
///
/// Timezone conversion: <see cref="TimeZoneInfo.ConvertTimeFromUtc"/> with fallback to UTC
/// if the IANA ID is unresolvable.
/// </summary>
public static class ReengagementEvaluator
{
    /// <summary>Reasons a nudge is ineligible. <see cref="None"/> means the nudge may proceed.</summary>
    public enum NotEligibleReason
    {
        None,
        DisabledByParent,
        DailyCapReached,
        QuietHours,
    }

    /// <summary>Result returned by <see cref="Evaluate"/>.</summary>
    public readonly record struct EvalResult(bool Eligible, NotEligibleReason Reason);

    /// <summary>
    /// Evaluates whether a nudge should be dispatched given the parent-set preference row,
    /// current UTC time, and how many nudges have already been sent today for this (child, category) pair.
    /// </summary>
    /// <param name="prefs">
    ///   The parent-controlled preference row. Must not be null — callers should synthesise a
    ///   default instance if no row exists yet.
    /// </param>
    /// <param name="nowUtc">Wall-clock UTC instant (from <c>ISystemClock.UtcNow</c>).</param>
    /// <param name="sentsToday">
    ///   Number of nudges already dispatched today for the same (child, category) combination.
    ///   Sourced from Redis dedupe count or DB fallback.
    /// </param>
    public static EvalResult Evaluate(
        ChildReengagementPreference prefs,
        DateTime nowUtc,
        int sentsToday)
    {
        // 1. Both channels off → parent has explicitly disabled this category.
        if (!prefs.Push && !prefs.InApp)
            return new EvalResult(false, NotEligibleReason.DisabledByParent);

        // 2. Daily cap reached → no more nudges for this (child, category) today.
        if (sentsToday >= prefs.DailyCap)
            return new EvalResult(false, NotEligibleReason.DailyCapReached);

        // 3. Quiet-hours check — applies to push channel only; in-app inbox is unaffected.
        //    If push is already disabled by parent, no need to check quiet hours for push.
        if (prefs.Push)
        {
            var isQuiet = IsInQuietWindow(nowUtc, prefs.QuietHoursStartLocal, prefs.QuietHoursEndLocal, prefs.TimeZoneId);
            if (isQuiet)
                return new EvalResult(false, NotEligibleReason.QuietHours);
        }

        return new EvalResult(true, NotEligibleReason.None);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if <paramref name="nowUtc"/> falls inside the quiet window
    /// [<paramref name="startLocal"/>, <paramref name="endLocal"/>] when converted to the
    /// specified <paramref name="timeZoneId"/> (IANA or Windows ID).
    /// Handles cross-midnight windows (start &gt; end).
    /// </summary>
    private static bool IsInQuietWindow(
        DateTime nowUtc,
        TimeOnly startLocal,
        TimeOnly endLocal,
        string timeZoneId)
    {
        var localNow = ConvertToLocalTime(nowUtc, timeZoneId);
        var localTime = TimeOnly.FromTimeSpan(localNow.TimeOfDay);

        // Cross-midnight window (e.g. 22:00 → 08:00): start > end
        if (startLocal > endLocal)
        {
            // Blocked when: localTime >= start (evening side) OR localTime < end (morning side)
            return localTime >= startLocal || localTime < endLocal;
        }

        // Same-day window (e.g. 13:00 → 14:00): start <= end
        return localTime >= startLocal && localTime < endLocal;
    }

    private static DateTime ConvertToLocalTime(DateTime nowUtc, string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        }
        catch
        {
            // Fallback to UTC if the TZ ID cannot be resolved.
            return nowUtc;
        }
    }
}

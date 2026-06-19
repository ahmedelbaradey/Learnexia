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

        // ── P9-07 arbitration reasons ────────────────────────────────────────────
        /// <summary>
        /// The child's global per-day push budget (across ALL categories) has been reached.
        /// The in-app inbox row is still written; only the push channel is suppressed.
        /// Budget authority is the DB-derived push count from <c>INotificationInboxService.CountPushesSentTodayAsync</c>
        /// so this guard is upheld even when Redis is unavailable.
        /// </summary>
        GlobalBudgetExhausted,

        /// <summary>
        /// A higher-priority category consumed the last available push slot in the child's daily
        /// budget before this nudge was dispatched. Priority order is config-driven
        /// (StreakAtRisk &gt; DailyMission &gt; LapseWinBack &gt; Achievement &gt; WeeklyReport/recap).
        /// Because handlers fire independently per integration event (no batch window / queue),
        /// priority is realised EMERGENTLY: higher-priority types consume the scarce budget first;
        /// lower-priority types encounter GlobalBudgetExhausted. This reason is set when the arbiter
        /// can identify that the current type's priority rank is below the threshold for the remaining
        /// budget. See "no-batching / emergent priority" note in HANDOFF.md.
        /// </summary>
        PriorityLost,

        /// <summary>
        /// A type-specific cooldown (set via Redis SETNX when the previous send of this exact
        /// notification code was dispatched) prevents the same type from firing again within its
        /// configured TTL. Fail-open: if Redis is unavailable the cooldown is not enforced
        /// (consistent with the existing <c>ReengagementDedupeStore</c> pattern).
        /// </summary>
        Cooldown,
    }

    /// <summary>Result returned by <see cref="Evaluate"/>.</summary>
    public readonly record struct EvalResult(bool Eligible, NotEligibleReason Reason);

    /// <summary>Result returned by <see cref="ArbitratePush"/>.</summary>
    public readonly record struct ArbiterResult(bool ShouldPush, NotEligibleReason SuppressReason);

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

    /// <summary>
    /// Pure arbitration gate for the PUSH channel only (P9-07). In-app inbox is ALWAYS written
    /// by <c>NudgeDispatcher</c> — this method only decides whether push is permitted.
    ///
    /// Decision order (short-circuits on first suppression condition):
    /// 1. Budget exhausted (<paramref name="pushesSentToday"/> &gt;= <paramref name="globalBudget"/>)
    ///    → <see cref="NotEligibleReason.GlobalBudgetExhausted"/> (DB count is the authority).
    /// 2. Cooldown active for this type code → <see cref="NotEligibleReason.Cooldown"/>.
    /// 3. Priority rank too low for remaining budget — when budget is exhausted after this send
    ///    would leave it at 0 and priority rank exceeds <paramref name="priorityRank"/> threshold
    ///    → <see cref="NotEligibleReason.PriorityLost"/> (emergent priority; no batch window).
    ///
    /// Returns <c>ShouldPush=true</c> (with <see cref="NotEligibleReason.None"/>) if all gates pass.
    /// </summary>
    /// <param name="pushesSentToday">Cross-category push count for this child today (from DB inbox).</param>
    /// <param name="globalBudget">Per-child daily push budget (from pref column or config default).</param>
    /// <param name="cooldownActive">Whether this type's Redis cooldown key is set (SETNX check).</param>
    /// <param name="priorityRank">This type's priority rank (lower = higher priority, e.g. 0 = top).</param>
    /// <param name="budgetRemainingAfterHigherPriority">
    ///   How many push slots remain once higher-priority nudges have been accounted for.
    ///   When 0 and this type's rank is &gt; 0 (not top priority), it loses the slot.
    ///   Pass <c>globalBudget - pushesSentToday</c> when no cross-category slot-accounting is done.
    ///   For the emergent-priority model, pass the remaining budget minus slots reserved for
    ///   higher-priority categories that have not yet fired (typically just the raw remaining budget).
    /// </param>
    public static ArbiterResult ArbitratePush(
        int pushesSentToday,
        int globalBudget,
        bool cooldownActive,
        int priorityRank,
        int budgetRemainingAfterHigherPriority)
    {
        // 1. Global daily budget check (DB count is authoritative).
        if (pushesSentToday >= globalBudget)
            return new ArbiterResult(false, NotEligibleReason.GlobalBudgetExhausted);

        // 2. Per-type cooldown (Redis SETNX; fail-open when Redis is unavailable).
        if (cooldownActive)
            return new ArbiterResult(false, NotEligibleReason.Cooldown);

        // 3. Emergent priority: if no budget remains for lower-priority types after higher-priority
        //    reservation is accounted for, suppress. Priority is rank-ordered; 0 = highest priority.
        //    Because handlers fire independently (no batch window), this is realised emergently —
        //    higher-priority types naturally consume the budget first; this check lets the arbiter
        //    suppress a low-priority type that would exhaust the last slot that higher-priority types
        //    still might need today. In practice this activates only when budgetRemaining <= 0.
        if (budgetRemainingAfterHigherPriority <= 0 && priorityRank > 0)
            return new ArbiterResult(false, NotEligibleReason.PriorityLost);

        return new ArbiterResult(true, NotEligibleReason.None);
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

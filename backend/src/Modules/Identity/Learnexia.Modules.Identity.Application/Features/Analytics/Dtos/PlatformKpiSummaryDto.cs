using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;

/// <summary>
/// Platform-wide KPI summary returned by <c>GET /api/Admin/Analytics/kpis</c>.
/// P7-10 analytics dashboard — honest v1 over existing data.
///
/// <para>Facets that carry real data: learning completions, active-student proxy, engagement (XP + missions),
/// subscription counts by tier, AI safety events.</para>
///
/// <para>Facets that are explicitly N/A or deferred:</para>
/// <list type="bullet">
///   <item><see cref="RetentionNaReason"/> — retention cohorts require P5-03 (analytics event backbone).</item>
///   <item><see cref="SessionDurationNaReason"/> — true session duration requires P5-03.</item>
///   <item><see cref="Subscription"/>.<see cref="PlatformSubscriptionStats.RevenueNaReason"/> — revenue is synthetic (Fake payment provider).</item>
///   <item><see cref="AiSafety"/>.<see cref="PlatformAiSafetyStats.AiRequestVolumeNaReason"/> — AI request volume requires P7-11 AiUsageLogs.</item>
/// </list>
///
/// <para>DAU/WAU/MAU: these are labelled as <b>activity proxies</b> (distinct students with a completed
/// attempt in the window) NOT true session metrics. True sessions require P5-03.</para>
/// </summary>
public sealed record PlatformKpiSummaryDto
{
    // ── Date range for which the KPIs were computed ──────────────────────────────────────────────
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc   { get; init; }

    // ── Learning KPIs (real data) ─────────────────────────────────────────────────────────────────
    public int LessonsCompleted { get; init; }

    /// <summary>
    /// Total completed attempts in the window (all students, all lessons).
    /// One attempt = one quiz session for a lesson.
    /// </summary>
    public int TotalAttempts { get; init; }

    /// <summary>
    /// Distinct students with at least one completed attempt in the window.
    /// <b>Activity proxy for DAU/WAU/MAU — NOT a true session metric.</b>
    /// Label: "Active Learners (activity proxy)" until P5-03 delivers true sessions.
    /// </summary>
    public int DistinctActiveStudents { get; init; }

    /// <summary>
    /// Explicit N/A marker for quizzes-completed as a separate counter.
    /// In the current data model, one Attempt = one lesson's quiz session.
    /// There is no distinct "quiz attempt" vs "lesson attempt" row — they are the same entity.
    /// Use <see cref="LessonsCompleted"/> as the single completion counter.
    /// </summary>
    public string QuizzesCompletedNaReason { get; init; } =
        "N/A (quiz attempts are not distinguishable from lesson attempts — same Attempt entity; use LessonsCompleted)";

    // ── Learning breakdowns (real data, may be empty if no data in window) ────────────────────────
    public IReadOnlyList<SubjectBreakdown>  BySubject  { get; init; } = [];
    public IReadOnlyList<GradeBreakdown>    ByGrade    { get; init; } = [];

    /// <summary>
    /// Completion breakdown by curriculum language (ar/en). The curriculum is bilingual
    /// parallel trees, so language is a first-class breakdown dimension (story P7-10 AC) —
    /// it lets an admin compare engagement/throughput across the two languages.
    /// </summary>
    public IReadOnlyList<LanguageBreakdown> ByLanguage { get; init; } = [];

    // ── Engagement KPIs (real data from Gamification) ────────────────────────────────────────────
    public int  MissionsCompleted { get; init; }
    public long XpEarnedInWindow  { get; init; }

    // ── Subscription KPIs (real data from Billing, revenue N/A) ─────────────────────────────────
    public int                              TotalActiveSubscriptions { get; init; }
    public IReadOnlyList<SubscriptionTierCount> SubscriptionsByTier  { get; init; } = [];

    /// <summary>
    /// Explicit N/A marker for revenue. Set when revenue data is unavailable (Fake payment provider).
    /// Null = revenue data available (reserved for when Paymob is integrated).
    /// </summary>
    public string? RevenueNaReason { get; init; }

    // ── AI Safety KPIs (real data from Ai module) ────────────────────────────────────────────────
    public int TotalAiSafetyEvents { get; init; }
    public int AiBlockedCount      { get; init; }
    public int AiFlaggedCount      { get; init; }

    /// <summary>
    /// Total AI tutor requests in the window — real data from <c>ai.AiUsageLogs</c> (P7-11 tutor-cost).
    /// Counts non-streaming completions (streamed responses are a documented v1 capture gap).
    /// </summary>
    public int AiRequestVolume { get; init; }

    /// <summary>
    /// Explicit N/A marker for AI request volume. Now <c>null</c> (the <c>ai.AiUsageLogs</c> table exists,
    /// so the volume is real); retained so the FE can distinguish "0 requests" from "data unavailable".
    /// </summary>
    public string? AiRequestVolumeNaReason { get; init; }

    // ── P5-03-deferred facets (explicit "available after P5-03" state) ───────────────────────────

    /// <summary>
    /// Retention cohort data is not available in v1.
    /// Requires the P5-03 analytics events backbone (session/cohort capture) — not yet built.
    /// </summary>
    public string RetentionNaReason { get; init; } =
        "Available after P5-03 (analytics events backbone)";

    /// <summary>
    /// Session duration data is not available in v1.
    /// <c>Attempt.DurationSeconds</c> is per-attempt, not per-session boundary.
    /// Requires the P5-03 analytics events backbone (session boundary events) — not yet built.
    /// </summary>
    public string SessionDurationNaReason { get; init; } =
        "Available after P5-03 (analytics events backbone — session boundary events)";
}

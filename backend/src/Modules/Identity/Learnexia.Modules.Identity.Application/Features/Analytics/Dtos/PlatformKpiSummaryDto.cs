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
/// subscription counts by tier, AI safety events, session/retention (P5-03 — real going-forward).</para>
///
/// <para>Facets that are explicitly N/A or deferred:</para>
/// <list type="bullet">
///   <item><see cref="RetentionNaReason"/> — now <c>null</c>: P5-03 Analytics backbone is built;
///   retention facets (<see cref="AvgActiveDaysPerStudent"/>, <see cref="ReturningStudentRate"/>)
///   are real going-forward from <c>analytics.ActivityEvents</c>.</item>
///   <item><see cref="SessionDurationNaReason"/> — now <c>null</c>: P5-03 is built;
///   <see cref="AvgSessionDurationSeconds"/> is real going-forward.</item>
///   <item><see cref="PlatformSubscriptionStats.RevenueNaReason"/> — revenue is synthetic (Fake payment provider).</item>
///   <item><see cref="PlatformAiSafetyStats.AiRequestVolumeNaReason"/> — AI request volume is real from <c>ai.AiUsageLogs</c>.</item>
/// </list>
///
/// <para>DAU/WAU/MAU: <see cref="DistinctActiveStudents"/> is the Learning-sourced activity proxy
/// (distinct students with a completed attempt); it retains its historical data.
/// <see cref="AnalyticsActiveStudents"/> is the Analytics-sourced true activity count (going-forward
/// from <c>analytics.ActivityEvents</c>).</para>
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
    /// <b>Activity proxy for DAU/WAU/MAU — sourced from Learning (attempt completions).</b>
    /// Retained with its historical data. See <see cref="AnalyticsActiveStudents"/> for the
    /// Analytics-derived true activity count (going-forward from P5-03 events).
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
    /// Counts both non-streaming (<c>CompleteAsync</c>) and streaming (<c>StreamAsync</c>) calls
    /// (P7-11b: streaming capture gap closed).
    /// </summary>
    public int AiRequestVolume { get; init; }

    /// <summary>
    /// Explicit N/A marker for AI request volume. Now <c>null</c> (the <c>ai.AiUsageLogs</c> table exists,
    /// so the volume is real); retained so the FE can distinguish "0 requests" from "data unavailable".
    /// </summary>
    public string? AiRequestVolumeNaReason { get; init; }

    // ── P5-03 Analytics — session/retention facets (real data, going-forward) ─────────────────────

    /// <summary>
    /// Distinct active students in the window, sourced from <c>analytics.ActivityEvents</c>
    /// (P5-03 Analytics module — true activity-based count, going-forward).
    /// Distinct from <see cref="DistinctActiveStudents"/> which is the Learning attempt-completion proxy
    /// with historical data. Zero until activity events accrue.
    /// </summary>
    public int AnalyticsActiveStudents { get; init; }

    /// <summary>
    /// Total session count across all active students in the window.
    /// Derived read-time from <c>analytics.ActivityEvents</c> using an inactivity-window gap-split
    /// (default 30 min gap = session boundary). Real data going-forward from P5-03.
    /// Zero when no events exist in the window.
    /// </summary>
    public int TotalSessions { get; init; }

    /// <summary>
    /// Mean session duration in seconds across all sessions in the window.
    /// Derived read-time from <c>analytics.ActivityEvents</c> (gap-split wall-clock span).
    /// Real data going-forward from P5-03. Zero when no sessions exist.
    /// </summary>
    public double AvgSessionDurationSeconds { get; init; }

    /// <summary>
    /// Average number of distinct UTC calendar days on which each active student had at least
    /// one activity event. Foundation for D1/D7/D30 cohort retention curves (further analytics
    /// layers build on this). Real data going-forward from P5-03.
    /// Zero when no active students exist.
    /// </summary>
    public double AvgActiveDaysPerStudent { get; init; }

    /// <summary>
    /// Fraction of distinct active students who were active on ≥ 2 distinct UTC calendar days
    /// in the window. Range [0.0, 1.0]. Real data going-forward from P5-03.
    /// Zero when no active students exist.
    /// </summary>
    public double ReturningStudentRate { get; init; }

    /// <summary>
    /// Explicit N/A marker for retention facets.
    /// Now <c>null</c> — P5-03 (analytics events backbone) is built; retention data is real
    /// going-forward. Retained as nullable so the FE contract stays additive
    /// (existing clients that check for non-null continue to work correctly).
    /// </summary>
    public string? RetentionNaReason { get; init; } = null;

    /// <summary>
    /// Explicit N/A marker for session duration.
    /// Now <c>null</c> — P5-03 (analytics events backbone) is built; session duration is real
    /// going-forward. Retained as nullable so the FE contract stays additive.
    /// </summary>
    public string? SessionDurationNaReason { get; init; } = null;
}

using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// Persisted behavioral model for a student — the output of <c>StudentProfileEngine.Derive</c> (P3-13).
///
/// This entity holds ONLY the four rule-based behavioral attributes derived from captured
/// learning signals (<c>StudentAnswer</c> / <c>Attempt</c> / <c>QuizQuestion</c>, P2-08;
/// <c>StudentSkillMastery</c>, P3-09). It is architecturally distinct from the Gamification
/// module's XP/streak/hearts store — see architecture.md §16 ("StudentLearningProfile").
///
/// <b>Behavioral attributes only — absolutely NO XP/streak/hearts/gamification fields.</b>
/// Those counters stay in the Gamification module.
///
/// <b>Module isolation:</b> <c>StudentId</c> is a loose <c>int</c> reference to the Identity
/// student user. There is NO cross-module foreign key. The unique index on <c>StudentId</c>
/// is defined in <c>StudentLearningProfileConfig</c>; exactly one profile row per student.
///
/// <b>Cold-start safety (AC4):</b> <c>DataPointCount</c> tracks how many answer/attempt data
/// points fed the last recompute. Consumers (P3-03 prompt builder, P3-08 adaptivity engine)
/// treat <c>DataPointCount == 0</c> as "cold-start / neutral defaults" and do not adapt yet.
///
/// <b>Grade-transition preservation:</b> the profile is per-student and grade-agnostic.
/// A grade change must NOT reset this entity (BE7 network effect).
///
/// <b>P5-03 proxy (v1):</b> <c>AttentionSpanMinutes</c> is derived from
/// <c>Attempt</c>/<c>StudentAnswer</c> timestamps (P2-08) as a v1 proxy. When P5-03
/// (session signals) lands, the engine can enrich this without a structural migration.
///
/// <b>P3-03 seam (deferred):</b> <c>IStudentProfileService.GetProfile</c> is the in-process
/// read seam for the unbuilt AI module (P3-03). Actual prompt-builder wiring is deferred.
/// </summary>
public class StudentLearningProfile : FullAuditedEntity
{
    /// <summary>
    /// Loose reference to the Identity student user (plain int — NO cross-module FK).
    /// Unique index defined in <see cref="StudentLearningProfileConfig"/> (one row per student).
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Per-<c>QuestionType</c> success-rate map stored as <c>jsonb</c>.
    /// Shape: <c>{ "MCQ": 0.82, "TrueFalse": 0.60, "Matching": 0.75, "FillInBlank": 0.45 }</c>
    /// — keys are <c>QuestionType</c> enum names; values are success rates [0..1].
    ///
    /// Null or empty string = no sufficient data (cold-start).
    /// Consumers must treat absent/empty as "no affinity data yet."
    ///
    /// Mapped as <c>jsonb</c> in <c>StudentLearningProfileConfig</c> — the same approach
    /// used by <c>ContentBlock.Payload</c> (a <c>string</c> holding JSON serialised by the
    /// application layer, stored as <c>jsonb</c> in PostgreSQL).
    /// </summary>
    public string? QuestionTypeAffinity { get; set; }

    /// <summary>
    /// List of skill IDs with recurring wrong-answer patterns, stored as <c>jsonb</c>.
    /// Shape: <c>[ { "skillId": 12, "errorCount": 5 }, ... ]</c>
    ///
    /// Null or empty string = no recurring errors detected (cold-start or clean slate).
    /// Mapped as <c>jsonb</c> in <c>StudentLearningProfileConfig</c>.
    /// </summary>
    public string? RecurringErrorClusters { get; set; }

    /// <summary>
    /// Estimated number of minutes before the student's accuracy noticeably drops within
    /// a session. Null = insufficient data to derive (cold-start or not enough attempts).
    ///
    /// v1 proxy from <c>Attempt</c>/<c>StudentAnswer</c> timestamps (P2-08).
    /// Will be enriched from P5-03 session signals when that story lands.
    /// </summary>
    public int? AttentionSpanMinutes { get; set; }

    /// <summary>
    /// Detailed bucket-level accuracy-drop data stored as <c>jsonb</c> — the raw signal
    /// underlying <see cref="AttentionSpanMinutes"/>.
    /// Shape: <c>[ { "minuteBucket": 5, "accuracy": 0.9 }, { "minuteBucket": 10, "accuracy": 0.72 }, ... ]</c>
    ///
    /// Null = no fatigue-signal data yet. Mapped as <c>jsonb</c> and nullable in
    /// <c>StudentLearningProfileConfig</c>.
    /// </summary>
    public string? FatigueSignal { get; set; }

    /// <summary>
    /// Derived preferred style for receiving explanations. Stored as <c>int</c> via
    /// <c>HasConversion&lt;int&gt;()</c>. Defaults to <see cref="ExplanationStyle.Standard"/>
    /// (0) — the cold-start / neutral value.
    ///
    /// Provisional — enum values pending confirmation against P3-03 prompt builder (AI module,
    /// not yet built). See <c>ExplanationStyle</c> XML doc.
    /// </summary>
    public ExplanationStyle PreferredExplanationStyle { get; set; } = ExplanationStyle.Standard;

    /// <summary>
    /// Number of answer/attempt data points that fed the most recent recompute. Used by
    /// consumers as a cold-start confidence indicator: 0 = no data; below the configured
    /// <c>ColdStartDataPointThreshold</c> = low confidence; above = full profile.
    /// </summary>
    public int DataPointCount { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent successful recompute. Stored as <c>timestamptz</c>.
    /// Null = never recomputed (row exists but profile engine has not run yet for this student).
    /// The nightly <c>StudentProfileRecomputeJob</c> uses this to detect stale profiles
    /// (older than 24 h or null).
    /// </summary>
    public DateTime? LastRecomputedAt { get; set; }
}

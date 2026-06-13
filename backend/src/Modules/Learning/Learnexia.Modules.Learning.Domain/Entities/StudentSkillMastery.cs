using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// Persisted per-(student, skill) mastery record — the output of the Student Modeling Engine (P3-09).
///
/// Unique constraint on <c>(StudentId, SkillId)</c> — exactly one row per student/skill pair.
///
/// <b>Module isolation:</b> <c>StudentId</c> is a loose <c>int</c> reference to the Identity
/// student user. There is NO cross-module foreign key; index only (module isolation rule 1).
/// <c>SkillId</c> is an intra-Learning foreign key to <see cref="Skill"/> (Restrict delete) —
/// both entities live in the <c>learning</c> schema, so this is allowed.
///
/// <b>Spaced-repetition columns (P3-10 reservation):</b>
/// <c>ReviewIntervalDays</c>, <c>NextReviewDueAt</c>, and <c>RepetitionNumber</c> are reserved here
/// so P3-10 adds only logic — it requires NO additional migration on this table.
///
/// <b>Formula:</b> cumulative accuracy = round(correctAnswers / totalAnswers * 100).
/// Same formula as <c>GetSkillStatsQueryHandler</c> / P2-04's transient aggregate (Q1 / Q5).
/// <c>MasteryEngine.Compute</c> is the single source of truth; do not inline the formula elsewhere.
/// </summary>
public class StudentSkillMastery : FullAuditedEntity
{
    /// <summary>
    /// Loose reference to Identity student user (plain int, no FK — module isolation).
    /// Indexed via <c>StudentSkillMasteryConfig</c> for the "all my mastery" query.
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Intra-Learning FK → <see cref="Skill.Id"/> (Restrict delete — cannot delete a skill while
    /// mastery rows reference it; both are in the <c>learning</c> schema).
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>Navigation to the owning <see cref="Skill"/>.</summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// Cumulative accuracy percentage [0..100] computed by <c>MasteryEngine.Compute</c>.
    /// Recomputed and upserted after each attempt completion that touches this skill.
    /// </summary>
    public int MasteryPercentage { get; set; }

    /// <summary>
    /// Mastery state machine value. Stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>.
    /// Defaults to <see cref="MasteryStatus.NotStarted"/> (0) via the EF column default.
    /// </summary>
    public MasteryStatus Status { get; set; } = MasteryStatus.NotStarted;

    /// <summary>Total completed attempts that touched this skill (cumulative; bumped on each upsert).</summary>
    public int AttemptsCount { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent attempt that triggered an upsert for this skill.
    /// Stored as <c>timestamptz</c>. Used by P3-10 spaced-repetition scheduler to detect forgetting.
    /// </summary>
    public DateTime LastPracticedAt { get; set; }

    // ── Spaced-repetition columns reserved for P3-10 ──────────────────────────────────────────────
    // These columns are fully nullable/defaulted here. P3-10 adds only scheduling logic — no
    // additional migration on this table is required.

    /// <summary>
    /// P3-10 (reserved): current SR interval in days. Default 0 (not yet scheduled).
    /// </summary>
    public int ReviewIntervalDays { get; set; } = 0;

    /// <summary>
    /// P3-10 (reserved): UTC datetime when the next spaced-repetition review is due.
    /// Null until P3-10 sets the first schedule. Stored as <c>timestamptz</c>.
    /// </summary>
    public DateTime? NextReviewDueAt { get; set; }

    /// <summary>
    /// P3-10 (reserved): SM-2 repetition counter (number of successful reviews in a row).
    /// Default 0.
    /// </summary>
    public int RepetitionNumber { get; set; } = 0;
}

using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Learning.Domain.Entities;

/// <summary>
/// Per-question empirical difficulty aggregate produced by the calibration job (P5-07 BE-1).
///
/// One row per <c>QuestionId</c>, enforced by a unique index
/// (<c>UX_QuestionDifficultyStats_QuestionId</c>). The job upserts this row on every run —
/// re-running is idempotent (UPDATE if exists, INSERT otherwise).
///
/// <b>De-identification (AC5):</b> this entity stores ONLY aggregate counts/averages grouped
/// by <c>QuestionId</c>. No <c>StudentId</c>, <c>AttemptId</c>, or any other child identifier
/// is persisted here. The aggregation query projects counts only:
/// <c>SampleSize = COUNT(*)</c>, <c>PValue = AVG(IsCorrect)</c>,
/// <c>AvgTimeSpentSeconds = AVG(TimeSpentSeconds)</c>,
/// <c>HintUsageRate = AVG(HintUsed)</c>.
///
/// <b>FK:</b> <c>QuestionId</c> is a real intra-module FK to <c>QuizQuestion</c> (Restrict) —
/// consistent with <c>StudentAnswer.QuestionId</c>. Defined in <c>QuestionDifficultyStatsConfig</c>.
///
/// <b>P-value units:</b> 0.0–1.0 (fraction correct, matching the config-driven thresholds in
/// <c>CalibrationOptions</c>).
/// </summary>
public class QuestionDifficultyStats : FullAuditedEntity
{
    /// <summary>
    /// Real intra-module FK to <see cref="QuizQuestion.Id"/> (Restrict — consistent with StudentAnswer).
    /// Unique index on this column enforces one stats row per question (upsert idempotency key).
    /// </summary>
    public int QuestionId { get; set; }

    /// <summary>Navigation to the owning question (intra-module FK, Restrict).</summary>
    public QuizQuestion Question { get; set; } = null!;

    /// <summary>
    /// Number of <c>StudentAnswer</c> rows aggregated for this question.
    /// The calibration job gates proposals/flags on <c>SampleSize &gt;= CalibrationOptions.MinSamples</c>.
    /// </summary>
    public int SampleSize { get; set; }

    /// <summary>
    /// Fraction of correct answers (0.0–1.0). Computed as <c>AVG(CASE WHEN IsCorrect THEN 1 ELSE 0 END)</c>.
    /// High p-value ⇒ question is empirically easy; low p-value ⇒ empirically hard.
    /// </summary>
    public double PValue { get; set; }

    /// <summary>
    /// Mean <c>TimeSpentSeconds</c> across all aggregated <c>StudentAnswer</c> rows.
    /// </summary>
    public double AvgTimeSpentSeconds { get; set; }

    /// <summary>
    /// Fraction of answers where <c>HintUsed = true</c> (0.0–1.0).
    /// Computed as <c>AVG(CASE WHEN HintUsed THEN 1 ELSE 0 END)</c>.
    /// </summary>
    public double HintUsageRate { get; set; }

    /// <summary>
    /// UTC timestamp of the last aggregation run that produced this row.
    /// Stored as <c>timestamptz</c>. Updated on every upsert.
    /// </summary>
    public DateTime ComputedAtUtc { get; set; }
}

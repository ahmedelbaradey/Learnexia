using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Application-layer interface for the calibration pipeline (P5-07 BE-1/BE-2/BE-3).
///
/// OPTION C — EF only in Infrastructure. This interface lives in Application; the implementation
/// (<c>CalibrationService</c>) lives in Infrastructure and uses <c>LearningDbContext</c> directly.
/// Handlers and jobs inject this interface; no EF or DbContext references cross into Application.
///
/// TRANSACTION NOTE — two call sites:
///   (a) <c>CalibrationJob</c>: runs OUTSIDE any HTTP command pipeline. Each sweep step stages
///       changes; the job calls <c>LearningDbContext.SaveChangesAsync(userId: 0)</c> explicitly.
///   (b) BE-5 command handlers (Promote/Reject/Clear/Confirm) run INSIDE the
///       <c>UnitOfWorkBehavior</c> transaction — the UoW commits after the handler returns.
///
/// DE-IDENTIFICATION (AC5): <c>AggregateQuestionStatsAsync</c> groups by QuestionId and projects
/// counts/averages ONLY — no StudentId, AttemptId, or any child identifier crosses this boundary.
/// </summary>
public interface ICalibrationService
{
    // ── BE-1: aggregation (job sweep) ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Groups <c>StudentAnswer</c> rows by <c>QuestionId</c> and upserts one
    /// <see cref="QuestionDifficultyStats"/> row per question.
    ///
    /// Projection: <c>SampleSize=COUNT(*)</c>, <c>PValue=AVG(IsCorrect)</c>,
    /// <c>AvgTimeSpentSeconds=AVG(TimeSpentSeconds)</c>, <c>HintUsageRate=AVG(HintUsed)</c>.
    /// <b>No StudentId or AttemptId is selected, persisted, or logged (AC5).</b>
    ///
    /// Returns the count of questions processed (for job logging).
    /// Idempotent — re-running overwrites the same row.
    /// </summary>
    Task<int> AggregateQuestionStatsAsync(CancellationToken ct = default);

    // ── BE-2: proposals (job sweep) ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sweeps all <see cref="QuestionDifficultyStats"/> rows and, for each question whose empirical
    /// band diverges from authored <c>Difficulty</c> with <c>SampleSize >= MinSamples</c>, upserts a
    /// <see cref="QuestionRecalibrationProposal"/> in <c>Pending</c> state.
    ///
    /// Uses <see cref="CalibrationEngine.EvaluateDivergence"/> for the rule logic.
    /// <b>NEVER mutates <c>QuizQuestion.Difficulty</c></b> — proposals are propose-only; the human
    /// Promote path (BE-5) is the only place authored band can change.
    ///
    /// Returns the count of proposals upserted (for job logging).
    /// </summary>
    Task<int> UpsertDivergenceProposalsAsync(CancellationToken ct = default);

    // ── BE-3: AI flagging (job sweep) ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sweeps all <see cref="QuestionDifficultyStats"/> rows and flags AI-generated questions whose
    /// empirical p-value is outside the safe bounds with <c>SampleSize >= MinSamples</c>.
    ///
    /// Sets <c>QuizQuestion.QualityState = FlaggedForReview</c> + a stable <c>FlagReason</c> code.
    /// Also CLEARS the flag (<c>QualityState = Approved</c>, <c>FlagReason = null</c>) for AI
    /// questions that previously had extreme p-values but whose most recent aggregate has improved.
    ///
    /// Returns the count of questions whose flag state was changed (for job logging).
    /// </summary>
    Task<int> ApplyAiFlagsAsync(CancellationToken ct = default);

    // ── BE-5: admin query + command helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paged list of <see cref="QuestionRecalibrationProposal"/> rows filtered by
    /// <paramref name="status"/>. Ordered by most-recent-first (<c>CreatedAt DESC</c>).
    /// </summary>
    Task<PaginatedResult<QuestionRecalibrationProposal>> GetProposalsPagedAsync(
        int? statusFilter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Returns a paged list of <see cref="QuizQuestion"/> rows whose
    /// <c>QualityState == FlaggedForReview</c>. Ordered by question Id DESC.
    /// Also returns the corresponding <see cref="QuestionDifficultyStats"/> for each question
    /// so the DTO can carry the evidence p-value.
    /// </summary>
    Task<PaginatedResult<FlaggedQuestionRow>> GetFlaggedQuestionsPagedAsync(
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Loads a tracked <see cref="QuestionRecalibrationProposal"/> by id (EF change-tracking ON),
    /// including its navigation to <see cref="QuizQuestion"/>.
    /// Returns <c>null</c> when not found.
    /// Used by BE-5 Promote / Reject handlers.
    /// </summary>
    Task<QuestionRecalibrationProposal?> GetProposalTrackedAsync(int proposalId, CancellationToken ct = default);

    /// <summary>
    /// Loads a tracked <see cref="QuizQuestion"/> by id (EF change-tracking ON).
    /// Returns <c>null</c> when not found.
    /// Used by BE-5 Clear / Confirm flag handlers.
    /// </summary>
    Task<QuizQuestion?> GetQuestionTrackedAsync(int questionId, CancellationToken ct = default);
}

/// <summary>
/// A materialized row pairing a flagged question with its calibration stats evidence.
/// Returned by <see cref="ICalibrationService.GetFlaggedQuestionsPagedAsync"/> so callers
/// can access the p-value without joining across layers.
/// </summary>
public sealed record FlaggedQuestionRow(
    QuizQuestion Question,
    QuestionDifficultyStats? Stats);

using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for attempt-write operations used by AbandonAttemptCommandHandler and
/// CompleteAttemptCommandHandler (Stage 6 — Option C refactor).
///
/// All EF queries and entity staging are encapsulated here — Application handlers
/// inject this interface only; no IQueryable / DbSet / EF extension methods cross this boundary.
///
/// TRANSACTION NOTE: Every method that stages changes (Update, UpsertMastery, AdvanceSR) operates
/// inside the ambient UoW transaction opened by UnitOfWorkBehavior. The service MUST NOT call
/// SaveChangesAsync or open its own transaction. The UoW behavior commits everything atomically
/// after the handler returns.
///
/// The sole exception is the AttemptService.StartNewAsync used by StartAttemptCommandHandler —
/// that deliberately flushes immediately to get the DB-generated Id (the existing escape hatch).
/// </summary>
public interface IAttemptWriteService
{
    // ── Load operations ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="Attempt"/> with the given id using EF tracking ON
    /// (required for subsequent Update staging). Returns null when not found.
    /// </summary>
    Task<Attempt?> GetAttemptTrackedAsync(int attemptId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="StudentAnswer"/> rows for the given attemptId. AsNoTracking.
    /// </summary>
    Task<List<StudentAnswer>> GetAnswersForAttemptAsync(int attemptId, CancellationToken ct = default);

    // ── Stage operations (NO SaveChangesAsync — UoW behavior commits) ─────────────────────────

    /// <summary>
    /// Stages an update to the <see cref="Attempt"/> entity.
    /// Does NOT call SaveChangesAsync — the UoW behavior commits after the handler returns.
    /// </summary>
    Task StageAttemptUpdateAsync(Attempt attempt, CancellationToken ct = default);

    /// <summary>
    /// Stage a new <see cref="StudentAnswer"/> entity.
    /// Does NOT call SaveChangesAsync — the UoW behavior commits after the handler returns.
    /// </summary>
    Task StageAddStudentAnswerAsync(StudentAnswer answer, CancellationToken ct = default);

    // ── Mastery + Spaced-Repetition upsert (CompleteAttempt path) ─────────────────────────────

    /// <summary>
    /// P3-09 / P3-10 — Upserts <see cref="StudentSkillMastery"/> rows for every skill touched
    /// by the given attempt's answers (MasteryEngine.Compute per distinct skill), then runs the
    /// P3-10 SpacedRepetitionEngine interval-progression hook for skills that were DUE before
    /// this attempt.
    ///
    /// Both the mastery upsert and SR field updates are STAGED inside the ambient UoW transaction
    /// — no SaveChangesAsync is called; the UoW behavior commits atomically with the attempt
    /// status update.
    ///
    /// Returns (totalAnswers, correctAnswers) for aggregating into the response DTO.
    /// </summary>
    Task UpsertMasteryAndAdvanceSRAsync(
        int studentId,
        int attemptId,
        IList<StudentAnswer> answers,
        CancellationToken ct = default);

    // ── Submit-Answer helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="Attempt"/> with tracking ON (needed to check status).
    /// Returns null when not found.
    /// Separated from GetAttemptTrackedAsync only as a documentation alias — same implementation.
    /// </summary>
    Task<QuizQuestion?> GetQuestionForAnswerAsync(int questionId, int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a <see cref="StudentAnswer"/> row already exists for
    /// (attemptId, questionId) — used to enforce the re-answer guard.
    /// </summary>
    Task<bool> IsAlreadyAnsweredAsync(int attemptId, int questionId, CancellationToken ct = default);
}

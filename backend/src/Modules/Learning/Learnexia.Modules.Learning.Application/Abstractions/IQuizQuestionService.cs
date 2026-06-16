using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface IQuizQuestionService : IBaseService<QuizQuestion>
{
    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a Lesson with the given id exists (non-deleted global filter applies).
    /// Used by AddQuestionCommandHandler and ReorderQuestionsCommandHandler to verify the parent lesson.
    /// </summary>
    Task<bool> LessonExistsAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns the maximum SequenceOrder for QuizQuestions belonging to the given lesson,
    /// or -1 when no questions exist yet (append-semantics: next order = max + 1).
    /// All EF calls (MaxAsync) stay inside Infrastructure.
    /// </summary>
    Task<int> GetMaxSequenceOrderAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Stages a new QuizQuestion via AddAsync and returns the tracked instance so the handler
    /// can raise a domain event on it. No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task<QuizQuestion> StageAddQuestionAsync(QuizQuestion question, CancellationToken ct = default);

    // ── Edit / Delete / SetActive path ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked QuizQuestion for the given id (EF change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<QuizQuestion?> GetQuestionTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked QuizQuestion entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageQuestionUpdateAsync(QuizQuestion question, CancellationToken ct = default);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and returns tracked QuizQuestion entities scoped to the given lessonId and matching
    /// the given id set. The handler applies new SequenceOrder values and stages UpdateAsync calls;
    /// the UoW commits them atomically. Returns only rows that satisfy BOTH predicates so the
    /// caller can detect cross-lesson mismatches by comparing counts.
    /// </summary>
    Task<List<QuizQuestion>> GetQuestionsTrackedByIdsForLessonAsync(
        IReadOnlyCollection<int> ids, int lessonId, CancellationToken ct = default);

    // ── Query path ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a single QuizQuestion by id (non-tracked, global IsDeleted filter applies),
    /// or null when not found. Used by GetQuestionByIdQueryHandler.
    /// </summary>
    Task<QuizQuestion?> GetQuestionByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted QuizQuestions for the given lesson ordered by SequenceOrder ASC,
    /// capped at 500 rows (DoS backstop). Admin read — IsActive is not filtered.
    /// Used by ListQuestionsForLessonQueryHandler.
    /// </summary>
    Task<List<QuizQuestion>> GetQuestionsForLessonAsync(int lessonId, CancellationToken ct = default);
}

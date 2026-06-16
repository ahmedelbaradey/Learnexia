using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for the data-loading steps inside StartAttemptCommandHandler
/// (Stage 6 — Option C refactor).
///
/// Encapsulates all EF reads needed to: verify the lesson exists and is Published+Active;
/// walk Lesson → Unit → Subject for the P8-03 language guard; find an existing in-progress
/// attempt; and load the quiz-question candidate pool.
///
/// No IQueryable / DbSet / EF extension methods cross this boundary — all results are
/// materialized domain objects or primitives.
/// </summary>
public interface IStartAttemptService
{
    /// <summary>
    /// Returns the <see cref="Lesson"/> if it exists and is Published+Active,
    /// or null when the lesson does not exist or is not published/active.
    /// AsNoTracking.
    /// </summary>
    Task<Lesson?> GetPublishedActiveLessonAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns the <see cref="Subject"/> that owns the lesson via the
    /// Lesson → Unit → Subject chain, or null when the unit/subject is not found.
    /// Used by the P8-03 language-guard check.
    /// AsNoTracking.
    /// </summary>
    Task<Subject?> GetOwningSubjectForLessonAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns an existing in-progress <see cref="Attempt"/> for the student on the lesson,
    /// or null when no such attempt exists. AsNoTracking.
    /// </summary>
    Task<Attempt?> GetInProgressAttemptAsync(int studentId, int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns all Published+Active <see cref="QuizQuestion"/> rows for the given lesson
    /// (IsActive=true, LifecycleState=Published). Soft-deleted rows are excluded by the
    /// global query filter. AsNoTracking.
    /// </summary>
    Task<List<QuizQuestion>> GetPublishedActiveQuestionsAsync(int lessonId, CancellationToken ct = default);
}

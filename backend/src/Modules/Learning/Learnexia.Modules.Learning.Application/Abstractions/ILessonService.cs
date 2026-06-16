using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface ILessonService : IBaseService<Lesson>
{
    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true when a Unit with the given id exists (non-deleted).</summary>
    Task<bool> UnitExistsAsync(int unitId, CancellationToken ct = default);

    /// <summary>Returns true when a Skill with the given id exists (non-deleted).</summary>
    Task<bool> SkillExistsAsync(int skillId, CancellationToken ct = default);

    /// <summary>
    /// Stages the Lesson via AddAsync then FlushAsync within the UoW transaction so the DB assigns
    /// a primary-key Id. Returns the same tracked instance so the caller can raise a domain event.
    /// </summary>
    Task<Lesson> StageAddLessonAsync(Lesson lesson, int userId, CancellationToken ct = default);

    // ── Edit path ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the tracked Lesson, applies the mapped update fields, stages UpdateAsync, and returns
    /// the tracked instance so the handler can raise a domain event on it.
    /// Returns null when the lesson does not exist.
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task<Lesson?> StageEditLessonAsync<TDto>(TDto dto, CancellationToken ct = default)
        where TDto : Learnexia.Shared.Kernel.Dtos.BaseDto;

    // ── Delete path ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Lesson for the given id (EF change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<Lesson?> GetLessonTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Loads all non-deleted ContentBlocks for the given lesson with change-tracking ON.
    /// Used by the cascade soft-delete path so the caller can mutate each block.
    /// </summary>
    Task<List<ContentBlock>> GetContentBlocksTrackedAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked Lesson entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageLessonUpdateAsync(Lesson lesson, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked ContentBlock entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageContentBlockUpdateAsync(ContentBlock block, CancellationToken ct = default);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and returns tracked Lesson entities for all given ids. The handler applies new
    /// SequenceOrder values and stages UpdateAsync calls; the UoW commits them atomically.
    /// </summary>
    Task<List<Lesson>> GetLessonsTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    // ── List/pagination ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated, materialized list of Lessons projected to <see cref="SingleLessonResponse"/>.
    /// When <paramref name="unitId"/> is provided only lessons for that unit are returned.
    /// All EF + ProjectTo + pagination composition stays inside Infrastructure.
    /// </summary>
    Task<PaginatedResult<SingleLessonResponse>> GetPagedAsync(
        int? unitId,
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken ct = default);

    // ── Student GetLesson query path ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the active + published Lesson matching the given id, or null when not found,
    /// inactive, or not published. Used by the student-facing GetLessonQueryHandler.
    /// </summary>
    Task<Lesson?> GetActiveLessonAsync(int lessonId, CancellationToken ct = default);

    /// <summary>
    /// Returns the Subject that owns the given Unit (via Unit.SubjectId / Unit.Subject navigation),
    /// or null when the Unit does not exist. Used for the language guard in GetLessonQueryHandler.
    /// </summary>
    Task<Subject?> GetOwningSubjectByUnitAsync(int unitId, CancellationToken ct = default);

    /// <summary>
    /// Returns the first QuizQuestion for the given lesson ordered by Id ASC, or null when none exist.
    /// Used by GetLessonQueryHandler to fill the QuickCheck field.
    /// </summary>
    Task<QuizQuestion?> GetFirstQuizQuestionAsync(int lessonId, CancellationToken ct = default);

    // ── Admin GetLesson query path ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Lesson with its non-deleted ContentBlocks (Include) ordered by SequenceOrder,
    /// without IsActive filtering. Used by the admin-facing GetAdminLessonQueryHandler.
    /// Global IsDeleted filter still applies.
    /// </summary>
    Task<Lesson?> GetAdminLessonWithContentBlocksAsync(int lessonId, CancellationToken ct = default);

    // ── Attempt-completion helper ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the SkillId of the given lesson, or null if the lesson has no skill assigned
    /// (or does not exist). Used by CompleteAttemptCommandHandler to publish
    /// LessonCompletedIntegrationEvent. AsNoTracking.
    /// </summary>
    Task<int?> GetLessonSkillIdAsync(int lessonId, CancellationToken ct = default);
}

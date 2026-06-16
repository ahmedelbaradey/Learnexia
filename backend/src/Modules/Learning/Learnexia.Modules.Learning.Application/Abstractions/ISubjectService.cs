using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetLanguageCoverage;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface ISubjectService : IBaseService<Subject>
{
    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true when a Grade with the given id exists.</summary>
    Task<bool> GradeExistsAsync(int gradeId, CancellationToken ct = default);

    /// <summary>
    /// Returns true when a soft-deleted Subject (IsDeleted == true) with the same natural key
    /// (GradeId, SubjectCode, Language) already exists. IgnoreQueryFilters applied inside.
    /// </summary>
    Task<bool> SoftDeletedSubjectExistsAsync(int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default);

    /// <summary>Returns true when a live (non-deleted) Subject with the same natural key exists.</summary>
    Task<bool> LiveSubjectExistsAsync(int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default);

    /// <summary>
    /// Stages the Subject via AddAsync then FlushAsync within the UoW transaction so the DB assigns
    /// a primary-key Id. Returns the same tracked instance so the caller can raise a domain event.
    /// </summary>
    Task<Subject> StageAddSubjectAsync(Subject subject, int userId, CancellationToken ct = default);

    // ── Delete / SetActive path ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the tracked Subject for the given id (EF change-tracking ON), or null when not found.
    /// The caller mutates the entity and the UoW behavior commits the change.
    /// </summary>
    Task<Subject?> GetSubjectTrackedAsync(int id, CancellationToken ct = default);

    /// <summary>Returns true when the Subject has at least one non-deleted Unit.</summary>
    Task<bool> SubjectHasUnitsAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Stages an update to the already-tracked Subject entity (calls repository UpdateAsync).
    /// No SaveChangesAsync — the UoW behavior commits after the handler.
    /// </summary>
    Task StageSubjectUpdateAsync(Subject subject, CancellationToken ct = default);

    // ── Edit path ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the existing Subject (trackChanges=false) so the handler can read its immutable
    /// natural-key fields (SubjectCode, Language) before delegating to UpdateAsync.
    /// Returns null when not found.
    /// </summary>
    Task<Subject?> GetSubjectNoTrackingAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Returns true when a conflict exists: another Subject (id != excludeId) already occupies the
    /// same (GradeId, SubjectCode, Language) slot — i.e. a GradeId move that would create a duplicate.
    /// </summary>
    Task<bool> SubjectConflictExistsAsync(int excludeId, int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads and returns tracked Subject entities for all given ids. The handler applies new
    /// SequenceOrder values and stages UpdateAsync calls; the UoW commits them atomically.
    /// </summary>
    Task<List<Subject>> GetSubjectsTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    // ── List/pagination ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated, materialized list of Subjects projected to <see cref="SingleSubjectResponse"/>.
    /// When <paramref name="gradeId"/> is provided only subjects for that grade are returned.
    /// All EF + ProjectTo + pagination composition stays inside Infrastructure.
    /// </summary>
    Task<PaginatedResult<SingleSubjectResponse>> GetPagedAsync(
        int? gradeId,
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken ct = default);

    // ── GetSubjectsForGrade ──────────────────────────────────────────────────────────────────────

    /// <summary>Returns the Grade with the given number, or null when not found.</summary>
    Task<Grade?> GetGradeByNumberAsync(int gradeNumber, CancellationToken ct = default);

    /// <summary>
    /// Returns all active + published Subjects for the given GradeId, materialized.
    /// Used by GetSubjectsForGradeQueryHandler for in-memory per-code resolution.
    /// </summary>
    Task<List<Subject>> GetActivePublishedSubjectsForGradeAsync(int gradeId, CancellationToken ct = default);

    // ── GetSubjectLessons / GetSubjectSkillTree (language-redirect path) ─────────────────────────

    /// <summary>
    /// Returns a single active + published Subject by id, or null.
    /// Used for the initial existence check and language-mismatch detection.
    /// </summary>
    Task<Subject?> GetActivePublishedSubjectAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the active + published Subject matching the given natural-key triple, or null when absent.
    /// Used for the language-redirect path (serve correct-language tree when client presents wrong-language id).
    /// </summary>
    Task<Subject?> GetActivePublishedSubjectByNaturalKeyAsync(int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default);

    /// <summary>
    /// Returns active + published Units for the given subject, each with their Lessons (via Include),
    /// ordered by SequenceOrder. All EF Include/OrderBy/ToListAsync stay inside Infrastructure.
    /// </summary>
    Task<List<Unit>> GetUnitsWithLessonsAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns Concepts for the given subject, each with their active Skills and each Skill's
    /// active+published Lessons (filtered Include/ThenInclude), ordered by Concept.Id.
    /// All EF Include/ThenInclude/OrderBy/ToListAsync stay inside Infrastructure.
    /// </summary>
    Task<List<Concept>> GetConceptsWithSkillsAndLessonsAsync(int subjectId, CancellationToken ct = default);

    // ── LearningPathEngine bulk inputs (GetSubjectLessons + GetSubjectSkillTree) ────────────────────

    /// <summary>
    /// Returns all KnowledgeNode rows for the subject. Delegates to the Learning repository.
    /// All EF materialisation happens inside Infrastructure.
    /// </summary>
    Task<IReadOnlyList<KnowledgeNode>> GetSubjectKnowledgeNodesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all KnowledgeEdge rows where both source and target belong to the subject.
    /// Delegates to the Learning repository.
    /// </summary>
    Task<IReadOnlyList<KnowledgeEdge>> GetSubjectKnowledgeEdgesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns per-skill mastery aggregates for the given student and subject.
    /// Delegates to the Learning repository.
    /// </summary>
    Task<IReadOnlyDictionary<int, SkillMastery>> GetSkillMasteryForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of lesson Ids completed by the student in the given subject.
    /// Delegates to the Learning repository.
    /// </summary>
    Task<IReadOnlySet<int>> GetCompletedLessonIdsForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all Lessons whose Unit belongs to the given subject.
    /// Delegates to the Learning repository.
    /// </summary>
    Task<IReadOnlyList<Lesson>> GetSubjectLessonsAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all active Skills in the subject (via Skill.Concept.SubjectId).
    /// Used by GetSubjectLessonsQueryHandler to build the skillsById lookup for LearningPathEngine.
    /// All EF materialisation happens inside Infrastructure.
    /// </summary>
    Task<List<Skill>> GetSubjectSkillsAsync(int subjectId, CancellationToken ct = default);

    // ── GetSubjectLanguageCoverage ────────────────────────────────────────────────────────────────

    /// <summary>Returns the Grade with the given id (no tracking), or null when not found.</summary>
    Task<Grade?> GetGradeByIdAsync(int gradeId, CancellationToken ct = default);

    /// <summary>
    /// Returns all non-deleted Subjects for the given GradeId without IsActive filtering
    /// (admin read — inactive trees are included). Global IsDeleted filter still applies.
    /// </summary>
    Task<List<Subject>> GetSubjectsForGradeAdminAsync(int gradeId, CancellationToken ct = default);
}

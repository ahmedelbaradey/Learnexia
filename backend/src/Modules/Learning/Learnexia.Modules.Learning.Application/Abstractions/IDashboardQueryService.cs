using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Services;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// Service seam for the Learning-module DB reads inside GetDashboardQueryHandler
/// (Stage 6 — Option C refactor).
///
/// Encapsulates ALL EF queries that the dashboard handler needs from the Learning module's
/// own tables. Cross-module seam calls (IStudentXpQuery, IStudentStreakQuery, etc.) and
/// pure-Domain engine calls (LearningPathEngine) remain in the handler as they are EF-free.
///
/// No IQueryable / DbSet / EF extension methods cross this boundary — all results are
/// materialized domain objects or primitives.
/// </summary>
public interface IDashboardQueryService
{
    // ── Primary-subject resolution ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the SubjectId for the most-recently-started Attempt by the given student,
    /// resolved via Attempt.LessonId → Lesson.Unit.SubjectId.
    /// Returns null if the student has no Attempt rows. AsNoTracking.
    /// </summary>
    Task<int?> GetMostRecentActivitySubjectIdAsync(int studentId, CancellationToken ct = default);

    // ── Grade-1 subject loading ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all Grade-1 <see cref="Subject"/> rows that are Active and Published,
    /// with the Grade navigation populated. Used by the handler to build the
    /// P8-03-BE-5 resolved subject list.
    /// AsNoTracking.
    /// </summary>
    Task<List<Subject>> GetPublishedActiveGrade1SubjectsAsync(CancellationToken ct = default);

    // ── LearningPathEngine inputs (per subject) ────────────────────────────────────────────────

    /// <summary>
    /// Returns all <see cref="KnowledgeNode"/>s whose SubjectId == subjectId. AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<KnowledgeNode>> GetSubjectKnowledgeNodesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="KnowledgeEdge"/>s where both endpoints belong to subjectId. AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<KnowledgeEdge>> GetSubjectKnowledgeEdgesAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Aggregates per-skill mastery for the student across the given subject's skills. AsNoTracking.
    /// Returns a SkillMastery entry for every skill in the subject (zero answers → zeroed entry).
    /// </summary>
    Task<IReadOnlyDictionary<int, SkillMastery>> GetSkillMasteryForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the set of distinct LessonIds where the student has at least one Completed Attempt
    /// in the given subject. AsNoTracking.
    /// </summary>
    Task<IReadOnlySet<int>> GetCompletedLessonIdsForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all Active+Published <see cref="Lesson"/>s whose Unit.SubjectId == subjectId. AsNoTracking.
    /// </summary>
    Task<IReadOnlyList<Lesson>> GetSubjectLessonsAsync(int subjectId, CancellationToken ct = default);

    /// <summary>
    /// Returns all <see cref="Concept"/> rows for the given subject, each with their
    /// <c>Skills</c> navigation collection populated. Used to build the skillsById dictionary
    /// for the LearningPathEngine call.
    /// AsNoTracking.
    /// </summary>
    Task<List<Concept>> GetConceptsWithSkillsForSubjectAsync(int subjectId, CancellationToken ct = default);

    // ── Name resolution ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Id and Name of the <see cref="Unit"/> with the given id,
    /// or null when not found. Used to fill ContinueTargetDto.UnitName.
    /// AsNoTracking.
    /// </summary>
    Task<(int Id, string Name)?> GetUnitNameAsync(int unitId, CancellationToken ct = default);

    /// <summary>
    /// Returns the Name of the <see cref="Subject"/> with the given id,
    /// or null when not found. Used as a fallback when the subject is not in the
    /// pre-loaded subject list.
    /// AsNoTracking.
    /// </summary>
    Task<string?> GetSubjectNameAsync(int subjectId, CancellationToken ct = default);
}

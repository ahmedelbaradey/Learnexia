using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IDashboardQueryService"/> (Stage 6 — Option C).
///
/// Encapsulates all Learning-module DB reads required by GetDashboardQueryHandler.
/// Cross-module seam calls (IStudentXpQuery, etc.) and LearningPathEngine calls remain
/// in the handler as they are EF-free.
///
/// The 5 LearningPathEngine input methods (nodes, edges, mastery, completedLessons, lessons)
/// delegate to the shared ILearningRepository specialised methods to avoid duplicating SQL.
/// The Concept+Skills query and name look-ups are implemented inline (EF-only Infrastructure code).
///
/// All results are fully materialized — no IQueryable crosses the service boundary.
/// No SaveChangesAsync — read-only service.
/// </summary>
public class DashboardQueryService : IDashboardQueryService
{
    private readonly LearningDbContext _dbContext;
    private readonly ILearningRepository _repository;

    public DashboardQueryService(LearningDbContext dbContext, ILearningRepository repository)
    {
        _dbContext  = dbContext;
        _repository = repository;
    }

    // ── Primary-subject resolution ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<int?> GetMostRecentActivitySubjectIdAsync(int studentId, CancellationToken ct = default)
        => _repository.GetMostRecentActivitySubjectIdAsync(studentId, ct);

    // ── Grade-1 subject loading ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<Subject>> GetPublishedActiveGrade1SubjectsAsync(CancellationToken ct = default)
        => await _dbContext.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .Where(s => s.Grade.Number == 1
                     && s.IsActive
                     && s.LifecycleState == LifecycleState.Published)
            .ToListAsync(ct);

    // ── LearningPathEngine inputs ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IReadOnlyList<KnowledgeNode>> GetSubjectKnowledgeNodesAsync(int subjectId, CancellationToken ct = default)
        => _repository.GetSubjectKnowledgeNodesAsync(subjectId, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<KnowledgeEdge>> GetSubjectKnowledgeEdgesAsync(int subjectId, CancellationToken ct = default)
        => _repository.GetSubjectKnowledgeEdgesAsync(subjectId, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<int, SkillMastery>> GetSkillMasteryForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default)
        => _repository.GetSkillMasteryForStudentInSubjectAsync(studentId, subjectId, ct);

    /// <inheritdoc/>
    public Task<IReadOnlySet<int>> GetCompletedLessonIdsForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default)
        => _repository.GetCompletedLessonIdsForStudentInSubjectAsync(studentId, subjectId, ct);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Lesson>> GetSubjectLessonsAsync(int subjectId, CancellationToken ct = default)
        => _repository.GetSubjectLessonsAsync(subjectId, ct);

    /// <inheritdoc/>
    public async Task<List<Concept>> GetConceptsWithSkillsForSubjectAsync(int subjectId, CancellationToken ct = default)
        => await _dbContext.Concepts
            .AsNoTracking()
            .Include(c => c.Skills)
            .Where(c => c.SubjectId == subjectId)
            .ToListAsync(ct);

    // ── Name resolution ────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(int Id, string Name)?> GetUnitNameAsync(int unitId, CancellationToken ct = default)
    {
        var row = await _dbContext.Units
            .AsNoTracking()
            .Where(u => u.Id == unitId)
            .Select(u => new { u.Id, u.Name })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.Id, row.Name);
    }

    /// <inheritdoc/>
    public async Task<string?> GetSubjectNameAsync(int subjectId, CancellationToken ct = default)
        => await _dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
}

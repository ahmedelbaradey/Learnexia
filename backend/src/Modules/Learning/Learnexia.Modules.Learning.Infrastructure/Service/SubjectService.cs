using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetLanguageCoverage;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

public class SubjectService : LearningBaseService<Subject>, ISubjectService
{
    public SubjectService(ILearningRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
        : base(repository, mapper, localizer)
    {
    }

    // ── Add path ──────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> GradeExistsAsync(int gradeId, CancellationToken ct = default)
        => await _repository.GetAll<Grade>(trackChanges: false)
                            .AnyAsync(g => g.Id == gradeId, ct);

    /// <inheritdoc />
    public async Task<bool> SoftDeletedSubjectExistsAsync(int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(
                    s => s.GradeId == gradeId && s.SubjectCode == code && s.Language == lang,
                    trackChanges: false)
                .IgnoreQueryFilters()
                .AnyAsync(s => s.IsDeleted == true, ct);

    /// <inheritdoc />
    public async Task<bool> LiveSubjectExistsAsync(int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default)
        => await _repository.GetAll<Subject>(trackChanges: false)
                            .AnyAsync(s => s.GradeId == gradeId && s.SubjectCode == code && s.Language == lang, ct);

    /// <inheritdoc />
    public async Task<Subject> StageAddSubjectAsync(Subject subject, int userId, CancellationToken ct = default)
    {
        await _repository.AddAsync(subject, ct);
        await _repository.FlushAsync(userId, ct);
        return subject;
    }

    // ── Delete path ───────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Subject?> GetSubjectTrackedAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(s => s.Id == id, trackChanges: true)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<bool> SubjectHasUnitsAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetAll<Unit>(trackChanges: false)
                            .AnyAsync(u => u.SubjectId == subjectId, ct);

    /// <inheritdoc />
    public async Task StageSubjectUpdateAsync(Subject subject, CancellationToken ct = default)
        => await _repository.UpdateAsync(subject);

    // ── Edit path ─────────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Subject?> GetSubjectNoTrackingAsync(int id, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(s => s.Id == id, trackChanges: false)
                            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<bool> SubjectConflictExistsAsync(int excludeId, int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default)
        => await _repository.GetAll<Subject>(trackChanges: false)
                            .AnyAsync(s => s.Id != excludeId
                                        && s.GradeId == gradeId
                                        && s.SubjectCode == code
                                        && s.Language == lang, ct);

    // ── Reorder path ─────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<List<Subject>> GetSubjectsTrackedByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(s => ids.Contains(s.Id), trackChanges: true)
                            .ToListAsync(ct);

    // ── List/pagination ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PaginatedResult<SingleSubjectResponse>> GetPagedAsync(
        int? gradeId,
        int pageNumber,
        int pageSize,
        string? orderBy,
        CancellationToken ct = default)
    {
        var query = gradeId.HasValue
            ? _repository.GetByCondition<Subject>(s => s.GradeId == gradeId.Value, trackChanges: false)
            : _repository.GetAll<Subject>(trackChanges: false);

        if (!query.Any())
            return PaginatedResult<SingleSubjectResponse>.EmptyCollection();

        return await _mapper
            .ProjectTo<SingleSubjectResponse>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }

    // ── GetSubjectsForGrade ──────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Grade?> GetGradeByNumberAsync(int gradeNumber, CancellationToken ct = default)
        => await _repository.GetAll<Grade>(trackChanges: false)
                            .FirstOrDefaultAsync(g => g.Number == gradeNumber, ct);

    /// <inheritdoc />
    public async Task<List<Subject>> GetActivePublishedSubjectsForGradeAsync(int gradeId, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(
                    s => s.GradeId == gradeId && s.IsActive && s.LifecycleState == LifecycleState.Published,
                    trackChanges: false)
                .ToListAsync(ct);

    // ── GetSubjectLessons / GetSubjectSkillTree ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Subject?> GetActivePublishedSubjectAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(
                    s => s.Id == subjectId && s.IsActive && s.LifecycleState == LifecycleState.Published,
                    trackChanges: false)
                .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<Subject?> GetActivePublishedSubjectByNaturalKeyAsync(int gradeId, SubjectCode code, ContentLanguage lang, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(
                    s => s.GradeId == gradeId
                      && s.SubjectCode == code
                      && s.Language == lang
                      && s.IsActive
                      && s.LifecycleState == LifecycleState.Published,
                    trackChanges: false)
                .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<List<Unit>> GetUnitsWithLessonsAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetByCondition<Unit>(
                    u => u.SubjectId == subjectId && u.IsActive && u.LifecycleState == LifecycleState.Published,
                    trackChanges: false)
                .Include(u => u.Lessons)
                .OrderBy(u => u.SequenceOrder)
                .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<Concept>> GetConceptsWithSkillsAndLessonsAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetByCondition<Concept>(c => c.SubjectId == subjectId, trackChanges: false)
                .Include(c => c.Skills.Where(sk => sk.IsActive))
                    .ThenInclude(sk => sk.Lessons.Where(l => l.IsActive && l.LifecycleState == LifecycleState.Published))
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

    // ── LearningPathEngine bulk inputs ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeNode>> GetSubjectKnowledgeNodesAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetSubjectKnowledgeNodesAsync(subjectId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeEdge>> GetSubjectKnowledgeEdgesAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetSubjectKnowledgeEdgesAsync(subjectId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, SkillMastery>> GetSkillMasteryForStudentInSubjectAsync(int studentId, int subjectId, CancellationToken ct = default)
        => await _repository.GetSkillMasteryForStudentInSubjectAsync(studentId, subjectId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlySet<int>> GetCompletedLessonIdsForStudentInSubjectAsync(int studentId, int subjectId, CancellationToken ct = default)
        => await _repository.GetCompletedLessonIdsForStudentInSubjectAsync(studentId, subjectId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Lesson>> GetSubjectLessonsAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetSubjectLessonsAsync(subjectId, ct);

    /// <inheritdoc />
    public async Task<List<Skill>> GetSubjectSkillsAsync(int subjectId, CancellationToken ct = default)
        => await _repository.GetByCondition<Skill>(sk => sk.Concept.SubjectId == subjectId, trackChanges: false)
                            .ToListAsync(ct);

    // ── GetSubjectLanguageCoverage ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Grade?> GetGradeByIdAsync(int gradeId, CancellationToken ct = default)
        => await _repository.GetAll<Grade>(trackChanges: false)
                            .FirstOrDefaultAsync(g => g.Id == gradeId, ct);

    /// <inheritdoc />
    public async Task<List<Subject>> GetSubjectsForGradeAdminAsync(int gradeId, CancellationToken ct = default)
        => await _repository.GetByCondition<Subject>(s => s.GradeId == gradeId, trackChanges: false)
                            .ToListAsync(ct);
}

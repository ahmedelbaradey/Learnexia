using System.Linq.Expressions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Repository;

/// <summary>
/// Generic repository for the Learning module. Implements <see cref="ILearningRepository"/> which
/// extends <see cref="IGenericRepository"/>.
///
/// IMPORTANT — deferred-commit module (ADR 0001/0002): write methods stage changes only.
/// <c>SaveChangesAsync</c> is NEVER called here. The <c>UnitOfWorkBehavior</c> owns the single
/// commit per command. This is intentionally different from Catalog's <c>GenericRepository</c>
/// which calls <c>SaveChangesAsync</c> on every write.
/// </summary>
public class LearningRepository : ILearningRepository
{
    protected readonly LearningDbContext RepositoryContext;
    protected readonly ICurrentUserService CurrentUserService;

    public LearningRepository(LearningDbContext dbContext, ICurrentUserService currentUserService)
    {
        RepositoryContext = dbContext;
        CurrentUserService = currentUserService;
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    public IQueryable<T> GetAll<T>(bool trackChanges) where T : class
        => !trackChanges ? RepositoryContext.Set<T>().AsNoTracking() : RepositoryContext.Set<T>();

    public IQueryable<T> GetByCondition<T>(Expression<Func<T, bool>> condition, bool trackChanges = false) where T : class
        => !trackChanges ? RepositoryContext.Set<T>().Where(condition).AsNoTracking() : RepositoryContext.Set<T>().Where(condition);

    public async Task<T> GetByIdAsync<T>(int id, bool trackChanges) where T : class
    {
        var entity = await RepositoryContext.Set<T>().FindAsync(id).ConfigureAwait(false);
        if (entity is null)
            throw new InvalidOperationException($"Entity of type {typeof(T).Name} with ID {id} was not found.");
        return entity;
    }

    public async Task<T> GetByIdAsync<T>(int id, bool trackChanges, Func<IQueryable<T>, IQueryable<T>>? include = null) where T : class
    {
        IQueryable<T> query = RepositoryContext.Set<T>();
        if (!trackChanges)
            query = query.AsNoTracking();
        if (include is not null)
            query = include(query);
        return (await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id))!;
    }

    public async Task<bool> AnyAsync<T>(Expression<Func<T, bool>> expression) where T : class
        => await RepositoryContext.Set<T>().AnyAsync(expression);

    // ── Write (stage-only — no SaveChangesAsync) ──────────────────────────────

    public async Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
    {
        await RepositoryContext.Set<T>().AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task AddRangeAsync<T>(ICollection<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        await RepositoryContext.Set<T>().AddRangeAsync(entities, cancellationToken);
    }

    public Task<bool> UpdateAsync<T>(T entity) where T : class
    {
        RepositoryContext.Set<T>().Update(entity);
        return Task.FromResult(true);
    }

    public Task UpdateRangeAsync<T>(ICollection<T> entities) where T : class
    {
        RepositoryContext.Set<T>().UpdateRange(entities);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync<T>(T entity) where T : class
    {
        RepositoryContext.Set<T>().Remove(entity);
        return Task.FromResult(true);
    }

    public Task DeleteRangeAsync<T>(ICollection<T> entities) where T : class
    {
        RepositoryContext.Set<T>().RemoveRange(entities);
        return Task.CompletedTask;
    }

    // ── Skill dependency graph (P2-11 BE-5) ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<KnowledgeNode>> GetPrerequisiteNodesAsync(int nodeId, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeEdges
            .AsNoTracking()
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite && e.TargetNodeId == nodeId)
            .Select(e => e.SourceNode)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<List<KnowledgeNode>> GetUnlockedByNodeAsync(int nodeId, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeEdges
            .AsNoTracking()
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite && e.SourceNodeId == nodeId)
            .Select(e => e.TargetNode)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<bool> KnowledgeNodeExistsAsync(int nodeId, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeNodes
            .AsNoTracking()
            .AnyAsync(n => n.Id == nodeId, ct);

    // ── Learning Path Engine (P2-04) ──────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KnowledgeNode>> GetSubjectKnowledgeNodesAsync(
        int subjectId, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SubjectId == subjectId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KnowledgeEdge>> GetSubjectKnowledgeEdgesAsync(
        int subjectId, CancellationToken ct = default)
    {
        // Collect the node IDs that belong to this subject first, then filter edges
        // whose both endpoints are within that set. Mirrors the two-step pattern used by
        // GetPrerequisiteNodesAsync / GetUnlockedByNodeAsync (P2-11) where we traverse edges
        // via navigation rather than doing a cross-joined subquery.
        var nodeIds = await RepositoryContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SubjectId == subjectId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        var nodeIdSet = nodeIds.ToHashSet();

        return await RepositoryContext.KnowledgeEdges
            .AsNoTracking()
            .Where(e => nodeIdSet.Contains(e.SourceNodeId) && nodeIdSet.Contains(e.TargetNodeId))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<int, SkillMastery>> GetSkillMasteryForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default)
    {
        // Step 1 — Collect all skills in the subject (via Concept → Subject chain).
        var skills = await RepositoryContext.Skills
            .AsNoTracking()
            .Include(s => s.Concept)
            .Where(s => s.Concept.SubjectId == subjectId)
            .Select(s => new { s.Id, s.MasteryThreshold })
            .ToListAsync(ct);

        var skillIds = skills.Select(s => s.Id).ToHashSet();

        // Step 2 — Aggregate StudentAnswers for this student where the question has a skill in this subject.
        // Math mirrors GetSkillStatsQueryHandler: accuracy = Math.Round(correct/total*100, 2). (P2-08)
        var aggregates = await RepositoryContext.StudentAnswers
            .AsNoTracking()
            .Where(sa => sa.Attempt.StudentId == studentId
                      && sa.Question.SkillId.HasValue
                      && skillIds.Contains(sa.Question.SkillId!.Value))
            .GroupBy(sa => sa.Question.SkillId!.Value)
            .Select(g => new
            {
                SkillId = g.Key,
                Total   = g.Count(),
                Correct = g.Count(sa => sa.IsCorrect)
            })
            .ToListAsync(ct);

        var aggById = aggregates.ToDictionary(a => a.SkillId);

        // Step 3 — Build the result dictionary: every skill in the subject gets an entry.
        // Skills with no answers receive TotalAnswers = 0 and AccuracyPercentage = 0 (Q2 guard).
        var result = new Dictionary<int, SkillMastery>(skills.Count);
        foreach (var skill in skills)
        {
            var (total, correct) = aggById.TryGetValue(skill.Id, out var a)
                ? (a.Total, a.Correct)
                : (0, 0);

            var accuracy = total == 0
                ? 0.0
                : Math.Round((double)correct / total * 100, 2);

            result[skill.Id] = new SkillMastery(skill.Id, accuracy, total);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<int>> GetCompletedLessonIdsForStudentInSubjectAsync(
        int studentId, int subjectId, CancellationToken ct = default)
    {
        var ids = await RepositoryContext.Attempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId
                     && a.Status == AttemptStatus.Completed
                     && RepositoryContext.Lessons.Any(l => l.Id == a.LessonId
                                                        && l.Unit.SubjectId == subjectId))
            .Select(a => a.LessonId)
            .Distinct()
            .ToListAsync(ct);

        return ids.ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Lesson>> GetSubjectLessonsAsync(
        int subjectId, CancellationToken ct = default)
        // P7-02: Filter IsActive — student-facing reads must not see inactive lessons.
        // P7-05: Filter LifecycleState == Published — Draft/Archived lessons not served to students.
        // The global IsDeleted filter already excludes soft-deleted rows.
        => await RepositoryContext.Lessons
            .AsNoTracking()
            .Where(l => l.Unit.SubjectId == subjectId && l.IsActive && l.LifecycleState == LifecycleState.Published)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<int?> GetLessonSkillIdAsync(int lessonId, CancellationToken ct = default)
    {
        return await RepositoryContext.Lessons
            .AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => l.SkillId)
            .FirstOrDefaultAsync(ct);
    }

    // ── Dashboard (P2-09) ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int?> GetMostRecentActivitySubjectIdAsync(
        int studentId, CancellationToken ct = default)
    {
        // Attempt.LessonId is a plain int (no EF FK / navigation property — module isolation).
        // Use a join via a sub-select: for each Attempt (most recent first), look up the
        // SubjectId via the Lesson → Unit chain and return the first non-null result.
        // Mirrors the sub-query pattern in GetCompletedLessonIdsForStudentInSubjectAsync.
        return await RepositoryContext.Attempts
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.StartedAt)
            .Select(a => RepositoryContext.Lessons
                .AsNoTracking()
                .Where(l => l.Id == a.LessonId)
                .Select(l => (int?)l.Unit.SubjectId)
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);
    }

    // ── P7-03 Knowledge-graph admin authoring ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<KnowledgeNode?> GetNodeBySkillIdAsync(int skillId, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.SkillId == skillId, ct);

    /// <inheritdoc/>
    public async Task<List<KnowledgeEdge>> GetEdgesForNodeAsync(int nodeId, bool trackChanges, CancellationToken ct = default)
    {
        var query = trackChanges
            ? RepositoryContext.KnowledgeEdges
            : RepositoryContext.KnowledgeEdges.AsNoTracking();

        return await query
            .Where(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<List<KnowledgeEdge>> GetAllPrerequisiteEdgesAsync(CancellationToken ct = default)
        => await RepositoryContext.KnowledgeEdges
            .AsNoTracking()
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<Subject?> GetSubjectForNodeAsync(int nodeId, CancellationToken ct = default)
    {
        var node = await RepositoryContext.KnowledgeNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == nodeId, ct);

        if (node is null)
            return null;

        return await RepositoryContext.Subjects
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == node.SubjectId, ct);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeNode?> GetKnowledgeNodeByIdAsync(int nodeId, bool trackChanges, CancellationToken ct = default)
    {
        var query = trackChanges
            ? RepositoryContext.KnowledgeNodes
            : RepositoryContext.KnowledgeNodes.AsNoTracking();

        return await query.FirstOrDefaultAsync(n => n.Id == nodeId, ct);
    }

    /// <inheritdoc/>
    public async Task<KnowledgeEdge?> GetKnowledgeEdgeByIdAsync(int edgeId, bool trackChanges, CancellationToken ct = default)
    {
        var query = trackChanges
            ? RepositoryContext.KnowledgeEdges
            : RepositoryContext.KnowledgeEdges.AsNoTracking();

        return await query.FirstOrDefaultAsync(e => e.Id == edgeId, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> KnowledgeEdgeDuplicateExistsAsync(int sourceNodeId, int targetNodeId, int relationshipType, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeEdges
            .AnyAsync(e =>
                e.SourceNodeId == sourceNodeId &&
                e.TargetNodeId == targetNodeId &&
                (int)e.RelationshipType == relationshipType, ct);

    /// <inheritdoc/>
    public async Task<List<KnowledgeNode>> GetGraphNodesAsync(int subjectId, CancellationToken ct = default)
        => await RepositoryContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SubjectId == subjectId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<List<KnowledgeEdge>> GetGraphEdgesAsync(int subjectId, CancellationToken ct = default)
    {
        var nodeIds = await RepositoryContext.KnowledgeNodes
            .AsNoTracking()
            .Where(n => n.SubjectId == subjectId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        var nodeIdSet = nodeIds.ToHashSet();

        return await RepositoryContext.KnowledgeEdges
            .AsNoTracking()
            .Where(e => nodeIdSet.Contains(e.SourceNodeId) && nodeIdSet.Contains(e.TargetNodeId))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Subject?> GetSubjectByConceptIdAsync(int conceptId, CancellationToken ct = default)
    {
        var concept = await RepositoryContext.Concepts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conceptId, ct);

        if (concept is null)
            return null;

        return await RepositoryContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == concept.SubjectId, ct);
    }

    // ── P7-05 Content lifecycle / versioning ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> GetMaxVersionNumberAsync(VersionedEntityType entityType, int entityId, CancellationToken ct = default)
        => await RepositoryContext.ContentVersions
            .AsNoTracking()
            .Where(cv => cv.EntityType == entityType && cv.EntityId == entityId)
            .Select(cv => (int?)cv.VersionNumber)
            .MaxAsync(ct) ?? 0;

    /// <inheritdoc/>
    public async Task<List<ContentVersion>> GetVersionHistoryAsync(VersionedEntityType entityType, int entityId, CancellationToken ct = default)
        => await RepositoryContext.ContentVersions
            .AsNoTracking()
            .Where(cv => cv.EntityType == entityType && cv.EntityId == entityId)
            .OrderByDescending(cv => cv.VersionNumber)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<ContentVersion?> GetVersionAsync(VersionedEntityType entityType, int entityId, int versionNumber, CancellationToken ct = default)
        => await RepositoryContext.ContentVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(cv => cv.EntityType == entityType && cv.EntityId == entityId && cv.VersionNumber == versionNumber, ct);

    /// <inheritdoc/>
    public async Task<Subject?> GetOwningSubjectAsync(VersionedEntityType entityType, int entityId, CancellationToken ct = default)
    {
        // Walk the ownership chain per entity type.
        switch (entityType)
        {
            case VersionedEntityType.Subject:
                return await RepositoryContext.Subjects
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Id == entityId, ct);

            case VersionedEntityType.Unit:
            {
                var unit = await RepositoryContext.Units
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(u => u.Subject)
                    .FirstOrDefaultAsync(u => u.Id == entityId, ct);
                return unit?.Subject;
            }

            case VersionedEntityType.Lesson:
            {
                var lesson = await RepositoryContext.Lessons
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(l => l.Unit)
                        .ThenInclude(u => u.Subject)
                    .FirstOrDefaultAsync(l => l.Id == entityId, ct);
                return lesson?.Unit?.Subject;
            }

            case VersionedEntityType.QuizQuestion:
            {
                var question = await RepositoryContext.QuizQuestions
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(q => q.Id == entityId, ct);

                if (question is null)
                    return null;

                var lesson = await RepositoryContext.Lessons
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Include(l => l.Unit)
                        .ThenInclude(u => u.Subject)
                    .FirstOrDefaultAsync(l => l.Id == question.LessonId, ct);

                return lesson?.Unit?.Subject;
            }

            default:
                return null;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Subject>> GetSubjectsForCoverageAsync(int gradeId, CancellationToken ct = default)
        => await RepositoryContext.Subjects
            .AsNoTracking()
            .Where(s => s.GradeId == gradeId)
            .ToListAsync(ct);

    // ── Mastery (P3-09) ──────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<List<StudentSkillMastery>> GetSkillMasteryRowsAsync(
        int studentId, IReadOnlyCollection<int> skillIds, CancellationToken ct = default)
        => await RepositoryContext.StudentSkillMasteries
            .AsNoTracking()
            .Where(m => m.StudentId == studentId && skillIds.Contains(m.SkillId))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task UpsertStudentSkillMasteryAsync(StudentSkillMastery mastery, CancellationToken ct = default)
    {
        // Check whether a row already exists in the DB for this (StudentId, SkillId) pair.
        // We must query with tracking ON so EF can manage the update state.
        var existing = await RepositoryContext.StudentSkillMasteries
            .FirstOrDefaultAsync(m => m.StudentId == mastery.StudentId && m.SkillId == mastery.SkillId, ct);

        if (existing is null)
        {
            // Insert path: stage the new entity (no SaveChanges — UoW behavior commits).
            await RepositoryContext.StudentSkillMasteries.AddAsync(mastery, ct);
        }
        else
        {
            // Update path: mutate the tracked entity in-place so EF generates an UPDATE statement.
            existing.MasteryPercentage  = mastery.MasteryPercentage;
            existing.Status             = mastery.Status;
            existing.AttemptsCount      = mastery.AttemptsCount;
            existing.LastPracticedAt    = mastery.LastPracticedAt;
            // SR columns (P3-10 reserved) are not touched here — they remain at their current values.
            RepositoryContext.StudentSkillMasteries.Update(existing);
        }
    }

    /// <inheritdoc/>
    public async Task<List<StudentSkillMastery>> GetAllMasteryForStudentAsync(
        int studentId, CancellationToken ct = default)
        => await RepositoryContext.StudentSkillMasteries
            .AsNoTracking()
            .Where(m => m.StudentId == studentId)
            .OrderBy(m => m.SkillId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<StudentSkillMastery?> GetSkillMasteryForStudentAsync(
        int studentId, int skillId, CancellationToken ct = default)
        => await RepositoryContext.StudentSkillMasteries
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.StudentId == studentId && m.SkillId == skillId, ct);
}

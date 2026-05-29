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
        => await RepositoryContext.Lessons
            .AsNoTracking()
            .Where(l => l.Unit.SubjectId == subjectId)
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
}

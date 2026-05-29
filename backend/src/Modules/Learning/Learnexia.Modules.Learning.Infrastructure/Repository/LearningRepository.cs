using System.Linq.Expressions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
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
}

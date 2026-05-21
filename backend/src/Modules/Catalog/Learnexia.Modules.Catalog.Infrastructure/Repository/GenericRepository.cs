using System.Linq.Expressions;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Catalog.Infrastructure.Repository;

public class GenericRepository : IGenericRepository
{
    protected readonly CatalogDbContext RepositoryContext;
    protected readonly ICurrentUserService CurrentUserService;

    public GenericRepository(CatalogDbContext dbContext, ICurrentUserService currentUserService)
    {
        RepositoryContext = dbContext;
        CurrentUserService = currentUserService;
    }

    public IQueryable<T> GetAll<T>(bool trackChanges) where T : class
        => !trackChanges ? RepositoryContext.Set<T>().AsNoTracking() : RepositoryContext.Set<T>();

    public virtual IQueryable<T> GetByCondition<T>(Expression<Func<T, bool>> expression, bool trackChanges = false) where T : class
        => !trackChanges ? RepositoryContext.Set<T>().Where(expression).AsNoTracking() : RepositoryContext.Set<T>().Where(expression);

    public virtual async Task<T> GetByIdAsync<T>(int id, bool trackChanges) where T : class
    {
        var entity = await RepositoryContext.Set<T>().FindAsync(id).ConfigureAwait(false);
        if (entity == null)
            throw new InvalidOperationException($"Entity of type {typeof(T).Name} with ID {id} was not found.");
        return entity;
    }

    public virtual async Task<T> GetByIdAsync<T>(int id, bool trackChanges, Func<IQueryable<T>, IQueryable<T>>? include = null) where T : class
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

    public virtual async Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class
    {
        await RepositoryContext.Set<T>().AddAsync(entity, cancellationToken);
        await RepositoryContext.SaveChangesAsync(CurrentUserService.UserId.GetValueOrDefault());
        return entity;
    }

    public virtual async Task AddRangeAsync<T>(ICollection<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        await RepositoryContext.Set<T>().AddRangeAsync(entities, cancellationToken);
        await RepositoryContext.SaveChangesAsync(CurrentUserService.UserId.GetValueOrDefault());
    }

    public virtual async Task<bool> DeleteAsync<T>(T entity) where T : class
    {
        RepositoryContext.Set<T>().Remove(entity);
        var rowEffectedCount = await RepositoryContext.SaveChangesAsync(CurrentUserService.UserId.GetValueOrDefault());
        return rowEffectedCount > 0;
    }

    public virtual async Task DeleteRangeAsync<T>(ICollection<T> entities) where T : class
    {
        RepositoryContext.Set<T>().RemoveRange(entities);
        await RepositoryContext.SaveChangesAsync(CurrentUserService.UserId.GetValueOrDefault());
    }

    public virtual async Task<bool> UpdateAsync<T>(T entity) where T : class
    {
        RepositoryContext.Set<T>().Update(entity);
        var rowUpdated = await RepositoryContext.SaveChangesAsync(CurrentUserService.UserId.GetValueOrDefault());
        return rowUpdated > 0;
    }

    public virtual async Task UpdateRangeAsync<T>(ICollection<T> entities) where T : class
    {
        RepositoryContext.Set<T>().UpdateRange(entities);
        await RepositoryContext.SaveChangesAsync(CurrentUserService.UserId.GetValueOrDefault());
    }
}

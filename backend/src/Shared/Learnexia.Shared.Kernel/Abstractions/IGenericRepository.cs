using System.Linq.Expressions;

namespace Learnexia.Shared.Kernel.Abstractions;

public interface IGenericRepository
{
    IQueryable<T> GetAll<T>(bool trackChanges) where T : class;
    IQueryable<T> GetByCondition<T>(Expression<Func<T, bool>> condition, bool trackChanges = false) where T : class;
    Task<T> GetByIdAsync<T>(int id, bool trackChanges, Func<IQueryable<T>, IQueryable<T>>? include = null) where T : class;
    Task<T> GetByIdAsync<T>(int id, bool trackChanges) where T : class;
    Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;
    Task AddRangeAsync<T>(ICollection<T> entities, CancellationToken cancellationToken = default) where T : class;
    Task<bool> UpdateAsync<T>(T entity) where T : class;
    Task UpdateRangeAsync<T>(ICollection<T> entities) where T : class;
    Task<bool> DeleteAsync<T>(T entity) where T : class;
    Task DeleteRangeAsync<T>(ICollection<T> entities) where T : class;
    Task<bool> AnyAsync<T>(Expression<Func<T, bool>> expression) where T : class;
}

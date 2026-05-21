using System.Linq.Expressions;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Shared.Kernel.Abstractions;

public interface IBaseService<TEntity>
{
    Task<BaseResponse<string>> AddAsync<TDto>(TDto entity, CancellationToken cancellationToken = default) where TDto : BaseDto;
    Task<BaseResponse<string>> UpdateAsync<TDto>(TDto entity) where TDto : BaseDto;
    Task<BaseResponse<string>> DeleteAsync(int id);
    Task<BaseResponse<TDto>> GetByIdAsync<TDto>(int id, bool trackChanges) where TDto : BaseDto;
    Task<PaginatedResult<TDto>> GetAllPagedAsync<TDto>(int pageNumber, int pageSize, string? orderBy, bool trackChanges) where TDto : BaseDto;
    Task<BaseResponse<PaginatedResult<TDto>>> GetAllByConditionPagedAsync<TDto>(Expression<Func<TEntity, bool>> expression, int pageNumber, int pageSize, string? orderBy, bool trackChanges) where TDto : BaseDto;
    IQueryable<TEntity> GetAllAsync(bool trackChanges);
    IQueryable<TEntity> GetAllByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges);
}

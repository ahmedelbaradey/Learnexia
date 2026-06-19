using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Base service for Learning module aggregates. Mirrors Catalog's <c>BaseService&lt;TEntity&gt;</c>
/// shape for structural consistency, BUT this module is deferred-commit:
/// write methods stage changes via the repository (which does NOT call <c>SaveChangesAsync</c>).
/// The <c>UnitOfWorkBehavior</c> commits once after the handler returns.
/// </summary>
public class LearningBaseService<TEntity> : BaseResponseHandler, IBaseService<TEntity> where TEntity : class
{
    protected readonly ILearningRepository _repository;
    protected readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LearningBaseService(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _mapper = mapper;
        _localizer = localizer;
    }

    public virtual async Task<BaseResponse<string>> AddAsync<TDto>(TDto entity, CancellationToken cancellationToken = default) where TDto : BaseDto
    {
        var original = _mapper.Map<TEntity>(entity);
        await _repository.AddAsync(original, cancellationToken);
        return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
    }

    public virtual async Task<BaseResponse<string>> UpdateAsync<TDto>(TDto entity) where TDto : BaseDto
    {
        var original = await _repository.GetByIdAsync<TEntity>(entity.Id, true);
        if (original is null)
            return NotFound<string>(_localizer[SharedResourcesKey.NoRecords]);
        _mapper.Map(entity, original);
        await _repository.UpdateAsync(original);
        return Success<string>(_localizer[SharedResourcesKey.OperationCompletedSuccessfully]);
    }

    public virtual async Task<BaseResponse<string>> DeleteAsync(int id)
    {
        var entity = await _repository.GetByIdAsync<TEntity>(id, false);
        if (entity is null)
            return NotFound<string>(_localizer[SharedResourcesKey.NoRecords]);
        await _repository.DeleteAsync(entity);
        return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
    }

    public virtual async Task<BaseResponse<TDto>> GetByIdAsync<TDto>(int id, bool trackChanges) where TDto : BaseDto
    {
        var entity = await _repository.GetByIdAsync<TEntity>(id, trackChanges);
        if (entity is null)
            return NotFound<TDto>(_localizer[SharedResourcesKey.NoRecords]);
        return Success(_mapper.Map<TDto>(entity));
    }

    public virtual async Task<PaginatedResult<TDto>> GetAllPagedAsync<TDto>(int pageNumber, int pageSize, string? orderBy, bool trackChanges) where TDto : BaseDto
    {
        var result = _repository.GetAll<TEntity>(trackChanges);
        if (!result.Any())
            return PaginatedResult<TDto>.EmptyCollection();
        return await _mapper.ProjectTo<TDto>(result).ToPaginatedListAsync(pageNumber, pageSize, orderBy);
    }

    public virtual async Task<BaseResponse<PaginatedResult<TDto>>> GetAllByConditionPagedAsync<TDto>(
        Expression<Func<TEntity, bool>> expression, int pageNumber, int pageSize, string? orderBy, bool trackChanges) where TDto : BaseDto
    {
        var result = _repository.GetByCondition(expression, trackChanges);
        if (!result.Any())
            return EmptyCollection(PaginatedResult<TDto>.Success(new List<TDto>(), 0, 0, 0));
        return Success(await _mapper.ProjectTo<TDto>(result).ToPaginatedListAsync(pageNumber, pageSize, orderBy));
    }

    public IQueryable<TEntity> GetAllAsync(bool trackChanges)
        => _repository.GetAll<TEntity>(trackChanges);

    public IQueryable<TEntity> GetAllByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges)
        => _repository.GetByCondition(expression, trackChanges);
}

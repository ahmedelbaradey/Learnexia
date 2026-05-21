using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Catalog.Infrastructure.Service;

public class BaseService<TEntity> : BaseResponseHandler, IBaseService<TEntity> where TEntity : class
{
    protected IGenericRepository _repository;
    protected IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BaseService(IGenericRepository repository, IMapper mapper, IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _mapper = mapper;
        _localizer = localizer;
    }

    public virtual async Task<BaseResponse<string>> AddAsync<TDto>(TDto entity, CancellationToken cancellationToken = default) where TDto : BaseDto
    {
        TEntity originalEntity = _mapper.Map<TEntity>(entity);
        var status = await _repository.AddAsync(originalEntity, cancellationToken);
        if (status == null)
            return BadRequest<string>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        return Success<string>(_localizer[SharedResourcesKey.RecordSavedSuccessfully]);
    }

    public virtual async Task<BaseResponse<string>> UpdateAsync<TDto>(TDto entity) where TDto : BaseDto
    {
        var originalEntity = await _repository.GetByIdAsync<TEntity>(entity.Id, true);
        _mapper.Map(entity, originalEntity);
        var status = await _repository.UpdateAsync(originalEntity);
        if (!status)
            return BadRequest<string>("Update Operation Failed.");
        return Success("Update Operation Successfully.");
    }

    public virtual async Task<BaseResponse<string>> DeleteAsync(int id)
    {
        var entity = await _repository.GetByIdAsync<TEntity>(id, false);
        var status = await _repository.DeleteAsync(entity);
        if (!status)
            return BadRequest<string>("Delete Operation Failed.");
        return Success("Delete Operation Successfully.");
    }

    public virtual async Task<BaseResponse<TDto>> GetByIdAsync<TDto>(int id, bool trackChanges) where TDto : BaseDto
    {
        var entity = await _repository.GetByIdAsync<TEntity>(id, trackChanges);
        if (entity == null)
            return NotFound<TDto>("Entity with this Id not found!");
        var entityMapper = _mapper.Map<TDto>(entity);
        return Success(entityMapper);
    }

    public virtual async Task<PaginatedResult<TDto>> GetAllPagedAsync<TDto>(int pageNumber, int pageSize, string? orderBy, bool trackChanges) where TDto : BaseDto
    {
        var result = _repository.GetAll<TEntity>(trackChanges);
        if (!result.Any())
            return PaginatedResult<TDto>.EmptyCollection();

        var list = await _mapper.ProjectTo<TDto>(result).ToPaginatedListAsync(pageNumber, pageSize, orderBy);
        return list;
    }

    public virtual async Task<BaseResponse<PaginatedResult<TDto>>> GetAllByConditionPagedAsync<TDto>(Expression<Func<TEntity, bool>> expression, int pageNumber, int pageSize, string? orderBy, bool trackChanges) where TDto : BaseDto
    {
        var result = _repository.GetByCondition(expression, trackChanges);
        if (!result.Any())
            return EmptyCollection(PaginatedResult<TDto>.Success(new List<TDto>(), 0, 0, 0));

        var list = await _mapper.ProjectTo<TDto>(result).ToPaginatedListAsync(pageNumber, pageSize, orderBy);
        return Success(list);
    }

    public IQueryable<TEntity> GetAllAsync(bool trackChanges)
        => _repository.GetAll<TEntity>(trackChanges);

    public IQueryable<TEntity> GetAllByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges)
        => _repository.GetByCondition(expression, trackChanges);
}

using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Learnexia.Modules.Catalog.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Queries.List;

public class ListQueryHandler : BaseResponseHandler, IQueryHandler<ListQuery, BaseResponse<PaginatedResult<SingleCategoryResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly IRepositoryManager _repository;
    private readonly IMapper _mapper;

    public ListQueryHandler(IRepositoryManager repository, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleCategoryResponse>>> Handle(ListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = _repository.Categories.GetByCondition<Category>(c => string.IsNullOrWhiteSpace(request.Search) ? true : c.Name.Contains(request.Search), false);
            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleCategoryResponse>.Success(new List<SingleCategoryResponse>(), 0, 0, 0));

            var categories = await _mapper.ProjectTo<SingleCategoryResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListQuery");
            return ServerError<PaginatedResult<SingleCategoryResponse>>(ex.Message);
        }
    }
}

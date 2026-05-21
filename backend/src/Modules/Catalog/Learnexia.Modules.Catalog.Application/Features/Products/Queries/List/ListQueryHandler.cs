using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Queries.List;

public class ListQueryHandler : BaseResponseHandler, IQueryHandler<ListQuery, BaseResponse<PaginatedResult<SingleProductResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public ListQueryHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleProductResponse>>> Handle(ListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = _service.ProductService.GetAllAsync(false);
            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleProductResponse>.Success(new List<SingleProductResponse>(), 0, 0, 0));

            var productList = await _mapper.ProjectTo<SingleProductResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(productList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListQuery");
            return ServerError<PaginatedResult<SingleProductResponse>>(ex.Message);
        }
    }
}

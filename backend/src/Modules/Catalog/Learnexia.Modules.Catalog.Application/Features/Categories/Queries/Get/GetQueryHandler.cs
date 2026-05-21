using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Learnexia.Modules.Catalog.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Queries.Get;

public class GetQueryHandler : BaseResponseHandler, IQueryHandler<GetQuery, BaseResponse<SingleCategoryResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly IRepositoryManager _repository;
    private readonly IMapper _mapper;

    public GetQueryHandler(IRepositoryManager repository, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<BaseResponse<SingleCategoryResponse>> Handle(GetQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var categorty = await _repository.Categories.GetByIdAsync<Category>(request.Id, false);
            if (categorty is null)
                return NotFound<SingleCategoryResponse>("Category with this Id not found!");

            var categortyMapper = _mapper.Map<SingleCategoryResponse>(categorty);
            return Success(categortyMapper);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetCategoryByIdQuery");
            return ServerError<SingleCategoryResponse>(ex.Message);
        }
    }
}

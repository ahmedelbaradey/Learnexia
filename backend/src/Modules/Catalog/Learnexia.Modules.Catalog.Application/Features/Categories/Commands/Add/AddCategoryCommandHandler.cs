using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Commands.Add;

public class AddCategoryCommandHandler : BaseResponseHandler, ICommandHandler<AddCategoryCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IRepositoryManager _repository;
    private readonly IMapper _mapper;

    public AddCategoryCommandHandler(IRepositoryManager repository, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<BaseResponse<string>> Handle(AddCategoryCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest<string>("the request can't be blank");

            var cateogryMapper = _mapper.Map<Category>(request);

            var result = await _repository.Categories.AddAsync(cateogryMapper);
            if (result is null)
                return BadRequest<string>("Added Operation Failed.");

            return Success("Added Operation Successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddProductCommand");
            return ServerError<string>(ex.Message);
        }
    }
}

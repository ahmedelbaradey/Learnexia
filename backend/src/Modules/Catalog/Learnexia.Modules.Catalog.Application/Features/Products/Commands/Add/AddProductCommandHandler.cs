using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Commands.Add;

// NOTE: backend/ named this class "AddCategoryCommandHandler" (copy-paste typo). Renamed to match the command.
public class AddProductCommandHandler : BaseResponseHandler, ICommandHandler<AddProductCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public AddProductCommandHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<string>> Handle(AddProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest<string>("the request can't be blank");

            return await _service.ProductService.AddAsync<AddProductCommand>(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddProductCommand");
            return ServerError<string>(ex.Message);
        }
    }
}

using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Commands.Delete;

public class DeleteProductCommandHandler : BaseResponseHandler, ICommandHandler<DeleteProductCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public DeleteProductCommandHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<string>> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest<string>("the request can't be blank");

            return await _service.ProductService.DeleteAsync(request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditProductCommand");
            return ServerError<string>(ex.Message);
        }
    }
}

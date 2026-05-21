using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Commands.Edit;

public class EditProductCommandHandler : BaseResponseHandler, ICommandHandler<EditProductCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public EditProductCommandHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<string>> Handle(EditProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest<string>("the request can't be blank");

            return await _service.ProductService.UpdateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditProductCommand");
            return ServerError<string>(ex.Message);
        }
    }
}

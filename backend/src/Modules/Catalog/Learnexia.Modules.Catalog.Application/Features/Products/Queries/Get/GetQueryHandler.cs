using AutoMapper;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Queries.Get;

public class GetQueryHandler : BaseResponseHandler, IQueryHandler<GetQuery, BaseResponse<SingleProductResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly IServiceManager _service;
    private readonly IMapper _mapper;

    public GetQueryHandler(IServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<SingleProductResponse>> Handle(GetQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // NOTE: ported verbatim from backend/. `ProductService.GetByIdAsync` already returns a
            // BaseResponse<SingleProductResponse>; backend re-maps that wrapper and ignores not-found —
            // a latent bug. Kept as-is for parity. Revisit when reconciling Catalog behavior.
            var product = await _service.ProductService.GetByIdAsync<SingleProductResponse>(request.Id, false);
            if (product == null)
                return NotFound<SingleProductResponse>("Product with this Id not found!");

            var productMapper = _mapper.Map<SingleProductResponse>(product);
            return Success(productMapper);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetDoctorByIdQuery");
            return ServerError<SingleProductResponse>(ex.Message);
        }
    }
}

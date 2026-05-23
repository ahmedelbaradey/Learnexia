using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Queries.Get;

public class GetUnitQueryHandler : BaseResponseHandler, IQueryHandler<GetUnitQuery, BaseResponse<SingleUnitResponse>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public GetUnitQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<SingleUnitResponse>> Handle(GetUnitQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _service.UnitService.GetByIdAsync<SingleUnitResponse>(request.Id, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetUnitQuery");
            return ServerError<SingleUnitResponse>(ex.Message);
        }
    }
}

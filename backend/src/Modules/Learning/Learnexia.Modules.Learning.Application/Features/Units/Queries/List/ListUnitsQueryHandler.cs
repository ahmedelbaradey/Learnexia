using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Queries.List;

public class ListUnitsQueryHandler : BaseResponseHandler, IQueryHandler<ListUnitsQuery, BaseResponse<PaginatedResult<SingleUnitResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public ListUnitsQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<PaginatedResult<SingleUnitResponse>>> Handle(ListUnitsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Fully-materialized result from the service — no IQueryable/EF types in Application.
            var list = await _service.UnitService.GetPagedAsync(
                request.SubjectId,
                request.PageNumber,
                request.PageSize,
                request.OrderBy,
                cancellationToken);

            if (list.TotalCount == 0)
                return EmptyCollection(PaginatedResult<SingleUnitResponse>.Success(new List<SingleUnitResponse>(), 0, 0, 0));

            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListUnitsQuery");
            return ServerError<PaginatedResult<SingleUnitResponse>>();
        }
    }
}

using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Queries.List;

public class ListUnitsQueryHandler : BaseResponseHandler, IQueryHandler<ListUnitsQuery, BaseResponse<PaginatedResult<SingleUnitResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;

    public ListUnitsQueryHandler(ILearningServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleUnitResponse>>> Handle(ListUnitsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.SubjectId.HasValue
                ? _service.UnitService.GetAllByConditionAsync(u => u.SubjectId == request.SubjectId.Value, false)
                : _service.UnitService.GetAllAsync(false);

            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleUnitResponse>.Success(new List<SingleUnitResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleUnitResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListUnitsQuery");
            return ServerError<PaginatedResult<SingleUnitResponse>>();
        }
    }
}

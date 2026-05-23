using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Queries.List;

public class ListGradesQueryHandler : BaseResponseHandler, IQueryHandler<ListGradesQuery, BaseResponse<PaginatedResult<SingleGradeResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;

    public ListGradesQueryHandler(ILearningServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleGradeResponse>>> Handle(ListGradesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = _service.GradeService.GetAllAsync(false);
            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleGradeResponse>.Success(new List<SingleGradeResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleGradeResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListGradesQuery");
            return ServerError<PaginatedResult<SingleGradeResponse>>(ex.Message);
        }
    }
}

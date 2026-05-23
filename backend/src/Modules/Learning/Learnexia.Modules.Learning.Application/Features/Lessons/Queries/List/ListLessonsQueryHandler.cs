using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.List;

public class ListLessonsQueryHandler : BaseResponseHandler, IQueryHandler<ListLessonsQuery, BaseResponse<PaginatedResult<SingleLessonResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;

    public ListLessonsQueryHandler(ILearningServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleLessonResponse>>> Handle(ListLessonsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.UnitId.HasValue
                ? _service.LessonService.GetAllByConditionAsync(l => l.UnitId == request.UnitId.Value, false)
                : _service.LessonService.GetAllAsync(false);

            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleLessonResponse>.Success(new List<SingleLessonResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleLessonResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListLessonsQuery");
            return ServerError<PaginatedResult<SingleLessonResponse>>(ex.Message);
        }
    }
}

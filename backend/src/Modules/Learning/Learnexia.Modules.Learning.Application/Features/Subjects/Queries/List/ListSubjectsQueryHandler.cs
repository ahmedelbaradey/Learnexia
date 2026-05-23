using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.List;

public class ListSubjectsQueryHandler : BaseResponseHandler, IQueryHandler<ListSubjectsQuery, BaseResponse<PaginatedResult<SingleSubjectResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;

    public ListSubjectsQueryHandler(ILearningServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleSubjectResponse>>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.GradeId.HasValue
                ? _service.SubjectService.GetAllByConditionAsync(s => s.GradeId == request.GradeId.Value, false)
                : _service.SubjectService.GetAllAsync(false);

            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleSubjectResponse>.Success(new List<SingleSubjectResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleSubjectResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListSubjectsQuery");
            return ServerError<PaginatedResult<SingleSubjectResponse>>(ex.Message);
        }
    }
}

using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Queries.List;

public class ListConceptsQueryHandler : BaseResponseHandler, IQueryHandler<ListConceptsQuery, BaseResponse<PaginatedResult<SingleConceptResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;

    public ListConceptsQueryHandler(ILearningServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleConceptResponse>>> Handle(ListConceptsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.SubjectId.HasValue
                ? _service.ConceptService.GetAllByConditionAsync(c => c.SubjectId == request.SubjectId.Value, false)
                : _service.ConceptService.GetAllAsync(false);

            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleConceptResponse>.Success(new List<SingleConceptResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleConceptResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListConceptsQuery");
            return ServerError<PaginatedResult<SingleConceptResponse>>(ex.Message);
        }
    }
}

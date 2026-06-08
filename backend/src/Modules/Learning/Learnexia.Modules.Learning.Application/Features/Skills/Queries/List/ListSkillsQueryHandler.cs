using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Queries.List;

public class ListSkillsQueryHandler : BaseResponseHandler, IQueryHandler<ListSkillsQuery, BaseResponse<PaginatedResult<SingleSkillResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly IMapper _mapper;

    public ListSkillsQueryHandler(ILearningServiceManager service, IMapper mapper, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
        _mapper = mapper;
    }

    public async Task<BaseResponse<PaginatedResult<SingleSkillResponse>>> Handle(ListSkillsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.ConceptId.HasValue
                ? _service.SkillService.GetAllByConditionAsync(s => s.ConceptId == request.ConceptId.Value, false)
                : _service.SkillService.GetAllAsync(false);

            if (!result.Any())
                return EmptyCollection(PaginatedResult<SingleSkillResponse>.Success(new List<SingleSkillResponse>(), 0, 0, 0));

            var list = await _mapper.ProjectTo<SingleSkillResponse>(result).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);
            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListSkillsQuery");
            return ServerError<PaginatedResult<SingleSkillResponse>>();
        }
    }
}

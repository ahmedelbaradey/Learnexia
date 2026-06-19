using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Queries.List;

/// <summary>
/// Option-C refactor: IQueryable + ProjectTo + ToPaginatedListAsync moved into
/// ISkillService.GetPagedAsync (Infrastructure). Handler is now thin.
/// </summary>
public class ListSkillsQueryHandler : BaseResponseHandler, IQueryHandler<ListSkillsQuery, BaseResponse<PaginatedResult<SingleSkillResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public ListSkillsQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<PaginatedResult<SingleSkillResponse>>> Handle(ListSkillsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SkillService.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.OrderBy,
                request.ConceptId,
                request.Search,
                cancellationToken);

            if (result.TotalCount == 0)
                return EmptyCollection(PaginatedResult<SingleSkillResponse>.Success(new List<SingleSkillResponse>(), 0, 0, 0));

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListSkillsQuery");
            return ServerError<PaginatedResult<SingleSkillResponse>>();
        }
    }
}

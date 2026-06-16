using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Queries.List;

public class ListConceptsQueryHandler : BaseResponseHandler, IQueryHandler<ListConceptsQuery, BaseResponse<PaginatedResult<SingleConceptResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public ListConceptsQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<PaginatedResult<SingleConceptResponse>>> Handle(ListConceptsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Fully-materialized result from the service — no IQueryable/EF types in Application.
            var list = await _service.ConceptService.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.OrderBy,
                request.SubjectId,
                cancellationToken);

            if (list.TotalCount == 0)
                return EmptyCollection(PaginatedResult<SingleConceptResponse>.Success(new List<SingleConceptResponse>(), 0, 0, 0));

            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListConceptsQuery");
            return ServerError<PaginatedResult<SingleConceptResponse>>(ex.Message);
        }
    }
}

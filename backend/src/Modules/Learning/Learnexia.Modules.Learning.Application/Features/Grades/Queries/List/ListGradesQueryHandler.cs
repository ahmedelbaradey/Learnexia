using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Queries.List;

public class ListGradesQueryHandler : BaseResponseHandler, IQueryHandler<ListGradesQuery, BaseResponse<PaginatedResult<SingleGradeResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public ListGradesQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<PaginatedResult<SingleGradeResponse>>> Handle(ListGradesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Fully-materialized result from the service — no IQueryable/EF types in Application.
            var list = await _service.GradeService.GetPagedAsync(
                request.PageNumber,
                request.PageSize,
                request.OrderBy,
                cancellationToken);

            if (list.TotalCount == 0)
                return EmptyCollection(PaginatedResult<SingleGradeResponse>.Success(new List<SingleGradeResponse>(), 0, 0, 0));

            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListGradesQuery");
            return ServerError<PaginatedResult<SingleGradeResponse>>(ex.Message);
        }
    }
}

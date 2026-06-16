using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.List;

public class ListSubjectsQueryHandler : BaseResponseHandler, IQueryHandler<ListSubjectsQuery, BaseResponse<PaginatedResult<SingleSubjectResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public ListSubjectsQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<PaginatedResult<SingleSubjectResponse>>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Fully-materialized result from the service — no IQueryable/EF types in Application.
            var list = await _service.SubjectService.GetPagedAsync(
                request.GradeId,
                request.PageNumber,
                request.PageSize,
                request.OrderBy,
                cancellationToken);

            if (list.TotalCount == 0)
                return EmptyCollection(PaginatedResult<SingleSubjectResponse>.Success(new List<SingleSubjectResponse>(), 0, 0, 0));

            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListSubjectsQuery");
            return ServerError<PaginatedResult<SingleSubjectResponse>>();
        }
    }
}

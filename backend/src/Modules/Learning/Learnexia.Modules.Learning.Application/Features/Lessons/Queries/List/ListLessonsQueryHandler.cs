using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.List;

/// <summary>
/// Admin-only paginated lesson list.
/// Admins must see inactive lessons too (so they can re-activate them). IsActive filtering is
/// NOT applied here; the [Authorize(Policy=AdminOnly)] attribute on the controller is the guard.
/// Students use GET /Subjects/{id}/Lessons (GetSubjectLessonsQueryHandler) which retains its own IsActive filter.
///
/// Option-C: IQueryable no longer returned from Application layer. Pagination + ProjectTo
/// composition moved into ILessonService.GetPagedAsync (Infrastructure). Handler is now thin.
/// </summary>
public class ListLessonsQueryHandler : BaseResponseHandler, IQueryHandler<ListLessonsQuery, BaseResponse<PaginatedResult<SingleLessonResponse>>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;

    public ListLessonsQueryHandler(ILearningServiceManager service, ILoggerManager logger)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<BaseResponse<PaginatedResult<SingleLessonResponse>>> Handle(ListLessonsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // GetPagedAsync: composes IQueryable + ProjectTo + ToPaginatedListAsync entirely inside Infrastructure.
            var list = await _service.LessonService.GetPagedAsync(
                request.UnitId,
                request.PageNumber,
                request.PageSize,
                request.OrderBy,
                cancellationToken);

            if (!list.Data.Any())
                return EmptyCollection(list);

            return Success(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListLessonsQuery");
            return ServerError<PaginatedResult<SingleLessonResponse>>();
        }
    }
}

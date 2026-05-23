using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.List;

public record ListLessonsQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleLessonResponse>>>
{
    /// <summary>Optional filter: when set, only lessons under this unit are returned.</summary>
    public int? UnitId { get; set; }
}

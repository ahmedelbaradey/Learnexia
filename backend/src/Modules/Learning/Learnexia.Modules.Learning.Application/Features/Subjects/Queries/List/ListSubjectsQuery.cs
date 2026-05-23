using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.List;

public record ListSubjectsQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleSubjectResponse>>>
{
    /// <summary>Optional filter: when set, only subjects under this grade are returned.</summary>
    public int? GradeId { get; set; }
}

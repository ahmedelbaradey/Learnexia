using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Queries.List;

public record ListConceptsQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleConceptResponse>>>
{
    /// <summary>Optional filter: when set, only concepts under this subject are returned.</summary>
    public int? SubjectId { get; set; }
}

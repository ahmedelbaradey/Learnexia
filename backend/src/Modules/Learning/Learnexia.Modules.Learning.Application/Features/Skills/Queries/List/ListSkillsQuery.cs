using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Queries.List;

public record ListSkillsQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleSkillResponse>>>
{
    /// <summary>Optional filter: when set, only skills under this concept are returned.</summary>
    public int? ConceptId { get; set; }
}

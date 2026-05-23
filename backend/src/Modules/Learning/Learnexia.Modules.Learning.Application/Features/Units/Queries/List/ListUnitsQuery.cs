using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Queries.List;

public record ListUnitsQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleUnitResponse>>>
{
    /// <summary>Optional filter: when set, only units under this subject are returned.</summary>
    public int? SubjectId { get; set; }
}

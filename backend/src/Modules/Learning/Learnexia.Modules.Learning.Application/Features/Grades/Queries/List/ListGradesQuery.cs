using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Queries.List;

public record ListGradesQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleGradeResponse>>>
{
}

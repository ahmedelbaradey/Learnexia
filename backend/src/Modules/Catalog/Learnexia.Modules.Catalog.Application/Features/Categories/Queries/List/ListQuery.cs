using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Queries.List;

public record ListQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleCategoryResponse>>>
{
}

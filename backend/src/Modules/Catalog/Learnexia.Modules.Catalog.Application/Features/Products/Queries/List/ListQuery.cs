using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Queries.List;

public record ListQuery : BaseListDto, IQuery<BaseResponse<PaginatedResult<SingleProductResponse>>>
{
}

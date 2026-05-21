using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Queries.Get;

public record GetQuery : IQuery<BaseResponse<SingleCategoryResponse>>
{
    public int Id { get; set; }
}

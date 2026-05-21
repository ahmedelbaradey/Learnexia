using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Queries.Get;

public record GetQuery : IQuery<BaseResponse<SingleProductResponse>>
{
    public int Id { get; set; }
}

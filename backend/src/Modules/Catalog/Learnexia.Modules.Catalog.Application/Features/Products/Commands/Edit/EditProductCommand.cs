using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Commands.Edit;

public record EditProductCommand : EditProductDto, ICommand<BaseResponse<string>>
{
}

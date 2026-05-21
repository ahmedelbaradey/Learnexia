using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Products.Commands.Delete;

public record DeleteProductCommand : BaseDto, ICommand<BaseResponse<string>>
{
}

using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Commands.Add;

public record AddCategoryCommand : AddCategoryDto, ICommand<BaseResponse<string>>
{
}

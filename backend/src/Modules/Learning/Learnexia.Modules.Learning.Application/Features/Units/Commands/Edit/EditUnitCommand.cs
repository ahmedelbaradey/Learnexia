using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Edit;

public record EditUnitCommand : EditUnitDto, ICommand<BaseResponse<string>>
{
}

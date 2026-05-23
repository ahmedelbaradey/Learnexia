using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Add;

public record AddUnitCommand : AddUnitDto, ICommand<BaseResponse<string>>
{
}

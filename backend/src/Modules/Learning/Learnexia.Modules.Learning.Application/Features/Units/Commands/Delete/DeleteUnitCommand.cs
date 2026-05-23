using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Delete;

public record DeleteUnitCommand : BaseDto, ICommand<BaseResponse<string>>
{
}

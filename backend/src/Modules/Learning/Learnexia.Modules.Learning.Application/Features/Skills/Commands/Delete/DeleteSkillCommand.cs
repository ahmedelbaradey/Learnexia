using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Delete;

public record DeleteSkillCommand : BaseDto, ICommand<BaseResponse<string>>
{
}

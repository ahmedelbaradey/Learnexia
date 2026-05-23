using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.Edit;

public record EditSkillCommand : EditSkillDto, ICommand<BaseResponse<string>>
{
}

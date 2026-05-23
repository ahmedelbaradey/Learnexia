using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;

public record AddLessonCommand : AddLessonDto, ICommand<BaseResponse<string>>
{
}

using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Delete;

public record DeleteLessonCommand : BaseDto, ICommand<BaseResponse<string>>
{
}

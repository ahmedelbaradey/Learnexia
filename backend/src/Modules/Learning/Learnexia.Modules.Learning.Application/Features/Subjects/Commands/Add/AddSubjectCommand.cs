using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;

public record AddSubjectCommand : AddSubjectDto, ICommand<BaseResponse<string>>
{
}

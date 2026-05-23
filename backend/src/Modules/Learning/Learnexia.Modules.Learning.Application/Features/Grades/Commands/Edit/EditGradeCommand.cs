using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Commands.Edit;

public record EditGradeCommand : EditGradeDto, ICommand<BaseResponse<string>>
{
}

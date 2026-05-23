using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Edit;

public record EditConceptCommand : EditConceptDto, ICommand<BaseResponse<string>>
{
}

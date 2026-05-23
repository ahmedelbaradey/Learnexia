using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Add;

public record AddConceptCommand : AddConceptDto, ICommand<BaseResponse<string>>
{
}

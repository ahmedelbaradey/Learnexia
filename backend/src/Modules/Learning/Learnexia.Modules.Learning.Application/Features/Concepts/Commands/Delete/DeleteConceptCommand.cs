using Learnexia.Shared.Kernel.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Delete;

public record DeleteConceptCommand : BaseDto, ICommand<BaseResponse<string>>
{
}

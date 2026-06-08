using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Add;

/// <summary>
/// Adds a new content block to a lesson. The block is appended at the end
/// (SequenceOrder = current max + 1). IsActive defaults to true.
/// </summary>
public record AddContentBlockCommand : AddContentBlockDto, ICommand<BaseResponse<string>>
{
}

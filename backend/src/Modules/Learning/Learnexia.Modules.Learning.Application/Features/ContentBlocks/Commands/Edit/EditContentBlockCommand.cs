using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Edit;

/// <summary>
/// Edits the Payload and/or BlockType of an existing content block.
/// SequenceOrder, IsActive, and IsDeleted are not editable via this command.
/// </summary>
public record EditContentBlockCommand : EditContentBlockDto, ICommand<BaseResponse<string>>
{
}

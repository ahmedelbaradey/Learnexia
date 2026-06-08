using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Delete;

/// <summary>
/// Soft-deletes a content block by setting IsDeleted = true.
/// The block is no longer served to students or returned in admin queries by default.
/// </summary>
public record DeleteContentBlockCommand : ICommand<BaseResponse<string>>
{
    public int Id { get; init; }
}

using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Reorder;

/// <summary>
/// Batch-sets SequenceOrder on the ContentBlocks specified by the ordered list of IDs.
/// All blocks must belong to the same LessonId — cross-lesson reorder is rejected.
/// The handler assigns SequenceOrder = index within the list (0-based).
/// </summary>
public record ReorderContentBlocksCommand : ICommand<BaseResponse<string>>
{
    /// <summary>
    /// Ordered list of ContentBlock IDs — position in the list becomes the new SequenceOrder.
    /// All IDs must belong to the same Lesson.
    /// </summary>
    public List<int> ContentBlockIds { get; init; } = new();
}

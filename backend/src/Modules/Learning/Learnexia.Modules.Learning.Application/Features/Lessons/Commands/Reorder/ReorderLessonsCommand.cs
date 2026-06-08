using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Reorder;

/// <summary>
/// Batch-sets SequenceOrder on the Lessons specified by the ordered list of IDs.
/// All Lessons must belong to the same UnitId — cross-unit reorder is rejected.
/// The handler assigns SequenceOrder = index within the list (0-based).
/// </summary>
public record ReorderLessonsCommand : ICommand<BaseResponse<string>>
{
    /// <summary>
    /// Ordered list of Lesson IDs — position in the list becomes the new SequenceOrder.
    /// All IDs must belong to the same Unit.
    /// </summary>
    public List<int> LessonIds { get; init; } = new();
}

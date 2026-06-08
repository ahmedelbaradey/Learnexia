using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Reorder;

/// <summary>
/// Batch-sets <c>SequenceOrder</c> on the QuizQuestions specified by the ordered list of IDs.
/// All questions must belong to the supplied <c>LessonId</c> — any ID that resolves to a different
/// lesson is rejected before the update is applied (access-scoping anchor).
/// The handler assigns SequenceOrder = index within the list (0-based).
/// </summary>
public record ReorderQuestionsCommand : ICommand<BaseResponse<string>>
{
    /// <summary>
    /// The lesson that owns all questions in this reorder request.
    /// Acts as an access-scoping anchor: every QuestionId must resolve to this LessonId.
    /// </summary>
    public int LessonId { get; init; }

    /// <summary>
    /// Ordered list of QuizQuestion IDs — position in the list becomes the new SequenceOrder.
    /// All IDs must belong to LessonId.
    /// </summary>
    public List<int> QuestionIds { get; init; } = new();
}

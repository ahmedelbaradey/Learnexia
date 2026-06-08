using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Reorder;

/// <summary>
/// Batch-sets SequenceOrder on the Subjects specified by the ordered list of IDs.
/// All Subjects in the list must belong to the same (GradeId, Language) tree.
/// The handler assigns SequenceOrder = index within the list (0-based).
/// Uses an explicit transaction (multi-write, per ADR 0001 "no UoW, open explicit transaction").
/// </summary>
public record ReorderSubjectsCommand : ICommand<BaseResponse<string>>
{
    /// <summary>
    /// Ordered list of Subject IDs — position in the list becomes the new SequenceOrder.
    /// All IDs must belong to the same (GradeId, Language) tree.
    /// </summary>
    public List<int> SubjectIds { get; init; } = new();
}

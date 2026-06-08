using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Reorder;

/// <summary>
/// Batch-sets SequenceOrder on the Units specified by the ordered list of IDs.
/// All Units must belong to the same SubjectId (and therefore the same language tree).
/// The handler assigns SequenceOrder = index within the list (0-based).
/// The UnitOfWorkBehavior's transaction wraps all staged updates atomically (deferred-commit module).
/// </summary>
public record ReorderUnitsCommand : ICommand<BaseResponse<string>>
{
    /// <summary>
    /// Ordered list of Unit IDs — position in the list becomes the new SequenceOrder.
    /// All IDs must belong to the same Subject.
    /// </summary>
    public List<int> UnitIds { get; init; } = new();
}

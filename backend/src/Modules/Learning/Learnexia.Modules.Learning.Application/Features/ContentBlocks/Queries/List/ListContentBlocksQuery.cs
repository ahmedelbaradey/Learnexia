using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Queries.List;

/// <summary>
/// Returns all non-deleted content blocks for a lesson.
/// Admin reads: all blocks (incl. inactive), ordered by SequenceOrder.
/// Student reads: only active (IsActive=true) blocks, ordered by SequenceOrder.
/// The <c>ForAdmin</c> flag distinguishes the two (controller sets it based on route/policy).
/// </summary>
public record ListContentBlocksQuery : IQuery<BaseResponse<List<AdminContentBlockDto>>>
{
    public int LessonId { get; init; }
}

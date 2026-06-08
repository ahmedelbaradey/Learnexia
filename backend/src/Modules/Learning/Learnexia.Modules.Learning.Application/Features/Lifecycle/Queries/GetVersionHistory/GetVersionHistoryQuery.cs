using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetVersionHistory;

/// <summary>
/// Returns the version history for a single curriculum entity, ordered newest-first.
/// AdminOnly — all snapshots including CorrectAnswer for QuizQuestion are visible to admins.
/// </summary>
public record GetVersionHistoryQuery : IQuery<BaseResponse<List<ContentVersionDto>>>
{
    public VersionedEntityType EntityType { get; init; }
    public int EntityId { get; init; }
}

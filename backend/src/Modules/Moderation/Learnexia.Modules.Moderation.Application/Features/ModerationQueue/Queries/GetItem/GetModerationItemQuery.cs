using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Queries.GetItem;

/// <summary>
/// Returns the full detail of a single moderation queue item by its id (AdminOnly).
/// Queries are NOT auto-validated (ValidationBehavior runs for ICommand only).
/// </summary>
public record GetModerationItemQuery(int Id)
    : IQuery<BaseResponse<ModerationItemDetailDto>>;

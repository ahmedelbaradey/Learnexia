using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetPreview;

/// <summary>
/// Returns the current state of a curriculum entity (Draft/Published/Archived) as a preview,
/// WITHOUT publishing it. The entity is returned in its current live state regardless of
/// LifecycleState — admins can preview Draft content before publishing.
///
/// AdminOnly — enforced at the controller layer.
/// The entity is served from the live DB row (not from a historical ContentVersion snapshot).
/// </summary>
public record GetPreviewQuery : IQuery<BaseResponse<PreviewDto>>
{
    public VersionedEntityType EntityType { get; init; }
    public int EntityId { get; init; }
}

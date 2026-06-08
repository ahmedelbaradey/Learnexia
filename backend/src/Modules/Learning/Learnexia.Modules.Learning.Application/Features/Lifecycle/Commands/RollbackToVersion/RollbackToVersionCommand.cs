using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.RollbackToVersion;

/// <summary>
/// Rolls back a curriculum entity to a previously published <see cref="Domain.Entities.ContentVersion"/> snapshot.
///
/// P7-05: Deserializes the snapshot of the requested version and applies the stored fields
/// back onto the live entity. After rollback the entity is re-published (LifecycleState = Published)
/// and a NEW ContentVersion row is created to record the rollback event (append-only history).
///
/// The UnitOfWorkBehavior wraps all staged writes in a single transaction.
/// AdminOnly — enforced at the controller layer.
/// </summary>
public record RollbackToVersionCommand : ICommand<BaseResponse<string>>
{
    /// <summary>Which curriculum entity type to roll back.</summary>
    public VersionedEntityType EntityType { get; init; }

    /// <summary>Primary key of the entity to roll back.</summary>
    public int EntityId { get; init; }

    /// <summary>The version number to restore (must exist in ContentVersions).</summary>
    public int VersionNumber { get; init; }
}

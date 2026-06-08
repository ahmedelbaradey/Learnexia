using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.TransitionLifecycle;

/// <summary>
/// Transitions a curriculum entity's <see cref="LifecycleState"/> to the requested <see cref="TargetState"/>.
///
/// P7-05: Handles Publish (Draft → Published), Archive (Published → Archived),
/// and Unpublish (Published → Draft) for Subject, Unit, Lesson, and QuizQuestion.
///
/// On transition to Published:
/// - A new <c>ContentVersion</c> snapshot row is created (VersionNumber = max + 1).
/// - <c>PublishedByUserId</c> is resolved from the JWT (never from the request body).
/// - <c>Language</c> is resolved via the owning Subject chain.
///
/// Illegal transitions (e.g. Archived → Published directly) return Successed=false.
/// AdminOnly — enforced at the controller layer.
///
/// Derives from DTO + ICommand per project rule (no separate DTO class needed here since
/// the inputs are primitive: EntityType, EntityId, TargetState).
/// </summary>
public record TransitionLifecycleCommand : ICommand<BaseResponse<string>>
{
    /// <summary>Which curriculum entity type to transition.</summary>
    public VersionedEntityType EntityType { get; init; }

    /// <summary>Primary key of the entity to transition.</summary>
    public int EntityId { get; init; }

    /// <summary>The desired target lifecycle state.</summary>
    public LifecycleState TargetState { get; init; }
}

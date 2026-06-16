using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Entities;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;
// Disambiguate Learning.Domain.Entities.Unit from MediatR.Unit
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.TransitionLifecycle;

/// <summary>
/// Handles <see cref="TransitionLifecycleCommand"/>.
///
/// Transitions a curriculum entity's <see cref="LifecycleState"/>. Legal transitions:
///   Draft      → Published  (creates a new <see cref="ContentVersion"/> snapshot)
///   Draft      → Archived   (ILLEGAL — must publish first)
///   Published  → Archived
///   Published  → Draft      (unpublish / re-open for editing)
///   Archived   → Draft      (restore for re-editing)
///   Archived   → Published  (ILLEGAL — must go through Draft first)
///
/// On transition to Published:
/// - Loads the entity with trackChanges=true.
/// - Serializes it to JSON as the snapshot (via ILifecycleService.SerializeSnapshot).
/// - Resolves the owning Subject language via ILifecycleService.GetOwningSubjectAsync.
/// - Creates a new <see cref="ContentVersion"/> row (VersionNumber = maxExisting + 1).
///
/// The UnitOfWorkBehavior wraps all staged writes in a single transaction.
/// No manual BeginTransaction is needed — the UoW behavior owns the transaction boundary
/// (per ADR 0001 §2; the UoW already opens a transaction and calls SaveChangesAsync once).
///
/// P7-12: Domain event raised on the tracked aggregate (post each switch branch) — dispatched
/// post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// P7-05 security notes:
/// - PublishedByUserId is read from JWT (_currentUser.UserId) — NEVER from the request body.
/// - No client-supplied Snapshot — snapshot is server-generated from the live entity.
///
/// Option C: all EF access delegated to ILifecycleService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class TransitionLifecycleCommandHandler
    : BaseResponseHandler, ICommandHandler<TransitionLifecycleCommand, BaseResponse<string>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public TransitionLifecycleCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<string>> Handle(
        TransitionLifecycleCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── Load entity and current state via switch on EntityType ──────────────────
            string entityTypeName;
            AggregateRoot trackedAggregate;

            switch (request.EntityType)
            {
                case VersionedEntityType.Subject:
                {
                    var entity = await _service.LifecycleService
                        .GetTrackedSubjectIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _service.LifecycleService.StageEntityUpdateAsync(entity, cancellationToken);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(Subject);
                    trackedAggregate = entity;
                    break;
                }

                case VersionedEntityType.Unit:
                {
                    var entity = await _service.LifecycleService
                        .GetTrackedUnitIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _service.LifecycleService.StageEntityUpdateAsync(entity, cancellationToken);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(LearningUnit);
                    trackedAggregate = entity;
                    break;
                }

                case VersionedEntityType.Lesson:
                {
                    var entity = await _service.LifecycleService
                        .GetTrackedLessonIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _service.LifecycleService.StageEntityUpdateAsync(entity, cancellationToken);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(Lesson);
                    trackedAggregate = entity;
                    break;
                }

                case VersionedEntityType.QuizQuestion:
                {
                    var entity = await _service.LifecycleService
                        .GetTrackedQuizQuestionIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _service.LifecycleService.StageEntityUpdateAsync(entity, cancellationToken);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(QuizQuestion);
                    trackedAggregate = entity;
                    break;
                }

                default:
                    return BadRequest<string>(_localizer[SharedResourcesKey.InvalidVersionedEntityType]);
            }

            // ── Raise domain event on tracked aggregate — dispatched post-commit (ADR 0002 / P7-12) ──
            var action = request.TargetState switch
            {
                LifecycleState.Published => AdminActions.ContentPublished,
                LifecycleState.Archived  => AdminActions.ContentArchived,
                LifecycleState.Draft     => AdminActions.ContentUnpublished,
                _                        => AdminActions.ContentPublished
            };

            trackedAggregate.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: action,
                TargetEntityType: entityTypeName,
                TargetEntityId: request.EntityId,
                Details: $"EntityType={request.EntityType} TargetState={request.TargetState}"));

            var successMessage = request.TargetState switch
            {
                LifecycleState.Published => _localizer[SharedResourcesKey.ContentPublishedSuccessfully],
                LifecycleState.Archived  => _localizer[SharedResourcesKey.ContentArchivedSuccessfully],
                LifecycleState.Draft     => _localizer[SharedResourcesKey.ContentUnpublishedSuccessfully],
                _                        => _localizer[SharedResourcesKey.OperationCompletedSuccessfully]
            };

            return Success<string>(successMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in TransitionLifecycleCommand for EntityType={request.EntityType} EntityId={request.EntityId}");
            return ServerError<string>();
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a failure response if the transition is illegal; null when allowed.
    ///
    /// Allowed transitions:
    ///   Draft      → Published  (publish)
    ///   Draft      → Archived   ILLEGAL — must publish first
    ///   Published  → Archived   (archive)
    ///   Published  → Draft      (unpublish / re-open)
    ///   Archived   → Draft      (restore for re-editing)
    ///   Archived   → Published  ILLEGAL — must go through Draft first
    ///   X          → X          no-op, treat as success
    /// </summary>
    private BaseResponse<string>? ValidateTransition(LifecycleState current, LifecycleState target)
    {
        if (current == target)
            return null; // idempotent no-op; success

        var allowed = (current, target) switch
        {
            (LifecycleState.Draft,     LifecycleState.Published) => true,
            (LifecycleState.Published, LifecycleState.Archived)  => true,
            (LifecycleState.Published, LifecycleState.Draft)     => true,
            (LifecycleState.Archived,  LifecycleState.Draft)     => true,
            _                                                      => false,
        };

        return allowed ? null : BadRequest<string>(_localizer[SharedResourcesKey.IllegalLifecycleTransition]);
    }

    /// <summary>
    /// Creates a <see cref="ContentVersion"/> snapshot for the given entity at publish time.
    /// Resolves the owning Subject language for the Language tag.
    /// Assigns VersionNumber = maxExisting + 1.
    /// PublishedByUserId is stamped from the JWT.
    ///
    /// Security #4: snapshot serializes ONLY the editorial (whitelist) fields — via
    /// ILifecycleService.SerializeSnapshot (same whitelist as ApplyXxxSnapshot in the rollback handler).
    /// </summary>
    private async Task CreateSnapshotAsync(
        VersionedEntityType entityType,
        int entityId,
        object entity,
        CancellationToken cancellationToken)
    {
        var maxVersion = await _service.LifecycleService
            .GetMaxVersionNumberAsync(entityType, entityId, cancellationToken);

        var owningSubject = await _service.LifecycleService
            .GetOwningSubjectAsync(entityType, entityId, cancellationToken);

        var language = owningSubject?.Language ?? ContentLanguage.Ar;

        var snapshot = _service.LifecycleService.SerializeSnapshot(entityType, entity);

        var version = new ContentVersion
        {
            EntityType        = entityType,
            EntityId          = entityId,
            VersionNumber     = maxVersion + 1,
            Snapshot          = snapshot,
            PublishedByUserId = _currentUser.UserId.GetValueOrDefault(),
            PublishedAtUtc    = DateTime.UtcNow,
            Language          = language,
        };

        await _service.LifecycleService.StageAddContentVersionAsync(version, cancellationToken);
    }
}

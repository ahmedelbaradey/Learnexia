using System.Text.Json;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Entities;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
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
/// - Serializes it to JSON as the snapshot.
/// - Resolves the owning Subject language via <see cref="ILearningRepository.GetOwningSubjectAsync"/>.
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
/// </summary>
public class TransitionLifecycleCommandHandler
    : BaseResponseHandler, ICommandHandler<TransitionLifecycleCommand, BaseResponse<string>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public TransitionLifecycleCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
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
                    var entity = await _repository.Learning
                        .GetByCondition<Subject>(s => s.Id == request.EntityId, trackChanges: true)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _repository.Learning.UpdateAsync(entity);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(Subject);
                    trackedAggregate = entity;
                    break;
                }

                case VersionedEntityType.Unit:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<LearningUnit>(u => u.Id == request.EntityId, trackChanges: true)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _repository.Learning.UpdateAsync(entity);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(LearningUnit);
                    trackedAggregate = entity;
                    break;
                }

                case VersionedEntityType.Lesson:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<Lesson>(l => l.Id == request.EntityId, trackChanges: true)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _repository.Learning.UpdateAsync(entity);

                    if (request.TargetState == LifecycleState.Published)
                        await CreateSnapshotAsync(request.EntityType, entity.Id, entity, cancellationToken);

                    entityTypeName   = nameof(Lesson);
                    trackedAggregate = entity;
                    break;
                }

                case VersionedEntityType.QuizQuestion:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<QuizQuestion>(q => q.Id == request.EntityId, trackChanges: true)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be lifecycle-transitioned.
                    if (entity.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    var validation = ValidateTransition(entity.LifecycleState, request.TargetState);
                    if (validation is not null) return validation;

                    entity.LifecycleState = request.TargetState;
                    await _repository.Learning.UpdateAsync(entity);

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
    /// Security #4: snapshot serializes ONLY the editorial (whitelist) fields that the
    /// corresponding <c>ApplyXxxSnapshot</c> rollback method restores — not the full
    /// EF-tracked entity (which would cause circular-reference JsonException via navigations,
    /// and produce bloated/nondeterministic snapshots). Snapshot ↔ rollback are symmetric:
    /// the same field names are used by <see cref="RollbackToVersionCommandHandler"/>'s
    /// <c>ApplyXxxSnapshot</c> helpers to deserialise and reapply state.
    /// </summary>
    private async Task CreateSnapshotAsync(
        VersionedEntityType entityType,
        int entityId,
        object entity,
        CancellationToken cancellationToken)
    {
        var maxVersion = await _repository.Learning
            .GetMaxVersionNumberAsync(entityType, entityId, cancellationToken);

        var owningSubject = await _repository.Learning
            .GetOwningSubjectAsync(entityType, entityId, cancellationToken);

        var language = owningSubject?.Language ?? ContentLanguage.Ar;

        // Serialize a whitelist-DTO of only the editorial fields — symmetric with the
        // ApplyXxxSnapshot rollback helpers in RollbackToVersionCommandHandler.
        var snapshot = entityType switch
        {
            VersionedEntityType.Subject when entity is Subject s => JsonSerializer.Serialize(new
            {
                s.Name,
                s.Country,
                SubjectCode    = (int)s.SubjectCode,
                Language       = (int)s.Language,
                s.SequenceOrder,
                s.IsActive,
            }),
            VersionedEntityType.Unit when entity is LearningUnit u => JsonSerializer.Serialize(new
            {
                u.Name,
                u.SequenceOrder,
                u.IsActive,
            }),
            VersionedEntityType.Lesson when entity is Lesson l => JsonSerializer.Serialize(new
            {
                l.Name,
                Difficulty      = (int)l.Difficulty,
                l.SequenceOrder,
                l.IsLocked,
                l.IsActive,
                l.EstimatedMinutes,
                l.Explanation,
                l.Visual,
                l.IsBoss,
            }),
            VersionedEntityType.QuizQuestion when entity is QuizQuestion q => JsonSerializer.Serialize(new
            {
                q.QuestionText,
                QuestionType = (int)q.QuestionType,
                q.Options,
                q.CorrectAnswer,
                Difficulty   = (int)q.Difficulty,
                GeneratedBy  = (int)q.GeneratedBy,
                q.SequenceOrder,
                q.IsActive,
            }),
            _ => throw new InvalidOperationException(
                $"No snapshot whitelist defined for EntityType={entityType}"),
        };

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

        await _repository.Learning.AddAsync(version, cancellationToken);
    }
}

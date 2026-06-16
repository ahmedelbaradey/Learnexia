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
using Microsoft.Extensions.Localization;
using Resources;
// Disambiguate Learning.Domain.Entities.Unit from MediatR.Unit
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.RollbackToVersion;

/// <summary>
/// Handles <see cref="RollbackToVersionCommand"/>.
///
/// Loads the requested <see cref="ContentVersion"/> snapshot, deserializes its fields,
/// applies them back to the live entity, and re-publishes a new <see cref="ContentVersion"/>
/// row to record the rollback (append-only history). The entity is set to
/// <see cref="LifecycleState.Published"/> after rollback.
///
/// Field application strategy: the snapshot JSON is deserialized into an anonymous holder
/// using <see cref="JsonDocument"/>, and only the content/editorial fields are applied —
/// audit fields (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, DeletedAt, etc.) are NOT
/// restored from the snapshot, ensuring the rollback itself appears as a new audit event.
///
/// The UnitOfWorkBehavior wraps all staged writes in a single transaction.
/// AdminOnly — enforced at the controller layer.
///
/// P7-12: Domain event raised on the tracked aggregate (post switch) — dispatched
/// post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// P7-05 security notes:
/// - PublishedByUserId on the new ContentVersion is from JWT — never from the snapshot.
/// - Snapshot is read server-side from ContentVersions — never from the client request.
///
/// Option C: all EF access delegated to ILifecycleService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class RollbackToVersionCommandHandler
    : BaseResponseHandler, ICommandHandler<RollbackToVersionCommand, BaseResponse<string>>
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public RollbackToVersionCommandHandler(
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
        RollbackToVersionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the requested ContentVersion snapshot.
            var versionRow = await _service.LifecycleService
                .GetVersionAsync(request.EntityType, request.EntityId, request.VersionNumber, cancellationToken);

            if (versionRow is null)
                return NotFound<string>(_localizer[SharedResourcesKey.ContentVersionNotFound]);

            string entityTypeName;
            AggregateRoot trackedAggregate;

            // Apply snapshot fields to the live entity via switch on EntityType.
            switch (request.EntityType)
            {
                case VersionedEntityType.Subject:
                {
                    var live = await _service.LifecycleService
                        .GetTrackedSubjectIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (live is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be rolled back.
                    if (live.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    ApplySubjectSnapshot(live, versionRow.Snapshot);
                    live.LifecycleState = LifecycleState.Published;
                    await _service.LifecycleService.StageEntityUpdateAsync(live, cancellationToken);
                    entityTypeName   = nameof(Subject);
                    trackedAggregate = live;
                    break;
                }

                case VersionedEntityType.Unit:
                {
                    var live = await _service.LifecycleService
                        .GetTrackedUnitIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (live is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be rolled back.
                    if (live.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    ApplyUnitSnapshot(live, versionRow.Snapshot);
                    live.LifecycleState = LifecycleState.Published;
                    await _service.LifecycleService.StageEntityUpdateAsync(live, cancellationToken);
                    entityTypeName   = nameof(LearningUnit);
                    trackedAggregate = live;
                    break;
                }

                case VersionedEntityType.Lesson:
                {
                    var live = await _service.LifecycleService
                        .GetTrackedLessonIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (live is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be rolled back.
                    if (live.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    ApplyLessonSnapshot(live, versionRow.Snapshot);
                    live.LifecycleState = LifecycleState.Published;
                    await _service.LifecycleService.StageEntityUpdateAsync(live, cancellationToken);
                    entityTypeName   = nameof(Lesson);
                    trackedAggregate = live;
                    break;
                }

                case VersionedEntityType.QuizQuestion:
                {
                    var live = await _service.LifecycleService
                        .GetTrackedQuizQuestionIgnoreFiltersAsync(request.EntityId, cancellationToken);

                    if (live is null)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    // Security #3: soft-deleted entities must not be rolled back.
                    if (live.IsDeleted == true)
                        return NotFound<string>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    ApplyQuizQuestionSnapshot(live, versionRow.Snapshot);
                    live.LifecycleState = LifecycleState.Published;
                    await _service.LifecycleService.StageEntityUpdateAsync(live, cancellationToken);
                    entityTypeName   = nameof(QuizQuestion);
                    trackedAggregate = live;
                    break;
                }

                default:
                    return BadRequest<string>(_localizer[SharedResourcesKey.InvalidVersionedEntityType]);
            }

            // Create a new ContentVersion row to record the rollback (append-only history).
            var maxVersion = await _service.LifecycleService
                .GetMaxVersionNumberAsync(request.EntityType, request.EntityId, cancellationToken);

            var owningSubject = await _service.LifecycleService
                .GetOwningSubjectAsync(request.EntityType, request.EntityId, cancellationToken);

            var rollbackVersion = new ContentVersion
            {
                EntityType        = request.EntityType,
                EntityId          = request.EntityId,
                VersionNumber     = maxVersion + 1,
                Snapshot          = versionRow.Snapshot,  // re-use the rolled-back snapshot content
                PublishedByUserId = _currentUser.UserId.GetValueOrDefault(),
                PublishedAtUtc    = DateTime.UtcNow,
                Language          = owningSubject?.Language ?? ContentLanguage.Ar,
            };

            await _service.LifecycleService.StageAddContentVersionAsync(rollbackVersion, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            trackedAggregate.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.ContentRolledBack,
                TargetEntityType: entityTypeName,
                TargetEntityId: request.EntityId,
                Details: $"RolledBackToVersion={request.VersionNumber}"));

            return Success<string>(_localizer[SharedResourcesKey.ContentRolledBackSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in RollbackToVersionCommand for EntityType={request.EntityType} EntityId={request.EntityId}");
            return ServerError<string>();
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────────
    // Snapshot application helpers (editorial fields only — audit fields not restored)
    // ────────────────────────────────────────────────────────────────────────────────────

    private static void ApplySubjectSnapshot(Subject entity, string snapshot)
    {
        using var doc = JsonDocument.Parse(snapshot);
        var root = doc.RootElement;

        if (root.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String)
            entity.Name = name.GetString()!;

        if (root.TryGetProperty("Country", out var country))
            entity.Country = country.ValueKind == JsonValueKind.Null ? null : country.GetString();

        if (root.TryGetProperty("SubjectCode", out var code) && code.ValueKind == JsonValueKind.Number)
            entity.SubjectCode = (SubjectCode)code.GetInt32();

        if (root.TryGetProperty("Language", out var lang) && lang.ValueKind == JsonValueKind.Number)
            entity.Language = (ContentLanguage)lang.GetInt32();

        if (root.TryGetProperty("SequenceOrder", out var seq) && seq.ValueKind == JsonValueKind.Number)
            entity.SequenceOrder = seq.GetInt32();

        if (root.TryGetProperty("IsActive", out var isActive) && isActive.ValueKind != JsonValueKind.Null)
            entity.IsActive = isActive.GetBoolean();
    }

    private static void ApplyUnitSnapshot(LearningUnit entity, string snapshot)
    {
        using var doc = JsonDocument.Parse(snapshot);
        var root = doc.RootElement;

        if (root.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String)
            entity.Name = name.GetString()!;

        if (root.TryGetProperty("SequenceOrder", out var seq) && seq.ValueKind == JsonValueKind.Number)
            entity.SequenceOrder = seq.GetInt32();

        if (root.TryGetProperty("IsActive", out var isActive) && isActive.ValueKind != JsonValueKind.Null)
            entity.IsActive = isActive.GetBoolean();
    }

    private static void ApplyLessonSnapshot(Lesson entity, string snapshot)
    {
        using var doc = JsonDocument.Parse(snapshot);
        var root = doc.RootElement;

        if (root.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String)
            entity.Name = name.GetString()!;

        if (root.TryGetProperty("Difficulty", out var diff) && diff.ValueKind == JsonValueKind.Number)
            entity.Difficulty = (DifficultyLevel)diff.GetInt32();

        if (root.TryGetProperty("SequenceOrder", out var seq) && seq.ValueKind == JsonValueKind.Number)
            entity.SequenceOrder = seq.GetInt32();

        if (root.TryGetProperty("IsLocked", out var isLocked) && isLocked.ValueKind != JsonValueKind.Null)
            entity.IsLocked = isLocked.GetBoolean();

        if (root.TryGetProperty("IsActive", out var isActive) && isActive.ValueKind != JsonValueKind.Null)
            entity.IsActive = isActive.GetBoolean();

        if (root.TryGetProperty("EstimatedMinutes", out var est) && est.ValueKind == JsonValueKind.Number)
            entity.EstimatedMinutes = est.GetInt32();

        if (root.TryGetProperty("Explanation", out var expl))
            entity.Explanation = expl.ValueKind == JsonValueKind.Null ? null : expl.GetString();

        if (root.TryGetProperty("Visual", out var visual))
            entity.Visual = visual.ValueKind == JsonValueKind.Null ? null : visual.GetString();

        if (root.TryGetProperty("IsBoss", out var isBoss) && isBoss.ValueKind != JsonValueKind.Null)
            entity.IsBoss = isBoss.GetBoolean();
    }

    private static void ApplyQuizQuestionSnapshot(QuizQuestion entity, string snapshot)
    {
        using var doc = JsonDocument.Parse(snapshot);
        var root = doc.RootElement;

        if (root.TryGetProperty("QuestionText", out var text) && text.ValueKind == JsonValueKind.String)
            entity.QuestionText = text.GetString()!;

        if (root.TryGetProperty("QuestionType", out var qtype) && qtype.ValueKind == JsonValueKind.Number)
            entity.QuestionType = (QuestionType)qtype.GetInt32();

        if (root.TryGetProperty("Options", out var opts) && opts.ValueKind == JsonValueKind.String)
            entity.Options = opts.GetString()!;

        if (root.TryGetProperty("CorrectAnswer", out var ca) && ca.ValueKind == JsonValueKind.String)
            entity.CorrectAnswer = ca.GetString()!;

        if (root.TryGetProperty("Difficulty", out var diff) && diff.ValueKind == JsonValueKind.Number)
            entity.Difficulty = (DifficultyLevel)diff.GetInt32();

        if (root.TryGetProperty("GeneratedBy", out var gen) && gen.ValueKind == JsonValueKind.Number)
            entity.GeneratedBy = (GeneratedBy)gen.GetInt32();

        if (root.TryGetProperty("SequenceOrder", out var seq) && seq.ValueKind == JsonValueKind.Number)
            entity.SequenceOrder = seq.GetInt32();

        if (root.TryGetProperty("IsActive", out var isActive) && isActive.ValueKind != JsonValueKind.Null)
            entity.IsActive = isActive.GetBoolean();
    }
}

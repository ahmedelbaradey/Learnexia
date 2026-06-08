using System.Text.Json;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;
// Disambiguate Learning.Domain.Entities.Unit from MediatR.Unit (indirect via Shared.Kernel.Messaging)
using LearningUnit = Learnexia.Modules.Learning.Domain.Entities.Unit;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetPreview;

/// <summary>
/// Handles <see cref="GetPreviewQuery"/>.
///
/// Loads the live entity ignoring the LifecycleState filter (admins see Draft/Published/Archived).
/// Serializes the entity to JSON as the preview snapshot. Resolves the owning Subject's language.
/// AdminOnly — enforced at the controller layer.
///
/// SECURITY: IgnoreQueryFilters() is intentional here — admins must see Draft content.
/// This handler MUST NOT be reachable by student roles (enforced by [Authorize(AdminOnly)] on the endpoint).
/// </summary>
public class GetPreviewQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPreviewQuery, BaseResponse<PreviewDto>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetPreviewQueryHandler(
        ILearningRepositoryManager repository,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<PreviewDto>> Handle(
        GetPreviewQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.EntityId <= 0)
                return BadRequest<PreviewDto>(_localizer[SharedResourcesKey.EntityIdRequired]);

            LifecycleState currentState;
            string snapshot;

            // Load the entity ignoring ALL query filters (soft-delete + global filters).
            // IgnoreQueryFilters is the correct call here: the global filter excludes IsDeleted rows,
            // and there is no separate LifecycleState global filter (LifecycleState filtering is
            // applied per-query in student reads). However we still want to see archived/draft rows.
            switch (request.EntityType)
            {
                case VersionedEntityType.Subject:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<Subject>(s => s.Id == request.EntityId, trackChanges: false)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    currentState = entity.LifecycleState;
                    snapshot = JsonSerializer.Serialize(entity);
                    break;
                }

                case VersionedEntityType.Unit:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<LearningUnit>(u => u.Id == request.EntityId, trackChanges: false)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    currentState = entity.LifecycleState;
                    snapshot = JsonSerializer.Serialize(entity);
                    break;
                }

                case VersionedEntityType.Lesson:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<Lesson>(l => l.Id == request.EntityId, trackChanges: false)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    currentState = entity.LifecycleState;
                    snapshot = JsonSerializer.Serialize(entity);
                    break;
                }

                case VersionedEntityType.QuizQuestion:
                {
                    var entity = await _repository.Learning
                        .GetByCondition<QuizQuestion>(q => q.Id == request.EntityId, trackChanges: false)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (entity is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);

                    currentState = entity.LifecycleState;
                    snapshot = JsonSerializer.Serialize(entity);
                    break;
                }

                default:
                    return BadRequest<PreviewDto>(_localizer[SharedResourcesKey.InvalidVersionedEntityType]);
            }

            // Resolve the owning Subject's language for traceability.
            var owningSubject = await _repository.Learning
                .GetOwningSubjectAsync(request.EntityType, request.EntityId, cancellationToken);

            var dto = new PreviewDto
            {
                EntityType    = request.EntityType,
                EntityId      = request.EntityId,
                CurrentState  = currentState,
                Language      = owningSubject?.Language ?? ContentLanguage.Ar,
                Snapshot      = snapshot,
            };

            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.PreviewRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in GetPreviewQuery for EntityType={request.EntityType} EntityId={request.EntityId}");
            return ServerError<PreviewDto>();
        }
    }
}

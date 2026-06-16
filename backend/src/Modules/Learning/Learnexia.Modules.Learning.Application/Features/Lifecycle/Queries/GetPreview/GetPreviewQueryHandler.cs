using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

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
///
/// Option C: all EF access delegated to ILifecycleService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class GetPreviewQueryHandler
    : BaseResponseHandler, IQueryHandler<GetPreviewQuery, BaseResponse<PreviewDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetPreviewQueryHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
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
            switch (request.EntityType)
            {
                case VersionedEntityType.Subject:
                {
                    var result = await _service.LifecycleService
                        .GetPreviewSubjectAsync(request.EntityId, cancellationToken);
                    if (result is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);
                    (currentState, snapshot) = result.Value;
                    break;
                }

                case VersionedEntityType.Unit:
                {
                    var result = await _service.LifecycleService
                        .GetPreviewUnitAsync(request.EntityId, cancellationToken);
                    if (result is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);
                    (currentState, snapshot) = result.Value;
                    break;
                }

                case VersionedEntityType.Lesson:
                {
                    var result = await _service.LifecycleService
                        .GetPreviewLessonAsync(request.EntityId, cancellationToken);
                    if (result is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);
                    (currentState, snapshot) = result.Value;
                    break;
                }

                case VersionedEntityType.QuizQuestion:
                {
                    var result = await _service.LifecycleService
                        .GetPreviewQuizQuestionAsync(request.EntityId, cancellationToken);
                    if (result is null)
                        return NotFound<PreviewDto>(_localizer[SharedResourcesKey.VersionedEntityNotFound]);
                    (currentState, snapshot) = result.Value;
                    break;
                }

                default:
                    return BadRequest<PreviewDto>(_localizer[SharedResourcesKey.InvalidVersionedEntityType]);
            }

            // Resolve the owning Subject's language for traceability.
            var owningSubject = await _service.LifecycleService
                .GetOwningSubjectAsync(request.EntityType, request.EntityId, cancellationToken);

            var dto = new PreviewDto
            {
                EntityType   = request.EntityType,
                EntityId     = request.EntityId,
                CurrentState = currentState,
                Language     = owningSubject?.Language ?? ContentLanguage.Ar,
                Snapshot     = snapshot,
            };

            var result2 = Success(dto);
            result2.Message = _localizer[SharedResourcesKey.PreviewRetrievedSuccessfully];
            return result2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in GetPreviewQuery for EntityType={request.EntityType} EntityId={request.EntityId}");
            return ServerError<PreviewDto>();
        }
    }
}

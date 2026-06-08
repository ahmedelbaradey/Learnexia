using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetVersionHistory;

/// <summary>
/// Handles <see cref="GetVersionHistoryQuery"/>.
/// Returns ContentVersion rows for (EntityType, EntityId), newest first.
/// AdminOnly — enforced at the controller layer.
/// </summary>
public class GetVersionHistoryQueryHandler
    : BaseResponseHandler, IQueryHandler<GetVersionHistoryQuery, BaseResponse<List<ContentVersionDto>>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetVersionHistoryQueryHandler(
        ILearningRepositoryManager repository,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<ContentVersionDto>>> Handle(
        GetVersionHistoryQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.EntityId <= 0)
                return BadRequest<List<ContentVersionDto>>(_localizer[SharedResourcesKey.EntityIdRequired]);

            var versions = await _repository.Learning
                .GetVersionHistoryAsync(request.EntityType, request.EntityId, cancellationToken);

            var dtos = versions.Select(v => new ContentVersionDto
            {
                Id                = v.Id,
                EntityType        = v.EntityType,
                EntityId          = v.EntityId,
                VersionNumber     = v.VersionNumber,
                PublishedAtUtc    = v.PublishedAtUtc.ToUniversalTime(),  // HANDOFF P4-11
                PublishedByUserId = v.PublishedByUserId,
                Language          = v.Language,
                Snapshot          = v.Snapshot,
            }).ToList();

            var result = Success(dtos);
            result.Message = _localizer[SharedResourcesKey.VersionHistoryRetrievedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error: in GetVersionHistoryQuery for EntityType={request.EntityType} EntityId={request.EntityId}");
            return ServerError<List<ContentVersionDto>>();
        }
    }
}

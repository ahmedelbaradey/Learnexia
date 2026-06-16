using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Reorder;

/// <summary>
/// Batch-updates SequenceOrder on the given ContentBlocks.
/// All blocks must belong to the same LessonId — cross-lesson reorder is rejected.
///
/// P7-12: ContentBlock is FullAuditedEntity (not AggregateRoot) — domain event raised on the
/// parent Lesson aggregate (representative for the batch).
/// Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Option-C: all EF calls moved into IContentBlockService (Infrastructure). Handler is now thin.
/// </summary>
public class ReorderContentBlocksCommandHandler
    : BaseResponseHandler, ICommandHandler<ReorderContentBlocksCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderContentBlocksCommandHandler(
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
        ReorderContentBlocksCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.ContentBlockIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Load all specified blocks with tracking.
            var blocks = await _service.ContentBlockService.GetContentBlocksTrackedByIdsAsync(request.ContentBlockIds, cancellationToken);

            if (blocks.Count != request.ContentBlockIds.Count)
                return NotFound<string>(_localizer[SharedResourcesKey.ContentBlockNotFound]);

            // Validate all belong to the same LessonId — cross-lesson reorder is forbidden.
            var lessonIds = blocks.Select(cb => cb.LessonId).Distinct().ToList();
            if (lessonIds.Count > 1)
                return BadRequest<string>(_localizer[SharedResourcesKey.ContentBlockReorderCrossLessonForbidden]);

            // Apply new SequenceOrder based on position in the requested list.
            var blocksById = blocks.ToDictionary(cb => cb.Id);
            for (var i = 0; i < request.ContentBlockIds.Count; i++)
            {
                blocksById[request.ContentBlockIds[i]].SequenceOrder = i;
                await _service.ContentBlockService.StageContentBlockUpdateAsync(blocksById[request.ContentBlockIds[i]], cancellationToken);
            }

            // ContentBlock is FullAuditedEntity (not AggregateRoot) — raise on the parent Lesson aggregate.
            var lesson = await _service.ContentBlockService.GetLessonTrackedAsync(lessonIds[0], cancellationToken);

            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.ContentBlockReordered,
                TargetEntityType: nameof(ContentBlock),
                TargetEntityId: 0,
                Details: $"Reordered {request.ContentBlockIds.Count} blocks in LessonId={lessonIds[0]}"));

            return Success<string>(_localizer[SharedResourcesKey.ContentBlockReorderedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderContentBlocksCommand");
            return ServerError<string>();
        }
    }
}

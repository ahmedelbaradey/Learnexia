using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Add;

/// <summary>
/// Appends a new <see cref="ContentBlock"/> to the lesson identified by <c>LessonId</c>.
/// SequenceOrder is set to max(existing) + 1 so the block lands at the end.
/// IsActive defaults to true (entity default — not mapped from command input).
///
/// P7-12: ContentBlock derives from FullAuditedEntity (not AggregateRoot) so cannot raise domain
/// events directly. The domain event is raised on the parent Lesson aggregate instead.
///
/// Option-C: all EF calls moved into IContentBlockService (Infrastructure). Handler is now thin.
/// </summary>
public class AddContentBlockCommandHandler
    : BaseResponseHandler, ICommandHandler<AddContentBlockCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddContentBlockCommandHandler(
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
        AddContentBlockCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Verify the lesson exists (global IsDeleted filter active) and load it tracked so we
            // can raise the domain event on it (ContentBlock is FullAuditedEntity, not AggregateRoot).
            var lesson = await _service.ContentBlockService.GetLessonTrackedAsync(request.LessonId, cancellationToken);

            if (lesson is null)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Determine the next SequenceOrder (append semantics: max + 1, or 0 if no blocks yet).
            var maxOrder = await _service.ContentBlockService.GetMaxSequenceOrderAsync(request.LessonId, cancellationToken);
            var sequenceOrder = maxOrder + 1;

            var block = new ContentBlock
            {
                LessonId      = request.LessonId,
                BlockType     = request.BlockType,
                Payload       = request.Payload,
                SequenceOrder = sequenceOrder,
                IsActive      = true   // explicit default — mass-assignment guard
            };

            await _service.ContentBlockService.StageContentBlockAddAsync(block, cancellationToken);

            // ContentBlock is FullAuditedEntity (not AggregateRoot) — raise on the parent Lesson aggregate.
            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.ContentBlockAdded,
                TargetEntityType: nameof(ContentBlock),
                TargetEntityId: 0,
                Details: $"LessonId={request.LessonId}, BlockType={request.BlockType}"));

            return Success<string>(_localizer[SharedResourcesKey.ContentBlockAddedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddContentBlockCommand");
            return ServerError<string>();
        }
    }
}

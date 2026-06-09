using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
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
/// Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Media payloads: the caller provides the asset url/key in the Payload JSON.
/// IStorageService is NOT called here — the asset must have been uploaded via the dedicated
/// upload endpoint before the block is created. This keeps the command handler stateless
/// and the storage concern at the API surface (consistent with how Lesson.Visual works today).
/// </summary>
public class AddContentBlockCommandHandler
    : BaseResponseHandler, ICommandHandler<AddContentBlockCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddContentBlockCommandHandler(
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
        AddContentBlockCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Verify the lesson exists (global IsDeleted filter active) and load it tracked so we
            // can raise the domain event on it (ContentBlock is FullAuditedEntity, not AggregateRoot).
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.LessonId, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Determine the next SequenceOrder (append semantics: max + 1, or 0 if no blocks yet).
            var maxOrder = await _repository.Learning
                .GetByCondition<ContentBlock>(cb => cb.LessonId == request.LessonId, false)
                .Select(cb => (int?)cb.SequenceOrder)
                .MaxAsync(cancellationToken);

            var sequenceOrder = (maxOrder ?? -1) + 1;

            var block = new ContentBlock
            {
                LessonId      = request.LessonId,
                BlockType     = request.BlockType,
                Payload       = request.Payload,
                SequenceOrder = sequenceOrder,
                IsActive      = true   // explicit default — mass-assignment guard
            };

            await _repository.Learning.AddAsync(block, cancellationToken);

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

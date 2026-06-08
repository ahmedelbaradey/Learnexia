using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Add;

/// <summary>
/// Appends a new <see cref="ContentBlock"/> to the lesson identified by <c>LessonId</c>.
/// SequenceOrder is set to max(existing) + 1 so the block lands at the end.
/// IsActive defaults to true (entity default — not mapped from command input).
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
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
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddContentBlockCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _publisher = publisher;
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

            // Verify the lesson exists (global IsDeleted filter active).
            var lessonExists = await _repository.Learning
                .AnyAsync<Lesson>(l => l.Id == request.LessonId);

            if (!lessonExists)
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

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.ContentBlockAdded,
                    TargetEntityType: nameof(ContentBlock),
                    TargetEntityId: 0,
                    Details: $"LessonId={request.LessonId}, BlockType={request.BlockType}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, "P7-02: AdminActionPerformedEvent publish failed for AddContentBlockCommand");
            }

            return Success<string>(_localizer[SharedResourcesKey.ContentBlockAddedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddContentBlockCommand");
            return ServerError<string>();
        }
    }
}

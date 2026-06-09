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

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Delete;

/// <summary>
/// P7-02: Soft-deletes a Lesson by setting <c>IsDeleted = true</c>, and cascade-soft-deletes
/// all non-deleted ContentBlocks that belong to this lesson (blocks have no independent lifecycle).
///
/// Both the lesson and its blocks are staged in the same EF change-tracker snapshot;
/// the <c>UnitOfWorkBehavior</c> wraps the handler in a single transaction (opened before the
/// handler runs, committed after it returns). No explicit nested transaction is required —
/// the UoW transaction boundary covers everything staged here.
///
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class DeleteLessonCommandHandler : BaseResponseHandler, ICommandHandler<DeleteLessonCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteLessonCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(DeleteLessonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == request.Id, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (lesson is null)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Cascade soft-delete to all non-deleted ContentBlocks for this lesson.
            // ContentBlocks have no independent lifecycle — they are atomically removed with their lesson.
            // The global IsDeleted filter is active, so this query only returns non-deleted blocks.
            var contentBlocks = await _repository.Learning
                .GetByCondition<ContentBlock>(cb => cb.LessonId == request.Id, trackChanges: true)
                .ToListAsync(cancellationToken);

            foreach (var block in contentBlocks)
            {
                block.IsDeleted = true;
                await _repository.Learning.UpdateAsync(block);
            }

            // Soft-delete the lesson; UnitOfWorkBehavior stamps DeletedAt/DeletedBy on commit.
            lesson.IsDeleted = true;
            await _repository.Learning.UpdateAsync(lesson);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.LessonDeleted,
                TargetEntityType: nameof(Lesson),
                TargetEntityId: request.Id,
                Details: $"Cascade-soft-deleted {contentBlocks.Count} content blocks"));

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteLessonCommand");
            return ServerError<string>();
        }
    }
}

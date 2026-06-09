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

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Edit;

/// <summary>
/// Updates <c>BlockType</c> and <c>Payload</c> on an existing <see cref="ContentBlock"/>.
/// SequenceOrder, IsActive, and IsDeleted are NOT touched here — dedicated commands own those fields.
///
/// P7-12: ContentBlock is FullAuditedEntity (not AggregateRoot) — domain event raised on parent Lesson.
/// Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
/// </summary>
public class EditContentBlockCommandHandler
    : BaseResponseHandler, ICommandHandler<EditContentBlockCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditContentBlockCommandHandler(
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
        EditContentBlockCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var block = await _repository.Learning
                .GetByCondition<ContentBlock>(cb => cb.Id == request.Id, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (block is null)
                return NotFound<string>(_localizer[SharedResourcesKey.ContentBlockNotFound]);

            // Only update editable fields — do NOT touch SequenceOrder, IsActive, IsDeleted.
            block.BlockType = request.BlockType;
            block.Payload   = request.Payload;

            await _repository.Learning.UpdateAsync(block);

            // ContentBlock is FullAuditedEntity (not AggregateRoot) — raise on the parent Lesson aggregate.
            // The Lesson is in the ChangeTracker (or loaded fresh; EF identity map dedups).
            var lesson = await _repository.Learning
                .GetByCondition<Lesson>(l => l.Id == block.LessonId, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.ContentBlockUpdated,
                TargetEntityType: nameof(ContentBlock),
                TargetEntityId: request.Id,
                Details: $"BlockType={request.BlockType}"));

            return Success<string>(_localizer[SharedResourcesKey.ContentBlockUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditContentBlockCommand");
            return ServerError<string>();
        }
    }
}

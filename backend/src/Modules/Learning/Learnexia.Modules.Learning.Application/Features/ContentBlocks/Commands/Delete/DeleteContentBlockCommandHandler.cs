using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Delete;

/// <summary>
/// Soft-deletes a <see cref="ContentBlock"/> by setting <c>IsDeleted = true</c>.
/// The global query filter excludes deleted blocks from all subsequent reads.
///
/// P7-12: ContentBlock is FullAuditedEntity (not AggregateRoot) — domain event raised on parent Lesson.
/// Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Option-C: all EF calls moved into IContentBlockService (Infrastructure). Handler is now thin.
/// </summary>
public class DeleteContentBlockCommandHandler
    : BaseResponseHandler, ICommandHandler<DeleteContentBlockCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteContentBlockCommandHandler(
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
        DeleteContentBlockCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var block = await _service.ContentBlockService.GetContentBlockTrackedAsync(request.Id, cancellationToken);

            if (block is null)
                return NotFound<string>(_localizer[SharedResourcesKey.ContentBlockNotFound]);

            // Soft-delete: set the flag; UnitOfWorkBehavior stamps DeletedAt/DeletedBy.
            block.IsDeleted = true;
            await _service.ContentBlockService.StageContentBlockUpdateAsync(block, cancellationToken);

            // ContentBlock is FullAuditedEntity (not AggregateRoot) — raise on the parent Lesson aggregate.
            // EF identity map may have the Lesson already tracked; if not, load it now.
            var lesson = await _service.ContentBlockService.GetLessonTrackedAsync(block.LessonId, cancellationToken);

            // Dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            lesson?.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.ContentBlockDeleted,
                TargetEntityType: nameof(ContentBlock),
                TargetEntityId: request.Id,
                Details: null));

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteContentBlockCommand");
            return ServerError<string>();
        }
    }
}

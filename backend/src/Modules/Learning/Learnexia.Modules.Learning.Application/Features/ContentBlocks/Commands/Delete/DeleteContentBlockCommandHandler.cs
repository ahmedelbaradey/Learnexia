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

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Delete;

/// <summary>
/// Soft-deletes a <see cref="ContentBlock"/> by setting <c>IsDeleted = true</c>.
/// The global query filter excludes deleted blocks from all subsequent reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class DeleteContentBlockCommandHandler
    : BaseResponseHandler, ICommandHandler<DeleteContentBlockCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteContentBlockCommandHandler(
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
        DeleteContentBlockCommand request,
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

            // Soft-delete: set the flag; UnitOfWorkBehavior stamps DeletedAt/DeletedBy.
            block.IsDeleted = true;
            await _repository.Learning.UpdateAsync(block);

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.ContentBlockDeleted,
                    TargetEntityType: nameof(ContentBlock),
                    TargetEntityId: request.Id,
                    Details: null),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-02: AdminActionPerformedEvent publish failed for DeleteContentBlockCommand, BlockId={request.Id}");
            }

            return Success<string>(_localizer[SharedResourcesKey.ItemDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteContentBlockCommand");
            return ServerError<string>();
        }
    }
}

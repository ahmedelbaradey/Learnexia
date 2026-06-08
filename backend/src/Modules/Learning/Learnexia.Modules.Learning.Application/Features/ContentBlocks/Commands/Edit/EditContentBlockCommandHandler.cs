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

namespace Learnexia.Modules.Learning.Application.Features.ContentBlocks.Commands.Edit;

/// <summary>
/// Updates <c>BlockType</c> and <c>Payload</c> on an existing <see cref="ContentBlock"/>.
/// SequenceOrder, IsActive, and IsDeleted are NOT touched here — dedicated commands own those fields.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class EditContentBlockCommandHandler
    : BaseResponseHandler, ICommandHandler<EditContentBlockCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditContentBlockCommandHandler(
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

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.ContentBlockUpdated,
                    TargetEntityType: nameof(ContentBlock),
                    TargetEntityId: request.Id,
                    Details: $"BlockType={request.BlockType}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-02: AdminActionPerformedEvent publish failed for EditContentBlockCommand, BlockId={request.Id}");
            }

            return Success<string>(_localizer[SharedResourcesKey.ContentBlockUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditContentBlockCommand");
            return ServerError<string>();
        }
    }
}

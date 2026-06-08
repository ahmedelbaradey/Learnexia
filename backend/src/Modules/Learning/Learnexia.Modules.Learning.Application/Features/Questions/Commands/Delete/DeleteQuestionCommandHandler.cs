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

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Delete;

/// <summary>
/// Soft-deletes a <see cref="QuizQuestion"/> by setting <c>IsDeleted = true</c>.
/// The global query filter excludes deleted questions from all subsequent reads,
/// including student quiz/attempt reads.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class DeleteQuestionCommandHandler
    : BaseResponseHandler, ICommandHandler<DeleteQuestionCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public DeleteQuestionCommandHandler(
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
        DeleteQuestionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var question = await _repository.Learning
                .GetByCondition<QuizQuestion>(q => q.Id == request.Id, trackChanges: true)
                .FirstOrDefaultAsync(cancellationToken);

            if (question is null)
                return NotFound<string>(_localizer[SharedResourcesKey.QuizQuestionNotFound]);

            // Soft-delete: set the flag; UnitOfWorkBehavior stamps DeletedAt/DeletedBy.
            question.IsDeleted = true;
            await _repository.Learning.UpdateAsync(question);

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.QuizQuestionDeleted,
                    TargetEntityType: nameof(QuizQuestion),
                    TargetEntityId: request.Id,
                    Details: null),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-04: AdminActionPerformedEvent publish failed for DeleteQuestionCommand, QuestionId={request.Id}");
            }

            return Success<string>(_localizer[SharedResourcesKey.QuizQuestionDeletedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in DeleteQuestionCommand");
            return ServerError<string>();
        }
    }
}

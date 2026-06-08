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

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Edit;

/// <summary>
/// Updates <c>QuestionType</c>, <c>QuestionText</c>, <c>Options</c>, <c>CorrectAnswer</c>,
/// and <c>Difficulty</c> on an existing <see cref="QuizQuestion"/>.
/// SequenceOrder, IsActive, and IsDeleted are NOT touched here — dedicated commands own those.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class EditQuestionCommandHandler
    : BaseResponseHandler, ICommandHandler<EditQuestionCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditQuestionCommandHandler(
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
        EditQuestionCommand request,
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

            // Only update editable fields — do NOT touch LessonId, SkillId, SequenceOrder, IsActive, IsDeleted.
            question.QuestionType  = request.QuestionType;
            question.QuestionText  = request.QuestionText;
            question.Options       = request.Options;
            question.CorrectAnswer = request.CorrectAnswer;
            question.Difficulty    = request.Difficulty;

            await _repository.Learning.UpdateAsync(question);

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.QuizQuestionUpdated,
                    TargetEntityType: nameof(QuizQuestion),
                    TargetEntityId: request.Id,
                    Details: $"QuestionType={request.QuestionType}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, $"P7-04: AdminActionPerformedEvent publish failed for EditQuestionCommand, QuestionId={request.Id}");
            }

            return Success<string>(_localizer[SharedResourcesKey.QuizQuestionUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditQuestionCommand");
            return ServerError<string>();
        }
    }
}

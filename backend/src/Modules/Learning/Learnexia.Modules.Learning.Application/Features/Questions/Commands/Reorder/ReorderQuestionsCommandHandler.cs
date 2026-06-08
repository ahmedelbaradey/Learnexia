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

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Reorder;

/// <summary>
/// Batch-updates <c>SequenceOrder</c> on the given QuizQuestions.
/// All questions must belong to the <c>LessonId</c> anchor supplied in the command —
/// any ID that resolves to a different lesson is rejected before any update is applied.
/// Publishes <see cref="AdminActionPerformedEvent"/> post-commit, best-effort.
/// </summary>
public class ReorderQuestionsCommandHandler
    : BaseResponseHandler, ICommandHandler<ReorderQuestionsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderQuestionsCommandHandler(
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
        ReorderQuestionsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.QuestionIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Verify the anchor lesson exists before loading questions.
            var lessonExists = await _repository.Learning
                .AnyAsync<Lesson>(l => l.Id == request.LessonId);
            if (!lessonExists)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Load all specified questions scoped to the anchor LessonId — this is the
            // access-scoping check: any ID from a different lesson simply won't be found here.
            var questions = await _repository.Learning
                .GetByCondition<QuizQuestion>(
                    q => request.QuestionIds.Contains(q.Id) && q.LessonId == request.LessonId,
                    trackChanges: true)
                .ToListAsync(cancellationToken);

            // If the counts differ, at least one ID was not found under this LessonId.
            if (questions.Count != request.QuestionIds.Count)
                return BadRequest<string>(_localizer[SharedResourcesKey.QuizQuestionReorderLessonMismatch]);

            // Apply new SequenceOrder based on position in the requested list.
            var questionsById = questions.ToDictionary(q => q.Id);
            for (var i = 0; i < request.QuestionIds.Count; i++)
            {
                questionsById[request.QuestionIds[i]].SequenceOrder = i;
                await _repository.Learning.UpdateAsync(questionsById[request.QuestionIds[i]]);
            }

            // Best-effort post-commit event publish.
            try
            {
                await _publisher.Publish(new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: DateTime.UtcNow,
                    AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                    Action: AdminActions.QuizQuestionReordered,
                    TargetEntityType: nameof(QuizQuestion),
                    TargetEntityId: 0,
                    Details: $"Reordered {request.QuestionIds.Count} questions in LessonId={request.LessonId}"),
                    cancellationToken);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, "P7-04: AdminActionPerformedEvent publish failed for ReorderQuestionsCommand");
            }

            return Success<string>(_localizer[SharedResourcesKey.QuizQuestionReorderedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderQuestionsCommand");
            return ServerError<string>();
        }
    }
}

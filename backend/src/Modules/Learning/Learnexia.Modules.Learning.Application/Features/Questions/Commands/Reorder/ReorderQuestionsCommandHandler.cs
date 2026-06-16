using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Reorder;

/// <summary>
/// Batch-updates <c>SequenceOrder</c> on the given QuizQuestions.
/// All questions must belong to the <c>LessonId</c> anchor supplied in the command —
/// any ID that resolves to a different lesson is rejected before any update is applied.
///
/// P7-12: Domain event raised on the first question in the batch (most representative) —
/// dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Option-C refactor: AnyAsync + ToListAsync moved into IQuizQuestionService
/// (LessonExistsAsync and GetQuestionsTrackedByIdsForLessonAsync).
/// </summary>
public class ReorderQuestionsCommandHandler
    : BaseResponseHandler, ICommandHandler<ReorderQuestionsCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ReorderQuestionsCommandHandler(
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
        ReorderQuestionsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null || request.QuestionIds.Count == 0)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Verify the anchor lesson exists before loading questions.
            var lessonExists = await _service.QuizQuestionService.LessonExistsAsync(request.LessonId, cancellationToken);
            if (!lessonExists)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Load all specified questions scoped to the anchor LessonId — this is the
            // access-scoping check: any ID from a different lesson simply won't be found here.
            var questions = await _service.QuizQuestionService.GetQuestionsTrackedByIdsForLessonAsync(
                request.QuestionIds, request.LessonId, cancellationToken);

            // If the counts differ, at least one ID was not found under this LessonId.
            if (questions.Count != request.QuestionIds.Count)
                return BadRequest<string>(_localizer[SharedResourcesKey.QuizQuestionReorderLessonMismatch]);

            // Apply new SequenceOrder based on position in the requested list.
            var questionsById = questions.ToDictionary(q => q.Id);
            for (var i = 0; i < request.QuestionIds.Count; i++)
            {
                questionsById[request.QuestionIds[i]].SequenceOrder = i;
                await _service.QuizQuestionService.StageQuestionUpdateAsync(questionsById[request.QuestionIds[i]], cancellationToken);
            }

            // Raise domain event on the first question in the batch (most representative) —
            // dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            questionsById[request.QuestionIds[0]].RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.QuizQuestionReordered,
                TargetEntityType: nameof(QuizQuestion),
                TargetEntityId: 0,
                Details: $"Reordered {request.QuestionIds.Count} questions in LessonId={request.LessonId}"));

            return Success<string>(_localizer[SharedResourcesKey.QuizQuestionReorderedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReorderQuestionsCommand");
            return ServerError<string>();
        }
    }
}

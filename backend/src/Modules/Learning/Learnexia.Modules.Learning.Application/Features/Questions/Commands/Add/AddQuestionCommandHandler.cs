using System.Text.Json;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Events;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Add;

/// <summary>
/// Appends a new <see cref="QuizQuestion"/> to the lesson's implicit quiz.
/// SequenceOrder is set to max(existing for lessonId) + 1 (append semantics).
/// IsActive defaults to true (entity default — mass-assignment guard).
///
/// P7-12: Domain event raised on the QuizQuestion aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Option-C refactor: all EF calls (AnyAsync, MaxAsync) moved into IQuizQuestionService (Infrastructure).
/// Handler is thin: validate → call service → raise domain event → return BaseResponse.
/// </summary>
public class AddQuestionCommandHandler
    : BaseResponseHandler, ICommandHandler<AddQuestionCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddQuestionCommandHandler(
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
        AddQuestionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            // Verify the lesson exists (global IsDeleted filter active).
            var lessonExists = await _service.QuizQuestionService.LessonExistsAsync(request.LessonId, cancellationToken);
            if (!lessonExists)
                return NotFound<string>(_localizer[SharedResourcesKey.LessonNotFound]);

            // Determine the next SequenceOrder (append semantics: max + 1, or 0 if no questions yet).
            var maxOrder = await _service.QuizQuestionService.GetMaxSequenceOrderAsync(request.LessonId, cancellationToken);
            var sequenceOrder = maxOrder + 1;

            // P7-04 bugfix: CorrectAnswer is stored as jsonb.
            // Scalar types (MCQ, TrueFalse, FillInBlank) arrive as raw strings (e.g. "A", "true")
            // which are not valid JSON → Postgres rejects with 22P02.
            // Mirror the seeder exactly: JsonSerializer.Serialize(scalar) wraps in JSON quotes
            // so "A" → "\"A\"", matching the shape AnswerComparator.NormalizeJsonScalar decodes.
            // Matching arrives as an already-valid JSON object string — store as-is.
            var correctAnswerJson = request.QuestionType == QuestionType.Matching
                ? request.CorrectAnswer
                : JsonSerializer.Serialize(request.CorrectAnswer);

            var question = new QuizQuestion
            {
                LessonId      = request.LessonId,
                SkillId       = request.SkillId,
                QuestionType  = request.QuestionType,
                QuestionText  = request.QuestionText,
                Options       = request.Options,
                CorrectAnswer = correctAnswerJson,
                Difficulty    = request.Difficulty,
                GeneratedBy   = request.GeneratedBy,
                SequenceOrder = sequenceOrder,
                IsActive      = true   // explicit default — mass-assignment guard
            };

            await _service.QuizQuestionService.StageAddQuestionAsync(question, cancellationToken);

            // Raise domain event on the tracked QuizQuestion aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            question.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.QuizQuestionAdded,
                TargetEntityType: nameof(QuizQuestion),
                TargetEntityId: 0,
                Details: $"LessonId={request.LessonId}, QuestionType={request.QuestionType}"));

            return Success<string>(_localizer[SharedResourcesKey.QuizQuestionAddedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in AddQuestionCommand");
            return ServerError<string>();
        }
    }
}

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

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Edit;

/// <summary>
/// Updates <c>QuestionType</c>, <c>QuestionText</c>, <c>Options</c>, <c>CorrectAnswer</c>,
/// and <c>Difficulty</c> on an existing <see cref="QuizQuestion"/>.
/// SequenceOrder, IsActive, and IsDeleted are NOT touched here — dedicated commands own those.
///
/// P7-12: Domain event raised on the QuizQuestion aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12 fix).
///
/// Option-C refactor: FirstOrDefaultAsync moved into IQuizQuestionService.GetQuestionTrackedAsync.
/// </summary>
public class EditQuestionCommandHandler
    : BaseResponseHandler, ICommandHandler<EditQuestionCommand, BaseResponse<string>>
{
    private readonly ILoggerManager _logger;
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public EditQuestionCommandHandler(
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
        EditQuestionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest<string>(_localizer[SharedResourcesKey.EmptyRequestValidation]);

            var question = await _service.QuizQuestionService.GetQuestionTrackedAsync(request.Id, cancellationToken);

            if (question is null)
                return NotFound<string>(_localizer[SharedResourcesKey.QuizQuestionNotFound]);

            // Only update editable fields — do NOT touch LessonId, SkillId, SequenceOrder, IsActive, IsDeleted.
            // P7-04 bugfix: CorrectAnswer is stored as jsonb.
            // Scalar types (MCQ, TrueFalse, FillInBlank) arrive as raw strings (e.g. "A", "true")
            // which are not valid JSON → Postgres rejects with 22P02.
            // Mirror the seeder exactly: JsonSerializer.Serialize(scalar) wraps in JSON quotes
            // so "A" → "\"A\"", matching the shape AnswerComparator.NormalizeJsonScalar decodes.
            // Matching arrives as an already-valid JSON object string — store as-is.
            var correctAnswerJson = request.QuestionType == QuestionType.Matching
                ? request.CorrectAnswer
                : JsonSerializer.Serialize(request.CorrectAnswer);

            question.QuestionType  = request.QuestionType;
            question.QuestionText  = request.QuestionText;
            question.Options       = request.Options;
            question.CorrectAnswer = correctAnswerJson;
            question.Difficulty    = request.Difficulty;

            await _service.QuizQuestionService.StageQuestionUpdateAsync(question, cancellationToken);

            // Raise domain event on the tracked aggregate — dispatched post-commit by UnitOfWorkBehavior (ADR 0002 / P7-12).
            question.RaiseDomainEvent(new AdminActionPerformedDomainEvent(
                AdminUserId: _currentUser.UserId.GetValueOrDefault(),
                Action: AdminActions.QuizQuestionUpdated,
                TargetEntityType: nameof(QuizQuestion),
                TargetEntityId: request.Id,
                Details: $"QuestionType={request.QuestionType}"));

            return Success<string>(_localizer[SharedResourcesKey.QuizQuestionUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in EditQuestionCommand");
            return ServerError<string>();
        }
    }
}

using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.SubmitAnswer;

/// <summary>
/// Handles SubmitAnswerCommand.
///
/// Validates ownership and attempt state, checks correctness server-side, and stages a
/// StudentAnswer row. UnitOfWorkBehavior owns the commit — do NOT call SaveChangesAsync here.
///
/// StudentId is resolved from the JWT (ICurrentUserService) — never from the client request.
/// IsCorrect is computed server-side via case-insensitive string equality on the raw JSON values.
/// P2-07 will refine the correctness check per QuestionType and wire the hint availability stub.
/// </summary>
public class SubmitAnswerCommandHandler : BaseResponseHandler,
    ICommandHandler<SubmitAnswerCommand, BaseResponse<SubmitAnswerResponse>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public SubmitAnswerCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<SubmitAnswerResponse>> Handle(
        SubmitAnswerCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Resolve StudentId from the authenticated JWT; never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<SubmitAnswerResponse>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 2 — Load the attempt with tracking (needed to verify state; child rows are written separately).
            var attempt = _repository.Learning
                .GetByCondition<Attempt>(a => a.Id == request.AttemptId, trackChanges: true)
                .FirstOrDefault();
            if (attempt is null)
                return NotFound<SubmitAnswerResponse>(_localizer[SharedResourcesKey.AttemptNotFound]);

            // Step 3 — Ownership guard: student may only submit to their own attempt.
            if (attempt.StudentId != studentId.Value)
                return Unauthorized<SubmitAnswerResponse>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 4 — State guard: only InProgress attempts accept new answers.
            if (attempt.Status != AttemptStatus.InProgress)
                return BusinessValidation<SubmitAnswerResponse>(
                    _localizer[SharedResourcesKey.AttemptNotInProgress]);

            // Step 5 — Load the question and enforce same-lesson guard to prevent cross-lesson injection.
            var question = _repository.Learning
                .GetByCondition<QuizQuestion>(
                    q => q.Id == request.QuestionId && q.LessonId == attempt.LessonId,
                    trackChanges: false)
                .FirstOrDefault();
            if (question is null)
                return NotFound<SubmitAnswerResponse>(_localizer[SharedResourcesKey.QuestionNotFound]);

            // Step 6 — Re-answer guard: reject if the student already answered this question in this attempt.
            var alreadyAnswered = _repository.Learning
                .GetByCondition<StudentAnswer>(
                    sa => sa.AttemptId == request.AttemptId && sa.QuestionId == request.QuestionId,
                    trackChanges: false)
                .Any();
            if (alreadyAnswered)
                return BusinessValidation<SubmitAnswerResponse>(
                    _localizer[SharedResourcesKey.QuestionAlreadyAnswered]);

            // Step 7 — Correctness check: case-insensitive string equality on raw JSON values.
            // P2-07 will refine this per QuestionType.
            var isCorrect = string.Equals(
                request.AnswerPayload.Trim(),
                question.CorrectAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase);

            // Step 8 — Stage the new StudentAnswer via AutoMapper (IsCorrect is computed here, not from command).
            // UnitOfWorkBehavior commits atomically — do NOT call SaveChangesAsync.
            var studentAnswer = _mapper.Map<StudentAnswer>(request);
            studentAnswer.IsCorrect = isCorrect;
            await _repository.Learning.AddAsync(studentAnswer, cancellationToken);

            // TODO P2-07: publish AnswerSubmittedIntegrationEvent here

            // Step 9 — Return feedback response.
            var response = new SubmitAnswerResponse
            {
                IsCorrect = isCorrect,
                // CorrectAnswer is returned only when wrong — never give it away for free.
                CorrectAnswer = isCorrect ? null : question.CorrectAnswer,
                HintAvailable = false, // stub; P2-07 will wire hint availability
            };

            var result = Success(response);
            result.Message = _localizer[SharedResourcesKey.AnswerSubmittedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Step 10 — Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in SubmitAnswerCommand");
            return ServerError<SubmitAnswerResponse>();
        }
    }
}

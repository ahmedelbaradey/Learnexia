using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
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
/// IsCorrect is computed server-side via AnswerComparator.AreEqual, dispatching per QuestionType.
/// HintAvailable is a stub (false) today; P3-04 will wire AI-tutor hint availability.
/// On success, publishes AnswerSubmittedIntegrationEvent (skipped when QuizQuestion.SkillId is null).
///
/// Option C (no EF in Application): all DB access delegated to IAttemptWriteService.
/// </summary>
public class SubmitAnswerCommandHandler : BaseResponseHandler,
    ICommandHandler<SubmitAnswerCommand, BaseResponse<SubmitAnswerResponse>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;

    public SubmitAnswerCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher)
    {
        _service     = service;
        _currentUser = currentUser;
        _mapper      = mapper;
        _logger      = logger;
        _localizer   = localizer;
        _publisher   = publisher;
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

            // Step 2 — Load the attempt with tracking (needed to verify state).
            var attempt = await _service.AttemptWriteService
                .GetAttemptTrackedAsync(request.AttemptId, cancellationToken);
            if (attempt is null)
                return NotFound<SubmitAnswerResponse>(_localizer[SharedResourcesKey.AttemptNotFound]);

            // Step 3 — Ownership guard: student may only submit to their own attempt.
            if (attempt.StudentId != studentId.Value)
                return Unauthorized<SubmitAnswerResponse>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 4 — State guard: only InProgress attempts accept new answers.
            if (attempt.Status != AttemptStatus.InProgress)
                return BusinessValidation<SubmitAnswerResponse>(
                    _localizer[SharedResourcesKey.AttemptNotInProgress]);

            // Step 5 — Load the question and enforce same-lesson guard.
            var question = await _service.AttemptWriteService
                .GetQuestionForAnswerAsync(request.QuestionId, attempt.LessonId, cancellationToken);
            if (question is null)
                return NotFound<SubmitAnswerResponse>(_localizer[SharedResourcesKey.QuestionNotFound]);

            // Step 6 — Re-answer guard: reject if the student already answered this question.
            var alreadyAnswered = await _service.AttemptWriteService
                .IsAlreadyAnsweredAsync(request.AttemptId, request.QuestionId, cancellationToken);
            if (alreadyAnswered)
                return BusinessValidation<SubmitAnswerResponse>(
                    _localizer[SharedResourcesKey.QuestionAlreadyAnswered]);

            // Step 7 — Correctness check: per-QuestionType comparison via AnswerComparator.
            var isCorrect = AnswerComparator.AreEqual(
                question.QuestionType,
                request.AnswerPayload,
                question.CorrectAnswer);

            // Step 8 — Stage the new StudentAnswer via AutoMapper.
            // UnitOfWorkBehavior commits atomically — do NOT call SaveChangesAsync.
            var studentAnswer = _mapper.Map<Learnexia.Modules.Learning.Domain.Entities.StudentAnswer>(request);
            studentAnswer.IsCorrect = isCorrect;
            await _service.AttemptWriteService.StageAddStudentAnswerAsync(studentAnswer, cancellationToken);

            // Publish AnswerSubmittedIntegrationEvent (Option B — direct publish per lead decision).
            if (question.SkillId.HasValue)
            {
                try
                {
                    var integrationEvent = new AnswerSubmittedIntegrationEvent(
                        EventId: Guid.NewGuid(),
                        OccurredOnUtc: DateTime.UtcNow,
                        StudentId: studentId.Value,
                        LessonId: attempt.LessonId,
                        SkillId: question.SkillId.Value,
                        CorrectAnswerCount: isCorrect ? 1 : 0);

                    await _publisher.Publish(integrationEvent, cancellationToken);
                }
                catch (Exception publishEx)
                {
                    _logger.LogError(publishEx, $"P2-07: AnswerSubmittedIntegrationEvent publish failed for AttemptId={attempt.Id}, QuestionId={question.Id}, StudentId={studentId.Value}");
                }
            }
            else
            {
                _logger.LogWarn($"P2-07: AnswerSubmittedIntegrationEvent skipped — QuestionId={question.Id} has no SkillId (TODO P3-09 will track no-skill answers separately).");
            }

            // Step 9 — Return feedback response.
            var response = new SubmitAnswerResponse
            {
                IsCorrect     = isCorrect,
                // CorrectAnswer is returned only when wrong — never give it away for free.
                CorrectAnswer = isCorrect ? null : question.CorrectAnswer,
                HintAvailable = false, // TODO P3-04: AI-tutor hint availability
            };

            var result = Success(response);
            result.Message = _localizer[SharedResourcesKey.AnswerSubmittedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SubmitAnswerCommand");
            return ServerError<SubmitAnswerResponse>();
        }
    }
}

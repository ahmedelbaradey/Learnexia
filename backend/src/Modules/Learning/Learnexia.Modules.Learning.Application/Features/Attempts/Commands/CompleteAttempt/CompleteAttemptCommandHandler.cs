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

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.CompleteAttempt;

/// <summary>
/// Handles CompleteAttemptCommand.
///
/// Transitions an InProgress attempt to Completed, recomputes aggregates over all submitted
/// StudentAnswer rows (fresh query — guards against concurrent submits), and returns an
/// AttemptSummaryDto.
///
/// Idempotency: if the attempt is already Completed, returns current state without mutation.
/// BusinessValidation: if the attempt is Abandoned, the transition is rejected.
///
/// StudentId is resolved from the authenticated JWT — NEVER from the client.
/// UnitOfWorkBehavior owns the commit — do NOT call SaveChangesAsync here.
///
/// Concurrency note: a race between a final SubmitAnswer and this CompleteAttempt arriving
/// simultaneously may produce aggregates that exclude the last answer. This is acceptable for
/// Phase 2; pessimistic locking is a Phase 3+ concern.
/// </summary>
public class CompleteAttemptCommandHandler : BaseResponseHandler,
    ICommandHandler<CompleteAttemptCommand, BaseResponse<AttemptSummaryDto>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public CompleteAttemptCommandHandler(
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

    public async Task<BaseResponse<AttemptSummaryDto>> Handle(
        CompleteAttemptCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Resolve StudentId from the authenticated JWT; never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<AttemptSummaryDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 2 — Load the attempt with tracking (needs update).
            var attempt = _repository.Learning
                .GetByCondition<Attempt>(a => a.Id == request.AttemptId, trackChanges: true)
                .FirstOrDefault();
            if (attempt is null)
                return NotFound<AttemptSummaryDto>(_localizer[SharedResourcesKey.AttemptNotFound]);

            // Step 3 — Ownership guard: student may only complete their own attempt.
            if (attempt.StudentId != studentId.Value)
                return Unauthorized<AttemptSummaryDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 4 — Status checks.
            if (attempt.Status == AttemptStatus.Completed)
            {
                // Idempotent: attempt is already completed — return current state without mutation.
                var idempotentAnswers = _repository.Learning
                    .GetByCondition<StudentAnswer>(sa => sa.AttemptId == request.AttemptId, trackChanges: false)
                    .ToList();

                var idempotentDto = _mapper.Map<AttemptSummaryDto>(attempt);
                idempotentDto.TotalAnswers = idempotentAnswers.Count;
                idempotentDto.CorrectAnswers = idempotentAnswers.Count(a => a.IsCorrect);
                idempotentDto.Status = attempt.Status.ToString();

                var idempotentResult = Success(idempotentDto);
                idempotentResult.Message = _localizer[SharedResourcesKey.AttemptCompletedSuccessfully];
                return idempotentResult;
            }

            if (attempt.Status == AttemptStatus.Abandoned)
                return BusinessValidation<AttemptSummaryDto>(_localizer[SharedResourcesKey.AttemptAlreadyAbandoned]);

            // Step 5 — Load all StudentAnswer rows for this attempt (fresh query; guards concurrent submits).
            var answers = _repository.Learning
                .GetByCondition<StudentAnswer>(sa => sa.AttemptId == request.AttemptId, trackChanges: false)
                .ToList();

            // Step 6 — Recompute aggregates (server-side elapsed time is authoritative for DurationSeconds).
            RecomputeAggregates(attempt, answers);

            // Step 7 — Transition status.
            attempt.Status = AttemptStatus.Completed;

            // Step 8 — Stage update. UnitOfWorkBehavior commits atomically; do NOT call SaveChangesAsync.
            await _repository.Learning.UpdateAsync(attempt);

            // TODO P2-07: publish LessonCompletedIntegrationEvent here

            // Step 9 — Map and return summary DTO. TotalAnswers/CorrectAnswers filled explicitly
            // (AutoMapper ignores those two members — see QuizProfile).
            var dto = _mapper.Map<AttemptSummaryDto>(attempt);
            dto.TotalAnswers = answers.Count;
            dto.CorrectAnswers = answers.Count(a => a.IsCorrect);
            dto.Status = attempt.Status.ToString();

            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.AttemptCompletedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in CompleteAttemptCommand");
            return ServerError<AttemptSummaryDto>();
        }
    }

    /// <summary>
    /// Recomputes attempt-level aggregates from the current list of submitted answers.
    /// DurationSeconds uses server-side elapsed time (UtcNow - StartedAt) as the authoritative value;
    /// per-question TimeSpentSeconds is advisory (client-reported).
    /// Divide-by-zero is guarded: zero answers → AccuracyPercentage = 0.
    /// </summary>
    private static void RecomputeAggregates(Attempt attempt, IList<StudentAnswer> answers)
    {
        var answered = answers.Count;
        var correct = answers.Count(a => a.IsCorrect);

        attempt.AccuracyPercentage = answered == 0
            ? 0.0
            : Math.Round((double)correct / answered * 100, 2);

        // Normalize StartedAt to UTC before subtracting: Npgsql returns `timestamp with time zone`
        // columns with Kind=Local even though AttemptService stores UtcNow, so a naive
        // (UtcNow - StartedAt) is off by the server's UTC offset.
        var now = DateTime.UtcNow;
        attempt.DurationSeconds = Math.Max(0, (int)(now - attempt.StartedAt.ToUniversalTime()).TotalSeconds);
        attempt.HintsUsedCount = answers.Count(a => a.HintUsed);
        attempt.CompletedAt = now;
    }
}

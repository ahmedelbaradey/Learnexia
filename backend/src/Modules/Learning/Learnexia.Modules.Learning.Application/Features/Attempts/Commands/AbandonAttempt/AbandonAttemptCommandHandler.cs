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

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.AbandonAttempt;

/// <summary>
/// Handles AbandonAttemptCommand (BE-3 — partial capture on abandon).
///
/// Transitions an InProgress attempt to Abandoned, recomputing aggregates over whatever answers
/// have been submitted so far. Zero answers is valid (accuracy = 0; no divide-by-zero).
/// Previously-submitted StudentAnswer rows are preserved — only the Attempt aggregate fields change.
///
/// Idempotency: if the attempt is already Abandoned, returns current state without mutation.
/// BusinessValidation: if the attempt is already Completed, the transition is rejected.
///
/// StudentId is resolved from the authenticated JWT — NEVER from the client.
/// UnitOfWorkBehavior owns the commit — do NOT call SaveChangesAsync here.
///
/// Option C (no EF in Application): all DB access delegated to IAttemptWriteService.
/// </summary>
public class AbandonAttemptCommandHandler : BaseResponseHandler,
    ICommandHandler<AbandonAttemptCommand, BaseResponse<AttemptSummaryDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AbandonAttemptCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service     = service;
        _currentUser = currentUser;
        _mapper      = mapper;
        _logger      = logger;
        _localizer   = localizer;
    }

    public async Task<BaseResponse<AttemptSummaryDto>> Handle(
        AbandonAttemptCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1 — Resolve StudentId from the authenticated JWT; never from the client.
            var studentId = _currentUser.UserId;
            if (studentId is null)
                return Unauthorized<AttemptSummaryDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 2 — Load the attempt with tracking (needs update).
            var attempt = await _service.AttemptWriteService.GetAttemptTrackedAsync(request.AttemptId, cancellationToken);
            if (attempt is null)
                return NotFound<AttemptSummaryDto>(_localizer[SharedResourcesKey.AttemptNotFound]);

            // Step 3 — Ownership guard: student may only abandon their own attempt.
            if (attempt.StudentId != studentId.Value)
                return Unauthorized<AttemptSummaryDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 4 — Status checks.
            if (attempt.Status == AttemptStatus.Abandoned)
            {
                // Idempotent: attempt is already abandoned — return current state without mutation.
                var idempotentAnswers = await _service.AttemptWriteService
                    .GetAnswersForAttemptAsync(request.AttemptId, cancellationToken);

                var idempotentDto = _mapper.Map<AttemptSummaryDto>(attempt);
                idempotentDto.TotalAnswers   = idempotentAnswers.Count;
                idempotentDto.CorrectAnswers = idempotentAnswers.Count(a => a.IsCorrect);
                idempotentDto.Status         = attempt.Status.ToString();

                var idempotentResult = Success(idempotentDto);
                idempotentResult.Message = _localizer[SharedResourcesKey.AttemptAbandonedSuccessfully];
                return idempotentResult;
            }

            if (attempt.Status == AttemptStatus.Completed)
                return BusinessValidation<AttemptSummaryDto>(_localizer[SharedResourcesKey.AttemptAlreadyCompleted]);

            // Step 5 — Load all StudentAnswer rows for this attempt (fresh query; zero answers is valid).
            var answers = await _service.AttemptWriteService
                .GetAnswersForAttemptAsync(request.AttemptId, cancellationToken);

            // Step 6 — Recompute aggregates over the partial set of answers captured so far.
            RecomputeAggregates(attempt, answers);

            // Step 7 — Transition status.
            attempt.Status = AttemptStatus.Abandoned;

            // Step 8 — Stage update. UnitOfWorkBehavior commits atomically; do NOT call SaveChangesAsync.
            await _service.AttemptWriteService.StageAttemptUpdateAsync(attempt, cancellationToken);

            // Step 9 — Map and return summary DTO.
            var dto = _mapper.Map<AttemptSummaryDto>(attempt);
            dto.TotalAnswers   = answers.Count;
            dto.CorrectAnswers = answers.Count(a => a.IsCorrect);
            dto.Status         = attempt.Status.ToString();

            var result = Success(dto);
            result.Message = _localizer[SharedResourcesKey.AttemptAbandonedSuccessfully];
            return result;
        }
        catch (Exception ex)
        {
            // Log server-side; do NOT echo ex.Message to the client.
            _logger.LogError(ex, "Error in AbandonAttemptCommand");
            return ServerError<AttemptSummaryDto>();
        }
    }

    /// <summary>
    /// Recomputes attempt-level aggregates from the current list of submitted answers.
    /// DurationSeconds uses server-side elapsed time (UtcNow - StartedAt) as the authoritative value.
    /// Divide-by-zero is guarded: zero answers → AccuracyPercentage = 0.
    /// </summary>
    private static void RecomputeAggregates(Attempt attempt, IList<StudentAnswer> answers)
    {
        var answered = answers.Count;
        var correct  = answers.Count(a => a.IsCorrect);

        attempt.AccuracyPercentage = answered == 0
            ? 0.0
            : Math.Round((double)correct / answered * 100, 2);

        // Normalize StartedAt to UTC before subtracting.
        var now = DateTime.UtcNow;
        attempt.DurationSeconds = Math.Max(0, (int)(now - attempt.StartedAt.ToUniversalTime()).TotalSeconds);
        attempt.HintsUsedCount  = answers.Count(a => a.HintUsed);
        attempt.CompletedAt     = now;
    }
}

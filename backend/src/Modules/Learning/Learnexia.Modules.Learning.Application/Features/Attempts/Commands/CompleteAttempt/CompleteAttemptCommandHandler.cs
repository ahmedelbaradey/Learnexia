using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
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
///
/// ── P3-09 MASTERY UPSERT (transaction boundary) ──────────────────────────────────────────────
/// After the attempt is marked Completed, this handler also upserts StudentSkillMastery rows
/// for every skill touched by the attempt's answers (via IAttemptWriteService.UpsertMasteryAndAdvanceSRAsync).
///
/// TRANSACTION BOUNDARY (ADR 0001 escape hatch — explicit atomicity):
/// The UnitOfWorkBehavior wraps this entire handler in a single ambient EF Core transaction.
/// All four writes (attempt status update + mastery upserts + SR field updates + profile recompute)
/// are staged within that SAME transaction — they commit atomically together.
/// The service MUST NOT call SaveChangesAsync — the UoW behavior is the single commit point.
/// ────────────────────────────────────────────────────────────────────────────────────────────────
///
/// UnitOfWorkBehavior owns the commit — do NOT call SaveChangesAsync here.
/// Option C (no EF in Application): all DB access delegated to IAttemptWriteService +
/// ILearningServiceManager (for AttemptService.StartNewAsync) + IStudentProfileService.
/// </summary>
public class CompleteAttemptCommandHandler : BaseResponseHandler,
    ICommandHandler<CompleteAttemptCommand, BaseResponse<AttemptSummaryDto>>
{
    private readonly ILearningServiceManager _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IPublisher _publisher;
    private readonly IStudentProfileService _studentProfileService;

    public CompleteAttemptCommandHandler(
        ILearningServiceManager service,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher,
        IStudentProfileService studentProfileService)
    {
        _service               = service;
        _currentUser           = currentUser;
        _mapper                = mapper;
        _logger                = logger;
        _localizer             = localizer;
        _publisher             = publisher;
        _studentProfileService = studentProfileService;
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
            var attempt = await _service.AttemptWriteService.GetAttemptTrackedAsync(request.AttemptId, cancellationToken);
            if (attempt is null)
                return NotFound<AttemptSummaryDto>(_localizer[SharedResourcesKey.AttemptNotFound]);

            // Step 3 — Ownership guard: student may only complete their own attempt.
            if (attempt.StudentId != studentId.Value)
                return Unauthorized<AttemptSummaryDto>(_localizer[SharedResourcesKey.Unauthorized]);

            // Step 4 — Status checks.
            if (attempt.Status == AttemptStatus.Completed)
            {
                // Idempotent: attempt is already completed — return current state without mutation.
                var idempotentAnswers = await _service.AttemptWriteService
                    .GetAnswersForAttemptAsync(request.AttemptId, cancellationToken);

                var idempotentDto = _mapper.Map<AttemptSummaryDto>(attempt);
                idempotentDto.TotalAnswers   = idempotentAnswers.Count;
                idempotentDto.CorrectAnswers = idempotentAnswers.Count(a => a.IsCorrect);
                idempotentDto.Status         = attempt.Status.ToString();

                var idempotentResult = Success(idempotentDto);
                idempotentResult.Message = _localizer[SharedResourcesKey.AttemptCompletedSuccessfully];
                return idempotentResult;
            }

            if (attempt.Status == AttemptStatus.Abandoned)
                return BusinessValidation<AttemptSummaryDto>(_localizer[SharedResourcesKey.AttemptAlreadyAbandoned]);

            // Step 5 — Load all StudentAnswer rows for this attempt (fresh query; guards concurrent submits).
            var answers = await _service.AttemptWriteService
                .GetAnswersForAttemptAsync(request.AttemptId, cancellationToken);

            // Step 6 — Recompute aggregates (server-side elapsed time is authoritative for DurationSeconds).
            RecomputeAggregates(attempt, answers);

            // Step 7 — Transition status.
            attempt.Status = AttemptStatus.Completed;

            // Step 8 — Stage update. UnitOfWorkBehavior commits atomically; do NOT call SaveChangesAsync.
            await _service.AttemptWriteService.StageAttemptUpdateAsync(attempt, cancellationToken);

            // Step 8b — P3-09/P3-10: Upsert mastery rows + advance SR ladder for every skill
            // touched by this attempt. Both are staged within the SAME ambient UoW transaction.
            await _service.AttemptWriteService.UpsertMasteryAndAdvanceSRAsync(
                attempt.StudentId, attempt.Id, answers, cancellationToken);

            // Step 8c — P3-13: Student behavioral profile recompute (completion hook).
            // Called AFTER the P3-09 mastery upsert so derivation reads fresh mastery data.
            // Runs INSIDE the SAME ambient UoW transaction. The service internally catches and
            // logs exceptions so a profile failure does not abort the attempt completion.
            await _studentProfileService.RecomputeProfile(attempt.StudentId, cancellationToken);

            // Publish LessonCompletedIntegrationEvent (Option B — direct publish per lead decision).
            // Lesson.SkillId is nullable; the integration event requires SkillId, so skip when absent.
            var lessonSkillId = await _service.LessonService.GetLessonSkillIdAsync(attempt.LessonId, cancellationToken);
            if (lessonSkillId.HasValue)
            {
                try
                {
                    var integrationEvent = new LessonCompletedIntegrationEvent(
                        EventId: Guid.NewGuid(),
                        OccurredOnUtc: DateTime.UtcNow,
                        StudentId: studentId.Value,
                        LessonId: attempt.LessonId,
                        SkillId: lessonSkillId.Value,
                        AccuracyPercentage: (int)Math.Round(attempt.AccuracyPercentage),
                        CorrectAnswerCount: answers.Count(a => a.IsCorrect));

                    await _publisher.Publish(integrationEvent, cancellationToken);
                }
                catch (Exception publishEx)
                {
                    // Fail-soft: log + continue. We do NOT fail the user request because of a publisher failure.
                    // Ghost-event-on-rollback risk is accepted per ADR 0002; outbox is a future hardening story.
                    _logger.LogError(publishEx, $"P2-07: LessonCompletedIntegrationEvent publish failed for AttemptId={attempt.Id}, LessonId={attempt.LessonId}, StudentId={studentId.Value}");
                }
            }
            else
            {
                _logger.LogWarn($"P2-07: LessonCompletedIntegrationEvent skipped — LessonId={attempt.LessonId} has no SkillId (TODO P3-09 will track no-skill completions separately).");
            }

            // Step 9 — Map and return summary DTO. TotalAnswers/CorrectAnswers filled explicitly
            // (AutoMapper ignores those two members — see QuizProfile).
            var dto = _mapper.Map<AttemptSummaryDto>(attempt);
            dto.TotalAnswers   = answers.Count;
            dto.CorrectAnswers = answers.Count(a => a.IsCorrect);
            dto.Status         = attempt.Status.ToString();

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
    private static void RecomputeAggregates(
        Learnexia.Modules.Learning.Domain.Entities.Attempt attempt,
        IList<Learnexia.Modules.Learning.Domain.Entities.StudentAnswer> answers)
    {
        var answered = answers.Count;
        var correct  = answers.Count(a => a.IsCorrect);

        attempt.AccuracyPercentage = answered == 0
            ? 0.0
            : Math.Round((double)correct / answered * 100, 2);

        // Normalize StartedAt to UTC before subtracting: Npgsql returns `timestamp with time zone`
        // columns with Kind=Local even though AttemptService stores UtcNow, so a naive
        // (UtcNow - StartedAt) is off by the server's UTC offset.
        var now = DateTime.UtcNow;
        attempt.DurationSeconds = Math.Max(0, (int)(now - attempt.StartedAt.ToUniversalTime()).TotalSeconds);
        attempt.HintsUsedCount  = answers.Count(a => a.HintUsed);
        attempt.CompletedAt     = now;
    }
}

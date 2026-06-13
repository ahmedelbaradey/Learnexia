using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
/// After the attempt is marked Completed, this handler also upserts <c>StudentSkillMastery</c>
/// rows for every skill touched by the attempt's answers (per-distinct-SkillId aggregation).
///
/// TRANSACTION BOUNDARY (ADR 0001 escape hatch — explicit atomicity):
/// The <c>UnitOfWorkBehavior</c> wraps this entire handler in a single ambient EF Core transaction
/// (opened via BeginTransactionAsync before the handler runs, committed after SaveChangesAsync).
/// Both the attempt status update AND the mastery upserts are staged within that SAME transaction —
/// they commit atomically together. This is the sanctioned escape hatch from ADR 0001 §2:
/// "if you need atomic multi-writes, open an explicit transaction" — here the UoW behavior IS that
/// explicit transaction. A student will never see a Completed attempt with stale mastery (Q4).
///
/// PostgreSQL/Npgsql note: nested transactions (SAVEPOINT) are supported, but we intentionally
/// do NOT open a nested transaction here — we rely on the ambient UoW transaction to wrap everything.
/// ────────────────────────────────────────────────────────────────────────────────────────────────
///
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
    private readonly IPublisher _publisher;

    public CompleteAttemptCommandHandler(
        ILearningRepositoryManager repository,
        ICurrentUserService currentUser,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IPublisher publisher)
    {
        _repository = repository;
        _currentUser = currentUser;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
        _publisher = publisher;
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

            // Step 8b — P3-09: Upsert mastery rows for every skill touched by this attempt.
            // Both this upsert and the attempt update above are staged within the SAME ambient
            // UoW transaction (see class-level doc for the full transaction boundary explanation).
            await UpsertMasteryForAttemptAsync(attempt.StudentId, answers, cancellationToken);

            // Publish LessonCompletedIntegrationEvent (Option B — direct publish per lead decision).
            // Lesson.SkillId is nullable; the integration event requires SkillId, so skip when absent.
            // TODO P3-09: track no-skill lesson completions separately.
            var lessonSkillId = await _repository.Learning.GetLessonSkillIdAsync(attempt.LessonId, cancellationToken);
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
    /// P3-09 — Aggregates per-skill correct/total counts from the attempt's answers,
    /// calls MasteryEngine.Compute for each distinct skill, and upserts StudentSkillMastery rows.
    ///
    /// This method runs INSIDE the ambient UoW transaction opened by UnitOfWorkBehavior — all staged
    /// changes are committed atomically with the attempt status update (see class-level doc).
    ///
    /// NOTE: <paramref name="answers"/> does NOT have the Question navigation loaded — SkillId values
    /// are fetched from the DB via a JOIN on QuizQuestion (step A below).
    /// </summary>
    private async Task UpsertMasteryForAttemptAsync(
        int studentId,
        IList<StudentAnswer> answers,
        CancellationToken cancellationToken)
    {
        if (answers.Count == 0)
            return;

        var questionIds = answers.Select(a => a.QuestionId).Distinct().ToList();

        // Step A — Fetch the SkillId for each question in this attempt (batch, one query).
        // Questions with null SkillId are excluded (no skill attribution → no mastery update).
        var questionSkillMap = await _repository.Learning
            .GetByCondition<QuizQuestion>(qq => questionIds.Contains(qq.Id) && qq.SkillId.HasValue, trackChanges: false)
            .Select(qq => new { qq.Id, SkillId = qq.SkillId!.Value })
            .ToListAsync(cancellationToken);

        if (questionSkillMap.Count == 0)
        {
            _logger.LogWarn("P3-09: No skill-tagged questions found for studentId=" + studentId + "; mastery not updated.");
            return;
        }

        var skillIdByQuestionId = questionSkillMap.ToDictionary(q => q.Id, q => q.SkillId);

        // Step B — Aggregate per-skill correct/total counts (in memory — small list).
        var perSkillAggregates = answers
            .Where(a => skillIdByQuestionId.ContainsKey(a.QuestionId))
            .GroupBy(a => skillIdByQuestionId[a.QuestionId])
            .Select(g => new { SkillId = g.Key, Total = g.Count(), Correct = g.Count(a => a.IsCorrect) })
            .ToList();

        var skillIds = perSkillAggregates.Select(a => a.SkillId).ToList();

        // Step C — Fetch existing mastery rows (no tracking) and per-skill MasteryThreshold.
        var existingRows = await _repository.Learning
            .GetSkillMasteryRowsAsync(studentId, skillIds.AsReadOnly(), cancellationToken);
        var existingBySkillId = existingRows.ToDictionary(m => m.SkillId);

        var skills = await _repository.Learning
            .GetByCondition<Skill>(s => skillIds.Contains(s.Id), trackChanges: false)
            .Select(s => new { s.Id, s.MasteryThreshold })
            .ToListAsync(cancellationToken);
        var thresholdBySkillId = skills.ToDictionary(s => s.Id, s => s.MasteryThreshold);

        // Step D — For each skill, compute mastery and upsert.
        var now = DateTime.UtcNow;
        foreach (var agg in perSkillAggregates)
        {
            // Threshold: use per-skill value if available; fall back to 80 (FR-AD-3 default).
            var threshold = thresholdBySkillId.TryGetValue(agg.SkillId, out var t) ? t : 80;

            var (masteryPercentage, status) = MasteryEngine.Compute(agg.Total, agg.Correct, threshold);

            if (existingBySkillId.TryGetValue(agg.SkillId, out var existing))
            {
                // Update path: carry forward SR columns (P3-10 reserved) unchanged.
                var updated = new StudentSkillMastery
                {
                    Id                 = existing.Id,
                    StudentId          = studentId,
                    SkillId            = agg.SkillId,
                    MasteryPercentage  = masteryPercentage,
                    Status             = status,
                    AttemptsCount      = existing.AttemptsCount + 1,
                    LastPracticedAt    = now,
                    ReviewIntervalDays = existing.ReviewIntervalDays,
                    NextReviewDueAt    = existing.NextReviewDueAt,
                    RepetitionNumber   = existing.RepetitionNumber,
                };
                await _repository.Learning.UpsertStudentSkillMasteryAsync(updated, cancellationToken);
            }
            else
            {
                // Insert path: SR columns default to 0 / null per entity defaults (P3-10 reserved).
                var inserted = new StudentSkillMastery
                {
                    StudentId          = studentId,
                    SkillId            = agg.SkillId,
                    MasteryPercentage  = masteryPercentage,
                    Status             = status,
                    AttemptsCount      = 1,
                    LastPracticedAt    = now,
                };
                await _repository.Learning.UpsertStudentSkillMasteryAsync(inserted, cancellationToken);
            }
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

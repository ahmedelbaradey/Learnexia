using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Progress.Commands.ResetMathScienceProgress;

/// <summary>
/// Handles <see cref="ResetMathScienceProgressCommand"/> — deletes all Math/Science
/// <c>Attempt</c> rows for the specified student so derived mastery resets when the parent
/// changes the child's learning language (P8-04).
///
/// How Math/Science attempts are identified:
///   Attempt.LessonId → Lesson.Unit.SubjectId → Subject.SubjectCode ∈ {MATH, SCIENCE}
///   This deletes ALL Math/Science attempts for the student, regardless of which language tree
///   they belong to (locked decision §1 — full reset, not prior-language-only).
///
/// <c>StudentAnswer</c> rows cascade automatically via <c>DeleteBehavior.Cascade</c>
/// configured in <c>StudentAnswerConfig</c>. No explicit StudentAnswer delete is needed.
///
/// ADR 0001 compliance: the handler stages the RemoveRange but does NOT call SaveChangesAsync.
/// <c>UnitOfWorkBehavior</c> owns the single commit per command.
///
/// Idempotency: calling RemoveRange on an empty collection is a no-op — no error.
/// </summary>
public class ResetMathScienceProgressCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ResetMathScienceProgressCommand, BaseResponse<bool>>
{
    private readonly ILearningRepositoryManager _repository;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ResetMathScienceProgressCommandHandler(
        ILearningRepositoryManager repository,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repository = repository;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<bool>> Handle(
        ResetMathScienceProgressCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Identify Math/Science Attempt rows for the student via a two-step query:
            //
            // Step A — Collect all Lesson IDs whose Subject.SubjectCode is MATH or SCIENCE.
            //   Traversal: Lesson.Unit.Subject.SubjectCode ∈ {MATH, SCIENCE}
            //   AsNoTracking (read-only; only Attempt rows need tracking for deletion).
            //
            //   Mirrors the two-step pattern from LearningRepository.GetSubjectKnowledgeEdgesAsync:
            //   collect IDs first, then filter the target entity set by those IDs.
            var mathScienceLessonIds = await _repository.Learning
                .GetByCondition<Lesson>(
                    l => l.Unit.Subject.SubjectCode == SubjectCode.MATH
                      || l.Unit.Subject.SubjectCode == SubjectCode.SCIENCE,
                    trackChanges: false)
                .Select(l => l.Id)
                .ToListAsync(cancellationToken);

            if (mathScienceLessonIds.Count == 0)
            {
                // No Math/Science lessons in the curriculum yet — nothing to reset.
                _logger.LogInfo(
                    $"P8-04: ResetMathScienceProgress — no Math/Science lessons found in curriculum. " +
                    $"Student {request.StudentId} has no attempts to reset (originEventId={request.OriginEventId}).");
                return Success(true);
            }

            var lessonIdSet = mathScienceLessonIds.ToHashSet();

            // Step B — Load Attempt rows with tracking (required for EF RemoveRange).
            //   Filter: student = request.StudentId AND lesson is in the Math/Science lesson set.
            var attemptsToDelete = await _repository.Learning
                .GetByCondition<Attempt>(
                    a => a.StudentId == request.StudentId && lessonIdSet.Contains(a.LessonId),
                    trackChanges: true)
                .ToListAsync(cancellationToken);

            if (attemptsToDelete.Count == 0)
            {
                _logger.LogInfo(
                    $"P8-04: ResetMathScienceProgress — no Math/Science Attempt rows found " +
                    $"for student {request.StudentId} (originEventId={request.OriginEventId}). No-op.");
                return Success(true);
            }

            // Stage the deletes. UnitOfWorkBehavior commits; StudentAnswer rows cascade via
            // DeleteBehavior.Cascade in StudentAnswerConfig — no explicit StudentAnswer delete needed.
            // Do NOT call SaveChangesAsync here (ADR 0001).
            await _repository.Learning.DeleteRangeAsync(attemptsToDelete);

            _logger.LogInfo(
                $"P8-04: ResetMathScienceProgress staged deletion of {attemptsToDelete.Count} " +
                $"Math/Science Attempt rows for student {request.StudentId} " +
                $"(originEventId={request.OriginEventId}). UoW will commit.");

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ResetMathScienceProgressCommand for student {request.StudentId}");
            return ServerError<bool>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

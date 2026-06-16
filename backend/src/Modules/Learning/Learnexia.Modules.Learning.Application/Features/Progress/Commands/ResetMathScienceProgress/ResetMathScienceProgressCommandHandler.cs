using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
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
/// ADR 0001 compliance: IProgressService.ResetMathScienceProgressAsync stages deletes but
/// does NOT call SaveChangesAsync. <c>UnitOfWorkBehavior</c> owns the single commit per command.
///
/// Idempotency: calling RemoveRange on an empty collection is a no-op — no error.
///
/// Option C: all EF access delegated to IProgressService via ILearningServiceManager.
/// This handler injects only ILearningServiceManager — no ILearningRepositoryManager, no EF types.
/// </summary>
public class ResetMathScienceProgressCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ResetMathScienceProgressCommand, BaseResponse<bool>>
{
    private readonly ILearningServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ResetMathScienceProgressCommandHandler(
        ILearningServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<bool>> Handle(
        ResetMathScienceProgressCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Delegate the two-step query-then-stage to IProgressService (Option C).
            var (hadCurriculumLessons, deletedCount) = await _service.ProgressService
                .ResetMathScienceProgressAsync(request.StudentId, cancellationToken);

            if (!hadCurriculumLessons)
            {
                // No Math/Science lessons in the curriculum yet — nothing to reset.
                _logger.LogInfo(
                    $"P8-04: ResetMathScienceProgress — no Math/Science lessons found in curriculum. " +
                    $"Student {request.StudentId} has no attempts to reset (originEventId={request.OriginEventId}).");
            }
            else if (deletedCount == 0)
            {
                _logger.LogInfo(
                    $"P8-04: ResetMathScienceProgress — no Math/Science Attempt rows found " +
                    $"for student {request.StudentId} (originEventId={request.OriginEventId}). No-op.");
            }
            else
            {
                _logger.LogInfo(
                    $"P8-04: ResetMathScienceProgress staged deletion of {deletedCount} " +
                    $"Math/Science Attempt rows for student {request.StudentId} " +
                    $"(originEventId={request.OriginEventId}). UoW will commit.");
            }

            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in ResetMathScienceProgressCommand for student {request.StudentId}");
            return ServerError<bool>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }
}

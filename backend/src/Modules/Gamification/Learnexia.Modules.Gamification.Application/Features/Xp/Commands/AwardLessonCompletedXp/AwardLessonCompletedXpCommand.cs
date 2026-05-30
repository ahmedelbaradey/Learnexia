using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;

/// <summary>
/// Awards LessonComplete XP (and QuizPass XP if accuracy ≥ threshold) for a completed lesson.
/// Sent by <c>LessonCompletedIntegrationEventHandler</c> via MediatR. Runs through
/// <c>UnitOfWorkBehavior</c> so all writes commit atomically.
///
/// Command is trusted (comes from the integration event payload, not user input). Validators
/// check structural integrity only (non-zero IDs, non-empty EventId).
/// </summary>
public record AwardLessonCompletedXpCommand(
    int StudentId,
    int LessonId,
    int SkillId,
    int AccuracyPercentage,
    Guid OriginEventId,
    DateTime OccurredAtUtc) : ICommand<BaseResponse<Unit>>;

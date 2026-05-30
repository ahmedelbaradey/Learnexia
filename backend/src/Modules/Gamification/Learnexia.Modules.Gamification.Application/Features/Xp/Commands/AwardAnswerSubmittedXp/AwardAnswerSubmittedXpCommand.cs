using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;

/// <summary>
/// Awards CorrectAnswer XP for a correct answer submission.
/// Sent by <c>AnswerSubmittedIntegrationEventHandler</c> ONLY when <c>CorrectAnswerCount &gt; 0</c>.
///
/// Belt-and-suspenders: the handler also guards <c>CorrectAnswerCount == 0</c> and returns early.
/// Runs through <c>UnitOfWorkBehavior</c> for a clean commit boundary.
///
/// Command is trusted (comes from the integration event payload, not user input). Validators
/// check structural integrity only (non-zero IDs, non-empty EventId).
/// </summary>
public record AwardAnswerSubmittedXpCommand(
    int StudentId,
    int LessonId,
    int? SkillId,
    int CorrectAnswerCount,
    Guid OriginEventId,
    DateTime OccurredAtUtc) : ICommand<BaseResponse<Unit>>;

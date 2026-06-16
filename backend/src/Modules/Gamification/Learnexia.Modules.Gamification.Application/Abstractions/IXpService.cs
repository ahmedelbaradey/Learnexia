using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Workflow service for the XP aggregate.
/// Owns idempotency, practice-mode gate, profile load/upsert, XP math, and row staging.
/// Called by XP command handlers; never injects IGamificationRepository directly into handlers.
/// Transactions are owned by UnitOfWorkBehavior — this service only stages changes.
/// </summary>
public interface IXpService
{
    /// <summary>
    /// Awards CorrectAnswer XP (+10, boosted) if not already applied.
    /// Skips when student is in Practice Mode or award already exists.
    /// Returns Success(Unit.Value) in all non-error paths (including idempotent skip).
    /// </summary>
    Task<BaseResponse<Unit>> AwardAnswerSubmittedXpAsync(
        AwardAnswerSubmittedXpCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Awards LessonCompleted XP (+50, boosted) and optionally QuizPass XP (+20, boosted)
    /// when accuracy threshold is met. Skips when student is in Practice Mode or already applied.
    /// Returns Success(Unit.Value) in all non-error paths (including idempotent skip).
    /// </summary>
    Task<BaseResponse<Unit>> AwardLessonCompletedXpAsync(
        AwardLessonCompletedXpCommand command,
        CancellationToken ct = default);
}

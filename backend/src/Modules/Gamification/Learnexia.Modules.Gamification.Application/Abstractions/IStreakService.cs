using Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Workflow service for the Streak aggregate.
/// Owns idempotency, practice-mode gate, day classification, freeze milestone, StreakBonus XP staging.
/// Transactions are owned by UnitOfWorkBehavior.
/// </summary>
public interface IStreakService
{
    /// <summary>
    /// Advances the streak for the given student based on the event's OccurredAtUtc timestamp.
    /// Handles NoOp (same day), OutOfOrder (stale), FirstActivity/Advance, and Reset transitions.
    /// Grants a freeze on every N-day milestone. Awards StreakBonus XP.
    /// Returns Success(Unit.Value) in all non-error paths.
    /// </summary>
    Task<BaseResponse<Unit>> AdvanceStreakAsync(
        AdvanceStreakCommand command,
        CancellationToken ct = default);
}

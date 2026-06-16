using Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;
using Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Workflow service for the League aggregate.
/// Owns period key calculation, profile resolution, membership load, idempotency,
/// LeagueXpDeltaLog staging, Redis leaderboard mirror, and cohort standings projection.
/// Transactions are owned by UnitOfWorkBehavior (commands) or none (queries).
/// </summary>
public interface ILeagueService
{
    /// <summary>
    /// Increments WeeklyXp on the student's active league membership.
    /// Idempotency-guarded via LeagueXpDeltaLog.
    /// Mirrors the post-mutation score to Redis leaderboard (best-effort).
    /// Returns Success(Unit.Value) in all non-error paths (including no-membership no-op).
    /// </summary>
    Task<BaseResponse<Unit>> IncrementLeagueXpAsync(
        IncrementLeagueXpCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full cohort standings for the given student.
    /// Triggers lazy placement via IStudentLeagueQuery. Applies "Student #N" anonymization.
    /// READ-ONLY — no staging or commit.
    /// </summary>
    Task<BaseResponse<MyLeagueResponse>> GetMyLeagueAsync(
        int studentId,
        CancellationToken ct = default);
}

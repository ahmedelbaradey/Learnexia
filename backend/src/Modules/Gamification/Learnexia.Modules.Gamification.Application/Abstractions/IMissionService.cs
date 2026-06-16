using Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;
using Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Workflow service for the Mission aggregate.
/// Owns active-mission probe, row-lock, idempotency, attach convention, progress log staging,
/// inline completion (XpAward + domain event), and mission read projection.
/// Transactions are owned by UnitOfWorkBehavior (commands) or none (queries).
/// </summary>
public interface IMissionService
{
    /// <summary>
    /// Increments progress on all matching active missions for the student.
    /// Applies idempotency guard per mission. Inline-completes missions that cross the threshold.
    /// Returns Success(Unit.Value) in all non-error paths.
    /// </summary>
    Task<BaseResponse<Unit>> IncrementMissionProgressAsync(
        IncrementMissionProgressCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the daily + weekly missions with full period metadata for the given student.
    /// Triggers lazy instantiation via IStudentMissionsQuery before the repo fetch.
    /// READ-ONLY — no staging or commit.
    /// </summary>
    Task<BaseResponse<MyMissionsResponse>> GetMyMissionsAsync(
        int studentId,
        CancellationToken ct = default);
}

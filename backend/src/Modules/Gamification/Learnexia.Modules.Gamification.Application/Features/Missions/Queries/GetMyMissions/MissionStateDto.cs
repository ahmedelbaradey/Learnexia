using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;

/// <summary>
/// Full mission state DTO for the <c>GET /api/Gamification/Missions/Me</c> endpoint (P4-06).
/// Richer than the cross-module <see cref="MissionSummary"/>
/// because it exposes period boundaries and RewardXp (used by the Missions screen — P4-08).
///
/// Uses the mirrored DTO enums from <c>Learnexia.Shared.Contracts.Gamification</c> rather than
/// Domain enums to avoid leaking Domain types onto the API surface (F3 security fix).
/// Integer values are identical — JSON serialization is unchanged.
///
/// Positional record — all fields are required and non-nullable (nullable DateTime? is the exception).
/// </summary>
public sealed record MissionStateDto(
    string Code,
    string IconKey,
    string TitleKey,
    MissionTypeDto Cadence,
    MissionTargetTypeDto TargetType,
    int Target,
    int RewardXp,
    int Progress,
    MissionStatusDto Status,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateTime? CompletedAtUtc);

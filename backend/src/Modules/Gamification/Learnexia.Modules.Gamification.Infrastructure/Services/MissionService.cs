using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;
using Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Exceptions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IMissionService"/>.
/// Owns active-mission probe, row-lock, idempotency, attach convention, progress log staging,
/// inline completion (XpAward + domain event), and mission read projection
/// via <see cref="IGamificationRepository"/>. Transactions are owned by UnitOfWorkBehavior.
/// </summary>
internal sealed class MissionService : BaseResponseHandler, IMissionService
{
    private readonly IGamificationRepository _repo;
    private readonly IStudentMissionsQuery _missionsQuery;
    private readonly ILoggerManager _logger;
    private readonly IXpBoostCalculator _boostCalc;

    public MissionService(
        IGamificationRepository repo,
        IStudentMissionsQuery missionsQuery,
        ILoggerManager logger,
        IXpBoostCalculator boostCalc)
    {
        _repo = repo;
        _missionsQuery = missionsQuery;
        _logger = logger;
        _boostCalc = boostCalc;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> IncrementMissionProgressAsync(
        IncrementMissionProgressCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Cheap probe: any missions to process at all? (no lock needed yet) ────────
            var probe = await _repo.GetActiveMissionsForStudentByTargetTypeAsync(
                request.StudentId, request.TargetType, ct);
            if (probe.Count == 0)
                return Success(Unit.Value);

            // ── 2. Row-lock on profile to serialize concurrent progress updates ──────────────
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 3. Re-fetch under lock ────────────────────────────────────────────────────────
            var missions = await _repo.GetActiveMissionsForStudentByTargetTypeAsync(
                request.StudentId, request.TargetType, ct);
            if (missions.Count == 0)
                return Success(Unit.Value);

            StudentXpProfile? profile = null;

            foreach (var mission in missions)
            {
                // ── 4a. Idempotency pre-check ─────────────────────────────────────────────
                bool alreadyCounted = await _repo.HasMissionProgressLogAsync(
                    mission.Id, request.OriginEventId, ct);
                if (alreadyCounted)
                {
                    _logger.LogInfo(
                        $"P4-06: duplicate event (missionId={mission.Id}, " +
                        $"originEventId={request.OriginEventId}) — skipping.");
                    continue;
                }

                // ── 4b. Attach the mission + definition so EF tracks mutations ─────────────
                _repo.AttachMissionDefinition(mission.MissionDefinition);
                _repo.AttachStudentMission(mission);

                // ── 4c. Write MissionProgressLog (idempotency row) ───────────────────────
                var log = MissionProgressLog.Create(
                    mission,
                    request.OriginEventId,
                    request.OriginEventType,
                    request.IncrementBy);
                await _repo.AddMissionProgressLogAsync(log, ct);

                // ── 4d. Apply progress ────────────────────────────────────────────────────
                mission.ApplyProgress(request.IncrementBy, request.OccurredAtUtc, out bool completedNow);

                _logger.LogInfo(
                    $"P4-06: mission progress applied " +
                    $"(missionId={mission.Id}, delta={request.IncrementBy}, " +
                    $"newProgress={mission.Progress}/{mission.Target}, " +
                    $"completed={completedNow}, originEventId={request.OriginEventId}).");

                // ── 4e. Inline completion logic ───────────────────────────────────────────
                if (completedNow)
                {
                    if (profile is null)
                    {
                        profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                                  ?? StudentXpProfile.CreateFor(request.StudentId);
                        _repo.UpsertXpProfile(profile);
                    }

                    int effectiveRewardXp = await _boostCalc.GetEffectiveAmountAsync(
                        mission.MissionDefinition.RewardXp,
                        XpReason.MissionCompleted,
                        request.OccurredAtUtc,
                        ct);

                    int newLevel = LevelCurve.LevelForXp(profile.TotalXp + effectiveRewardXp);

                    profile.RecordMissionCompleted(
                        missionDefinitionId: mission.MissionDefinitionId,
                        code: mission.MissionDefinition.Code,
                        rewardXp: effectiveRewardXp,
                        newLevel: newLevel,
                        completedAtUtc: request.OccurredAtUtc);

                    var xpAward = XpAward.Create(
                        profile: profile,
                        reason: XpReason.MissionCompleted,
                        xpAmount: effectiveRewardXp,
                        originEventId: Guid.NewGuid(),
                        occurredAtUtc: request.OccurredAtUtc,
                        lessonId: null,
                        skillId: null);
                    await _repo.AddXpAwardAsync(xpAward, ct);

                    _logger.LogInfo(
                        $"P4-06: mission completed {mission.MissionDefinition.Code} " +
                        $"(+{mission.MissionDefinition.RewardXp} XP) for studentId={request.StudentId}.");
                }
            }

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
            when (ucEx.ConstraintHint.Contains("UX_MissionProgressLogs_StudentMissionId_OriginEventId"))
        {
            _logger.LogInfo(
                $"P4-06: unique-constraint violation on mission progress for " +
                $"studentId={request.StudentId} (originEventId={request.OriginEventId}) — already applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-06: Unexpected error in MissionService.IncrementMissionProgressAsync " +
                $"(studentId={request.StudentId}, targetType={request.TargetType}, " +
                $"originEventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }

    /// <inheritdoc />
    public async Task<BaseResponse<MyMissionsResponse>> GetMyMissionsAsync(
        int studentId,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Trigger lazy instantiation via the cross-module seam ──────────────────────
            _ = await _missionsQuery.GetByStudentIdAsync(studentId, ct);

            // ── 2. Load current-period missions with full metadata ────────────────────────────
            var activeMissions = await _repo.GetActiveMissionsForStudentAsync(studentId, ct);

            // ── 3. Split + project ────────────────────────────────────────────────────────────
            var daily = activeMissions
                .Where(m => m.MissionDefinition.Cadence == MissionType.Daily)
                .OrderBy(m => m.MissionDefinition.SortOrder)
                .ThenBy(m => m.MissionDefinition.Id)
                .Select(ToDto)
                .ToList();

            var weekly = activeMissions
                .Where(m => m.MissionDefinition.Cadence == MissionType.Weekly)
                .OrderBy(m => m.MissionDefinition.SortOrder)
                .ThenBy(m => m.MissionDefinition.Id)
                .Select(ToDto)
                .ToList();

            return Success(new MyMissionsResponse(daily, weekly));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P4-06: Error in MissionService.GetMyMissionsAsync for studentId={studentId}");
            return ServerError<MyMissionsResponse>();
        }
    }

    private static MissionStateDto ToDto(Domain.Entities.StudentMission m) => new(
        Code:           m.MissionDefinition.Code,
        IconKey:        m.MissionDefinition.IconKey,
        TitleKey:       m.MissionDefinition.TitleKey,
        Cadence:        (MissionTypeDto)(int)m.MissionDefinition.Cadence,
        TargetType:     (MissionTargetTypeDto)(int)m.MissionDefinition.TargetType,
        Target:         m.Target,
        RewardXp:       m.MissionDefinition.RewardXp,
        Progress:       m.Progress,
        Status:         (MissionStatusDto)(int)m.Status,
        PeriodStartUtc: m.PeriodStartUtc,
        PeriodEndUtc:   m.PeriodEndUtc,
        CompletedAtUtc: m.CompletedAtUtc);
}

using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;

/// <summary>
/// Handles <see cref="IncrementMissionProgressCommand"/> (P4-06, FR-GM-5, AC2 + AC3).
///
/// Steps (inside UnitOfWorkBehavior transaction):
///   1. Cheap probe: load matching active missions WITHOUT a lock — exit early (no-op) if none.
///      This avoids acquiring the row-lock for the common no-op path (F5 security fix).
///   2. Row-lock on profile to serialize concurrent progress updates for the same student.
///      Lock scope matches the missions query scope (both key on StudentXpProfile.StudentId — F1).
///   3. Re-fetch active missions UNDER the lock to get a consistent view (race safety).
///   4. For each matched mission:
///      a. Idempotency pre-check via HasMissionProgressLogAsync(missionId, OriginEventId).
///         Skip if already counted (AC2 idempotency).
///      b. Attach MissionDefinition as Unchanged (graph-nav convention — AsNoTracking source
///         loaded by GetActiveMissionsForStudentByTargetTypeAsync).
///         NOTE: GetActiveMissionsForStudentByTargetTypeAsync uses AsNoTracking, so the
///         missions are detached. We must call AddStudentMission equivalently or use
///         Update/Attach. Since these missions already exist in the DB, we Attach and mark
///         them as Modified so EF tracks the Progress/Status mutations.
///      c. Write MissionProgressLog (idempotency row).
///      d. Call mission.ApplyProgress(IncrementBy, OccurredAtUtc, out completedNow).
///      e. If completedNow: load profile (or create), compute new level, call
///         profile.RecordMissionCompleted, create XpAward, log.
///   5. Return Success(Unit.Value). UoW commits all rows atomically; domain events
///      (MissionCompletedDomainEvent, optionally StudentLeveledUpDomainEvent) are dispatched
///      AFTER commit by UnitOfWorkBehavior (ADR 0002 §2).
///   6. DbUpdateException catch narrowed to UX_MissionProgressLogs_StudentMissionId_OriginEventId
///      violation → race tie-break → return Success(Unit.Value).
///
/// Inline completion rationale: completing a mission is done inline within the same UoW
/// to avoid nested-transaction issues. Npgsql + EF Core does NOT support nested
/// BeginTransactionAsync without savepoints on a single DbContext; sending a separate
/// CompleteMissionCommand from inside this handler would trigger a second UnitOfWorkBehavior
/// transaction on the same DbContext and throw. The inline approach avoids this entirely —
/// one transaction covers increment + completion + XpAward + domain-event enqueue. The
/// mission's Status guard (ApplyProgress checks Status != Completed) provides double-fire
/// protection. If a future story needs strict "after-commit" completion semantics, introduce
/// MissionThresholdReachedDomainEvent at that point.
/// </summary>
public sealed class IncrementMissionProgressCommandHandler
    : BaseResponseHandler, ICommandHandler<IncrementMissionProgressCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly ILoggerManager _logger;

    public IncrementMissionProgressCommandHandler(
        IGamificationRepository repo,
        ILoggerManager logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        IncrementMissionProgressCommand request,
        CancellationToken ct)
    {
        try
        {
            // ── 1. Cheap probe: any missions to process at all? (no lock needed yet) ────────
            // Avoids acquiring the row-lock for the common no-op path (e.g., DAILY_KEEP_STREAK
            // after the student already completed it). F5 security fix.
            var probe = await _repo.GetActiveMissionsForStudentByTargetTypeAsync(
                request.StudentId, request.TargetType, ct);
            if (probe.Count == 0)
                return Success(Unit.Value); // no active missions of this type — no-op

            // ── 2. Row-lock on profile to serialize concurrent progress updates ──────────────
            // Row-lock is keyed on StudentXpProfile.StudentId (the cross-module soft reference).
            // The missions query below also filters by StudentXpProfile.StudentId — the two scopes
            // match, so this lock fully serializes concurrent increments for THIS student across
            // all matching missions. F1 comment (lock-scope cross-check).
            await _repo.AcquireProfileLockAsync(request.StudentId, ct);

            // ── 3. Re-fetch under lock (small cost; correctness) ──────────────────────────
            // Between the probe and lock acquisition a concurrent transaction may have completed
            // one or more missions. Re-fetching under the lock guarantees a consistent view.
            // The per-mission HasMissionProgressLogAsync idempotency check inside the loop also
            // catches any duplicate events that sneak through, providing defense-in-depth.
            var missions = await _repo.GetActiveMissionsForStudentByTargetTypeAsync(
                request.StudentId, request.TargetType, ct);
            if (missions.Count == 0)
                return Success(Unit.Value); // race: completed between probe and lock — safe no-op

            // Profile is loaded lazily only when a mission crosses the completion threshold.
            // This avoids an unnecessary DB round-trip for the common case where no mission
            // completes in this increment.
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
                // The repo returns AsNoTracking entities; we attach each mission as Modified
                // so that the Progress/Status/CompletedAtUtc mutations (internal set) are
                // picked up by UoW before commit. The MissionDefinition is attached as Unchanged
                // to prevent EF from trying to re-insert the catalog row.
                _repo.AttachMissionDefinition(mission.MissionDefinition);
                _repo.AttachStudentMission(mission);

                // ── 4c. Write MissionProgressLog (idempotency row) ───────────────────────
                var log = MissionProgressLog.Create(
                    mission,
                    request.OriginEventId,
                    request.OriginEventType,
                    request.IncrementBy);
                await _repo.AddMissionProgressLogAsync(log, ct);

                // ── 4d. Apply progress — mutates mission.Progress / Status / CompletedAtUtc
                mission.ApplyProgress(request.IncrementBy, request.OccurredAtUtc, out bool completedNow);

                _logger.LogInfo(
                    $"P4-06: mission progress applied " +
                    $"(missionId={mission.Id}, delta={request.IncrementBy}, " +
                    $"newProgress={mission.Progress}/{mission.Target}, " +
                    $"completed={completedNow}, originEventId={request.OriginEventId}).");

                // ── 4e. Inline completion logic ───────────────────────────────────────────
                if (completedNow)
                {
                    // Load profile once (lazy load on first completion in this batch)
                    if (profile is null)
                    {
                        profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct)
                                  ?? StudentXpProfile.CreateFor(request.StudentId);
                        _repo.UpsertXpProfile(profile);
                    }

                    // Recompute level — XP bonus may push past a threshold
                    int newLevel = LevelCurve.LevelForXp(
                        profile.TotalXp + mission.MissionDefinition.RewardXp);

                    // Domain mutation: applies XP, recomputes level, raises
                    // MissionCompletedDomainEvent (+ StudentLeveledUpDomainEvent if level crossed).
                    // Events are enqueued on the aggregate; UnitOfWorkBehavior dispatches AFTER commit.
                    profile.RecordMissionCompleted(
                        missionDefinitionId: mission.MissionDefinitionId,
                        code: mission.MissionDefinition.Code,
                        rewardXp: mission.MissionDefinition.RewardXp,
                        newLevel: newLevel,
                        completedAtUtc: request.OccurredAtUtc);

                    // Persist the XpAward ledger row (forensics + XP ledger).
                    // Uses a fresh Guid per completion so UX_XpAwards_OriginEventId_Reason
                    // is always unique. The mission.Status == Completed guard in ApplyProgress
                    // prevents double-completion; the unique index is the DB-layer backstop.
                    var xpAward = XpAward.Create(
                        profile: profile,
                        reason: XpReason.MissionCompleted,
                        xpAmount: mission.MissionDefinition.RewardXp,
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
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message?.Contains("UX_MissionProgressLogs_StudentMissionId_OriginEventId") == true)
        {
            // Idempotency race: concurrent duplicate delivery on the MissionProgressLog unique index.
            // Narrowed to the specific constraint name only (security F2 cleanup) — bare "23505"
            // fallback would silently swallow unrelated unique-constraint violations.
            _logger.LogInfo(
                $"P4-06: unique-constraint violation on mission progress for " +
                $"studentId={request.StudentId} (originEventId={request.OriginEventId}) — already applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-06: Unexpected error in IncrementMissionProgressCommandHandler " +
                $"(studentId={request.StudentId}, targetType={request.TargetType}, " +
                $"originEventId={request.OriginEventId}).");
            return ServerError<Unit>();
        }
    }
}

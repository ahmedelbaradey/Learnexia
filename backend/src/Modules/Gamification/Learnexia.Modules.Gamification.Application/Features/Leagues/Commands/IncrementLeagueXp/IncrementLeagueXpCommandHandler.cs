using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;

/// <summary>
/// Handles <see cref="IncrementLeagueXpCommand"/> (P4-07, AC1 — weekly XP tracking).
///
/// Steps (inside UnitOfWorkBehavior transaction):
///   1. Compute the current ISO week period key via
///      <see cref="MissionPeriodCalculator.GetCurrentPeriod"/> (Weekly cadence).
///   2. Load the student's <see cref="StudentXpProfile"/>. If null → no-op (brand-new student,
///      no profile yet; defensive log only — upstream chokepoint ensures profile exists before
///      <c>XpAwardedDomainEvent</c> fires).
///   3. Load the active <see cref="LeagueMembership"/> for the (profile.Id, periodKey) pair as a
///      TRACKED entity so mutations are picked up by EF's change tracker.
///   4. If no membership → no-op (D15: student hasn't loaded the dashboard this week; the
///      handler no-ops per the Q6 locked decision).
///   5. Idempotency pre-check via
///      <see cref="IGamificationRepository.HasLeagueXpDeltaLogAsync"/>. If already applied →
///      return Success (already processed, no double-count).
///   6. Mutate <see cref="LeagueMembership.AddWeeklyXp"/>; write a
///      <see cref="LeagueXpDeltaLog"/> idempotency row.
///   7. UoW commits both mutations atomically.
///   8. <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/> narrowed to
///      <c>UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId</c> violation → concurrent
///      duplicate race → return Success (idempotent).
/// </summary>
public sealed class IncrementLeagueXpCommandHandler
    : BaseResponseHandler, ICommandHandler<IncrementLeagueXpCommand, BaseResponse<Unit>>
{
    private readonly IGamificationRepository _repo;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly ILeagueLeaderboard _leaderboard;

    public IncrementLeagueXpCommandHandler(
        IGamificationRepository repo,
        ISystemClock clock,
        ILoggerManager logger,
        ILeagueLeaderboard leaderboard)
    {
        _repo        = repo;
        _clock       = clock;
        _logger      = logger;
        _leaderboard = leaderboard;
    }

    public async Task<BaseResponse<Unit>> Handle(
        IncrementLeagueXpCommand request,
        CancellationToken ct)
    {
        try
        {
            // ── 1. Week period key derived from the EVENT timestamp (not wall-clock) ───────
            // Using request.OccurredAtUtc preserves correctness at week boundaries: a delayed
            // dispatch landing after Monday 00:00 UTC still credits the prior week's membership.
            var (periodKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, request.OccurredAtUtc);

            // ── 2. Resolve profile ────────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct);
            if (profile is null)
            {
                // Defensive: by the time XpAwardedDomainEvent fires the profile MUST exist.
                // Log as warn and no-op rather than error — avoids polluting logs on edge cases.
                _logger.LogWarn(
                    $"P4-07: IncrementLeagueXp for studentId={request.StudentId} — " +
                    $"no profile yet, skipping.");
                return Success(Unit.Value);
            }

            // ── 3. Load membership (tracked — mutations must flow to EF change tracker) ───
            var membership = await _repo.GetMembershipForStudentTrackedAsync(profile.Id, periodKey, ct);
            if (membership is null)
            {
                // No active membership for the current week — student has not loaded the
                // dashboard yet. Per Q6 locked decision: handler no-ops; lazy placement is the
                // dashboard read's responsibility (D15 accepted data-loss for this week).
                _logger.LogDebug(
                    $"P4-07: IncrementLeagueXp for studentId={request.StudentId} " +
                    $"periodKey={periodKey} — no membership, no-op.");
                return Success(Unit.Value);
            }

            // ── 4. Idempotency pre-check ──────────────────────────────────────────────────
            bool already = await _repo.HasLeagueXpDeltaLogAsync(membership.Id, request.OriginEventId, ct);
            if (already)
            {
                _logger.LogInfo(
                    $"P4-07: duplicate league XP delta " +
                    $"studentId={request.StudentId} originEventId={request.OriginEventId} — " +
                    $"already applied.");
                return Success(Unit.Value);
            }

            // ── 5. Mutate membership + write idempotency log (both committed by UoW) ──────
            // The membership was loaded as a tracked entity (GetMembershipForStudentTrackedAsync),
            // so AddWeeklyXp mutation will be flushed by UnitOfWorkBehavior on SaveChanges.
            membership.AddWeeklyXp(request.Amount);

            var log = LeagueXpDeltaLog.Create(
                membership:     membership,
                originEventId:  request.OriginEventId,
                delta:          request.Amount,
                occurredAtUtc:  request.OccurredAtUtc);
            await _repo.AddLeagueXpDeltaLogAsync(log, ct);

            _logger.LogInfo(
                $"P4-07: league XP +{request.Amount} for studentId={request.StudentId} " +
                $"membershipId={membership.Id} (weeklyXp={membership.WeeklyXp}).");

            // ── 6. Mirror post-mutation WeeklyXp to the Redis sorted-set leaderboard ─────
            // Mirror to Redis sorted-set leaderboard (best-effort, fail-soft).
            // NOTE: this runs BEFORE UnitOfWorkBehavior commits Postgres. If SaveChanges
            // subsequently fails, the sorted-set will carry a phantom score until the
            // nightly GamificationCacheRebuildJob (03:00 UTC) corrects it. The Postgres
            // ledger remains the source of truth; the post-commit XpAwardedCacheInvalidator
            // also re-mirrors the persisted value via ZADD CH (idempotent).
            await _leaderboard.UpsertMembershipAsync(
                leagueId:     membership.LeagueId,
                membershipId: membership.Id,
                weeklyXp:     membership.WeeklyXp,
                joinOrder:    membership.JoinOrder,
                ct:           ct);

            return Success(Unit.Value);
        }
        catch (DbUpdateException dbEx)
            when (dbEx.InnerException?.Message?.Contains(
                      "UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId") == true)
        {
            // Narrowed: only this specific unique-constraint violation is the idempotency race.
            // A concurrent handler invocation beat us; the XP was already credited.
            _logger.LogInfo(
                $"P4-07: unique-constraint race on LeagueXpDeltaLogs for " +
                $"studentId={request.StudentId} originEventId={request.OriginEventId} — " +
                $"already applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-07: Unexpected error in IncrementLeagueXpCommandHandler " +
                $"studentId={request.StudentId} originEventId={request.OriginEventId}.");
            return ServerError<Unit>();
        }
    }
}

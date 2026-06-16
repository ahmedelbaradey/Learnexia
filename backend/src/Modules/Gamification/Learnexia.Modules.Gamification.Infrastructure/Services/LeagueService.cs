using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;
using Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Exceptions;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ILeagueService"/>.
/// Owns period key calculation, profile resolution, membership load, idempotency,
/// LeagueXpDeltaLog staging, Redis leaderboard mirror, and cohort standings projection
/// via <see cref="IGamificationRepository"/>. Transactions are owned by UnitOfWorkBehavior.
/// </summary>
internal sealed class LeagueService : BaseResponseHandler, ILeagueService
{
    private readonly IGamificationRepository _repo;
    private readonly ISystemClock _clock;
    private readonly IStudentLeagueQuery _leagueQuery;
    private readonly IOptions<LeagueOptions> _options;
    private readonly ILeagueLeaderboard _leaderboard;
    private readonly ILoggerManager _logger;

    public LeagueService(
        IGamificationRepository repo,
        ISystemClock clock,
        IStudentLeagueQuery leagueQuery,
        IOptions<LeagueOptions> options,
        ILeagueLeaderboard leaderboard,
        ILoggerManager logger)
    {
        _repo        = repo;
        _clock       = clock;
        _leagueQuery = leagueQuery;
        _options     = options;
        _leaderboard = leaderboard;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<Unit>> IncrementLeagueXpAsync(
        IncrementLeagueXpCommand request,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Week period key derived from the EVENT timestamp ────────────────────────
            var (periodKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, request.OccurredAtUtc);

            // ── 2. Resolve profile ────────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(request.StudentId, ct);
            if (profile is null)
            {
                _logger.LogWarn(
                    $"P4-07: IncrementLeagueXp for studentId={request.StudentId} — " +
                    $"no profile yet, skipping.");
                return Success(Unit.Value);
            }

            // ── 3. Load membership (tracked — mutations must flow to EF change tracker) ───
            var membership = await _repo.GetMembershipForStudentTrackedAsync(profile.Id, periodKey, ct);
            if (membership is null)
            {
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

            // ── 5. Mutate membership + write idempotency log ──────────────────────────────
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

            // ── 6. Mirror post-mutation WeeklyXp to Redis sorted-set leaderboard ─────────
            await _leaderboard.UpsertMembershipAsync(
                leagueId:     membership.LeagueId,
                membershipId: membership.Id,
                weeklyXp:     membership.WeeklyXp,
                joinOrder:    membership.JoinOrder,
                ct:           ct);

            return Success(Unit.Value);
        }
        catch (GamificationUniqueConstraintException ucEx)
            when (ucEx.ConstraintHint.Contains("UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId"))
        {
            _logger.LogInfo(
                $"P4-07: unique-constraint race on LeagueXpDeltaLogs for " +
                $"studentId={request.StudentId} originEventId={request.OriginEventId} — " +
                $"already applied.");
            return Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-07: Unexpected error in LeagueService.IncrementLeagueXpAsync " +
                $"studentId={request.StudentId} originEventId={request.OriginEventId}.");
            return ServerError<Unit>();
        }
    }

    /// <inheritdoc />
    public async Task<BaseResponse<MyLeagueResponse>> GetMyLeagueAsync(
        int studentId,
        CancellationToken ct = default)
    {
        try
        {
            // ── 1. Trigger lazy placement via the cross-module seam ──────────────────────────
            var snapshot = await _leagueQuery.GetByStudentIdAsync(studentId, ct);

            // ── 2. Brand-new student (GroupSize == 0): return empty response ─────────────────
            if (snapshot.GroupSize == 0)
            {
                var (emptyKey, emptyStart, emptyEnd) = MissionPeriodCalculator.GetCurrentPeriod(
                    MissionType.Weekly, _clock.UtcNow.ToUniversalTime());

                return Success(new MyLeagueResponse(
                    Tier:                 LeagueTierDto.Bronze,
                    PeriodKey:            emptyKey,
                    PeriodStartUtc:       emptyStart,
                    PeriodEndUtc:         emptyEnd,
                    MyRank:               0,
                    MyWeeklyXp:           0,
                    Standings:            Array.Empty<LeagueStandingDto>(),
                    PromotionCutoffRank:  0,
                    DemotionCutoffRank:   0));
            }

            // ── 3. Resolve profile ────────────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(studentId, ct);
            if (profile is null)
            {
                _logger.LogWarn($"P4-07: GetMyLeagueAsync — no profile for studentId={studentId} " +
                                $"despite GroupSize={snapshot.GroupSize}.");
                return ServerError<MyLeagueResponse>();
            }

            // ── 4. Resolve membership ─────────────────────────────────────────────────────────
            var now = _clock.UtcNow.ToUniversalTime();
            var (periodKey, periodStart, periodEnd) =
                MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

            var membership = await _repo.GetMembershipForStudentAsync(profile.Id, periodKey, ct);
            if (membership is null)
            {
                _logger.LogWarn($"P4-07: GetMyLeagueAsync — membership not found for " +
                                $"studentId={studentId} periodKey={periodKey} after snapshot read.");
                return ServerError<MyLeagueResponse>();
            }

            // ── 5. Load all cohort members ordered by D4: WeeklyXp DESC, JoinedAtUtc ASC ────
            var allMembers = await _repo.GetMembershipsByLeagueIdAsync(membership.LeagueId, ct);

            // ── 6. Build standings ────────────────────────────────────────────────────────────
            var standings = allMembers
                .Select((m, i) => new LeagueStandingDto(
                    Rank:        i + 1,
                    DisplayName: $"Student #{m.JoinOrder}",      // D2: anonymized; no PII
                    WeeklyXp:    m.WeeklyXp,
                    IsMe:        m.StudentXpProfileId == profile.Id))
                .ToList();

            // ── 7. Compute promotion/demotion cutlines ────────────────────────────────────────
            var opts = _options.Value;
            var tier = membership.League.Tier;
            var plan = LeagueStandings.ComputeCutoffs(
                allMembers.Count,
                tier,
                opts.PromoteCount,
                opts.DemoteCount);

            int promotionCutoff = plan.PromoteCount > 0 ? plan.PromoteCount : 0;
            int demotionCutoff = plan.DemoteCount > 0
                ? allMembers.Count - plan.DemoteCount + 1
                : 0;

            return Success(new MyLeagueResponse(
                Tier:                (LeagueTierDto)(int)tier,
                PeriodKey:           periodKey,
                PeriodStartUtc:      periodStart,
                PeriodEndUtc:        periodEnd,
                MyRank:              snapshot.CurrentRank,
                MyWeeklyXp:          snapshot.WeeklyXp,
                Standings:           standings,
                PromotionCutoffRank: promotionCutoff,
                DemotionCutoffRank:  demotionCutoff));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"P4-07: Error in LeagueService.GetMyLeagueAsync for studentId={studentId}.");
            return ServerError<MyLeagueResponse>();
        }
    }
}

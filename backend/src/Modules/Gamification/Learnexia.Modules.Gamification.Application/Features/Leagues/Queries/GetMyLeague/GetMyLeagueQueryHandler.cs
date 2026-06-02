using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;

/// <summary>
/// Handles <see cref="GetMyLeagueQuery"/> — returns the full cohort standings for the
/// JWT-resolved student (P4-07, AC3).
///
/// Algorithm:
///   1. Resolve StudentId from JWT via <c>ICurrentUserService</c>.
///   2. Call <see cref="IStudentLeagueQuery.GetByStudentIdAsync"/> — triggers lazy placement if
///      no membership exists for the current week, returns sentinel for brand-new students.
///   3. For brand-new students (GroupSize == 0): return an empty response.
///   4. Otherwise: re-fetch the current profile + membership, then load all cohort members
///      ordered WeeklyXp DESC / JoinedAtUtc ASC (D4).
///   5. Build <see cref="LeagueStandingDto"/> list using "Student #N" anonymization (D2).
///   6. Compute promotion/demotion cutlines from <see cref="LeagueOptions"/>.
///   7. Return <see cref="MyLeagueResponse"/>.
///
/// READ-ONLY query — no <c>UnitOfWorkBehavior</c> wrap, no <c>ValidationBehavior</c> run.
/// Mirrors <see cref="Missions.Queries.GetMyMissions.GetMyMissionsQueryHandler"/> shape.
/// </summary>
public sealed class GetMyLeagueQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMyLeagueQuery, BaseResponse<MyLeagueResponse>>
{
    private readonly IGamificationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentLeagueQuery _leagueQuery;
    private readonly IOptions<LeagueOptions> _options;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyLeagueQueryHandler(
        IGamificationRepository repo,
        ICurrentUserService currentUser,
        IStudentLeagueQuery leagueQuery,
        IOptions<LeagueOptions> options,
        ISystemClock clock,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _repo        = repo;
        _currentUser = currentUser;
        _leagueQuery = leagueQuery;
        _options     = options;
        _clock       = clock;
        _logger      = logger;
        _localizer   = localizer;
    }

    public async Task<BaseResponse<MyLeagueResponse>> Handle(
        GetMyLeagueQuery request, CancellationToken ct)
    {
        // Belt-and-suspenders auth guard — [Authorize] on the controller already blocks anonymous.
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<MyLeagueResponse>(_localizer[SharedResourcesKey.Unauthorized]);

        try
        {
            // ── 1. Trigger lazy placement via the cross-module seam ──────────────────────────────
            // GetByStudentIdAsync lazy-instantiates the membership if needed and returns the snapshot.
            var snapshot = await _leagueQuery.GetByStudentIdAsync(studentId, ct);

            // ── 2. Brand-new student (no profile yet, GroupSize == 0): return empty response ─────
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

            // ── 3. Resolve profile ────────────────────────────────────────────────────────────────
            var profile = await _repo.GetProfileByStudentIdAsync(studentId, ct);
            if (profile is null)
            {
                // Should not happen — snapshot.GroupSize > 0 implies a profile exists — but defensive.
                _logger.LogWarn($"P4-07: GetMyLeagueQuery — no profile for studentId={studentId} " +
                                $"despite GroupSize={snapshot.GroupSize}.");
                return ServerError<MyLeagueResponse>();
            }

            // ── 4. Resolve membership ─────────────────────────────────────────────────────────────
            var now = _clock.UtcNow.ToUniversalTime();
            var (periodKey, periodStart, periodEnd) =
                MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

            var membership = await _repo.GetMembershipForStudentAsync(profile.Id, periodKey, ct);
            if (membership is null)
            {
                // Race: snapshot said GroupSize > 0 but membership vanished — extremely rare.
                _logger.LogWarn($"P4-07: GetMyLeagueQuery — membership not found for " +
                                $"studentId={studentId} periodKey={periodKey} after snapshot read.");
                return ServerError<MyLeagueResponse>();
            }

            // ── 5. Load all cohort members (ordered by D4: WeeklyXp DESC, JoinedAtUtc ASC) ────────
            var allMembers = await _repo.GetMembershipsByLeagueIdAsync(membership.LeagueId, ct);

            // ── 6. Build standings ────────────────────────────────────────────────────────────────
            var standings = allMembers
                .Select((m, i) => new LeagueStandingDto(
                    Rank:        i + 1,
                    DisplayName: $"Student #{m.JoinOrder}",      // D2: anonymized; no PII
                    WeeklyXp:    m.WeeklyXp,
                    IsMe:        m.StudentXpProfileId == profile.Id))
                .ToList();

            // ── 7. Compute promotion/demotion cutlines ────────────────────────────────────────────
            var opts = _options.Value;
            var tier = membership.League.Tier;
            var plan = LeagueStandings.ComputeCutoffs(
                allMembers.Count,
                tier,
                opts.PromoteCount,
                opts.DemoteCount);

            // Promotion cutoff rank: top-N (members at rank 1..N are promoted).
            // 0 when Diamond (no promotion) or no promotions (small cohort scaled to 0).
            int promotionCutoff = plan.PromoteCount > 0 ? plan.PromoteCount : 0;

            // Demotion cutoff rank: bottom-N (members at rank (cohortSize-DemoteCount+1)..cohortSize are demoted).
            // 0 when Bronze (no demotion) or no demotions.
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
            _logger.LogError(ex, $"P4-07: Error in GetMyLeagueQuery for studentId={studentId}.");
            return ServerError<MyLeagueResponse>();   // do NOT echo ex.Message to the client
        }
    }
}

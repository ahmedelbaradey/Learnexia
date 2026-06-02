using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Finds or creates the appropriate league cohort for a student at a given tier + period,
/// then inserts the <see cref="LeagueMembership"/> row.
///
/// <para><b>Find-or-create algorithm (first-fit)</b>: uses
/// <see cref="IGamificationRepository.FindCohortWithCapacityAsync"/> to locate the first cohort
/// at <c>(tier, periodKey)</c> that still has capacity. If no such cohort exists, creates a new
/// <see cref="League"/> with <c>GroupIndex = MAX(existing) + 1</c> and stages it via
/// <see cref="IGamificationRepository.AddLeagueAsync"/>.</para>
///
/// <para><b>Race handling (D12)</b>: the DB unique constraint
/// <c>UX_LeagueMemberships_StudentXpProfileId_PeriodKey</c> prevents one student from being
/// placed twice. Concurrent placements may both find the same near-full cohort and push it
/// slightly over capacity — accepted MVP risk (cohort overfill is bounded to +N concurrent
/// requests). Documented in HANDOFF.</para>
///
/// <para>Not pure-static — needs <see cref="IGamificationRepository"/> and
/// <see cref="LeagueOptions"/>. Registered Scoped in
/// <c>Gamification.Infrastructure.DependencyInjection</c>.</para>
///
/// <para>Mirrors how <c>BadgeSeeder</c> and <c>MissionSeeder</c> are domain-service classes with
/// DI injection. Not a Strategy/Factory pattern — plain service class (CLAUDE.md rule 8).</para>
/// </summary>
public sealed class LeaguePlacementService
{
    private readonly IGamificationRepository _repo;
    private readonly IOptions<LeagueOptions> _options;
    private readonly ILoggerManager _logger;

    public LeaguePlacementService(
        IGamificationRepository repo,
        IOptions<LeagueOptions> options,
        ILoggerManager logger)
    {
        _repo    = repo;
        _options = options;
        _logger  = logger;
    }

    /// <summary>
    /// Finds a cohort with capacity at the given <paramref name="tier"/> + <paramref name="periodKey"/>,
    /// or creates a new one. Inserts a <see cref="LeagueMembership"/> for the student.
    ///
    /// <para>The membership is staged but NOT committed — the caller owns the UoW commit
    /// (typically via <c>GamificationDbContext.SaveChangesAsync</c> in the query impl
    /// or the UoW behavior for commands).</para>
    ///
    /// <para>Concurrent placement race: the DB unique constraint is the authoritative backstop.
    /// If two concurrent requests both pick the same near-full cohort, the cohort may briefly
    /// exceed <c>CohortSize</c> by 1–2. Accepted for MVP (D12).</para>
    /// </summary>
    /// <param name="profile">The student's XP profile (must have Id &gt; 0 if persisted).</param>
    /// <param name="tier">The league tier to place the student in (read from profile.CurrentTier).</param>
    /// <param name="periodKey">ISO week key for the current period (e.g. "W:2026-23").</param>
    /// <param name="periodStartUtc">Monday 00:00 UTC — start of the competition period.</param>
    /// <param name="periodEndUtc">Next Monday 00:00 UTC — end of the competition period.</param>
    /// <param name="joinedAtUtc">Instant the membership is created (from clock.UtcNow).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The staged <see cref="LeagueMembership"/> (not yet committed).</returns>
    public async Task<LeagueMembership> PlaceStudentAsync(
        StudentXpProfile profile,
        LeagueTier tier,
        string periodKey,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateTime joinedAtUtc,
        CancellationToken ct)
    {
        var opts = _options.Value;

        // ── 1. Find a cohort with capacity ──────────────────────────────────────────────────────
        var league = await _repo.FindCohortWithCapacityAsync(tier, periodKey, opts.CohortSize, ct);

        if (league is null)
        {
            // No cohort with space — create a new one with the next group index.
            var maxIndex = await _repo.GetMaxGroupIndexAsync(tier, periodKey, ct);
            league = League.Create(
                tier:           tier,
                periodKey:      periodKey,
                periodStartUtc: periodStartUtc,
                periodEndUtc:   periodEndUtc,
                groupIndex:     maxIndex + 1,
                capacity:       opts.CohortSize);
            await _repo.AddLeagueAsync(league, ct);
            _logger.LogInfo(
                $"P4-07: created new league cohort tier={tier}, periodKey={periodKey}, " +
                $"groupIndex={maxIndex + 1}.");
        }
        else
        {
            // Graph-nav convention: attach so EF does not attempt a duplicate INSERT
            // for an entity that was loaded AsNoTracking (fifth instance of the pattern).
            _repo.AttachLeague(league);
        }

        // ── 2. Compute join order ────────────────────────────────────────────────────────────────
        // Newly created cohort (Id == 0 before SaveChanges): first member is order 1.
        // Existing cohort: count existing members + 1.
        int joinOrder = league.Id == 0
            ? 1
            : await _repo.GetNextJoinOrderAsync(league.Id, ct);

        // ── 3. Stage the membership row ─────────────────────────────────────────────────────────
        var membership = LeagueMembership.Create(
            league:      league,
            profile:     profile,
            periodKey:   periodKey,
            joinOrder:   joinOrder,
            joinedAtUtc: joinedAtUtc);

        await _repo.AddMembershipAsync(membership, ct);

        _logger.LogInfo(
            $"P4-07: staged membership for studentXpProfileId={profile.Id} in " +
            $"tier={tier} cohort (leagueId={league.Id}, joinOrder={joinOrder}).");

        return membership;
    }
}

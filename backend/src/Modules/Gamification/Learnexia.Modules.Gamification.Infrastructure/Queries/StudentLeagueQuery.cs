using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IStudentLeagueQuery"/> against <see cref="GamificationDbContext"/> — P4-07.
///
/// Resolves the student's <c>StudentXpProfile.Id</c> first, then:
///   1. Computes the current weekly period key via <see cref="MissionPeriodCalculator"/>.
///   2. Checks for an existing <c>LeagueMembership</c> for (profile.Id, periodKey).
///   3. If no membership → lazy-instantiates via <see cref="LeaguePlacementService"/> (first-fit cohort
///      find-or-create); commits in this scope. The unique index
///      <c>UX_LeagueMemberships_StudentXpProfileId_PeriodKey</c> prevents duplicate instantiation under
///      concurrent dashboard reads; a constraint violation catch re-reads (D12 race-accepted).
///   4. Computes rank as <c>1 + COUNT(members with WeeklyXp &gt; caller OR (tie + earlier joinedAt))</c>.
///   5. Returns <see cref="StudentLeagueSnapshot"/>.
///
/// Brand-new students (no profile row): returns sentinel
/// <c>(Tier: Bronze, WeeklyXp: 0, CurrentRank: 0, GroupSize: 0)</c> — never null (D13).
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public sealed class StudentLeagueQuery : IStudentLeagueQuery
{
    private readonly GamificationDbContext _db;
    private readonly LeaguePlacementService _placement;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    private static readonly StudentLeagueSnapshot Sentinel =
        new(Tier: LeagueTierDto.Bronze, WeeklyXp: 0, CurrentRank: 0, GroupSize: 0);

    public StudentLeagueQuery(
        GamificationDbContext db,
        LeaguePlacementService placement,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _db        = db;
        _placement = placement;
        _clock     = clock;
        _logger    = logger;
    }

    public async Task<StudentLeagueSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        // ── 1. Resolve StudentXpProfile ───────────────────────────────────────────────────────────
        var profile = await _db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

        // Brand-new student (no profile row) — return sentinel (D13).
        if (profile is null)
            return Sentinel;

        // ── 2. Compute current week period key ────────────────────────────────────────────────────
        var now = _clock.UtcNow.ToUniversalTime();
        var (periodKey, periodStart, periodEnd) =
            MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);

        // ── 3. Look up existing membership ────────────────────────────────────────────────────────
        var membership = await _db.LeagueMemberships
            .AsNoTracking()
            .Include(m => m.League)
            .FirstOrDefaultAsync(
                m => m.StudentXpProfileId == profile.Id && m.PeriodKey == periodKey, ct);

        if (membership is null)
        {
            // Lazy instantiation: load a tracked profile so EF can resolve FK navigation.
            var trackedProfile = await _db.StudentXpProfiles
                .FirstOrDefaultAsync(p => p.Id == profile.Id, ct);

            if (trackedProfile is null)
                return Sentinel;   // defensive: race-deleted between outer read and here

            try
            {
                await _placement.PlaceStudentAsync(
                    profile:        trackedProfile,
                    tier:           trackedProfile.CurrentTier,
                    periodKey:      periodKey,
                    periodStartUtc: periodStart,
                    periodEndUtc:   periodEnd,
                    joinedAtUtc:    now,
                    ct:             ct);

                // GamificationDbContext.SaveChangesAsync(int userId) stamps audit fields;
                // userId = 0 (system/lazy placement, no user context in query scope).
                await _db.SaveChangesAsync(0);

                _logger.LogInfo(
                    $"P4-07: StudentLeagueQuery — lazy-placed studentId={studentId} " +
                    $"tier={trackedProfile.CurrentTier} periodKey={periodKey}.");
            }
            catch (DbUpdateException dbEx)
                when (dbEx.InnerException?.Message?.Contains(
                          "UX_LeagueMemberships_StudentXpProfileId_PeriodKey") == true
                   || dbEx.InnerException?.Message?.Contains(
                          "UX_LeagueMemberships_LeagueId_StudentXpProfileId") == true)
            {
                // Concurrent placement race: another request just instantiated this membership.
                // The unique constraint won; re-read below will find the committed row.
                _logger.LogInfo(
                    $"P4-07: StudentLeagueQuery — concurrent placement race for " +
                    $"studentId={studentId} periodKey={periodKey} — re-reading.");
            }

            // Re-fetch the (now committed) membership — whether we inserted or the race beat us.
            membership = await _db.LeagueMemberships
                .AsNoTracking()
                .Include(m => m.League)
                .FirstOrDefaultAsync(
                    m => m.StudentXpProfileId == profile.Id && m.PeriodKey == periodKey, ct);

            if (membership is null)
            {
                // Both insert AND re-read failed — defensive/unexpected path.
                _logger.LogError(null,
                    $"P4-07: StudentLeagueQuery — failed to lazy-instantiate league membership " +
                    $"for studentId={studentId} periodKey={periodKey}.");
                return Sentinel;
            }
        }

        // ── 4. Compute rank and group size ────────────────────────────────────────────────────────
        // Rank = 1 + COUNT(members in same league that outrank me).
        // Tiebreak per D4: WeeklyXp DESC, JoinedAtUtc ASC (earlier joiner wins ties).
        int rank = await _db.LeagueMemberships
            .AsNoTracking()
            .Where(m => m.LeagueId == membership.LeagueId
                     && (m.WeeklyXp > membership.WeeklyXp
                      || (m.WeeklyXp == membership.WeeklyXp
                          && m.JoinedAtUtc < membership.JoinedAtUtc)))
            .CountAsync(ct) + 1;

        int groupSize = await _db.LeagueMemberships
            .AsNoTracking()
            .CountAsync(m => m.LeagueId == membership.LeagueId, ct);

        // ── 5. Return snapshot ────────────────────────────────────────────────────────────────────
        return new StudentLeagueSnapshot(
            Tier:         (LeagueTierDto)(int)membership.League.Tier,
            WeeklyXp:     membership.WeeklyXp,
            CurrentRank:  rank,
            GroupSize:    groupSize);
    }
}

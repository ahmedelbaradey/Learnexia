using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Repository;

/// <summary>
/// Gamification module repository. Deferred-commit: write methods stage changes only.
/// <c>SaveChangesAsync</c> is owned by <c>UnitOfWorkBehavior</c> (ADR 0001).
///
/// Mirrors <c>LearningRepository</c> shape exactly. Only the DbContext type and entity types differ.
/// </summary>
public sealed class GamificationRepository : IGamificationRepository
{
    private readonly GamificationDbContext _context;

    public GamificationRepository(GamificationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<StudentXpProfile?> GetProfileByStudentIdAsync(
        int studentId, CancellationToken ct = default)
        => await _context.StudentXpProfiles
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

    /// <inheritdoc />
    public async Task AcquireProfileLockAsync(int studentId, CancellationToken ct = default)
        => await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM gamification.\"StudentXpProfiles\" WHERE \"StudentId\" = {studentId} FOR UPDATE",
            ct);

    /// <inheritdoc />
    public async Task<bool> HasXpAwardAsync(
        Guid originEventId, XpReason reason, CancellationToken ct = default)
        => await _context.XpAwards
            .AsNoTracking()
            .AnyAsync(a => a.OriginEventId == originEventId && a.Reason == reason, ct);

    /// <inheritdoc />
    public async Task AddXpAwardAsync(XpAward award, CancellationToken ct = default)
        => await _context.XpAwards.AddAsync(award, ct);

    /// <inheritdoc />
    public void UpsertXpProfile(StudentXpProfile profile)
    {
        if (profile.Id == 0)
            _context.StudentXpProfiles.Add(profile);
        else
            _context.StudentXpProfiles.Update(profile);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task<List<StudentXpProfile>> GetBrokenProfilesAsync(
        DateOnly threshold, CancellationToken ct = default)
        => await _context.StudentXpProfiles
            .Where(p => p.CurrentStreak > 0 && p.LastActivityDateUtc < threshold)
            .Take(1000)
            .ToListAsync(ct);

    // ---------------------------------------------------------------------------
    // Hearts (P4-04-B2a-4)
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<bool> HasHeartLossAsync(Guid originEventId, CancellationToken ct = default)
        => await _context.HeartLosses
            .AsNoTracking()
            .AnyAsync(h => h.OriginEventId == originEventId, ct);

    /// <inheritdoc />
    public async Task AddHeartLossAsync(HeartLoss heartLoss, CancellationToken ct = default)
        => await _context.HeartLosses.AddAsync(heartLoss, ct);

    // ---------------------------------------------------------------------------
    // Badges (P4-05)
    // ---------------------------------------------------------------------------

    // ── P7-13 Admin overrides ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<StudentXpProfile?> GetProfileByStudentIdTrackedAsync(
        int studentId, CancellationToken ct = default)
        => await _context.StudentXpProfiles
            .FirstOrDefaultAsync(p => p.StudentId == studentId, ct);

    /// <inheritdoc />
    public async Task<BadgeDefinition?> GetBadgeDefinitionByIdAsync(int id, CancellationToken ct = default)
        => await _context.BadgeDefinitions
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc />
    public async Task<MissionDefinition?> GetMissionDefinitionByIdAsync(int id, CancellationToken ct = default)
        => await _context.MissionDefinitions
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    /// <inheritdoc />
    public async Task<TimedEvent?> GetTimedEventByIdAsync(int id, CancellationToken ct = default)
        => await _context.TimedEvents
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    /// <inheritdoc />
    public async Task<List<BadgeDefinition>> GetAllBadgeDefinitionsAdminAsync(CancellationToken ct = default)
        => await _context.BadgeDefinitions
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<MissionDefinition>> GetAllMissionDefinitionsAdminAsync(CancellationToken ct = default)
        => await _context.MissionDefinitions
            .AsNoTracking()
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<bool> BadgeCodeExistsAsync(string code, CancellationToken ct = default)
        => await _context.BadgeDefinitions
            .AsNoTracking()
            .AnyAsync(d => d.Code == code, ct);

    /// <inheritdoc />
    public async Task<bool> MissionCodeExistsAsync(string code, CancellationToken ct = default)
        => await _context.MissionDefinitions
            .AsNoTracking()
            .AnyAsync(d => d.Code == code, ct);

    /// <inheritdoc />
    public async Task<bool> TimedEventCodeExistsAsync(string code, CancellationToken ct = default)
        => await _context.TimedEvents
            .AsNoTracking()
            .AnyAsync(e => e.Code == code, ct);

    /// <inheritdoc />
    public async Task<List<BadgeDefinition>> GetAllBadgeDefinitionsAsync(CancellationToken ct = default)
        => await _context.BadgeDefinitions
            .AsNoTracking()
            .Where(d => d.IsActive)   // F1: deactivated badges must not be awarded going forward
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<BadgeDefinition>> GetBadgeDefinitionsByTriggerAsync(
        BadgeTriggerType triggerType, CancellationToken ct = default)
        => await _context.BadgeDefinitions
            .AsNoTracking()
            .Where(d => d.TriggerType == triggerType && d.IsActive)   // F1: active filter — deactivated badges must not be awarded
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<HashSet<int>> GetEarnedBadgeIdsAsync(int studentId, CancellationToken ct = default)
    {
        var ids = await _context.StudentBadges
            .AsNoTracking()
            .Where(b => b.StudentXpProfile.StudentId == studentId)
            .Select(b => b.BadgeDefinitionId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <inheritdoc />
    public async Task<bool> HasBadgeAsync(int studentXpProfileId, int badgeDefinitionId, CancellationToken ct = default)
        => await _context.StudentBadges
            .AsNoTracking()
            .AnyAsync(b => b.StudentXpProfileId == studentXpProfileId && b.BadgeDefinitionId == badgeDefinitionId, ct);

    /// <inheritdoc />
    public async Task AddStudentBadgeAsync(StudentBadge badge, CancellationToken ct = default)
        => await _context.StudentBadges.AddAsync(badge, ct);

    /// <inheritdoc />
    public async Task<List<StudentBadge>> GetStudentBadgesAsync(int studentId, CancellationToken ct = default)
        => await _context.StudentBadges
            .AsNoTracking()
            .Where(b => b.StudentXpProfile.StudentId == studentId)
            .Include(b => b.BadgeDefinition)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<StudentBadge>> GetRecentStudentBadgesAsync(int studentId, int take, CancellationToken ct = default)
        => await _context.StudentBadges
            .AsNoTracking()
            .Where(b => b.StudentXpProfile.StudentId == studentId)
            .Include(b => b.BadgeDefinition)
            .OrderByDescending(b => b.AwardedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    // ─────────────────────────── Seeder (P4-05) ───────────────────────────

    /// <inheritdoc />
    public async Task<BadgeDefinition?> GetBadgeDefinitionByCodeAsync(string code, CancellationToken ct = default)
        => await _context.BadgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code, ct);

    /// <inheritdoc />
    public async Task AddBadgeDefinitionAsync(BadgeDefinition definition, CancellationToken ct = default)
        => await _context.BadgeDefinitions.AddAsync(definition, ct);

    /// <inheritdoc />
    public void AttachBadgeDefinition(BadgeDefinition definition)
    {
        // Only attach if not already tracked — avoids InvalidOperationException on double-attach.
        var entry = _context.Entry(definition);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            _context.BadgeDefinitions.Attach(definition);
    }

    // ---------------------------------------------------------------------------
    // Missions (P4-06)
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<List<MissionDefinition>> GetAllMissionDefinitionsAsync(CancellationToken ct = default)
        => await _context.MissionDefinitions
            .AsNoTracking()
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<MissionDefinition>> GetMissionDefinitionsByCadenceAsync(
        MissionType cadence, CancellationToken ct = default)
        => await _context.MissionDefinitions
            .AsNoTracking()
            .Where(d => d.Cadence == cadence)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<MissionDefinition?> GetMissionDefinitionByCodeAsync(
        string code, CancellationToken ct = default)
        => await _context.MissionDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code, ct);

    /// <inheritdoc />
    public async Task AddMissionDefinitionAsync(MissionDefinition definition, CancellationToken ct = default)
        => await _context.MissionDefinitions.AddAsync(definition, ct);

    /// <inheritdoc />
    public void AttachMissionDefinition(MissionDefinition definition)
    {
        // Only attach if not already tracked — avoids InvalidOperationException on double-attach.
        // Mirrors AttachBadgeDefinition.
        var entry = _context.Entry(definition);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            _context.MissionDefinitions.Attach(definition);
    }

    /// <inheritdoc />
    public async Task<List<StudentMission>> GetActiveMissionsForStudentAsync(
        int studentId, CancellationToken ct = default)
        => await _context.StudentMissions
            .AsNoTracking()
            .Where(m => m.StudentXpProfile.StudentId == studentId
                     && (m.Status == MissionStatus.NotStarted
                      || m.Status == MissionStatus.InProgress))
            .Include(m => m.MissionDefinition)
            .OrderBy(m => m.MissionDefinition.SortOrder)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<StudentMission>> GetActiveMissionsForStudentByTargetTypeAsync(
        int studentId, MissionTargetType targetType, CancellationToken ct = default)
        => await _context.StudentMissions
            .AsNoTracking()
            .Where(m => m.StudentXpProfile.StudentId == studentId
                     && m.MissionDefinition.TargetType == targetType
                     && (m.Status == MissionStatus.NotStarted
                      || m.Status == MissionStatus.InProgress))
            .Include(m => m.MissionDefinition)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<StudentMission>> GetMissionsForStudentInPeriodAsync(
        int studentId, string periodKey, CancellationToken ct = default)
        => await _context.StudentMissions
            .AsNoTracking()
            .Where(m => m.StudentXpProfile.StudentId == studentId
                     && m.PeriodKey == periodKey)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<StudentMission?> GetMissionByIdAsync(
        int studentMissionId, CancellationToken ct = default)
        => await _context.StudentMissions
            .Include(m => m.MissionDefinition)
            .FirstOrDefaultAsync(m => m.Id == studentMissionId, ct);

    /// <inheritdoc />
    public async Task AddStudentMissionAsync(StudentMission mission, CancellationToken ct = default)
        => await _context.StudentMissions.AddAsync(mission, ct);

    /// <inheritdoc />
    public void AttachStudentMission(StudentMission mission)
    {
        // If the entity is already tracked (e.g., loaded via a tracked query in the same DbContext
        // lifetime), do nothing — re-attaching would throw InvalidOperationException.
        var entry = _context.Entry(mission);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            _context.StudentMissions.Attach(mission);

        // Always mark as Modified so that Progress / Status / CompletedAtUtc mutations
        // (internal set on CreationAuditedEntity-derived entity) are flushed on SaveChanges.
        if (entry.State != Microsoft.EntityFrameworkCore.EntityState.Added)
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
    }

    /// <inheritdoc />
    public async Task<bool> HasMissionProgressLogAsync(
        int studentMissionId, Guid originEventId, CancellationToken ct = default)
        => await _context.MissionProgressLogs
            .AsNoTracking()
            .AnyAsync(l => l.StudentMissionId == studentMissionId && l.OriginEventId == originEventId, ct);

    /// <inheritdoc />
    public async Task AddMissionProgressLogAsync(MissionProgressLog log, CancellationToken ct = default)
        => await _context.MissionProgressLogs.AddAsync(log, ct);

    // ---------------------------------------------------------------------------
    // Leagues (P4-07)
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<League?> FindCohortWithCapacityAsync(
        LeagueTier tier, string periodKey, int capacity, CancellationToken ct = default)
    {
        // Find the first cohort at (tier, periodKey) whose current member count < capacity.
        // Sub-query counts memberships per league and returns the first with room.
        var leagues = await _context.Leagues
            .AsNoTracking()
            .Where(l => l.Tier == tier && l.PeriodKey == periodKey)
            .OrderBy(l => l.GroupIndex)
            .ToListAsync(ct);

        foreach (var league in leagues)
        {
            int count = await _context.LeagueMemberships
                .AsNoTracking()
                .CountAsync(m => m.LeagueId == league.Id, ct);
            if (count < capacity)
                return league;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<int> GetMaxGroupIndexAsync(
        LeagueTier tier, string periodKey, CancellationToken ct = default)
    {
        var max = await _context.Leagues
            .AsNoTracking()
            .Where(l => l.Tier == tier && l.PeriodKey == periodKey)
            .MaxAsync(l => (int?)l.GroupIndex, ct);
        return max ?? 0;
    }

    /// <inheritdoc />
    public async Task<List<League>> GetCohortsForPeriodAsync(
        string periodKey, CancellationToken ct = default)
        => await _context.Leagues
            .AsNoTracking()
            .Where(l => l.PeriodKey == periodKey)
            .OrderBy(l => l.Tier)
            .ThenBy(l => l.GroupIndex)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddLeagueAsync(League league, CancellationToken ct = default)
        => await _context.Leagues.AddAsync(league, ct);

    /// <inheritdoc />
    public void AttachLeague(League league)
    {
        // Only attach if not already tracked — avoids InvalidOperationException on double-attach.
        // Fifth instance of the graph-nav convention (mirrors AttachBadgeDefinition pattern).
        var entry = _context.Entry(league);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            _context.Leagues.Attach(league);
    }

    /// <inheritdoc />
    public async Task<LeagueMembership?> GetMembershipForStudentAsync(
        int studentXpProfileId, string periodKey, CancellationToken ct = default)
        => await _context.LeagueMemberships
            .AsNoTracking()
            .Include(m => m.League)
            .FirstOrDefaultAsync(
                m => m.StudentXpProfileId == studentXpProfileId && m.PeriodKey == periodKey, ct);

    /// <inheritdoc />
    public async Task<LeagueMembership?> GetMembershipForStudentTrackedAsync(
        int studentXpProfileId, string periodKey, CancellationToken ct = default)
        => await _context.LeagueMemberships
            .FirstOrDefaultAsync(
                m => m.StudentXpProfileId == studentXpProfileId && m.PeriodKey == periodKey, ct);

    /// <inheritdoc />
    public async Task<List<LeagueMembership>> GetMembershipsByLeagueIdAsync(
        int leagueId, CancellationToken ct = default)
        => await _context.LeagueMemberships
            .AsNoTracking()
            .Where(m => m.LeagueId == leagueId)
            .OrderByDescending(m => m.WeeklyXp)
            .ThenBy(m => m.JoinedAtUtc)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<List<LeagueMembership>> GetMembershipsForRolloverAsync(
        string periodKey, CancellationToken ct = default)
        => await _context.LeagueMemberships
            .Include(m => m.League)
            .Include(m => m.StudentXpProfile)
            .Where(m => m.PeriodKey == periodKey)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<int> GetMembershipCountAsync(
        int leagueId, CancellationToken ct = default)
        => await _context.LeagueMemberships
            .AsNoTracking()
            .CountAsync(m => m.LeagueId == leagueId, ct);

    /// <inheritdoc />
    public async Task<int> GetNextJoinOrderAsync(
        int leagueId, CancellationToken ct = default)
    {
        int count = await _context.LeagueMemberships
            .AsNoTracking()
            .CountAsync(m => m.LeagueId == leagueId, ct);
        return count + 1;
    }

    /// <inheritdoc />
    public async Task AddMembershipAsync(LeagueMembership membership, CancellationToken ct = default)
        => await _context.LeagueMemberships.AddAsync(membership, ct);

    /// <inheritdoc />
    public async Task<bool> HasLeagueXpDeltaLogAsync(
        int leagueMembershipId, Guid originEventId, CancellationToken ct = default)
        => await _context.LeagueXpDeltaLogs
            .AsNoTracking()
            .AnyAsync(
                l => l.LeagueMembershipId == leagueMembershipId && l.OriginEventId == originEventId, ct);

    /// <inheritdoc />
    public async Task AddLeagueXpDeltaLogAsync(LeagueXpDeltaLog log, CancellationToken ct = default)
        => await _context.LeagueXpDeltaLogs.AddAsync(log, ct);

    /// <inheritdoc />
    public async Task<List<int>> GetDistinctTiersForPeriodAsync(
        string periodKey, CancellationToken ct = default)
        => await _context.Leagues
            .AsNoTracking()
            .Where(l => l.PeriodKey == periodKey)
            .Select(l => (int)l.Tier)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

    // ---------------------------------------------------------------------------
    // Timed Events (P4-11-B1-B)
    // ---------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimedEvent>> GetActiveTimedEventsAsync(
        DateTime nowUtc, CancellationToken ct = default)
        => await _context.TimedEvents
            .AsNoTracking()
            .Where(e => e.IsActive && e.StartUtc <= nowUtc && e.EndUtc > nowUtc)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimedEvent>> GetTimedEventsToActivateAsync(
        DateTime nowUtc, CancellationToken ct = default)
        => await _context.TimedEvents
            .Where(e => !e.IsActive && e.StartUtc <= nowUtc && e.EndUtc > nowUtc)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimedEvent>> GetTimedEventsToDeactivateAsync(
        DateTime nowUtc, CancellationToken ct = default)
        => await _context.TimedEvents
            .Where(e => e.IsActive && e.EndUtc <= nowUtc)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<TimedEvent?> GetTimedEventByCodeAsync(
        string code, CancellationToken ct = default)
        => await _context.TimedEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == code, ct);

    /// <inheritdoc />
    public async Task AddTimedEventAsync(TimedEvent timedEvent, CancellationToken ct = default)
        => await _context.TimedEvents.AddAsync(timedEvent, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimedEvent>> GetAllTimedEventsAsync(CancellationToken ct = default)
        => await _context.TimedEvents
            .AsNoTracking()
            .OrderByDescending(e => e.StartUtc)
            .Take(200)
            .ToListAsync(ct);
}

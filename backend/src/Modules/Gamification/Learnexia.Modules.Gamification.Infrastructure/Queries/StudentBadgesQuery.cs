using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IStudentBadgesQuery"/> against <see cref="GamificationDbContext"/> — P4-05.
///
/// Resolves the student's <c>StudentXpProfile.Id</c> first (badges are keyed by StudentXpProfileId),
/// then issues a count query and a recent-3 projection. Both reads use <c>AsNoTracking</c>.
///
/// Brand-new students (no profile row): returns sentinel <c>StudentBadgesSnapshot(0, [])</c> —
/// never null. Mirrors the <see cref="IStudentHeartsQuery"/> sentinel-snapshot pattern (D2 / D8).
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public sealed class StudentBadgesQuery : IStudentBadgesQuery
{
    private readonly GamificationDbContext _db;

    public StudentBadgesQuery(GamificationDbContext db)
    {
        _db = db;
    }

    public async Task<StudentBadgesSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        // Resolve the StudentXpProfile.Id for this student (badges FK to the profile, not student).
        var profileId = await _db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);

        // Brand-new student — return sentinel snapshot (zero badges, empty recent list).
        if (profileId is null)
            return new StudentBadgesSnapshot(0, Array.Empty<BadgeSummary>());

        int totalCount = await _db.StudentBadges
            .AsNoTracking()
            .CountAsync(b => b.StudentXpProfileId == profileId.Value, ct);

        if (totalCount == 0)
            return new StudentBadgesSnapshot(0, Array.Empty<BadgeSummary>());

        var recent = await _db.StudentBadges
            .AsNoTracking()
            .Include(b => b.BadgeDefinition)
            .Where(b => b.StudentXpProfileId == profileId.Value)
            .OrderByDescending(b => b.AwardedAtUtc)
            .Take(3)
            .Select(b => new BadgeSummary(
                b.BadgeDefinition.Code,
                b.BadgeDefinition.IconKey,
                (BadgeRarityDto)(int)b.BadgeDefinition.Rarity,
                b.AwardedAtUtc))
            .ToListAsync(ct);

        return new StudentBadgesSnapshot(totalCount, recent);
    }
}

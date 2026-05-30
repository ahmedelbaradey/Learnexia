using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IStudentXpQuery"/> against <see cref="GamificationDbContext"/>.
/// Used by the Learning dashboard handler via the <see cref="IStudentXpQuery"/> seam in
/// <c>Shared.Contracts</c> — no cross-module DbContext reference, no cross-module project reference.
///
/// Read-only: <c>AsNoTracking()</c>. Returns <c>null</c> when no profile exists yet (brand-new student).
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped (mirrors IParentChildQuery pattern).
/// </summary>
public sealed class StudentXpQuery : IStudentXpQuery
{
    private readonly GamificationDbContext _db;

    public StudentXpQuery(GamificationDbContext db)
    {
        _db = db;
    }

    public async Task<StudentXpSnapshot?> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        var profile = await _db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .Select(p => new { p.StudentId, p.TotalXp, p.CurrentLevel })
            .FirstOrDefaultAsync(ct);

        if (profile is null)
            return null;

        return new StudentXpSnapshot(profile.StudentId, profile.TotalXp, profile.CurrentLevel);
    }
}

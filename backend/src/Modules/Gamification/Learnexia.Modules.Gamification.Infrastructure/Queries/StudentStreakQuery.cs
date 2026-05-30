using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IStudentStreakQuery"/> against <see cref="GamificationDbContext"/>.
/// Used by the Learning dashboard handler via the <see cref="IStudentStreakQuery"/> seam in
/// <c>Shared.Contracts</c> — no cross-module DbContext reference, no cross-module project reference.
///
/// Read-only: <c>AsNoTracking()</c>. Returns <c>null</c> when no profile exists yet (brand-new student).
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped (mirrors <see cref="StudentXpQuery"/> pattern).
/// </summary>
public sealed class StudentStreakQuery : IStudentStreakQuery
{
    private readonly GamificationDbContext _db;

    public StudentStreakQuery(GamificationDbContext db)
    {
        _db = db;
    }

    public async Task<StudentStreakSnapshot?> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        var profile = await _db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .Select(p => new { p.CurrentStreak, p.LongestStreak, p.LastActivityDateUtc })
            .FirstOrDefaultAsync(ct);

        if (profile is null)
            return null;

        return new StudentStreakSnapshot(
            profile.CurrentStreak,
            profile.LongestStreak,
            profile.LastActivityDateUtc);
    }
}

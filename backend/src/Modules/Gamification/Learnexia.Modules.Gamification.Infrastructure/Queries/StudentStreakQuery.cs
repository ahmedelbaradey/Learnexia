using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IStudentStreakQuery"/>. Wrapped by
/// <c>CachedStudentStreakQuery</c> (P4-10 decorator) at DI composition root — registered directly
/// as concrete type, not as <see cref="IStudentStreakQuery"/>.
/// Read-only: <c>AsNoTracking()</c>. Returns <c>null</c> when no profile exists yet (brand-new student).
/// </summary>
internal sealed class PostgresStudentStreakQuery : IStudentStreakQuery
{
    private readonly GamificationDbContext _db;

    public PostgresStudentStreakQuery(GamificationDbContext db)
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

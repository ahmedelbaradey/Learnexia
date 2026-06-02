using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IStudentXpQuery"/>. Wrapped by
/// <c>CachedStudentXpQuery</c> (P4-10 decorator) at DI composition root — registered directly
/// as concrete type, not as <see cref="IStudentXpQuery"/>.
/// Read-only: <c>AsNoTracking()</c>. Returns <c>null</c> when no profile exists yet (brand-new student).
/// </summary>
internal sealed class PostgresStudentXpQuery : IStudentXpQuery
{
    private readonly GamificationDbContext _db;

    public PostgresStudentXpQuery(GamificationDbContext db)
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

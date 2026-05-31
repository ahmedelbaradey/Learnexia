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
}

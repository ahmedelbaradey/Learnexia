using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IStudentTimedEventParticipationQuery"/> — P4-12.
///
/// Returns participation snapshots for a student's active events (window not yet closed).
/// Joins <c>TimedEventParticipations</c> to <c>TimedEvents</c> to resolve the Code field for
/// FE deep-links. Uses <c>AsNoTracking</c> (read-only projection). Never returns null.
/// </summary>
internal sealed class StudentTimedEventParticipationQuery : IStudentTimedEventParticipationQuery
{
    private readonly GamificationDbContext _db;

    public StudentTimedEventParticipationQuery(GamificationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TimedEventParticipationSnapshot>> GetActiveByStudentIdAsync(
        int studentId,
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;

        var rows = await _db.TimedEventParticipations
            .AsNoTracking()
            .Where(p => p.StudentXpProfile.StudentId == studentId
                     && p.EventEndUtc > nowUtc)
            .Join(_db.TimedEvents,
                p => p.TimedEventId,
                e => e.Id,
                (p, e) => new TimedEventParticipationSnapshot(
                    TimedEventId: p.TimedEventId,
                    Code:         e.Code,
                    Progress:     p.Progress,
                    Target:       p.Target,
                    Status:       (TimedEventParticipationStatusDto)(int)p.Status,
                    JoinedUtc:    p.JoinedUtc,
                    CompletedUtc: p.CompletedUtc,
                    EventEndUtc:  p.EventEndUtc))
            .ToListAsync(ct);

        return rows;
    }
}

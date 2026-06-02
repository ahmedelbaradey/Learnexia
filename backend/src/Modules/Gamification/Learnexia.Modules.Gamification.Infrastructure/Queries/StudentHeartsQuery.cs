using Learnexia.Modules.Gamification.Application.Common;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IStudentHeartsQuery"/> — P4-04. Wrapped by
/// <c>CachedStudentHeartsQuery</c> (P4-10 decorator) at DI composition root.
///
/// Persist-on-read (D5 / Q1.bis): when the lazy refiller detects a positive delta, the query
/// updates the <c>StudentXpProfiles</c> row via <c>ExecuteUpdateAsync</c> with an optimistic
/// WHERE-guard on <c>LastHeartRefillAtUtc</c>.
///
/// Brand-new students (no profile row): returns a sentinel snapshot with full hearts (D8 / Q10).
/// </summary>
internal sealed class PostgresStudentHeartsQuery : IStudentHeartsQuery
{
    private readonly GamificationDbContext _db;
    private readonly ISystemClock _clock;
    private readonly IOptions<HeartsOptions> _heartsOptions;

    public PostgresStudentHeartsQuery(
        GamificationDbContext db,
        ISystemClock clock,
        IOptions<HeartsOptions> heartsOptions)
    {
        _db = db;
        _clock = clock;
        _heartsOptions = heartsOptions;
    }

    public async Task<StudentHeartsSnapshot> GetByStudentIdAsync(
        int studentId, CancellationToken ct = default)
    {
        var opts = _heartsOptions.Value;
        var now = _clock.UtcNow;

        // Load profile with change tracking (required for persist-on-read).
        var profile = await _db.StudentXpProfiles
            .Where(p => p.StudentId == studentId)
            .FirstOrDefaultAsync(ct);

        // Brand-new student — return sentinel snapshot (full hearts, clock idle, not in Practice Mode).
        if (profile is null)
        {
            return new StudentHeartsSnapshot(
                Hearts: GamificationConstants.Hearts.Cap,
                Cap: opts.Cap,
                LastHeartRefillAtUtc: null,
                NextRefillAtUtc: null,
                InPracticeMode: false);
        }

        // Compute lazy refill.
        var (newHearts, newLastRefill, regenerated) = LazyHeartRefiller.Compute(
            profile.Hearts, opts.Cap, profile.LastHeartRefillAtUtc, now, opts.RefillIntervalMinutes);

        if (regenerated > 0)
        {
            // Persist-on-read with optimistic WHERE-guard on LastHeartRefillAtUtc to avoid
            // clobbering a concurrent write that already advanced the clock.
            // Race condition: two simultaneous reads could both compute +1 heart. The cap
            // (Math.Min in LazyHeartRefiller) and the WHERE-guard bound the worst case to a
            // temporary over-refill by at most 1 heart — strictly better UX than a row-lock
            // on a dashboard read. Documented trade-off (D5 / Q1.bis).
            var oldLastRefill = profile.LastHeartRefillAtUtc;

            await _db.StudentXpProfiles
                .Where(p => p.StudentId == studentId && p.LastHeartRefillAtUtc == oldLastRefill)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Hearts, newHearts)
                    .SetProperty(p => p.LastHeartRefillAtUtc, newLastRefill),
                    ct);
        }

        // Compute next refill time for the snapshot.
        DateTime? nextRefillAt = newHearts < opts.Cap && newLastRefill.HasValue
            ? newLastRefill.Value.AddMinutes(opts.RefillIntervalMinutes)
            : null;

        return new StudentHeartsSnapshot(
            Hearts: newHearts,
            Cap: opts.Cap,
            LastHeartRefillAtUtc: newLastRefill,
            NextRefillAtUtc: nextRefillAt,
            InPracticeMode: newHearts == 0);
    }
}

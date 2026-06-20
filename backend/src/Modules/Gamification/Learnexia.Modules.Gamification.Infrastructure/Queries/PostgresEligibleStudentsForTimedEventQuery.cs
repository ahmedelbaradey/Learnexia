using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IEligibleStudentsForTimedEventQuery"/> — P4-12.
///
/// "Eligible" = students whose <c>StudentXpProfile.LastActivityDateUtc</c> falls within
/// <c>TimedEventParticipationOptions.EligibilityWindowDays</c> of <paramref name="atUtc"/>.
/// This reuses the same in-module activity signal already scanned by <c>StreakAtRiskJob</c>;
/// no new cross-module coupling is required (module isolation rule 1 preserved).
///
/// Bounded by <c>Take(500)</c> page guard (MVP scale). When the count reaches 500, a WARN is
/// logged for operator attention. Returns an empty list (never null) when none qualify.
/// </summary>
internal sealed class PostgresEligibleStudentsForTimedEventQuery : IEligibleStudentsForTimedEventQuery
{
    private readonly GamificationDbContext _db;
    private readonly IOptions<TimedEventParticipationOptions> _options;
    private readonly ILoggerManager _logger;

    public PostgresEligibleStudentsForTimedEventQuery(
        GamificationDbContext db,
        IOptions<TimedEventParticipationOptions> options,
        ILoggerManager logger)
    {
        _db      = db;
        _options = options;
        _logger  = logger;
    }

    public async Task<IReadOnlyList<int>> GetEligibleAsync(
        int timedEventId,
        DateTime atUtc,
        CancellationToken ct = default)
    {
        var cutoffDate = DateOnly.FromDateTime(atUtc.AddDays(-_options.Value.EligibilityWindowDays));

        var studentIds = await _db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.LastActivityDateUtc != null && p.LastActivityDateUtc >= cutoffDate)
            .Select(p => p.StudentId)
            .Take(500)
            .ToListAsync(ct);

        if (studentIds.Count == 500)
        {
            _logger.LogInfo(
                $"P4-12: IEligibleStudentsForTimedEventQuery — page guard hit (count=500) " +
                $"for timedEventId={timedEventId}. Consider cursor-based pagination when student count grows.");
        }

        return studentIds;
    }
}

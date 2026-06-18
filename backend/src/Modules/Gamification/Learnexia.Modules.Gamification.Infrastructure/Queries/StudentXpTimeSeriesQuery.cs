using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IStudentXpTimeSeriesQuery"/>.
/// Reads from the <c>XpAward</c> append-only ledger and <c>StudentXpProfile</c> snapshot.
/// Pure read: <c>AsNoTracking()</c> throughout. Never writes. Sentinel-safe.
/// </summary>
public sealed class PostgresStudentXpTimeSeriesQuery : IStudentXpTimeSeriesQuery
{
    private readonly GamificationDbContext _db;

    public PostgresStudentXpTimeSeriesQuery(GamificationDbContext db)
    {
        _db = db;
    }

    // ── GetProfileSnapshotAsync ───────────────────────────────────────────────────

    public async Task<XpProfileSnapshot> GetProfileSnapshotAsync(
        int studentId,
        CancellationToken ct = default)
    {
        var profile = await _db.StudentXpProfiles
            .AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .Select(p => new { p.TotalXp, p.CurrentLevel, p.CurrentStreak, p.LongestStreak })
            .FirstOrDefaultAsync(ct);

        if (profile is null)
            return new XpProfileSnapshot(TotalXp: 0, CurrentLevel: 1, CurrentStreak: 0, BestStreak: 0);

        return new XpProfileSnapshot(
            TotalXp: profile.TotalXp,
            CurrentLevel: profile.CurrentLevel,
            CurrentStreak: profile.CurrentStreak,
            BestStreak: profile.LongestStreak);
    }

    // ── GetDailySeriesAsync ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DailyXpEntry>> GetDailySeriesAsync(
        int studentId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);

        // Sum XP per day from the ledger. Group by UTC date only.
        var rows = await _db.XpAwards
            .AsNoTracking()
            .Where(a => a.StudentXpProfile.StudentId == studentId
                     && a.OccurredAtUtc >= fromUtc
                     && a.OccurredAtUtc < toUtcExclusive)
            .GroupBy(a => a.OccurredAtUtc.Date)
            .Select(g => new { Day = g.Key, Xp = g.Sum(a => a.XpAmount) })
            .ToListAsync(ct);

        // Build a dense day-by-day series (zero-fill gaps).
        var dict = rows.ToDictionary(r => DateOnly.FromDateTime(r.Day), r => r.Xp);
        var result = new List<DailyXpEntry>();
        for (var d = fromDate; d <= toDate; d = d.AddDays(1))
            result.Add(new DailyXpEntry(d, dict.TryGetValue(d, out var xp) ? xp : 0));

        return result;
    }

    // ── GetTwentyDayTrendAsync ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DailyXpEntry>> GetTwentyDayTrendAsync(
        int studentId,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        var fromDate = asOfDate.AddDays(-19); // 20 days inclusive: asOfDate-19 .. asOfDate
        return await GetDailySeriesAsync(studentId, fromDate, asOfDate, ct);
    }

    // ── GetTimeOfDayBucketsAsync ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<TimeOfDayXpBucket>> GetTimeOfDayBucketsAsync(
        int studentId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        var fromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = toDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(1);

        // Pull the XP + hour for all awards in the window.
        var awards = await _db.XpAwards
            .AsNoTracking()
            .Where(a => a.StudentXpProfile.StudentId == studentId
                     && a.OccurredAtUtc >= fromUtc
                     && a.OccurredAtUtc < toUtcExclusive)
            .Select(a => new { Hour = a.OccurredAtUtc.Hour, a.XpAmount })
            .ToListAsync(ct);

        // Bucket UTC hour ranges:
        //   Morning   05–11  (hour >= 5  && hour < 12)
        //   Afternoon 12–16  (hour >= 12 && hour < 17)
        //   Evening   17–20  (hour >= 17 && hour < 21)
        //   Night     21–04  (hour >= 21 || hour < 5)
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Morning"]   = 0,
            ["Afternoon"] = 0,
            ["Evening"]   = 0,
            ["Night"]     = 0,
        };

        foreach (var a in awards)
        {
            var key = a.Hour switch
            {
                >= 5  and < 12 => "Morning",
                >= 12 and < 17 => "Afternoon",
                >= 17 and < 21 => "Evening",
                _              => "Night",
            };
            buckets[key] += a.XpAmount;
        }

        return new[]
        {
            new TimeOfDayXpBucket("Morning",   buckets["Morning"]),
            new TimeOfDayXpBucket("Afternoon", buckets["Afternoon"]),
            new TimeOfDayXpBucket("Evening",   buckets["Evening"]),
            new TimeOfDayXpBucket("Night",     buckets["Night"]),
        };
    }
}

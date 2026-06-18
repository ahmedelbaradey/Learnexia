using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Queries;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Gamification.UnitTests;

/// <summary>
/// Unit tests for <see cref="PostgresStudentXpTimeSeriesQuery"/> (<see cref="IStudentXpTimeSeriesQuery"/>).
/// Runs against SQLite in-memory — mirrors the Billing module test harness pattern.
///
/// Coverage:
///   XP-01  GetProfileSnapshotAsync → no profile row → zeroed sentinel (level=1, all zeros).
///   XP-02  GetProfileSnapshotAsync → profile exists → all fields populated.
///   XP-03  GetDailySeriesAsync → empty window → all-zero entries for each day in range.
///   XP-04  GetDailySeriesAsync → awards in window → summed per day, zero-filled gaps.
///   XP-05  GetDailySeriesAsync → awards outside window → not counted.
///   XP-06  GetDailySeriesAsync → result is ordered ascending by date.
///   XP-07  GetTwentyDayTrendAsync → always returns exactly 20 entries.
///   XP-08  GetTwentyDayTrendAsync → zero-filled when no history.
///   XP-09  GetTimeOfDayBucketsAsync → always returns exactly 4 entries.
///   XP-10  GetTimeOfDayBucketsAsync → hour=8 (morning), hour=14 (afternoon), hour=19 (evening), hour=22 (night).
///   XP-11  GetTimeOfDayBucketsAsync → zero buckets when no awards in window.
///   XP-12  GetDailySeriesAsync → another student's awards not counted (isolation).
/// </summary>
public sealed class StudentXpTimeSeriesQueryTests
{
    // ── SQLite helper ─────────────────────────────────────────────────────────────

    private static GamificationDbContext BuildDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new GamificationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static PostgresStudentXpTimeSeriesQuery BuildQuery(GamificationDbContext db)
        => new(db);

    // ── Seed helpers ──────────────────────────────────────────────────────────────

    private static async Task<StudentXpProfile> SeedProfileAsync(
        GamificationDbContext db,
        int studentId,
        int totalXp       = 0,
        int currentLevel  = 1,
        int currentStreak = 0,
        int longestStreak = 0)
    {
        var profile = new StudentXpProfile
        {
            StudentId      = studentId,
            TotalXp        = totalXp,
            CurrentLevel   = currentLevel,
            LastAwardAtUtc = DateTime.UtcNow,
        };
        // Use internal setters via reflection — streak fields are internal to the domain entity.
        typeof(StudentXpProfile)
            .GetProperty(nameof(StudentXpProfile.CurrentStreak))!
            .SetValue(profile, currentStreak);
        typeof(StudentXpProfile)
            .GetProperty(nameof(StudentXpProfile.LongestStreak))!
            .SetValue(profile, longestStreak);

        db.StudentXpProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }

    private static async Task SeedAwardAsync(
        GamificationDbContext db,
        StudentXpProfile profile,
        int xpAmount,
        DateTime occurredAtUtc)
    {
        var award = new XpAward
        {
            StudentXpProfile = profile,
            Reason           = XpReason.LessonCompleted,
            XpAmount         = xpAmount,
            OccurredAtUtc    = occurredAtUtc,
            OriginEventId    = Guid.NewGuid(),
        };
        db.XpAwards.Add(award);
        await db.SaveChangesAsync();
    }

    // ── XP-01: No profile → zeroed sentinel ──────────────────────────────────────

    [Fact]
    public async Task GetProfileSnapshotAsync_NoProfile_ReturnsZeroedSentinel()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        var result = await query.GetProfileSnapshotAsync(studentId: 999);

        result.TotalXp.Should().Be(0);
        result.CurrentLevel.Should().Be(1);
        result.CurrentStreak.Should().Be(0);
        result.BestStreak.Should().Be(0);
    }

    // ── XP-02: Profile exists → all fields populated ─────────────────────────────

    [Fact]
    public async Task GetProfileSnapshotAsync_ProfileExists_FieldsPopulated()
    {
        var db      = BuildDb();
        await SeedProfileAsync(db, studentId: 1,
            totalXp: 500, currentLevel: 5, currentStreak: 7, longestStreak: 14);
        var query = BuildQuery(db);

        var result = await query.GetProfileSnapshotAsync(studentId: 1);

        result.TotalXp.Should().Be(500);
        result.CurrentLevel.Should().Be(5);
        result.CurrentStreak.Should().Be(7);
        result.BestStreak.Should().Be(14);
    }

    // ── XP-03: Empty window → all-zero entries ────────────────────────────────────

    [Fact]
    public async Task GetDailySeriesAsync_NoAwards_AllZeroEntriesForEachDay()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);
        var from  = new DateOnly(2025, 6, 1);
        var to    = new DateOnly(2025, 6, 7);

        var result = await query.GetDailySeriesAsync(studentId: 10, from, to);

        result.Should().HaveCount(7);
        result.Should().AllSatisfy(e => e.Xp.Should().Be(0));
    }

    // ── XP-04: Awards in window → summed per day, gaps zero-filled ────────────────

    [Fact]
    public async Task GetDailySeriesAsync_AwardsInWindow_SummedPerDay()
    {
        var db      = BuildDb();
        var profile = await SeedProfileAsync(db, studentId: 2);
        var day1    = new DateTime(2025, 6, 3, 10, 0, 0, DateTimeKind.Utc);
        await SeedAwardAsync(db, profile, 50, day1);
        await SeedAwardAsync(db, profile, 30, day1.AddHours(2));  // same day — should sum
        var day2 = new DateTime(2025, 6, 5, 14, 0, 0, DateTimeKind.Utc);
        await SeedAwardAsync(db, profile, 20, day2);

        var query  = BuildQuery(db);
        var result = await query.GetDailySeriesAsync(studentId: 2,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 7));

        result.Should().HaveCount(7);
        result.Single(e => e.Day == new DateOnly(2025, 6, 3)).Xp.Should().Be(80);
        result.Single(e => e.Day == new DateOnly(2025, 6, 5)).Xp.Should().Be(20);
        result.Single(e => e.Day == new DateOnly(2025, 6, 4)).Xp.Should().Be(0); // gap day
    }

    // ── XP-05: Awards outside window → not counted ────────────────────────────────

    [Fact]
    public async Task GetDailySeriesAsync_AwardOutsideWindow_NotCounted()
    {
        var db      = BuildDb();
        var profile = await SeedProfileAsync(db, studentId: 3);
        // Before window
        await SeedAwardAsync(db, profile, 100,
            new DateTime(2025, 5, 31, 23, 59, 59, DateTimeKind.Utc));

        var query  = BuildQuery(db);
        var result = await query.GetDailySeriesAsync(studentId: 3,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 3));

        result.Should().AllSatisfy(e => e.Xp.Should().Be(0));
    }

    // ── XP-06: Result is ordered ascending ────────────────────────────────────────

    [Fact]
    public async Task GetDailySeriesAsync_ResultIsOrderedAscending()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);
        var from  = new DateOnly(2025, 6, 1);
        var to    = new DateOnly(2025, 6, 5);

        var result = await query.GetDailySeriesAsync(studentId: 50, from, to);

        var days = result.Select(e => e.Day).ToList();
        days.Should().BeInAscendingOrder();
    }

    // ── XP-07: GetTwentyDayTrendAsync → exactly 20 entries ───────────────────────

    [Fact]
    public async Task GetTwentyDayTrendAsync_AlwaysReturnsTwentyEntries()
    {
        var db     = BuildDb();
        var query  = BuildQuery(db);
        var asOf   = new DateOnly(2025, 6, 15);

        var result = await query.GetTwentyDayTrendAsync(studentId: 60, asOf);

        result.Should().HaveCount(20);
    }

    // ── XP-08: GetTwentyDayTrendAsync → zero-filled when no history ───────────────

    [Fact]
    public async Task GetTwentyDayTrendAsync_NoHistory_AllZero()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        var result = await query.GetTwentyDayTrendAsync(
            studentId: 70,
            new DateOnly(2025, 6, 15));

        result.Should().AllSatisfy(e => e.Xp.Should().Be(0));
    }

    // ── XP-09: GetTimeOfDayBucketsAsync → exactly 4 entries ──────────────────────

    [Fact]
    public async Task GetTimeOfDayBucketsAsync_AlwaysReturnsFourEntries()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        var result = await query.GetTimeOfDayBucketsAsync(
            studentId: 80,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 7));

        result.Should().HaveCount(4);
        result.Select(e => e.Bucket).Should()
            .BeEquivalentTo(["Morning", "Afternoon", "Evening", "Night"]);
    }

    // ── XP-10: Hour bucket boundaries ────────────────────────────────────────────

    [Fact]
    public async Task GetTimeOfDayBucketsAsync_HourBoundaries_CountedInCorrectBuckets()
    {
        var db      = BuildDb();
        var profile = await SeedProfileAsync(db, studentId: 4);
        var day     = new DateOnly(2025, 6, 10);

        // Morning: 08:00
        await SeedAwardAsync(db, profile, 10, new DateTime(2025, 6, 10, 8,  0, 0, DateTimeKind.Utc));
        // Afternoon: 14:00
        await SeedAwardAsync(db, profile, 20, new DateTime(2025, 6, 10, 14, 0, 0, DateTimeKind.Utc));
        // Evening: 19:00
        await SeedAwardAsync(db, profile, 30, new DateTime(2025, 6, 10, 19, 0, 0, DateTimeKind.Utc));
        // Night: 22:00
        await SeedAwardAsync(db, profile, 40, new DateTime(2025, 6, 10, 22, 0, 0, DateTimeKind.Utc));

        var query  = BuildQuery(db);
        var result = await query.GetTimeOfDayBucketsAsync(studentId: 4, day, day);

        result.Single(b => b.Bucket == "Morning").TotalXp.Should().Be(10);
        result.Single(b => b.Bucket == "Afternoon").TotalXp.Should().Be(20);
        result.Single(b => b.Bucket == "Evening").TotalXp.Should().Be(30);
        result.Single(b => b.Bucket == "Night").TotalXp.Should().Be(40);
    }

    // ── XP-11: Zero buckets when no awards ───────────────────────────────────────

    [Fact]
    public async Task GetTimeOfDayBucketsAsync_NoAwards_AllZeroBuckets()
    {
        var db    = BuildDb();
        var query = BuildQuery(db);

        var result = await query.GetTimeOfDayBucketsAsync(
            studentId: 90,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 7));

        result.Should().AllSatisfy(b => b.TotalXp.Should().Be(0));
    }

    // ── XP-12: Another student's awards not counted ───────────────────────────────

    [Fact]
    public async Task GetDailySeriesAsync_OtherStudentAwards_NotCounted()
    {
        var db       = BuildDb();
        var profile5 = await SeedProfileAsync(db, studentId: 5);
        var profile6 = await SeedProfileAsync(db, studentId: 6);

        var within = new DateTime(2025, 6, 3, 10, 0, 0, DateTimeKind.Utc);
        await SeedAwardAsync(db, profile6, 100, within); // only student 6 has an award

        var query  = BuildQuery(db);
        var result = await query.GetDailySeriesAsync(studentId: 5,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 5));

        result.Should().AllSatisfy(e => e.Xp.Should().Be(0));
    }
}

using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Queries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Gamification.UnitTests;

/// <summary>
/// Unit tests for <see cref="PlatformEngagementQueryAdapter"/> (P7-10).
/// Runs against SQLite in-memory — mirrors the StudentXpTimeSeriesQueryTests pattern.
///
/// Coverage:
///   PE-01  EmptyDb_ReturnsAllZeroes
///   PE-02  XpEarnedInWindow_SumsXpAmountInWindow
///   PE-03  XpOutsideWindow_Excluded
///   PE-04  MultipleStudents_XpAggregatesAcrossAll
///   PE-05  MissionsCompleted_CountsStatusCompletedInWindow
///   PE-06  MissionsCompleted_OutsideWindow_Excluded
///   PE-07  MissionsInProgress_NotCounted
/// </summary>
public sealed class PlatformEngagementQueryAdapterTests
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

    private static PlatformEngagementQueryAdapter BuildSut(GamificationDbContext db)
        => new(db);

    private static readonly DateTime Base = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime From = Base;
    private static readonly DateTime To   = Base.AddDays(30);

    // ── Seed helpers (mirror StudentXpTimeSeriesQueryTests pattern) ───────────────

    private static async Task<StudentXpProfile> SeedProfileAsync(GamificationDbContext db, int studentId)
    {
        var profile = new StudentXpProfile
        {
            StudentId      = studentId,
            TotalXp        = 0,
            CurrentLevel   = 1,
            LastAwardAtUtc = DateTime.UtcNow,
        };
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

    private static async Task<(StudentXpProfile, MissionDefinition)> SeedMissionPrereqsAsync(
        GamificationDbContext db, int studentId)
    {
        var profile = await SeedProfileAsync(db, studentId);

        var def = MissionDefinition.Create(
            code:       $"MISSION_{studentId}",
            iconKey:    "icon",
            titleKey:   "title",
            cadence:    MissionType.Daily,
            targetType: MissionTargetType.CompleteLessons,
            target:     3,
            rewardXp:   100);

        db.MissionDefinitions.Add(def);
        await db.SaveChangesAsync();
        return (profile, def);
    }

    private static async Task SeedCompletedMissionAsync(
        GamificationDbContext db,
        StudentXpProfile profile,
        MissionDefinition def,
        DateTime completedAtUtc,
        string periodKey)
    {
        // Create using factory then mutate to Completed using the domain method
        var mission = StudentMission.Create(
            profile:         profile,
            definition:      def,
            periodKey:       periodKey,
            periodStartUtc:  completedAtUtc.Date,
            periodEndUtc:    completedAtUtc.Date.AddDays(1));

        // Drive to completed via domain method (the only correct way to set CompletedAtUtc)
        mission.ApplyProgress(def.Target, completedAtUtc, out _);

        db.StudentMissions.Add(mission);
        await db.SaveChangesAsync();
    }

    // ── PE-01: empty database → zeroed stats ───────────────────────────────────

    [Fact]
    public async Task PE_01_EmptyDb_ReturnsAllZeroes()
    {
        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.GetPlatformAsync(From, To);

        result.MissionsCompleted.Should().Be(0);
        result.XpEarnedInWindow.Should().Be(0L);
    }

    // ── PE-02: XP earned in window sums correctly ─────────────────────────────

    [Fact]
    public async Task PE_02_XpEarnedInWindow_SumsXpAmountInWindow()
    {
        await using var db = BuildDb();

        var p1 = await SeedProfileAsync(db, studentId: 10);
        var p2 = await SeedProfileAsync(db, studentId: 11);

        await SeedAwardAsync(db, p1, 200, Base.AddDays(1));
        await SeedAwardAsync(db, p2, 150, Base.AddDays(10));
        await SeedAwardAsync(db, p1, 50,  Base.AddDays(20));

        var sut = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.XpEarnedInWindow.Should().Be(400L); // 200 + 150 + 50
    }

    // ── PE-03: XP outside window excluded ─────────────────────────────────────

    [Fact]
    public async Task PE_03_XpOutsideWindow_Excluded()
    {
        await using var db = BuildDb();

        var p = await SeedProfileAsync(db, studentId: 10);

        await SeedAwardAsync(db, p, 300, From.AddDays(-1)); // before From
        await SeedAwardAsync(db, p, 100, Base.AddDays(5));  // inside window
        await SeedAwardAsync(db, p, 500, To.AddDays(1));    // after To (exclusive)

        var sut = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.XpEarnedInWindow.Should().Be(100L);
    }

    // ── PE-04: multiple students — XP aggregates across all ───────────────────

    [Fact]
    public async Task PE_04_MultipleStudents_XpAggregatesAcrossAll()
    {
        await using var db = BuildDb();

        var p1 = await SeedProfileAsync(db, studentId: 10);
        var p2 = await SeedProfileAsync(db, studentId: 20);
        var p3 = await SeedProfileAsync(db, studentId: 30);

        await SeedAwardAsync(db, p1, 100, Base.AddDays(1));
        await SeedAwardAsync(db, p2, 200, Base.AddDays(2));
        await SeedAwardAsync(db, p3, 300, Base.AddDays(3));

        var sut = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.XpEarnedInWindow.Should().Be(600L);
    }

    // ── PE-05: missions completed in window counted ────────────────────────────

    [Fact]
    public async Task PE_05_MissionsCompleted_CountsStatusCompletedInWindow()
    {
        await using var db = BuildDb();

        var (p1, def1) = await SeedMissionPrereqsAsync(db, studentId: 10);
        var (p2, def2) = await SeedMissionPrereqsAsync(db, studentId: 11);

        await SeedCompletedMissionAsync(db, p1, def1, Base.AddDays(5),  periodKey: "D:2025-06-06");
        await SeedCompletedMissionAsync(db, p2, def2, Base.AddDays(15), periodKey: "D:2025-06-16");

        var sut = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.MissionsCompleted.Should().Be(2);
    }

    // ── PE-06: missions completed outside window excluded ─────────────────────

    [Fact]
    public async Task PE_06_MissionsCompleted_OutsideWindow_Excluded()
    {
        await using var db = BuildDb();

        var (p1, def1) = await SeedMissionPrereqsAsync(db, studentId: 10);
        var (p2, def2) = await SeedMissionPrereqsAsync(db, studentId: 11);

        // before window
        await SeedCompletedMissionAsync(db, p1, def1, From.AddDays(-5),  periodKey: "D:before");
        // after window
        await SeedCompletedMissionAsync(db, p2, def2, To.AddDays(1),     periodKey: "D:after");

        var sut = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.MissionsCompleted.Should().Be(0);
    }

    // ── PE-07: in-progress missions not counted ────────────────────────────────

    [Fact]
    public async Task PE_07_MissionsInProgress_NotCounted()
    {
        await using var db = BuildDb();

        var (profile, def) = await SeedMissionPrereqsAsync(db, studentId: 10);

        // Only apply partial progress (1 out of 3) — not completed
        var mission = StudentMission.Create(
            profile:        profile,
            definition:     def,
            periodKey:      "D:2025-06-10",
            periodStartUtc: Base.AddDays(9),
            periodEndUtc:   Base.AddDays(10));
        mission.ApplyProgress(1, Base.AddDays(9), out _); // partial — stays InProgress

        db.StudentMissions.Add(mission);
        await db.SaveChangesAsync();

        var sut = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.MissionsCompleted.Should().Be(0);
    }
}

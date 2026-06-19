using FluentAssertions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Modules.Ai.Infrastructure.Safety;
using Microsoft.EntityFrameworkCore;
using Xunit;
#pragma warning disable CA1707

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="PlatformAiSafetyStatsQueryAdapter"/> (P7-10).
/// Uses EF In-Memory provider — mirrors the pattern in SafetyLayerTests / AiCacheActivationTests.
///
/// Coverage:
///   AS-01  EmptyDb_ReturnsAllZeroes
///   AS-02  BlockedEvents_CountedAsBlocked
///   AS-03  OtherActions_CountedAsFlagged
///   AS-04  OutsideWindow_Excluded
///   AS-05  MixedActions_BlockedAndFlaggedCorrect
///   AS-06  AiRequestVolumeNaReason_IsNotNull
///   AS-07  TotalSafetyEvents_IsBlockedPlusFlagged
/// </summary>
public sealed class PlatformAiSafetyStatsQueryAdapterTests
{
    // ── InMemory helper (pattern: AiCacheActivationTests) ─────────────────────

    private static AiDbContext BuildDb()
    {
        var opts = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AiDbContext(opts);
    }

    private static PlatformAiSafetyStatsQueryAdapter BuildSut(AiDbContext db)
        => new(db);

    private static readonly DateTime Base = new(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime From = Base;
    private static readonly DateTime To   = Base.AddDays(30);

    private static SafetyEvent MakeEvent(int id, string actionTaken, DateTime occurredAtUtc)
        => new SafetyEvent
        {
            Id           = id,
            StudentId    = 10,
            TaskKind     = "Explain",
            FailedChecks = "[]",
            ReasonCodes  = "[]",
            ActionTaken  = actionTaken,
            ModelId      = "test-model",
            OccurredAtUtc = occurredAtUtc,
        };

    // ── AS-01: empty database → all zeroes ────────────────────────────────────

    [Fact]
    public async Task AS_01_EmptyDb_ReturnsAllZeroes()
    {
        await using var db  = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.GetPlatformAsync(From, To);

        result.TotalSafetyEvents.Should().Be(0);
        result.BlockedCount.Should().Be(0);
        result.FlaggedCount.Should().Be(0);
    }

    // ── AS-02: "Blocked" events counted as blocked ─────────────────────────────

    [Fact]
    public async Task AS_02_BlockedEvents_CountedAsBlocked()
    {
        await using var db = BuildDb();

        db.SafetyEvents.AddRange(
            MakeEvent(1, "Blocked", Base.AddDays(1)),
            MakeEvent(2, "Blocked", Base.AddDays(5)));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.BlockedCount.Should().Be(2);
        result.FlaggedCount.Should().Be(0);
        result.TotalSafetyEvents.Should().Be(2);
    }

    // ── AS-03: non-"Blocked" actions counted as flagged ───────────────────────

    [Fact]
    public async Task AS_03_OtherActions_CountedAsFlagged()
    {
        await using var db = BuildDb();

        db.SafetyEvents.AddRange(
            MakeEvent(1, "Regenerated",      Base.AddDays(1)),
            MakeEvent(2, "FallbackReturned", Base.AddDays(2)));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.BlockedCount.Should().Be(0);
        result.FlaggedCount.Should().Be(2);
        result.TotalSafetyEvents.Should().Be(2);
    }

    // ── AS-04: events outside window excluded ─────────────────────────────────

    [Fact]
    public async Task AS_04_OutsideWindow_Excluded()
    {
        await using var db = BuildDb();

        db.SafetyEvents.AddRange(
            MakeEvent(1, "Blocked", From.AddDays(-1)),  // before From
            MakeEvent(2, "Blocked", Base.AddDays(5)),   // inside window
            MakeEvent(3, "Blocked", To.AddDays(1)));    // after To (exclusive)
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.TotalSafetyEvents.Should().Be(1);
        result.BlockedCount.Should().Be(1);
    }

    // ── AS-05: mixed actions — blocked + flagged correct ──────────────────────

    [Fact]
    public async Task AS_05_MixedActions_BlockedAndFlaggedCorrect()
    {
        await using var db = BuildDb();

        db.SafetyEvents.AddRange(
            MakeEvent(1, "Blocked",          Base.AddDays(1)),
            MakeEvent(2, "Blocked",          Base.AddDays(2)),
            MakeEvent(3, "Regenerated",      Base.AddDays(3)),
            MakeEvent(4, "FallbackReturned", Base.AddDays(4)));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.BlockedCount.Should().Be(2);
        result.FlaggedCount.Should().Be(2);
        result.TotalSafetyEvents.Should().Be(4);
    }

    // ── AS-06: AI request volume N/A reason is set ────────────────────────────

    [Fact]
    public async Task AS_06_AiRequestVolumeNaReason_IsNotNull()
    {
        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.GetPlatformAsync(From, To);

        result.AiRequestVolumeNaReason.Should().NotBeNullOrWhiteSpace()
            .And.Contain("N/A");
    }

    // ── AS-07: TotalSafetyEvents = BlockedCount + FlaggedCount ───────────────

    [Fact]
    public async Task AS_07_TotalSafetyEvents_IsBlockedPlusFlagged()
    {
        await using var db = BuildDb();

        db.SafetyEvents.AddRange(
            MakeEvent(1, "Blocked",          Base.AddDays(1)),
            MakeEvent(2, "Regenerated",      Base.AddDays(2)),
            MakeEvent(3, "Blocked",          Base.AddDays(3)),
            MakeEvent(4, "FallbackReturned", Base.AddDays(4)));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.TotalSafetyEvents.Should().Be(result.BlockedCount + result.FlaggedCount);
    }
}

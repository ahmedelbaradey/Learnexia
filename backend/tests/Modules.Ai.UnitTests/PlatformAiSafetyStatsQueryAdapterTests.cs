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
///   AS-06  AiRequestVolume_CountsUsageLogsInWindow_ReasonNull
///   AS-07  TotalSafetyEvents_IsBlockedPlusFlagged
///   AS-08  NoUsageLogs_VolumeZero_ReasonStillNull
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

    private static AiUsageLog MakeUsage(int id, DateTime occurredAtUtc)
        => new AiUsageLog
        {
            Id               = id,
            Provider         = "Claude",
            ModelId          = "test-model",
            TaskKind         = "Explain",
            PromptTokens     = 100,
            CompletionTokens = 50,
            EstimatedCostUsd = 0.001m,
            LatencyMs        = 200,
            WasCacheHit      = false,
            StudentId        = null,
            OccurredAtUtc    = occurredAtUtc,
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

    // ── AS-06: AI request volume is real (from ai.AiUsageLogs); N/A reason is null ──

    [Fact]
    public async Task AS_06_AiRequestVolume_CountsUsageLogsInWindow_ReasonNull()
    {
        await using var db = BuildDb();

        // 2 in-window + 1 before From + 1 after To (exclusive) → only 2 counted.
        db.AiUsageLogs.AddRange(
            MakeUsage(1, Base.AddDays(2)),
            MakeUsage(2, Base.AddDays(7)),
            MakeUsage(3, From.AddDays(-1)),
            MakeUsage(4, To.AddDays(1)));
        await db.SaveChangesAsync();

        var sut    = BuildSut(db);
        var result = await sut.GetPlatformAsync(From, To);

        result.AiRequestVolume.Should().Be(2);
        // Data is available now (ai.AiUsageLogs exists) → no N/A marker.
        result.AiRequestVolumeNaReason.Should().BeNull();
    }

    // ── AS-08: empty AiUsageLogs → volume 0, still not N/A (0 is a real answer) ──

    [Fact]
    public async Task AS_08_NoUsageLogs_VolumeZero_ReasonStillNull()
    {
        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.GetPlatformAsync(From, To);

        result.AiRequestVolume.Should().Be(0);
        result.AiRequestVolumeNaReason.Should().BeNull();
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

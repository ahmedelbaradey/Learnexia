using FluentAssertions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Modules.Ai.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="AiTutorUsageService"/> (P7-11 usage/cost dashboard read model).
///
/// Uses EF InMemory database — same approach as <see cref="AiSafetyDashboardServiceTests"/>.
///
/// Coverage:
///   US-01  Empty window → zeroed TutorUsageDto (HTTP 200, never 404).
///   US-02  Single row → correct totals (calls, tokens, cost, latency, cache-hit rate).
///   US-03  Multiple rows → correct aggregate totals.
///   US-04  OccurredAtUtc filter — rows outside window are excluded.
///   US-05  ByModel breakdown — grouping and ordering by Calls descending.
///   US-06  ByTaskKind breakdown — grouping and ordering by Calls descending.
///   US-07  Trend — per-day buckets ordered ascending by date.
///   US-08  CacheHitRate — proportion computed correctly.
///   US-09  AvgLatencyMs — average computed correctly.
/// </summary>
public sealed class AiTutorUsageServiceTests : IDisposable
{
    // ── InMemory EF setup ───────────────────────────────────────────────────────

    private readonly AiDbContext _db;
    private readonly AiTutorUsageService _sut;

    public AiTutorUsageServiceTests()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db  = new AiDbContext(options);
        _sut = new AiTutorUsageService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static AiUsageLog MakeLog(
        string provider,
        string modelId,
        string taskKind,
        int promptTokens,
        int completionTokens,
        decimal estimatedCostUsd,
        long latencyMs,
        bool wasCacheHit,
        DateTime occurredAtUtc)
        => new()
        {
            Provider          = provider,
            ModelId           = modelId,
            TaskKind          = taskKind,
            PromptTokens      = promptTokens,
            CompletionTokens  = completionTokens,
            EstimatedCostUsd  = estimatedCostUsd,
            LatencyMs         = latencyMs,
            WasCacheHit       = wasCacheHit,
            OccurredAtUtc     = occurredAtUtc,
            StudentId         = null,
        };

    private static readonly DateTime Base = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── US-01: Empty window → zeroed summary ─────────────────────────────────────

    [Fact(DisplayName = "P711-US-01 Empty window → zeroed TutorUsageDto with empty breakdown lists")]
    public async Task GetUsageSummaryAsync_EmptyWindow_ReturnsZeroedDto()
    {
        // Arrange — no rows in DB; window covers any range.
        var from = Base;
        var to   = Base.AddDays(30);

        // Act
        var result = await _sut.GetUsageSummaryAsync(from, to);

        // Assert
        result.TotalCalls.Should().Be(0);
        result.TotalPromptTokens.Should().Be(0);
        result.TotalCompletionTokens.Should().Be(0);
        result.TotalEstimatedCostUsd.Should().Be(0m);
        result.AvgLatencyMs.Should().Be(0d);
        result.CacheHitRate.Should().Be(0d);
        result.ByModel.Should().BeEmpty();
        result.ByTaskKind.Should().BeEmpty();
        result.Trend.Should().BeEmpty();
    }

    // ── US-02: Single row → correct totals ────────────────────────────────────────

    [Fact(DisplayName = "P711-US-02 Single row → correct TotalCalls, tokens, cost, latency, cache-hit rate")]
    public async Task GetUsageSummaryAsync_SingleRow_CorrectTotals()
    {
        // Arrange
        _db.AiUsageLogs.Add(MakeLog(
            provider: "Claude",
            modelId: "claude-haiku-4-5",
            taskKind: "Explain",
            promptTokens: 100,
            completionTokens: 50,
            estimatedCostUsd: 0.000150m,
            latencyMs: 1200,
            wasCacheHit: true,
            occurredAtUtc: Base.AddHours(1)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert
        result.TotalCalls.Should().Be(1);
        result.TotalPromptTokens.Should().Be(100);
        result.TotalCompletionTokens.Should().Be(50);
        result.TotalEstimatedCostUsd.Should().Be(0.000150m);
        result.AvgLatencyMs.Should().BeApproximately(1200d, 0.01d);
        result.CacheHitRate.Should().BeApproximately(1.0d, 0.001d,
            "single cache-hit row → 100% cache hit rate");
    }

    // ── US-03: Multiple rows → correct aggregate totals ───────────────────────────

    [Fact(DisplayName = "P711-US-03 Multiple rows → correct aggregate totals")]
    public async Task GetUsageSummaryAsync_MultipleRows_CorrectAggregates()
    {
        // Arrange — 3 rows: 2 cache misses, 1 cache hit.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain",  100, 50, 0.0001m, 800,  false, Base.AddHours(1)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",     200, 80, 0.0002m, 1200, false, Base.AddHours(2)),
            MakeLog("OpenAi", "gpt-4o-mini",      "Explain",   50, 30, 0.0003m, 600,  true,  Base.AddHours(3)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert
        result.TotalCalls.Should().Be(3);
        result.TotalPromptTokens.Should().Be(350,
            "100 + 200 + 50 = 350 prompt tokens");
        result.TotalCompletionTokens.Should().Be(160,
            "50 + 80 + 30 = 160 completion tokens");
        result.TotalEstimatedCostUsd.Should().BeApproximately(0.0006m, 0.000001m,
            "0.0001 + 0.0002 + 0.0003 = 0.0006");
        result.CacheHitRate.Should().BeApproximately(1.0 / 3.0, 0.001d,
            "1 of 3 rows was a cache hit → ~33.3%");
    }

    // ── US-04: OccurredAtUtc filter — rows outside window excluded ────────────────

    [Fact(DisplayName = "P711-US-04 OccurredAtUtc filter — rows outside window are excluded")]
    public async Task GetUsageSummaryAsync_Filter_ExcludesRowsOutsideWindow()
    {
        // Arrange — row inside window and row outside window.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 100, 50, 0.0001m, 800, false, Base.AddHours(12)),
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 200, 80, 0.0002m, 900, false, Base.AddDays(5)));
        await _db.SaveChangesAsync();

        // Act — window covers only the first row.
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert
        result.TotalCalls.Should().Be(1,
            "only the row within the window must be counted");
        result.TotalPromptTokens.Should().Be(100);
    }

    // ── US-05: ByModel breakdown — grouping and ordering ──────────────────────────

    [Fact(DisplayName = "P711-US-05 ByModel breakdown grouped and ordered by Calls descending")]
    public async Task GetUsageSummaryAsync_ByModel_CorrectGroupingAndOrdering()
    {
        // Arrange — 3 Claude rows + 1 OpenAi row.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 100, 50, 0.0001m, 800, false, Base.AddHours(1)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",    200, 80, 0.0002m, 900, false, Base.AddHours(2)),
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 150, 60, 0.0003m, 700, false, Base.AddHours(3)),
            MakeLog("OpenAi", "gpt-4o-mini",      "Explain",  50, 20, 0.0001m, 600, true,  Base.AddHours(4)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert — two model groups; claude first (3 calls), openai second (1 call).
        result.ByModel.Should().HaveCount(2);
        result.ByModel[0].ModelId.Should().Be("claude-haiku-4-5",
            "claude-haiku-4-5 has more calls and should appear first");
        result.ByModel[0].Calls.Should().Be(3);
        result.ByModel[0].TotalTokens.Should().Be((100 + 50) + (200 + 80) + (150 + 60),
            "total tokens = prompt + completion for all claude rows");
        result.ByModel[1].ModelId.Should().Be("gpt-4o-mini");
        result.ByModel[1].Calls.Should().Be(1);
    }

    // ── US-06: ByTaskKind breakdown ───────────────────────────────────────────────

    [Fact(DisplayName = "P711-US-06 ByTaskKind breakdown grouped and ordered by Calls descending")]
    public async Task GetUsageSummaryAsync_ByTaskKind_CorrectGroupingAndOrdering()
    {
        // Arrange — 2 Explain + 1 Hint.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 100, 50, 0.0001m, 800, false, Base.AddHours(1)),
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 200, 80, 0.0002m, 900, false, Base.AddHours(2)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",    150, 60, 0.0003m, 700, false, Base.AddHours(3)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert
        result.ByTaskKind.Should().HaveCount(2);
        result.ByTaskKind[0].TaskKind.Should().Be("Explain", "Explain has more calls");
        result.ByTaskKind[0].Calls.Should().Be(2);
        result.ByTaskKind[1].TaskKind.Should().Be("Hint");
        result.ByTaskKind[1].Calls.Should().Be(1);
    }

    // ── US-07: Trend — per-day buckets ordered ascending ─────────────────────────

    [Fact(DisplayName = "P711-US-07 Trend returns per-day buckets ordered ascending by date")]
    public async Task GetUsageSummaryAsync_Trend_CorrectBucketsAscending()
    {
        // Arrange — two rows on day 1, one row on day 3.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 100, 50, 0.0001m, 800, false, Base.AddHours(1)),
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 200, 80, 0.0002m, 900, false, Base.AddHours(6)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",    150, 60, 0.0003m, 700, false, Base.AddDays(2).AddHours(4)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(5));

        // Assert — two buckets: day 1 (2 calls), day 3 (1 call).
        result.Trend.Should().HaveCount(2,
            "rows on day 1 and day 3 → two buckets (day 2 is empty and not emitted)");
        result.Trend[0].Calls.Should().Be(2, "two rows on day 1");
        result.Trend[0].TotalEstimatedCostUsd.Should().BeApproximately(0.0003m, 0.000001m);
        result.Trend[1].Calls.Should().Be(1, "one row on day 3");

        // Buckets must be ordered ascending by date.
        result.Trend[0].Date.Should().BeBefore(result.Trend[1].Date,
            "trend buckets must be ordered ascending by date");
    }

    // ── US-08: CacheHitRate ───────────────────────────────────────────────────────

    [Fact(DisplayName = "P711-US-08 CacheHitRate — correctly computed as proportion of cache-hit calls")]
    public async Task GetUsageSummaryAsync_CacheHitRate_CorrectProportion()
    {
        // Arrange — 2 cache hits out of 4 total → 50%.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 100, 50, 0.0001m, 800, true,  Base.AddHours(1)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",    200, 80, 0.0002m, 900, false, Base.AddHours(2)),
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 150, 60, 0.0003m, 700, true,  Base.AddHours(3)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",    120, 40, 0.0001m, 600, false, Base.AddHours(4)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert
        result.CacheHitRate.Should().BeApproximately(0.5d, 0.001d,
            "2 cache-hit rows out of 4 total → cache-hit rate = 0.5");
    }

    // ── US-09: AvgLatencyMs ────────────────────────────────────────────────────────

    [Fact(DisplayName = "P711-US-09 AvgLatencyMs — average computed correctly across all rows")]
    public async Task GetUsageSummaryAsync_AvgLatencyMs_CorrectAverage()
    {
        // Arrange — latency values: 1000, 2000, 3000 → avg = 2000.
        _db.AiUsageLogs.AddRange(
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 100, 50, 0.0001m, 1000, false, Base.AddHours(1)),
            MakeLog("Claude", "claude-haiku-4-5", "Hint",    200, 80, 0.0002m, 2000, false, Base.AddHours(2)),
            MakeLog("Claude", "claude-haiku-4-5", "Explain", 150, 60, 0.0003m, 3000, false, Base.AddHours(3)));
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetUsageSummaryAsync(Base, Base.AddDays(1));

        // Assert
        result.AvgLatencyMs.Should().BeApproximately(2000d, 0.1d,
            "average of 1000, 2000, 3000 = 2000 ms");
    }
}

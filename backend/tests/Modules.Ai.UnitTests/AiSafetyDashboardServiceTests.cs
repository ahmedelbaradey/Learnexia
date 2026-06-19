using FluentAssertions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Modules.Ai.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="AiSafetyDashboardService"/>.
///
/// Coverage:
///   DS-01  GetSummaryAsync — empty window returns zeroed summary.
///   DS-02  GetSummaryAsync — correct totals and action breakdown over a seeded window.
///   DS-03  GetSummaryAsync — in-memory jsonb reason code unnesting is correct.
///   DS-04  GetSummaryAsync — rates are computed correctly (two decimal places).
///   DS-05  GetFlaggedOutputsPagedAsync — filter by Action returns matching rows only.
///   DS-06  GetFlaggedOutputsPagedAsync — filter by ReasonCode returns matching rows.
///   DS-07  GetFlaggedOutputsPagedAsync — filter by TaskKind returns matching rows.
///   DS-08  GetFlaggedOutputsPagedAsync — pagination (page 2) returns the correct slice.
///   DS-09  GetFlaggedOutputsPagedAsync — empty result returns Success with TotalCount=0.
///   DS-10  GetTrendAsync — per-day bucket grouping is correct.
///   DS-11  GetTrendAsync — empty window returns empty list.
///   DS-12  GetSummaryAsync — malformed jsonb string does not throw (fail-soft).
///   DS-13  GetFlaggedOutputsPagedAsync — OccurredAtUtc filter (from/to) narrows results.
/// </summary>
public sealed class AiSafetyDashboardServiceTests : IDisposable
{
    // ── InMemory EF setup ───────────────────────────────────────────────────────

    private readonly AiDbContext _db;
    private readonly AiSafetyDashboardService _sut;

    public AiSafetyDashboardServiceTests()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db  = new AiDbContext(options);
        _sut = new AiSafetyDashboardService(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static SafetyEvent MakeEvent(
        string actionTaken,
        string taskKind,
        string modelId,
        string reasonCodes,
        DateTime occurredAtUtc,
        int studentId = 1)
        => new()
        {
            StudentId     = studentId,
            TaskKind      = taskKind,
            ActionTaken   = actionTaken,
            ModelId       = modelId,
            ReasonCodes   = reasonCodes,
            FailedChecks  = "[]",
            OccurredAtUtc = occurredAtUtc,
        };

    private static DateTime D(int day) => new DateTime(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    // ── DS-01 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_EmptyWindow_ReturnsZeroedSummary()
    {
        var result = await _sut.GetSummaryAsync(D(1), D(2));

        result.TotalEvents.Should().Be(0);
        result.BlockedCount.Should().Be(0);
        result.RegeneratedCount.Should().Be(0);
        result.FallbackReturnedCount.Should().Be(0);
        result.BreakdownByAction.Should().BeEmpty();
        result.BreakdownByReasonCode.Should().BeEmpty();
        result.BreakdownByModelId.Should().BeEmpty();
        result.BreakdownByTaskKind.Should().BeEmpty();
    }

    // ── DS-02 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_SeededEvents_CorrectTotalsAndActionBreakdown()
    {
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked",         "Explain", "claude-haiku", "[\"TOXICITY_HIGH\"]",   D(1)),
            MakeEvent("Blocked",         "Hint",    "claude-haiku", "[\"AGE_INAPPROPRIATE\"]", D(1)),
            MakeEvent("Regenerated",     "Explain", "gpt-4o",       "[\"TOXICITY_MODERATE\"]", D(2)),
            MakeEvent("FallbackReturned","Practice","claude-haiku",  "[\"GATEWAY_ERROR\"]",   D(3)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetSummaryAsync(D(1), D(4));

        result.TotalEvents.Should().Be(4);
        result.BlockedCount.Should().Be(2);
        result.RegeneratedCount.Should().Be(1);
        result.FallbackReturnedCount.Should().Be(1);

        // Action breakdown should list all three actions.
        result.BreakdownByAction.Should().HaveCount(3);
        result.BreakdownByAction.Should().ContainSingle(b => b.Label == "Blocked" && b.Count == 2);
        result.BreakdownByAction.Should().ContainSingle(b => b.Label == "Regenerated" && b.Count == 1);
        result.BreakdownByAction.Should().ContainSingle(b => b.Label == "FallbackReturned" && b.Count == 1);
    }

    // ── DS-03 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_ReasonCodes_InMemoryUnnestingIsCorrect()
    {
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked",     "Explain", "claude-haiku", "[\"TOXICITY_HIGH\",\"AGE_INAPPROPRIATE\"]", D(1)),
            MakeEvent("Regenerated", "Explain", "claude-haiku", "[\"TOXICITY_HIGH\"]",                     D(1)),
            MakeEvent("Blocked",     "Hint",    "claude-haiku", "[\"AGE_INAPPROPRIATE\"]",                 D(2)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetSummaryAsync(D(1), D(3));

        // TOXICITY_HIGH appears twice, AGE_INAPPROPRIATE twice.
        result.BreakdownByReasonCode.Should().HaveCount(2);
        result.BreakdownByReasonCode.Should().ContainSingle(b => b.Label == "TOXICITY_HIGH"     && b.Count == 2);
        result.BreakdownByReasonCode.Should().ContainSingle(b => b.Label == "AGE_INAPPROPRIATE" && b.Count == 2);
    }

    // ── DS-04 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_Rates_AreComputedToTwoDecimalPlaces()
    {
        // 1 Blocked out of 3 total → 33.33 %
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked",     "Explain", "m", "[]", D(1)),
            MakeEvent("Regenerated", "Explain", "m", "[]", D(1)),
            MakeEvent("Regenerated", "Explain", "m", "[]", D(1)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetSummaryAsync(D(1), D(2));

        result.BlockedRate.Should().BeApproximately(33.33, 0.01);
        result.RegeneratedRate.Should().BeApproximately(66.67, 0.01);
        result.FallbackReturnedRate.Should().Be(0d);
    }

    // ── DS-05 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedOutputsPagedAsync_FilterByAction_ReturnsMatchingRowsOnly()
    {
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked",     "Explain", "m", "[]", D(1)),
            MakeEvent("Regenerated", "Hint",    "m", "[]", D(2)),
            MakeEvent("Blocked",     "Practice","m", "[]", D(3)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetFlaggedOutputsPagedAsync(
            action: "Blocked", reasonCode: null, taskKind: null,
            from: null, to: null, pageNumber: 1, pageSize: 10);

        result.TotalCount.Should().Be(2);
        result.Data.Should().AllSatisfy(d => d.ActionTaken.Should().Be("Blocked"));
    }

    // ── DS-06 ────────────────────────────────────────────────────────────────────
    // NOTE: EF.Functions.JsonContains is a DB-native Npgsql jsonb operator (@>) that the
    // InMemory EF provider cannot translate. The reasonCode filter is therefore tested
    // against a real PostgreSQL instance by the integration test FLAGGED-6
    // (P7_11_AiSafetyDashboard_Tests.Flagged6_Filter_ReasonCode_ReturnsOnlyMatchingRows).
    // This unit test is intentionally skipped to avoid a provider-not-supported exception.
    [Fact(Skip = "EF.Functions.JsonContains requires a real PostgreSQL provider; covered by integration test FLAGGED-6")]
    public async Task GetFlaggedOutputsPagedAsync_FilterByReasonCode_ReturnsMatchingRowsOnly()
    {
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked", "Explain", "m", "[\"TOXICITY_HIGH\"]",    D(1)),
            MakeEvent("Blocked", "Hint",    "m", "[\"AGE_INAPPROPRIATE\"]", D(2)),
            MakeEvent("Blocked", "Explain", "m", "[\"TOXICITY_HIGH\",\"AGE_INAPPROPRIATE\"]", D(3)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetFlaggedOutputsPagedAsync(
            action: null, reasonCode: "TOXICITY_HIGH", taskKind: null,
            from: null, to: null, pageNumber: 1, pageSize: 10);

        // Events 1 and 3 contain TOXICITY_HIGH.
        result.TotalCount.Should().Be(2);
    }

    // ── DS-07 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedOutputsPagedAsync_FilterByTaskKind_ReturnsMatchingRowsOnly()
    {
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked", "Explain",  "m", "[]", D(1)),
            MakeEvent("Blocked", "Hint",     "m", "[]", D(2)),
            MakeEvent("Blocked", "Explain",  "m", "[]", D(3)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetFlaggedOutputsPagedAsync(
            action: null, reasonCode: null, taskKind: "Hint",
            from: null, to: null, pageNumber: 1, pageSize: 10);

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle(d => d.TaskKind == "Hint");
    }

    // ── DS-08 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedOutputsPagedAsync_Page2_ReturnsCorrectSlice()
    {
        // Seed 5 rows, page size 2 → page 2 should have rows 3+4 (newest-first ordering).
        for (var i = 1; i <= 5; i++)
            _db.SafetyEvents.Add(
                MakeEvent("Blocked", "Explain", "m", "[]",
                    new DateTime(2026, 6, i, 12, 0, 0, DateTimeKind.Utc)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetFlaggedOutputsPagedAsync(
            action: null, reasonCode: null, taskKind: null,
            from: null, to: null, pageNumber: 2, pageSize: 2);

        result.TotalCount.Should().Be(5);
        result.Data.Should().HaveCount(2);
        result.CurrentPage.Should().Be(2);
    }

    // ── DS-09 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedOutputsPagedAsync_EmptyResult_ReturnsSuccessWithZeroTotal()
    {
        var result = await _sut.GetFlaggedOutputsPagedAsync(
            action: "Blocked", reasonCode: null, taskKind: null,
            from: null, to: null, pageNumber: 1, pageSize: 20);

        result.TotalCount.Should().Be(0);
        result.Data.Should().BeEmpty();
        result.Successed.Should().BeTrue();
    }

    // ── DS-10 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrendAsync_PerDayBucketGroupingIsCorrect()
    {
        // 3 events on day 1, 1 event on day 2, 2 events on day 3.
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked",         "Explain", "m", "[]", new DateTime(2026, 6, 1, 8,  0, 0, DateTimeKind.Utc)),
            MakeEvent("Blocked",         "Explain", "m", "[]", new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc)),
            MakeEvent("Regenerated",     "Hint",    "m", "[]", new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc)),
            MakeEvent("FallbackReturned","Practice","m", "[]", new DateTime(2026, 6, 2, 9,  0, 0, DateTimeKind.Utc)),
            MakeEvent("Blocked",         "Explain", "m", "[]", new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc)),
            MakeEvent("Blocked",         "Explain", "m", "[]", new DateTime(2026, 6, 3, 18, 0, 0, DateTimeKind.Utc)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetTrendAsync(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 4, 0, 0, 0, DateTimeKind.Utc));

        result.Should().HaveCount(3);

        var day1 = result.Single(b => b.BucketDate.Day == 1);
        day1.TotalCount.Should().Be(3);
        day1.BlockedCount.Should().Be(2);
        day1.RegeneratedCount.Should().Be(1);
        day1.FallbackReturnedCount.Should().Be(0);

        var day2 = result.Single(b => b.BucketDate.Day == 2);
        day2.TotalCount.Should().Be(1);
        day2.FallbackReturnedCount.Should().Be(1);

        var day3 = result.Single(b => b.BucketDate.Day == 3);
        day3.TotalCount.Should().Be(2);
        day3.BlockedCount.Should().Be(2);
    }

    // ── DS-11 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTrendAsync_EmptyWindow_ReturnsEmptyList()
    {
        var result = await _sut.GetTrendAsync(D(1), D(2));
        result.Should().BeEmpty();
    }

    // ── DS-12 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSummaryAsync_MalformedJsonb_DoesNotThrow()
    {
        // Seed an event with malformed ReasonCodes json.
        _db.SafetyEvents.Add(new SafetyEvent
        {
            StudentId     = 1,
            TaskKind      = "Explain",
            ActionTaken   = "Blocked",
            ModelId       = "claude-haiku",
            ReasonCodes   = "NOT_VALID_JSON",   // malformed
            FailedChecks  = "[]",
            OccurredAtUtc = D(1),
        });
        await _db.SaveChangesAsync();

        // Must not throw; breakdown by reason should gracefully return empty.
        var act = async () => await _sut.GetSummaryAsync(D(1), D(2));
        await act.Should().NotThrowAsync();

        var result = await _sut.GetSummaryAsync(D(1), D(2));
        result.TotalEvents.Should().Be(1);
        result.BreakdownByReasonCode.Should().BeEmpty(); // malformed → empty, not exception
    }

    // ── DS-13 ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFlaggedOutputsPagedAsync_DateFilter_NarrowsResults()
    {
        _db.SafetyEvents.AddRange(
            MakeEvent("Blocked", "Explain", "m", "[]", D(1)),
            MakeEvent("Blocked", "Explain", "m", "[]", D(5)),
            MakeEvent("Blocked", "Explain", "m", "[]", D(10)));
        await _db.SaveChangesAsync();

        var result = await _sut.GetFlaggedOutputsPagedAsync(
            action: null, reasonCode: null, taskKind: null,
            from: D(4), to: D(6), pageNumber: 1, pageSize: 10);

        result.TotalCount.Should().Be(1);
        result.Data.Should().ContainSingle(d => d.OccurredAtUtc.Day == 5);
    }
}

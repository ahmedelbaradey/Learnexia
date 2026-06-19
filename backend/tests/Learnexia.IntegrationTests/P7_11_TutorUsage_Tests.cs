// ReSharper disable InconsistentNaming
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-11 integration tests: Admin AI Tutor Usage / Cost Dashboard.
///
/// Endpoint under test:
///   GET /api/Admin/AiSafety/usage?from=&amp;to=
///       → BaseResponse&lt;TutorUsageDto&gt;  (from&gt;=to → 400; empty window → 200 with zeros)
///
/// Cases:
///   AUTHZ-*       : anonymous → 401, parent → 403, basicuser (non-admin) → 403, admin → 200.
///   USAGE-AGGS-*  : seeded AiUsageLog rows; totals, ByModel, ByTaskKind, Trend buckets all correct.
///   USAGE-FILTER-*: rows outside [from,to] are excluded from aggregations.
///   USAGE-VALID-* : from&gt;=to → 400 BadRequest.
///   USAGE-EMPTY-* : future window with no rows → 200 with zeros and empty arrays (never 404).
///   USAGE-ENV-*   : BaseResponse envelope shape (statusCode/successed/message/data/errors).
///   USAGE-REC-*   : fire-and-forget recorder persists a row via IAiUsageRecorder.Record(…).
///
/// Docker + Postgres required to RUN (Testcontainers). Compile-only on Windows CI without Docker daemon.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_11_TutorUsage_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string UsageUrl          = "api/Admin/AiSafety/usage";

    // ── Seeded credentials (identical to P7_11_AiSafetyDashboard_Tests) ──────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    // ── Deterministic seed dates (narrow past window, avoids bleed from other suites) ─
    // We pick a window far in the past (2024-06) that no other test file uses.
    private static readonly DateTime Day1 = new DateTime(2024, 6, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new DateTime(2024, 6, 11, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new DateTime(2024, 6, 12, 8, 0, 0, DateTimeKind.Utc);

    // Query window that covers all 3 seeded days with a margin.
    private static readonly DateTime WindowFrom = new DateTime(2024, 6, 9, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowTo   = new DateTime(2024, 6, 13, 0, 0, 0, DateTimeKind.Utc);

    // ── Seeded data plan ─────────────────────────────────────────────────────
    //   Day1 — row A: model "gpt-4o",       taskKind "Explain",  prompt=100, completion=200, cost=0.003000m, latency=500, cacheHit=false
    //   Day1 — row B: model "gpt-4o",       taskKind "Hint",     prompt=50,  completion=80,  cost=0.001200m, latency=300, cacheHit=true
    //   Day2 — row C: model "claude-haiku", taskKind "Explain",  prompt=120, completion=160, cost=0.002400m, latency=400, cacheHit=false
    //   Day2 — row D: model "claude-haiku", taskKind "Hint",     prompt=60,  completion=90,  cost=0.001500m, latency=350, cacheHit=true
    //   Day3 — row E: model "gpt-4o",       taskKind "Explain",  prompt=80,  completion=140, cost=0.002200m, latency=450, cacheHit=false
    //
    //   TotalCalls       = 5
    //   TotalPromptTokens     = 100+50+120+60+80     = 410
    //   TotalCompletionTokens = 200+80+160+90+140    = 670
    //   TotalEstimatedCostUsd = 0.003000+0.001200+0.002400+0.001500+0.002200 = 0.010300
    //   AvgLatencyMs     = (500+300+400+350+450)/5   = 400.0
    //   CacheHitRate     = 2/5                        = 0.4000
    //
    //   ByModel["gpt-4o"]:       calls=3, totalTokens=(100+200)+(50+80)+(80+140)=650, cost=0.003000+0.001200+0.002200=0.006400
    //   ByModel["claude-haiku"]: calls=2, totalTokens=(120+160)+(60+90)=430, cost=0.002400+0.001500=0.003900
    //   ByTaskKind["Explain"]:   calls=3, totalTokens=(100+200)+(120+160)+(80+140)=800, cost=0.003000+0.002400+0.002200=0.007600
    //   ByTaskKind["Hint"]:      calls=2, totalTokens=(50+80)+(60+90)=280, cost=0.001200+0.001500=0.002700
    //
    //   Trend Day1: calls=2, cost=0.003000+0.001200=0.004200
    //   Trend Day2: calls=2, cost=0.002400+0.001500=0.003900
    //   Trend Day3: calls=1, cost=0.002200

    private bool _seeded;

    // ── Rows outside the window (used for filter tests) ──────────────────────
    // Row F is 30 days before the window — must NOT appear in [WindowFrom, WindowTo] aggregations.
    private static readonly DateTime OutsideDate = new DateTime(2024, 5, 1, 8, 0, 0, DateTimeKind.Utc);

    public P7_11_TutorUsage_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Belt-and-suspenders: ensure Ai migrations are applied (same as P7_11_AiSafetyDashboard_Tests).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            await db.Database.MigrateAsync();
        }

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AUTHZ: authorization matrix
    // =========================================================================

    [Fact(DisplayName = "AUTHZ-1: GET /api/Admin/AiSafety/usage anonymous → 401")]
    public async Task Authz1_Usage_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, UsageUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "usage is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-2: GET /api/Admin/AiSafety/usage with parent token → 403")]
    public async Task Authz2_Usage_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, UsageUrl, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usage is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-3: GET /api/Admin/AiSafety/usage with basicuser (non-admin) → 403")]
    public async Task Authz3_Usage_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, UsageUrl, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "usage is AdminOnly — basicuser (non-admin) must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-4: GET /api/Admin/AiSafety/usage with admin token → 200")]
    public async Task Authz4_Usage_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, UsageUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "usage must return 200 for admin; body: {0}", body);
    }

    // =========================================================================
    // USAGE-ENV: BaseResponse envelope shape
    // =========================================================================

    [Fact(DisplayName = "USAGE-ENV-1: usage response has BaseResponse envelope (statusCode/successed/message/data/errors)")]
    public async Task UsageEnv1_Envelope_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var succ).Should().BeTrue("envelope must have successed (sic); body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for a 200; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-ENV-2: data has TutorUsageDto shape (all required top-level fields present)")]
    public async Task UsageEnv2_Data_HasTutorUsageDtoShape()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "from",                   out _).Should().BeTrue("data must have from; body: {0}", body);
        TryProp(data, "to",                     out _).Should().BeTrue("data must have to; body: {0}", body);
        TryProp(data, "totalCalls",             out _).Should().BeTrue("data must have totalCalls; body: {0}", body);
        TryProp(data, "totalPromptTokens",      out _).Should().BeTrue("data must have totalPromptTokens; body: {0}", body);
        TryProp(data, "totalCompletionTokens",  out _).Should().BeTrue("data must have totalCompletionTokens; body: {0}", body);
        TryProp(data, "totalEstimatedCostUsd",  out _).Should().BeTrue("data must have totalEstimatedCostUsd; body: {0}", body);
        TryProp(data, "avgLatencyMs",           out _).Should().BeTrue("data must have avgLatencyMs; body: {0}", body);
        TryProp(data, "cacheHitRate",           out _).Should().BeTrue("data must have cacheHitRate; body: {0}", body);

        TryProp(data, "byModel",    out var byModel).Should().BeTrue("data must have byModel; body: {0}", body);
        byModel.ValueKind.Should().Be(JsonValueKind.Array, "byModel must be an array; body: {0}", body);

        TryProp(data, "byTaskKind", out var byTaskKind).Should().BeTrue("data must have byTaskKind; body: {0}", body);
        byTaskKind.ValueKind.Should().Be(JsonValueKind.Array, "byTaskKind must be an array; body: {0}", body);

        TryProp(data, "trend",      out var trend).Should().BeTrue("data must have trend; body: {0}", body);
        trend.ValueKind.Should().Be(JsonValueKind.Array, "trend must be an array; body: {0}", body);
    }

    // =========================================================================
    // USAGE-AGGS: aggregation correctness from seeded rows
    // =========================================================================

    [Fact(DisplayName = "USAGE-AGGS-1: seeded rows → TotalCalls≥5, TotalPromptTokens≥410, TotalCompletionTokens≥670")]
    public async Task UsageAggs1_TotalCounts_AreCorrect()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Multiple xUnit instances share the DB; each seeds 5 rows. Assert ≥ seeded minimum.
        TryProp(data, "totalCalls", out var totalCalls).Should().BeTrue("body: {0}", body);
        totalCalls.GetInt32().Should().BeGreaterThanOrEqualTo(5,
            "seeded at least 5 rows in the window; TotalCalls must be ≥5; body: {0}", body);

        TryProp(data, "totalPromptTokens", out var totalPrompt).Should().BeTrue("body: {0}", body);
        totalPrompt.GetInt32().Should().BeGreaterThanOrEqualTo(410,
            "sum of prompt tokens across seeded rows must be ≥410; body: {0}", body);

        TryProp(data, "totalCompletionTokens", out var totalCompletion).Should().BeTrue("body: {0}", body);
        totalCompletion.GetInt32().Should().BeGreaterThanOrEqualTo(670,
            "sum of completion tokens across seeded rows must be ≥670; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-AGGS-2: seeded rows → TotalEstimatedCostUsd≥0.010300, AvgLatencyMs≈400, CacheHitRate≈0.4")]
    public async Task UsageAggs2_CostLatencyCache_AreCorrect()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "totalEstimatedCostUsd", out var cost).Should().BeTrue("body: {0}", body);
        cost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.010300m,
            "sum of EstimatedCostUsd must be ≥0.010300 (seeded minimum); body: {0}", body);

        // All seeded rows have latency in {300,350,400,450,500}ms so average is always 400ms
        // regardless of how many times the same 5-row pattern is seeded (weighted average is invariant).
        TryProp(data, "avgLatencyMs", out var avgLat).Should().BeTrue("body: {0}", body);
        avgLat.GetDouble().Should().BeApproximately(400.0, 1.0,
            "all seeded rows have latencies averaging 400ms; body: {0}", body);

        // 2/5 = 40% cache hit rate is invariant since both true and false rows are seeded in the same ratio.
        TryProp(data, "cacheHitRate", out var hitRate).Should().BeTrue("body: {0}", body);
        hitRate.GetDouble().Should().BeApproximately(0.4, 0.0001,
            "2 of every 5 seeded rows have WasCacheHit=true → CacheHitRate=0.4 regardless of run count; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-AGGS-3: ByModel contains 'gpt-4o' (calls≥3) and 'claude-haiku' (calls≥2); gpt-4o has more calls")]
    public async Task UsageAggs3_ByModel_IsCorrect()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byModel", out var byModel).Should().BeTrue("body: {0}", body);
        byModel.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        var modelList = byModel.EnumerateArray().ToList();
        modelList.Should().HaveCount(2,
            "seeded rows span exactly 2 models (gpt-4o + claude-haiku); body: {0}", body);

        // Find gpt-4o entry
        var gpt4oList = modelList.Where(el =>
        {
            TryProp(el, "modelId", out var mid);
            return mid.GetString() == "gpt-4o";
        }).ToList();
        gpt4oList.Should().HaveCount(1, "gpt-4o must appear exactly once in ByModel; body: {0}", body);
        var gpt4oEntry = gpt4oList[0];
        TryProp(gpt4oEntry, "calls", out var gpt4oCalls).Should().BeTrue("body: {0}", body);
        gpt4oCalls.GetInt32().Should().BeGreaterThanOrEqualTo(3,
            "gpt-4o seeded with ≥3 rows (A, B, E per seed run); body: {0}", body);

        TryProp(gpt4oEntry, "totalTokens", out var gpt4oTokens).Should().BeTrue("body: {0}", body);
        gpt4oTokens.GetInt32().Should().BeGreaterThanOrEqualTo(650,
            "gpt-4o total tokens ≥650 per seed run; body: {0}", body);

        TryProp(gpt4oEntry, "totalEstimatedCostUsd", out var gpt4oCost).Should().BeTrue("body: {0}", body);
        gpt4oCost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.006400m,
            "gpt-4o cost ≥0.006400 per seed run; body: {0}", body);

        // Find claude-haiku entry
        var claudeList = modelList.Where(el =>
        {
            TryProp(el, "modelId", out var mid);
            return mid.GetString() == "claude-haiku";
        }).ToList();
        claudeList.Should().HaveCount(1, "claude-haiku must appear exactly once in ByModel; body: {0}", body);
        var claudeEntry = claudeList[0];
        TryProp(claudeEntry, "calls", out var claudeCalls).Should().BeTrue("body: {0}", body);
        claudeCalls.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            "claude-haiku seeded with ≥2 rows (C, D per seed run); body: {0}", body);

        TryProp(claudeEntry, "totalTokens", out var claudeTokens).Should().BeTrue("body: {0}", body);
        claudeTokens.GetInt32().Should().BeGreaterThanOrEqualTo(430,
            "claude-haiku total tokens ≥430 per seed run; body: {0}", body);

        TryProp(claudeEntry, "totalEstimatedCostUsd", out var claudeCost).Should().BeTrue("body: {0}", body);
        claudeCost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.003900m,
            "claude-haiku cost ≥0.003900 per seed run; body: {0}", body);

        // Ordering: ByModel is descending by calls — gpt-4o (more calls per run) must come first.
        TryProp(modelList[0], "modelId", out var firstModelId).Should().BeTrue("body: {0}", body);
        firstModelId.GetString().Should().Be("gpt-4o",
            "ByModel is ordered descending by calls; gpt-4o (3 per run) must be first; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-AGGS-4: ByTaskKind contains 'Explain' (calls≥3) and 'Hint' (calls≥2); Explain has more calls")]
    public async Task UsageAggs4_ByTaskKind_IsCorrect()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byTaskKind", out var byTaskKind).Should().BeTrue("body: {0}", body);
        byTaskKind.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        var taskList = byTaskKind.EnumerateArray().ToList();
        taskList.Should().HaveCount(2,
            "seeded rows span exactly 2 task kinds (Explain + Hint); body: {0}", body);

        // Explain
        var explainList = taskList.Where(el =>
        {
            TryProp(el, "taskKind", out var tk);
            return tk.GetString() == "Explain";
        }).ToList();
        explainList.Should().HaveCount(1, "Explain must appear exactly once in ByTaskKind; body: {0}", body);
        var explainEntry = explainList[0];
        TryProp(explainEntry, "calls", out var explainCalls).Should().BeTrue("body: {0}", body);
        explainCalls.GetInt32().Should().BeGreaterThanOrEqualTo(3,
            "Explain seeded with ≥3 rows (A, C, E per seed run); body: {0}", body);

        TryProp(explainEntry, "totalTokens", out var explainTokens).Should().BeTrue("body: {0}", body);
        explainTokens.GetInt32().Should().BeGreaterThanOrEqualTo(800,
            "Explain total tokens ≥800 per seed run; body: {0}", body);

        TryProp(explainEntry, "totalEstimatedCostUsd", out var explainCost).Should().BeTrue("body: {0}", body);
        explainCost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.007600m,
            "Explain cost ≥0.007600 per seed run; body: {0}", body);

        // Hint
        var hintList = taskList.Where(el =>
        {
            TryProp(el, "taskKind", out var tk);
            return tk.GetString() == "Hint";
        }).ToList();
        hintList.Should().HaveCount(1, "Hint must appear exactly once in ByTaskKind; body: {0}", body);
        var hintEntry = hintList[0];
        TryProp(hintEntry, "calls", out var hintCalls).Should().BeTrue("body: {0}", body);
        hintCalls.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            "Hint seeded with ≥2 rows (B, D per seed run); body: {0}", body);

        TryProp(hintEntry, "totalEstimatedCostUsd", out var hintCost).Should().BeTrue("body: {0}", body);
        hintCost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.002700m,
            "Hint cost ≥0.002700 per seed run; body: {0}", body);

        // Ordering: ByTaskKind is descending by calls — Explain (3 per run) must come first.
        TryProp(taskList[0], "taskKind", out var firstTaskKind).Should().BeTrue("body: {0}", body);
        firstTaskKind.GetString().Should().Be("Explain",
            "ByTaskKind is ordered descending by calls; Explain (3 per run) must be first; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-AGGS-5: Trend has exactly 3 per-day buckets (same 3 dates regardless of seed count), ascending by date")]
    public async Task UsageAggs5_Trend_HasCorrectBuckets()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "trend", out var trend).Should().BeTrue("body: {0}", body);
        trend.ValueKind.Should().Be(JsonValueKind.Array, "trend must be an array; body: {0}", body);

        var buckets = trend.EnumerateArray().ToList();
        buckets.Should().HaveCount(3,
            "seeded rows on 3 distinct days must produce exactly 3 trend buckets; body: {0}", body);

        // Each bucket must have Date, Calls, TotalEstimatedCostUsd
        foreach (var bucket in buckets)
        {
            TryProp(bucket, "date",                   out _).Should().BeTrue("bucket must have date; body: {0}", body);
            TryProp(bucket, "calls",                  out _).Should().BeTrue("bucket must have calls; body: {0}", body);
            TryProp(bucket, "totalEstimatedCostUsd",  out _).Should().BeTrue("bucket must have totalEstimatedCostUsd; body: {0}", body);
        }

        // Day1: ≥2 calls (rows A+B per seed run), cost ≥0.004200
        var day1Bucket = FindBucketForDate(buckets, Day1.Date);
        day1Bucket.Should().NotBeNull($"must have a bucket for Day1 ({Day1:yyyy-MM-dd}); body: {body}");
        TryProp(day1Bucket.GetValueOrDefault(), "calls", out var d1Calls).Should().BeTrue("body: {0}", body);
        d1Calls.GetInt32().Should().BeGreaterThanOrEqualTo(2, $"Day1 bucket must have ≥2 calls (rows A+B per seed run); body: {body}");
        TryProp(day1Bucket.GetValueOrDefault(), "totalEstimatedCostUsd", out var d1Cost).Should().BeTrue("body: {0}", body);
        d1Cost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.004200m, $"Day1 cost ≥0.004200 per seed run; body: {body}");

        // Day2: ≥2 calls (rows C+D per seed run), cost ≥0.003900
        var day2Bucket = FindBucketForDate(buckets, Day2.Date);
        day2Bucket.Should().NotBeNull($"must have a bucket for Day2 ({Day2:yyyy-MM-dd}); body: {body}");
        TryProp(day2Bucket.GetValueOrDefault(), "calls", out var d2Calls).Should().BeTrue("body: {0}", body);
        d2Calls.GetInt32().Should().BeGreaterThanOrEqualTo(2, $"Day2 bucket must have ≥2 calls (rows C+D per seed run); body: {body}");
        TryProp(day2Bucket.GetValueOrDefault(), "totalEstimatedCostUsd", out var d2Cost).Should().BeTrue("body: {0}", body);
        d2Cost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.003900m, $"Day2 cost ≥0.003900 per seed run; body: {body}");

        // Day3: ≥1 call (row E per seed run), cost ≥0.002200
        var day3Bucket = FindBucketForDate(buckets, Day3.Date);
        day3Bucket.Should().NotBeNull($"must have a bucket for Day3 ({Day3:yyyy-MM-dd}); body: {body}");
        TryProp(day3Bucket.GetValueOrDefault(), "calls", out var d3Calls).Should().BeTrue("body: {0}", body);
        d3Calls.GetInt32().Should().BeGreaterThanOrEqualTo(1, $"Day3 bucket must have ≥1 call (row E per seed run); body: {body}");
        TryProp(day3Bucket.GetValueOrDefault(), "totalEstimatedCostUsd", out var d3Cost).Should().BeTrue("body: {0}", body);
        d3Cost.GetDecimal().Should().BeGreaterThanOrEqualTo(0.002200m, $"Day3 cost ≥0.002200 per seed run; body: {body}");

        // Trend must be ascending by date.
        var dates = buckets
            .Where(b => TryProp(b, "date", out _))
            .Select(b => { TryProp(b, "date", out var d); return DateTime.Parse(d.GetString()!); })
            .ToList();

        for (int i = 0; i < dates.Count - 1; i++)
        {
            dates[i].Should().BeBefore(dates[i + 1],
                "trend buckets must be ordered ascending by date; body: {0}", body);
        }
    }

    // =========================================================================
    // USAGE-FILTER: rows outside [from,to] excluded
    // =========================================================================

    [Fact(DisplayName = "USAGE-FILTER-1: rows outside [from,to] are excluded — Day2+Day3 TotalCalls is less than full-window TotalCalls")]
    public async Task UsageFilter1_RowsOutsideWindow_AreExcluded()
    {
        await EnsureSeededAsync();

        // Full window covers Day1+Day2+Day3.
        var (fullResp, fullRoot, fullBody) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);
        fullResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", fullBody);
        TryProp(fullRoot, "data", out var fullData).Should().BeTrue("body: {0}", fullBody);
        TryProp(fullData, "totalCalls", out var fullCalls).Should().BeTrue("body: {0}", fullBody);

        // Narrow window: Day2+Day3 only (excludes Day1's rows A+B).
        var narrowFrom = new DateTime(2024, 6, 11, 0, 0, 0, DateTimeKind.Utc);
        var narrowTo   = new DateTime(2024, 6, 13, 0, 0, 0, DateTimeKind.Utc);

        var (narrowResp, narrowRoot, narrowBody) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(narrowFrom)}&to={Fmt(narrowTo)}",
            bearer: _adminToken);

        narrowResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", narrowBody);
        TryProp(narrowRoot, "data", out var narrowData).Should().BeTrue("body: {0}", narrowBody);
        TryProp(narrowData, "totalCalls", out var narrowCalls).Should().BeTrue("body: {0}", narrowBody);

        narrowCalls.GetInt32().Should().BeLessThan(fullCalls.GetInt32(),
            "excluding Day1 rows from the window must reduce TotalCalls; body: {0}", narrowBody);

        // Narrow window must also exclude the out-of-window 'outside-model' from USAGE-FILTER-2.
        TryProp(narrowData, "byModel", out var narrowByModel).Should().BeTrue("body: {0}", narrowBody);
        var modelNames = narrowByModel.EnumerateArray()
            .Select(el => { TryProp(el, "modelId", out var mid); return mid.GetString() ?? ""; })
            .ToList();
        modelNames.Should().NotContain("outside-model",
            "outside-model rows have OccurredAtUtc in May 2024 — must be excluded from June window; body: {0}", narrowBody);
    }

    [Fact(DisplayName = "USAGE-FILTER-2: out-of-window row does not appear in ByModel breakdown")]
    public async Task UsageFilter2_OutsideWindowRow_ExcludedFromByModel()
    {
        await EnsureSeededAsync();

        // This seeds an out-of-window row with model "outside-model" — it must NOT appear in results
        // when we query [WindowFrom, WindowTo].
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            db.AiUsageLogs.Add(new AiUsageLog
            {
                Provider         = "TestProvider",
                ModelId          = "outside-model",
                TaskKind         = "Explain",
                PromptTokens     = 999,
                CompletionTokens = 999,
                EstimatedCostUsd = 9.999m,
                LatencyMs        = 9999,
                WasCacheHit      = false,
                OccurredAtUtc    = OutsideDate,
            });
            await db.SaveChangesAsync(userId: 0);
        }

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowFrom)}&to={Fmt(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byModel", out var byModel).Should().BeTrue("body: {0}", body);

        var modelNames = byModel.EnumerateArray()
            .Select(el =>
            {
                TryProp(el, "modelId", out var mid);
                return mid.GetString() ?? "";
            })
            .ToList();

        modelNames.Should().NotContain("outside-model",
            "row with OccurredAtUtc=OutsideDate (before WindowFrom) must be excluded; body: {0}", body);
    }

    // =========================================================================
    // USAGE-VALID: invalid date range → 400
    // =========================================================================

    [Fact(DisplayName = "USAGE-VALID-1: from == to → 400 BadRequest with successed=false")]
    public async Task UsageValid1_FromEqualTo_Returns400()
    {
        var sameDateStr = Fmt(WindowFrom);
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={sameDateStr}&to={sameDateStr}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from == to must return 400 (from >= to validation); body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a 400 range error; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-VALID-2: from > to (reversed range) → 400 BadRequest with successed=false")]
    public async Task UsageValid2_FromAfterTo_Returns400()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(WindowTo)}&to={Fmt(WindowFrom)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from > to must return 400; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a reversed-range 400; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-VALID-3: window > 366 days → 400 BadRequest (AiSafetyDateRangeTooLarge)")]
    public async Task UsageValid3_WindowTooLarge_Returns400()
    {
        // 400 days > 366 days max cap — handler must reject with AiSafetyDateRangeTooLarge.
        var from = DateTime.UtcNow.AddDays(-400);
        var to   = DateTime.UtcNow;
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from={Fmt(from)}&to={Fmt(to)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "window > 366 days must return 400 (AiSafetyDateRangeTooLarge cap); body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a too-large window; body: {0}", body);
    }

    // =========================================================================
    // USAGE-EMPTY: future window → 200 with zeroed payload, never 404
    // =========================================================================

    [Fact(DisplayName = "USAGE-EMPTY-1: future window with no rows → 200 with TotalCalls=0 and zeroed totals")]
    public async Task UsageEmpty1_FutureWindow_Returns200WithZeros()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from=2099-01-01T00:00:00Z&to=2099-12-31T00:00:00Z",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty window must return 200, NOT 404; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true even for an empty result; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "totalCalls", out var totalCalls).Should().BeTrue("body: {0}", body);
        totalCalls.GetInt32().Should().Be(0, "no logs in a future window; TotalCalls must be 0; body: {0}", body);

        TryProp(data, "totalPromptTokens", out var prompt).Should().BeTrue("body: {0}", body);
        prompt.GetInt32().Should().Be(0, "TotalPromptTokens must be 0 for empty window; body: {0}", body);

        TryProp(data, "totalCompletionTokens", out var completion).Should().BeTrue("body: {0}", body);
        completion.GetInt32().Should().Be(0, "TotalCompletionTokens must be 0 for empty window; body: {0}", body);

        TryProp(data, "totalEstimatedCostUsd", out var cost).Should().BeTrue("body: {0}", body);
        cost.GetDecimal().Should().Be(0m, "TotalEstimatedCostUsd must be 0 for empty window; body: {0}", body);

        TryProp(data, "avgLatencyMs", out var avgLat).Should().BeTrue("body: {0}", body);
        avgLat.GetDouble().Should().Be(0d, "AvgLatencyMs must be 0 for empty window; body: {0}", body);

        TryProp(data, "cacheHitRate", out var hitRate).Should().BeTrue("body: {0}", body);
        hitRate.GetDouble().Should().Be(0d, "CacheHitRate must be 0 for empty window; body: {0}", body);
    }

    [Fact(DisplayName = "USAGE-EMPTY-2: future window → 200 with empty ByModel, ByTaskKind, and Trend arrays")]
    public async Task UsageEmpty2_FutureWindow_Returns200WithEmptyArrays()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{UsageUrl}?from=2099-01-01T00:00:00Z&to=2099-12-31T00:00:00Z",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "byModel", out var byModel).Should().BeTrue("body: {0}", body);
        byModel.ValueKind.Should().Be(JsonValueKind.Array, "byModel must be an array; body: {0}", body);
        byModel.GetArrayLength().Should().Be(0, "byModel must be empty for an empty window; body: {0}", body);

        TryProp(data, "byTaskKind", out var byTaskKind).Should().BeTrue("body: {0}", body);
        byTaskKind.ValueKind.Should().Be(JsonValueKind.Array, "byTaskKind must be an array; body: {0}", body);
        byTaskKind.GetArrayLength().Should().Be(0, "byTaskKind must be empty for an empty window; body: {0}", body);

        TryProp(data, "trend", out var trend).Should().BeTrue("body: {0}", body);
        trend.ValueKind.Should().Be(JsonValueKind.Array, "trend must be an array; body: {0}", body);
        trend.GetArrayLength().Should().Be(0, "trend must be empty for an empty window; body: {0}", body);
    }

    // =========================================================================
    // USAGE-REC: fire-and-forget recorder persists a row via IAiUsageRecorder
    // =========================================================================

    [Fact(DisplayName = "USAGE-REC-1: IAiUsageRecorder.Record persists an AiUsageLog row to ai.AiUsageLogs (fire-and-forget)")]
    public async Task UsageRec1_Recorder_PersistsRowToDatabase()
    {
        // Resolve the recorder from the factory's DI container (it is a singleton).
        var recorder = _factory.Services.GetRequiredService<IAiUsageRecorder>();

        // Pick a unique OccurredAtUtc window so we can identify the row after the background write.
        // The recorder sets OccurredAtUtc = DateTime.UtcNow internally, so we capture the time *before* calling.
        var beforeCall = DateTime.UtcNow.AddSeconds(-1);

        var usage = new AiUsage
        {
            Provider         = "TestProvider",
            ModelId          = "recorder-test-model",
            PromptTokens     = 42,
            CompletionTokens = 84,
            EstimatedCostUsd = 0.001234m,
            LatencyMs        = 123L,
            WasCacheHit      = false,
        };

        // Record is fire-and-forget (void) — call and allow up to 3 seconds for the background Task.Run to complete.
        recorder.Record(usage, AiTaskKind.Hint, studentId: null);

        // Poll the DB (with a short wait) to verify the row was persisted by the background task.
        AiUsageLog? persisted = null;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            await Task.Delay(300);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            persisted = await db.AiUsageLogs
                .AsNoTracking()
                .Where(l => l.ModelId == "recorder-test-model"
                         && l.OccurredAtUtc >= beforeCall)
                .FirstOrDefaultAsync();

            if (persisted != null) break;
        }

        persisted.Should().NotBeNull(
            "IAiUsageRecorder.Record must persist a row to ai.AiUsageLogs within 3 seconds");
        persisted!.Provider.Should().Be("TestProvider");
        persisted.PromptTokens.Should().Be(42);
        persisted.CompletionTokens.Should().Be(84);
        persisted.EstimatedCostUsd.Should().Be(0.001234m);
        persisted.LatencyMs.Should().Be(123L);
        persisted.WasCacheHit.Should().BeFalse();
        persisted.TaskKind.Should().Be("Hint");
        persisted.StudentId.Should().BeNull("studentId was passed as null");
    }

    // =========================================================================
    // Private helpers — seeding, HTTP, JSON
    // =========================================================================

    /// <summary>
    /// Seeds exactly 5 <see cref="AiUsageLog"/> rows across 3 days for aggregation tests.
    /// Idempotent per test-run (guarded by <c>_seeded</c> flag).
    /// Uses the same <c>AiDbContext.SaveChangesAsync(userId)</c> pattern as the safety tests.
    /// </summary>
    private async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        _seeded = true;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var rows = new[]
        {
            // Row A: Day1, gpt-4o, Explain, no cache hit
            new AiUsageLog
            {
                Provider         = "OpenAi",
                ModelId          = "gpt-4o",
                TaskKind         = "Explain",
                PromptTokens     = 100,
                CompletionTokens = 200,
                EstimatedCostUsd = 0.003000m,
                LatencyMs        = 500,
                WasCacheHit      = false,
                OccurredAtUtc    = Day1,
            },
            // Row B: Day1, gpt-4o, Hint, cache hit
            new AiUsageLog
            {
                Provider         = "OpenAi",
                ModelId          = "gpt-4o",
                TaskKind         = "Hint",
                PromptTokens     = 50,
                CompletionTokens = 80,
                EstimatedCostUsd = 0.001200m,
                LatencyMs        = 300,
                WasCacheHit      = true,
                OccurredAtUtc    = Day1.AddHours(2),
            },
            // Row C: Day2, claude-haiku, Explain, no cache hit
            new AiUsageLog
            {
                Provider         = "Claude",
                ModelId          = "claude-haiku",
                TaskKind         = "Explain",
                PromptTokens     = 120,
                CompletionTokens = 160,
                EstimatedCostUsd = 0.002400m,
                LatencyMs        = 400,
                WasCacheHit      = false,
                OccurredAtUtc    = Day2,
            },
            // Row D: Day2, claude-haiku, Hint, cache hit
            new AiUsageLog
            {
                Provider         = "Claude",
                ModelId          = "claude-haiku",
                TaskKind         = "Hint",
                PromptTokens     = 60,
                CompletionTokens = 90,
                EstimatedCostUsd = 0.001500m,
                LatencyMs        = 350,
                WasCacheHit      = true,
                OccurredAtUtc    = Day2.AddHours(3),
            },
            // Row E: Day3, gpt-4o, Explain, no cache hit
            new AiUsageLog
            {
                Provider         = "OpenAi",
                ModelId          = "gpt-4o",
                TaskKind         = "Explain",
                PromptTokens     = 80,
                CompletionTokens = 140,
                EstimatedCostUsd = 0.002200m,
                LatencyMs        = 450,
                WasCacheHit      = false,
                OccurredAtUtc    = Day3,
            },
        };

        db.AiUsageLogs.AddRange(rows);
        await db.SaveChangesAsync(userId: 0);
    }

    private static JsonElement? FindBucketForDate(List<JsonElement> buckets, DateTime targetDate)
    {
        foreach (var bucket in buckets)
        {
            if (TryProp(bucket, "date", out var dateProp))
            {
                var dateStr = dateProp.GetString();
                if (dateStr != null && DateTime.Parse(dateStr).Date == targetDate.Date)
                    return bucket;
            }
        }
        return null;
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase + linear scan).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object) { value = default; return false; }
        if (element.TryGetProperty(name, out value)) return true;
        if (element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> RegisterParentGetTokenAsync()
    {
        var email = $"p711u_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Formats a DateTime as ISO-8601 UTC string for query params.</summary>
    private static string Fmt(DateTime dt) => dt.ToString("yyyy-MM-ddTHH:mm:ssZ");
}

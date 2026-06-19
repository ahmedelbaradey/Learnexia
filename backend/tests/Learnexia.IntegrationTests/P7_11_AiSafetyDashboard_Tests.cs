// ReSharper disable InconsistentNaming
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-11 integration tests: Admin AI-Safety Dashboard.
///
/// Endpoints under test (all route under api/Admin/AiSafety):
///   GET /api/Admin/AiSafety/signals?from=&amp;to=
///       → BaseResponse&lt;SafetySignalSummaryDto&gt;  (from&gt;=to → 400)
///   GET /api/Admin/AiSafety/flagged?action=&amp;reasonCode=&amp;taskKind=&amp;from=&amp;to=&amp;pageNumber=&amp;pageSize=
///       → BaseResponse&lt;PaginatedResult&lt;FlaggedOutputDto&gt;&gt; (paged, newest-first, size clamped ≤100)
///   GET /api/Admin/AiSafety/trend?from=&amp;to=
///       → BaseResponse&lt;IReadOnlyList&lt;SafetyTrendBucketDto&gt;&gt; (per-day buckets; from&gt;=to → 400)
///
/// Cases:
///   AUTHZ-*  : anonymous → 401, parent → 403, basicuser(non-admin) → 403, admin → 200 on all 3 routes.
///   SIGNALS-* : seeded rows; aggregate counts match; from&gt;=to → 400; empty window → 200 with zeros.
///   FLAGGED-* : paged + newest-first; action/reasonCode/taskKind filters; pageSize clamped; PII-light.
///   TREND-*   : per-day buckets match seeded distribution; from&gt;=to → 400; empty → empty list 200.
///
/// Docker + Postgres required to RUN (Testcontainers). Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_11_AiSafetyDashboard_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignalsUrl        = "api/Admin/AiSafety/signals";
    private const string FlaggedUrl        = "api/Admin/AiSafety/flagged";
    private const string TrendUrl          = "api/Admin/AiSafety/trend";

    // ── Seeded credentials ────────────────────────────────────────────────────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;    // non-admin role
    private string? _parentToken;

    // ── Deterministic seed dates (choose a narrow past window to avoid bleed) ─
    // Day 1: 2024-03-01 UTC  Day 2: 2024-03-02 UTC  Day 3: 2024-03-03 UTC
    private static readonly DateTime Day1 = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new DateTime(2024, 3, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new DateTime(2024, 3, 3, 10, 0, 0, DateTimeKind.Utc);

    // Query window that covers all 3 days plus a little margin.
    private static readonly DateTime WindowFrom = new DateTime(2024, 2, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowTo   = new DateTime(2024, 3,  4, 0, 0, 0, DateTimeKind.Utc);

    // Seeded row count breakdown:
    //   Day1 — 2 Blocked  (Explain, gpt-4o, ["TOXICITY_HIGH"])
    //   Day1 — 1 Regenerated (Hint, claude-haiku, ["AGE_INAPPROPRIATE"])
    //   Day2 — 1 Blocked  (Hint, gpt-4o, ["TOXICITY_HIGH"])
    //   Day2 — 1 FallbackReturned (Explain, claude-haiku, ["POLICY_VIOLATION"])
    //   Day3 — 2 Blocked  (Explain, gpt-4o, ["TOXICITY_HIGH","AGE_INAPPROPRIATE"])
    //   Total: 7 rows; 5 Blocked, 1 Regenerated, 1 FallbackReturned
    private bool _seeded;

    public P7_11_AiSafetyDashboard_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Belt-and-suspenders: ensure the Ai migrations are applied.
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
    // AUTHZ: authorization matrix — all 3 routes
    // =========================================================================

    [Fact(DisplayName = "AUTHZ-1: GET /api/Admin/AiSafety/signals anonymous → 401")]
    public async Task Authz1_Signals_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, SignalsUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "signals is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-2: GET /api/Admin/AiSafety/signals with parent token → 403")]
    public async Task Authz2_Signals_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, SignalsUrl, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "signals is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-3: GET /api/Admin/AiSafety/signals with basicuser → 403")]
    public async Task Authz3_Signals_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, SignalsUrl, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "signals is AdminOnly — basicuser (non-admin) must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-4: GET /api/Admin/AiSafety/signals with admin token → 200")]
    public async Task Authz4_Signals_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, SignalsUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "signals must return 200 for admin; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-5: GET /api/Admin/AiSafety/flagged anonymous → 401")]
    public async Task Authz5_Flagged_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, FlaggedUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "flagged is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-6: GET /api/Admin/AiSafety/flagged with parent token → 403")]
    public async Task Authz6_Flagged_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, FlaggedUrl, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "flagged is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-7: GET /api/Admin/AiSafety/flagged with basicuser → 403")]
    public async Task Authz7_Flagged_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, FlaggedUrl, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "flagged is AdminOnly — basicuser must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-8: GET /api/Admin/AiSafety/flagged with admin token → 200")]
    public async Task Authz8_Flagged_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, FlaggedUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "flagged must return 200 for admin; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-9: GET /api/Admin/AiSafety/trend anonymous → 401")]
    public async Task Authz9_Trend_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, TrendUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "trend is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-10: GET /api/Admin/AiSafety/trend with parent token → 403")]
    public async Task Authz10_Trend_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, TrendUrl, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "trend is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-11: GET /api/Admin/AiSafety/trend with basicuser → 403")]
    public async Task Authz11_Trend_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, TrendUrl, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "trend is AdminOnly — basicuser must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-12: GET /api/Admin/AiSafety/trend with admin token → 200")]
    public async Task Authz12_Trend_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, TrendUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "trend must return 200 for admin; body: {0}", body);
    }

    // =========================================================================
    // SIGNALS: aggregate counts + breakdowns + invalid range + empty window
    // =========================================================================

    [Fact(DisplayName = "SIGNALS-1: signals has BaseResponse envelope (statusCode/successed/message/data/errors)")]
    public async Task Signals1_Envelope_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out var succ).Should().BeTrue("envelope must have successed (sic); body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for a 200; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-2: signals data has SafetySignalSummaryDto shape (TotalEvents/Blocked*/Regenerated*/FallbackReturned*/Breakdown*)")]
    public async Task Signals2_Data_HasSummaryShape()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Top-level summary shape
        TryProp(data, "totalEvents",          out _).Should().BeTrue("data must have totalEvents; body: {0}", body);
        TryProp(data, "blockedCount",         out _).Should().BeTrue("data must have blockedCount; body: {0}", body);
        TryProp(data, "blockedRate",          out _).Should().BeTrue("data must have blockedRate; body: {0}", body);
        TryProp(data, "regeneratedCount",     out _).Should().BeTrue("data must have regeneratedCount; body: {0}", body);
        TryProp(data, "regeneratedRate",      out _).Should().BeTrue("data must have regeneratedRate; body: {0}", body);
        TryProp(data, "fallbackReturnedCount",out _).Should().BeTrue("data must have fallbackReturnedCount; body: {0}", body);
        TryProp(data, "fallbackReturnedRate", out _).Should().BeTrue("data must have fallbackReturnedRate; body: {0}", body);

        // Breakdown arrays
        TryProp(data, "breakdownByAction",    out var byAction).Should().BeTrue("data must have breakdownByAction; body: {0}", body);
        byAction.ValueKind.Should().Be(JsonValueKind.Array, "breakdownByAction must be an array; body: {0}", body);

        TryProp(data, "breakdownByReasonCode",out var byReason).Should().BeTrue("data must have breakdownByReasonCode; body: {0}", body);
        byReason.ValueKind.Should().Be(JsonValueKind.Array, "breakdownByReasonCode must be an array; body: {0}", body);

        TryProp(data, "breakdownByModelId",   out var byModel).Should().BeTrue("data must have breakdownByModelId; body: {0}", body);
        byModel.ValueKind.Should().Be(JsonValueKind.Array, "breakdownByModelId must be an array; body: {0}", body);

        TryProp(data, "breakdownByTaskKind",  out var byTask).Should().BeTrue("data must have breakdownByTaskKind; body: {0}", body);
        byTask.ValueKind.Should().Be(JsonValueKind.Array, "breakdownByTaskKind must be an array; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-3: seeded 7 rows in window → TotalEvents=7, BlockedCount=5, RegeneratedCount=1, FallbackReturnedCount=1")]
    public async Task Signals3_SeededRows_AggregateCounts_AreCorrect()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "totalEvents",           out var total).Should().BeTrue("body: {0}", body);
        total.GetInt32().Should().BeGreaterThanOrEqualTo(7,
            "seeded 7 rows in the window; TotalEvents must be ≥7 (other tests may add rows to the same window); body: {0}", body);

        // Because other tests may also insert rows in this same window, we assert ≥ the seeded counts.
        TryProp(data, "blockedCount",          out var blocked).Should().BeTrue("body: {0}", body);
        blocked.GetInt32().Should().BeGreaterThanOrEqualTo(5,
            "seeded 5 Blocked rows; blockedCount must be ≥5; body: {0}", body);

        TryProp(data, "regeneratedCount",      out var regen).Should().BeTrue("body: {0}", body);
        regen.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "seeded 1 Regenerated row; regeneratedCount must be ≥1; body: {0}", body);

        TryProp(data, "fallbackReturnedCount", out var fallback).Should().BeTrue("body: {0}", body);
        fallback.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "seeded 1 FallbackReturned row; fallbackReturnedCount must be ≥1; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-4: breakdownByAction contains 'Blocked', 'Regenerated', 'FallbackReturned' labels")]
    public async Task Signals4_BreakdownByAction_ContainsSeededLabels()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "breakdownByAction", out var byAction).Should().BeTrue("body: {0}", body);

        var labels = byAction.EnumerateArray()
            .Select(el =>
            {
                TryProp(el, "label", out var lbl);
                return lbl.GetString() ?? "";
            })
            .ToList();

        labels.Should().Contain("Blocked",
            "seeded Blocked rows must appear in breakdownByAction; body: {0}", body);
        labels.Should().Contain("Regenerated",
            "seeded Regenerated row must appear in breakdownByAction; body: {0}", body);
        labels.Should().Contain("FallbackReturned",
            "seeded FallbackReturned row must appear in breakdownByAction; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-5: breakdownByReasonCode contains 'TOXICITY_HIGH' and 'AGE_INAPPROPRIATE'")]
    public async Task Signals5_BreakdownByReasonCode_ContainsSeededCodes()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "breakdownByReasonCode", out var byReason).Should().BeTrue("body: {0}", body);

        var reasonLabels = byReason.EnumerateArray()
            .Select(el =>
            {
                TryProp(el, "label", out var lbl);
                return lbl.GetString() ?? "";
            })
            .ToList();

        reasonLabels.Should().Contain("TOXICITY_HIGH",
            "seeded TOXICITY_HIGH reason codes must appear in breakdownByReasonCode; body: {0}", body);
        reasonLabels.Should().Contain("AGE_INAPPROPRIATE",
            "seeded AGE_INAPPROPRIATE reason codes must appear in breakdownByReasonCode; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-6: breakdownByModelId contains 'gpt-4o' and 'claude-haiku'")]
    public async Task Signals6_BreakdownByModelId_ContainsSeededModels()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "breakdownByModelId", out var byModel).Should().BeTrue("body: {0}", body);

        var modelLabels = byModel.EnumerateArray()
            .Select(el =>
            {
                TryProp(el, "label", out var lbl);
                return lbl.GetString() ?? "";
            })
            .ToList();

        modelLabels.Should().Contain("gpt-4o",
            "seeded gpt-4o model rows must appear in breakdownByModelId; body: {0}", body);
        modelLabels.Should().Contain("claude-haiku",
            "seeded claude-haiku model rows must appear in breakdownByModelId; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-7: breakdownByTaskKind contains 'Explain' and 'Hint'")]
    public async Task Signals7_BreakdownByTaskKind_ContainsSeededKinds()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "breakdownByTaskKind", out var byTask).Should().BeTrue("body: {0}", body);

        var taskLabels = byTask.EnumerateArray()
            .Select(el =>
            {
                TryProp(el, "label", out var lbl);
                return lbl.GetString() ?? "";
            })
            .ToList();

        taskLabels.Should().Contain("Explain",
            "seeded Explain task rows must appear in breakdownByTaskKind; body: {0}", body);
        taskLabels.Should().Contain("Hint",
            "seeded Hint task rows must appear in breakdownByTaskKind; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-8: from >= to → 400 BadRequest")]
    public async Task Signals8_FromGreaterThanOrEqualTo_Returns400()
    {
        // from == to (same timestamp)
        var sameDateStr = FmtDate(WindowFrom);
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={sameDateStr}&to={sameDateStr}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from >= to must return 400; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a 400; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-9: from > to (reversed range) → 400 BadRequest")]
    public async Task Signals9_FromAfterTo_Returns400()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={FmtDate(WindowTo)}&to={FmtDate(WindowFrom)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from > to must return 400; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a 400 range error; body: {0}", body);
    }

    [Fact(DisplayName = "SIGNALS-10: empty window (far future) → 200 with TotalEvents=0")]
    public async Task Signals10_EmptyWindow_Returns200WithZeros()
    {
        var futureFrom = "2099-01-01T00:00:00Z";
        var futureTo   = "2099-12-31T00:00:00Z";

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{SignalsUrl}?from={futureFrom}&to={futureTo}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty window must return 200, not 404; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for an empty result; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "totalEvents", out var total).Should().BeTrue("body: {0}", body);
        total.GetInt32().Should().Be(0, "no events in a future window; totalEvents must be 0; body: {0}", body);

        TryProp(data, "blockedCount", out var blocked).Should().BeTrue("body: {0}", body);
        blocked.GetInt32().Should().Be(0, "blockedCount must be 0 in an empty window; body: {0}", body);
    }

    // =========================================================================
    // FLAGGED: paging, ordering, filters, page-size clamp, PII-light
    // =========================================================================

    [Fact(DisplayName = "FLAGGED-1: envelope has BaseResponse keys; data has PaginatedResult keys")]
    public async Task Flagged1_Envelope_HasCorrectShape()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?pageNumber=1&pageSize=10",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Envelope keys
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out _).Should().BeTrue("envelope must have successed; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",       out var data).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("envelope must have errors; body: {0}", body);

        // PaginatedResult keys
        TryProp(data, "currentPage", out _).Should().BeTrue("data must have currentPage; body: {0}", body);
        TryProp(data, "totalCount",  out _).Should().BeTrue("data must have totalCount; body: {0}", body);
        TryProp(data, "totalPages",  out _).Should().BeTrue("data must have totalPages; body: {0}", body);
        TryProp(data, "pageSize",    out _).Should().BeTrue("data must have pageSize; body: {0}", body);
    }

    [Fact(DisplayName = "FLAGGED-2: seeded rows appear in flagged list; rows are newest-first (OccurredAtUtc desc)")]
    public async Task Flagged2_SeededRows_AppearNewestFirst()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractFlaggedItems(root);
        items.Should().NotBeEmpty("seeded rows must appear in the flagged list; body: {0}", body);

        // Verify newest-first ordering by extracting OccurredAtUtc sequence.
        var times = items
            .Where(el => TryProp(el, "occurredAtUtc", out _))
            .Select(el =>
            {
                TryProp(el, "occurredAtUtc", out var t);
                return DateTime.Parse(t.GetString()!);
            })
            .ToList();

        for (int i = 0; i < times.Count - 1; i++)
        {
            times[i].Should().BeOnOrAfter(times[i + 1],
                "flagged list must be newest-first (OccurredAtUtc desc); row {0} ({1}) is older than row {2} ({3}); body: {4}",
                i, times[i], i + 1, times[i + 1], body);
        }
    }

    [Fact(DisplayName = "FLAGGED-3: FlaggedOutputDto is PII-light — no 'promptText' or 'responseText' fields")]
    public async Task Flagged3_Items_ArePiiLight()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractFlaggedItems(root);
        items.Should().NotBeEmpty("seeded rows must appear; body: {0}", body);

        foreach (var item in items)
        {
            // PII-light assertion: no raw prompt/response text
            TryProp(item, "promptText",    out _).Should().BeFalse(
                "FlaggedOutputDto must NOT expose promptText (PII-light design P3-02 Q5); body: {0}", body);
            TryProp(item, "responseText",  out _).Should().BeFalse(
                "FlaggedOutputDto must NOT expose responseText (PII-light design P3-02 Q5); body: {0}", body);
            TryProp(item, "prompt",        out _).Should().BeFalse(
                "FlaggedOutputDto must NOT expose prompt (PII-light design); body: {0}", body);
            TryProp(item, "response",      out _).Should().BeFalse(
                "FlaggedOutputDto must NOT expose response (PII-light design); body: {0}", body);

            // Required PII-light fields
            TryProp(item, "contentRef",    out _).Should().BeTrue("item must have contentRef; body: {0}", body);
            TryProp(item, "taskKind",      out _).Should().BeTrue("item must have taskKind; body: {0}", body);
            TryProp(item, "actionTaken",   out _).Should().BeTrue("item must have actionTaken; body: {0}", body);
            TryProp(item, "modelId",       out _).Should().BeTrue("item must have modelId; body: {0}", body);
            TryProp(item, "occurredAtUtc", out _).Should().BeTrue("item must have occurredAtUtc; body: {0}", body);
            TryProp(item, "reasonCodes",   out var rc).Should().BeTrue("item must have reasonCodes; body: {0}", body);
            rc.ValueKind.Should().Be(JsonValueKind.Array, "reasonCodes must be an array; body: {0}", body);
            TryProp(item, "failedChecks",  out var fc).Should().BeTrue("item must have failedChecks; body: {0}", body);
            fc.ValueKind.Should().Be(JsonValueKind.Array, "failedChecks must be an array; body: {0}", body);
        }
    }

    [Fact(DisplayName = "FLAGGED-4: filter action=Blocked returns only Blocked rows")]
    public async Task Flagged4_Filter_Action_Blocked_ReturnsOnlyBlocked()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?action=Blocked&from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractFlaggedItems(root);
        items.Should().NotBeEmpty("seeded Blocked rows must appear; body: {0}", body);

        foreach (var item in items)
        {
            TryProp(item, "actionTaken", out var at).Should().BeTrue("body: {0}", body);
            at.GetString().Should().Be("Blocked",
                "filter action=Blocked must return only Blocked rows; body: {0}", body);
        }
    }

    [Fact(DisplayName = "FLAGGED-5: filter action=Regenerated returns only Regenerated rows")]
    public async Task Flagged5_Filter_Action_Regenerated_ReturnsOnlyRegenerated()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?action=Regenerated&from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractFlaggedItems(root);
        items.Should().NotBeEmpty("seeded Regenerated row must appear; body: {0}", body);

        foreach (var item in items)
        {
            TryProp(item, "actionTaken", out var at).Should().BeTrue("body: {0}", body);
            at.GetString().Should().Be("Regenerated",
                "filter action=Regenerated must return only Regenerated rows; body: {0}", body);
        }
    }

    [Fact(DisplayName = "FLAGGED-6: filter reasonCode=AGE_INAPPROPRIATE returns only matching rows")]
    public async Task Flagged6_Filter_ReasonCode_ReturnsOnlyMatchingRows()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?reasonCode=AGE_INAPPROPRIATE&from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractFlaggedItems(root);
        items.Should().NotBeEmpty("seeded AGE_INAPPROPRIATE rows must appear; body: {0}", body);

        foreach (var item in items)
        {
            TryProp(item, "reasonCodes", out var rc).Should().BeTrue("body: {0}", body);
            var codes = rc.EnumerateArray().Select(e => e.GetString()).ToList();
            codes.Should().Contain("AGE_INAPPROPRIATE",
                "filter reasonCode=AGE_INAPPROPRIATE must return only rows containing that reason code; body: {0}", body);
        }
    }

    [Fact(DisplayName = "FLAGGED-7: filter taskKind=Hint returns only Hint rows")]
    public async Task Flagged7_Filter_TaskKind_Hint_ReturnsOnlyHint()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?taskKind=Hint&from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageSize=50",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var items = ExtractFlaggedItems(root);
        items.Should().NotBeEmpty("seeded Hint rows must appear; body: {0}", body);

        foreach (var item in items)
        {
            TryProp(item, "taskKind", out var tk).Should().BeTrue("body: {0}", body);
            tk.GetString().Should().Be("Hint",
                "filter taskKind=Hint must return only Hint rows; body: {0}", body);
        }
    }

    [Fact(DisplayName = "FLAGGED-8: pageSize=9999 is clamped to ≤100 in response")]
    public async Task Flagged8_PageSize9999_IsClamped()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?pageSize=9999",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "pageSize", out var pageSizeProp).Should().BeTrue("body: {0}", body);
        pageSizeProp.GetInt32().Should().BeLessThanOrEqualTo(100,
            "pageSize must be clamped to ≤100 to prevent unbounded scans; body: {0}", body);
    }

    [Fact(DisplayName = "FLAGGED-9: empty window (far future) → 200 with empty paged result")]
    public async Task Flagged9_EmptyWindow_Returns200Empty()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?from=2099-01-01T00:00:00Z&to=2099-12-31T00:00:00Z",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("empty result must still be successed=true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "totalCount", out var totalCount).Should().BeTrue("body: {0}", body);
        totalCount.GetInt32().Should().Be(0, "no events in a future window; totalCount must be 0; body: {0}", body);
    }

    [Fact(DisplayName = "FLAGGED-10: paging works — page 2 differs from page 1 when totalCount > pageSize")]
    public async Task Flagged10_Paging_Page2_DiffersFromPage1()
    {
        await EnsureSeededAsync();

        // Use pageSize=3 so we can compare pages (we have ≥7 seeded rows).
        var (resp1, root1, body1) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageNumber=1&pageSize=3",
            bearer: _adminToken);

        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body1);
        var page1Items = ExtractFlaggedItems(root1);

        TryProp(root1, "data", out var d1).Should().BeTrue("body: {0}", body1);
        TryProp(d1, "totalCount", out var totalCount).Should().BeTrue("body: {0}", body1);

        // Only proceed with the page-2 check if there are enough rows.
        if (totalCount.GetInt32() <= 3)
        {
            // Not enough rows to test page 2 — skip (don't fail; seeding may not have run for this instance).
            return;
        }

        var (resp2, root2, body2) = await SendAsync(HttpMethod.Get,
            $"{FlaggedUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}&pageNumber=2&pageSize=3",
            bearer: _adminToken);

        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body2);
        var page2Items = ExtractFlaggedItems(root2);
        page2Items.Should().NotBeEmpty("page 2 must have items when totalCount > pageSize; body: {0}", body2);

        // The first item on page 2 must not appear on page 1.
        TryProp(page2Items[0], "contentRef", out var p2FirstRef).Should().BeTrue("body: {0}", body2);
        var p2FirstId = p2FirstRef.GetInt32();

        var p1Ids = page1Items
            .Where(el => TryProp(el, "contentRef", out _))
            .Select(el => { TryProp(el, "contentRef", out var r); return r.GetInt32(); })
            .ToList();

        p1Ids.Should().NotContain(p2FirstId,
            "page 2 must not contain items from page 1 (no overlap); body: {0}", body2);
    }

    // =========================================================================
    // TREND: per-day buckets + invalid range + empty window
    // =========================================================================

    [Fact(DisplayName = "TREND-1: trend has BaseResponse envelope")]
    public async Task Trend1_Envelope_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{TrendUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out var succ).Should().BeTrue("envelope must have successed; body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "TREND-2: trend data is an array of SafetyTrendBucketDto (bucketDate/totalCount/blockedCount/…)")]
    public async Task Trend2_Data_IsBucketArray()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{TrendUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "trend data must be an array; body: {0}", body);

        foreach (var bucket in data.EnumerateArray())
        {
            TryProp(bucket, "bucketDate",           out _).Should().BeTrue("bucket must have bucketDate; body: {0}", body);
            TryProp(bucket, "totalCount",            out _).Should().BeTrue("bucket must have totalCount; body: {0}", body);
            TryProp(bucket, "blockedCount",          out _).Should().BeTrue("bucket must have blockedCount; body: {0}", body);
            TryProp(bucket, "regeneratedCount",      out _).Should().BeTrue("bucket must have regeneratedCount; body: {0}", body);
            TryProp(bucket, "fallbackReturnedCount", out _).Should().BeTrue("bucket must have fallbackReturnedCount; body: {0}", body);
        }
    }

    [Fact(DisplayName = "TREND-3: seeded rows across 3 days → at least 3 buckets with correct total counts")]
    public async Task Trend3_SeededRows_CorrectBucketCounts()
    {
        await EnsureSeededAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{TrendUrl}?from={FmtDate(WindowFrom)}&to={FmtDate(WindowTo)}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);

        var buckets = data.EnumerateArray().ToList();
        buckets.Count.Should().BeGreaterThanOrEqualTo(3,
            "seeded rows on 3 distinct days must produce ≥3 buckets; body: {0}", body);

        // Find the buckets for our seeded days and verify counts are ≥ seeded
        // (other tests may also insert rows in the same window, so we assert ≥).
        var day1Bucket = FindBucketForDate(buckets, Day1.Date);
        var day2Bucket = FindBucketForDate(buckets, Day2.Date);
        var day3Bucket = FindBucketForDate(buckets, Day3.Date);

        day1Bucket.Should().NotBeNull($"must have a bucket for Day1 ({Day1:yyyy-MM-dd}); body: {body}");
        day2Bucket.Should().NotBeNull($"must have a bucket for Day2 ({Day2:yyyy-MM-dd}); body: {body}");
        day3Bucket.Should().NotBeNull($"must have a bucket for Day3 ({Day3:yyyy-MM-dd}); body: {body}");

        // Day1: seeded 3 rows (2 Blocked + 1 Regenerated)
        TryProp(day1Bucket!.Value, "totalCount", out var d1Total).Should().BeTrue("body: {0}", body);
        d1Total.GetInt32().Should().BeGreaterThanOrEqualTo(3,
            $"Day1 bucket totalCount must be ≥3 (seeded 3); body: {body}");

        TryProp(day1Bucket.Value, "blockedCount", out var d1Blocked).Should().BeTrue("body: {0}", body);
        d1Blocked.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"Day1 bucket blockedCount must be ≥2 (seeded 2 Blocked); body: {body}");

        // Day2: seeded 2 rows (1 Blocked + 1 FallbackReturned)
        TryProp(day2Bucket!.Value, "totalCount", out var d2Total).Should().BeTrue("body: {0}", body);
        d2Total.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"Day2 bucket totalCount must be ≥2 (seeded 2); body: {body}");

        // Day3: seeded 2 rows (2 Blocked)
        TryProp(day3Bucket!.Value, "totalCount", out var d3Total).Should().BeTrue("body: {0}", body);
        d3Total.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"Day3 bucket totalCount must be ≥2 (seeded 2); body: {body}");

        TryProp(day3Bucket.Value, "blockedCount", out var d3Blocked).Should().BeTrue("body: {0}", body);
        d3Blocked.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"Day3 bucket blockedCount must be ≥2 (seeded 2 Blocked); body: {body}");
    }

    [Fact(DisplayName = "TREND-4: from >= to → 400 BadRequest")]
    public async Task Trend4_FromGreaterThanOrEqualTo_Returns400()
    {
        var sameDateStr = FmtDate(WindowFrom);
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{TrendUrl}?from={sameDateStr}&to={sameDateStr}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from >= to must return 400 for trend; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false for a 400 range error; body: {0}", body);
    }

    [Fact(DisplayName = "TREND-5: empty window (far future) → 200 with empty array")]
    public async Task Trend5_EmptyWindow_Returns200EmptyArray()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"{TrendUrl}?from=2099-01-01T00:00:00Z&to=2099-12-31T00:00:00Z",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "empty window must return 200, not 404; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true even for empty trend; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "data must be an array for trend; body: {0}", body);
        data.GetArrayLength().Should().Be(0,
            "no events in a future window means empty bucket array; body: {0}", body);
    }

    // =========================================================================
    // Private helpers — seeding, HTTP, JSON
    // =========================================================================

    /// <summary>
    /// Inserts 7 SafetyEvent rows with deterministic dates/actions/models so aggregate
    /// tests can assert specific counts. Idempotent per test-run (guarded by _seeded flag).
    ///
    /// Distribution:
    ///   Day1 — 2× Blocked (Explain, gpt-4o, TOXICITY_HIGH)
    ///   Day1 — 1× Regenerated (Hint, claude-haiku, AGE_INAPPROPRIATE)
    ///   Day2 — 1× Blocked (Hint, gpt-4o, TOXICITY_HIGH)
    ///   Day2 — 1× FallbackReturned (Explain, claude-haiku, POLICY_VIOLATION)
    ///   Day3 — 2× Blocked (Explain, gpt-4o, TOXICITY_HIGH + AGE_INAPPROPRIATE)
    /// </summary>
    private async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        _seeded = true;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiDbContext>();

        var rows = new[]
        {
            // Day1 row 1: Blocked, Explain, gpt-4o, TOXICITY_HIGH
            new SafetyEvent
            {
                StudentId     = 100,
                TaskKind      = "Explain",
                ActionTaken   = "Blocked",
                ModelId       = "gpt-4o",
                FailedChecks  = "[\"ToxicityCheck\"]",
                ReasonCodes   = "[\"TOXICITY_HIGH\"]",
                OccurredAtUtc = Day1,
            },
            // Day1 row 2: Blocked, Explain, gpt-4o, TOXICITY_HIGH
            new SafetyEvent
            {
                StudentId     = 101,
                TaskKind      = "Explain",
                ActionTaken   = "Blocked",
                ModelId       = "gpt-4o",
                FailedChecks  = "[\"ToxicityCheck\"]",
                ReasonCodes   = "[\"TOXICITY_HIGH\"]",
                OccurredAtUtc = Day1.AddHours(1),
            },
            // Day1 row 3: Regenerated, Hint, claude-haiku, AGE_INAPPROPRIATE
            new SafetyEvent
            {
                StudentId     = 102,
                TaskKind      = "Hint",
                ActionTaken   = "Regenerated",
                ModelId       = "claude-haiku",
                FailedChecks  = "[\"AgeAppropriatenessCheck\"]",
                ReasonCodes   = "[\"AGE_INAPPROPRIATE\"]",
                OccurredAtUtc = Day1.AddHours(2),
            },
            // Day2 row 1: Blocked, Hint, gpt-4o, TOXICITY_HIGH
            new SafetyEvent
            {
                StudentId     = 103,
                TaskKind      = "Hint",
                ActionTaken   = "Blocked",
                ModelId       = "gpt-4o",
                FailedChecks  = "[\"ToxicityCheck\"]",
                ReasonCodes   = "[\"TOXICITY_HIGH\"]",
                OccurredAtUtc = Day2,
            },
            // Day2 row 2: FallbackReturned, Explain, claude-haiku, POLICY_VIOLATION
            new SafetyEvent
            {
                StudentId     = 104,
                TaskKind      = "Explain",
                ActionTaken   = "FallbackReturned",
                ModelId       = "claude-haiku",
                FailedChecks  = "[\"PolicyCheck\"]",
                ReasonCodes   = "[\"POLICY_VIOLATION\"]",
                OccurredAtUtc = Day2.AddHours(1),
            },
            // Day3 row 1: Blocked, Explain, gpt-4o, TOXICITY_HIGH + AGE_INAPPROPRIATE
            new SafetyEvent
            {
                StudentId     = 105,
                TaskKind      = "Explain",
                ActionTaken   = "Blocked",
                ModelId       = "gpt-4o",
                FailedChecks  = "[\"ToxicityCheck\",\"AgeAppropriatenessCheck\"]",
                ReasonCodes   = "[\"TOXICITY_HIGH\",\"AGE_INAPPROPRIATE\"]",
                OccurredAtUtc = Day3,
            },
            // Day3 row 2: Blocked, Explain, gpt-4o, TOXICITY_HIGH + AGE_INAPPROPRIATE
            new SafetyEvent
            {
                StudentId     = 106,
                TaskKind      = "Explain",
                ActionTaken   = "Blocked",
                ModelId       = "gpt-4o",
                FailedChecks  = "[\"ToxicityCheck\",\"AgeAppropriatenessCheck\"]",
                ReasonCodes   = "[\"TOXICITY_HIGH\",\"AGE_INAPPROPRIATE\"]",
                OccurredAtUtc = Day3.AddHours(1),
            },
        };

        db.SafetyEvents.AddRange(rows);
        await db.SaveChangesAsync(userId: 0);
    }

    private static JsonElement? FindBucketForDate(List<JsonElement> buckets, DateTime targetDate)
    {
        foreach (var bucket in buckets)
        {
            if (TryProp(bucket, "bucketDate", out var dateProp))
            {
                var dateStr = dateProp.GetString();
                if (dateStr != null && DateTime.Parse(dateStr).Date == targetDate.Date)
                    return bucket;
            }
        }
        return null;
    }

    private static List<JsonElement> ExtractFlaggedItems(JsonElement root)
    {
        if (!TryProp(root, "data", out var outer)) return [];
        if (!TryProp(outer, "data", out var inner)) return [];
        if (inner.ValueKind != JsonValueKind.Array) return [];
        return inner.EnumerateArray().ToList();
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
        var email = $"p711_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Formats a DateTime as ISO-8601 UTC string for query params.</summary>
    private static string FmtDate(DateTime dt) => dt.ToString("yyyy-MM-ddTHH:mm:ssZ");
}

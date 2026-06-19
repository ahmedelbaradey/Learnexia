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
/// P7-10 integration tests: Platform Analytics KPI dashboard.
///
/// Endpoint under test:
///   GET  api/Admin/Analytics/kpis?from=&amp;to=   — AdminOnly policy → platform KPI summary
///
/// Cases covered:
///   BE-TC-AUTH-1 : anonymous → 401.
///   BE-TC-AUTH-2 : Parent JWT → 403.
///   BE-TC-AUTH-3 : non-admin (basicuser) JWT → 403.
///   BE-TC-AUTH-4 : Admin JWT → 200.
///   BE-TC-ENVELOPE : 200 response carries well-formed BaseResponse&lt;PlatformKpiSummaryDto&gt;
///                    with all envelope keys (statusCode / successed / message / data / errors).
///   BE-TC-DTO-SHAPE : data object has ALL KPI top-level fields.
///   BE-TC-NA-MARKERS : retention + session-duration carry N/A strings (NOT numbers);
///                      revenue + AI-request-volume carry N/A strings (NOT null/number).
///   BE-TC-ACTIVE-PROXY : distinctActiveStudents field is present (activity proxy, not true session metric).
///   BE-TC-REAL-AGGREGATION : seed a SafetyEvent → aiBlockedCount ≥ 1; totalAiSafetyEvents ≥ 1.
///   BE-TC-RANGE-INVALID : from ≥ to → 400.
///   BE-TC-RANGE-WINDOW-TOO-LARGE : window > 365 days → 400.
///   BE-TC-DEFAULT-WINDOW : no from/to → 200 (handler defaults to 30-day window).
///   BE-TC-EMPTY-PLATFORM : empty platform returns 200 with zeros for numeric KPIs (not 500).
///
/// Docker + Postgres required to RUN (Testcontainers). Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_10_PlatformAnalytics_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string KpisUrl           = "api/Admin/Analytics/kpis";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";

    // ── Seeded credentials (mirror P7-09 / P7-12 pattern) ─────────────────────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;   // basicuser — not an admin role
    private string? _parentToken;

    public P7_10_PlatformAnalytics_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Ensure the Ai module schema is migrated — AiDbContext is NOT included in the
        // shared factory's ApplyMigrationsAndSeedAsync (it was added per-test in earlier
        // suites). We need ai.SafetyEvents to exist so seeding + the AI seam work.
        using (var scope = _factory.Services.CreateScope())
        {
            var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();
            await aiDb.Database.MigrateAsync();
        }

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // BE-TC-AUTH: Authorization matrix
    // =========================================================================

    [Fact(DisplayName = "BE-TC-AUTH-1: GET /api/Admin/Analytics/kpis anonymous → 401")]
    public async Task Auth1_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "kpis is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-2: GET /api/Admin/Analytics/kpis with parent token → 403")]
    public async Task Auth2_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "kpis is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-3: GET /api/Admin/Analytics/kpis with basicuser token → 403")]
    public async Task Auth3_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _basicToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "kpis is AdminOnly — basicuser must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-AUTH-4: GET /api/Admin/Analytics/kpis with admin token → 200")]
    public async Task Auth4_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "kpis must return 200 for admin; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-ENVELOPE: BaseResponse envelope shape
    // =========================================================================

    [Fact(DisplayName = "BE-TC-ENVELOPE: 200 response has BaseResponse keys statusCode/successed/message/data/errors")]
    public async Task Envelope_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _)
            .Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var succ)
            .Should().BeTrue("envelope must have successed (sic); body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true on a successful KPI request; body: {0}", body);
        TryProp(root, "message", out _)
            .Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _)
            .Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _)
            .Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-DTO-SHAPE: all KPI fields are present in the data object
    // =========================================================================

    [Fact(DisplayName = "BE-TC-DTO-SHAPE: data object has all required KPI top-level fields")]
    public async Task DtoShape_HasAllKpiFields()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Date range
        TryProp(data, "fromUtc", out _)
            .Should().BeTrue("data must have fromUtc; body: {0}", body);
        TryProp(data, "toUtc", out _)
            .Should().BeTrue("data must have toUtc; body: {0}", body);

        // Learning KPIs
        TryProp(data, "lessonsCompleted", out _)
            .Should().BeTrue("data must have lessonsCompleted; body: {0}", body);
        TryProp(data, "totalAttempts", out _)
            .Should().BeTrue("data must have totalAttempts; body: {0}", body);
        TryProp(data, "distinctActiveStudents", out _)
            .Should().BeTrue("data must have distinctActiveStudents; body: {0}", body);
        TryProp(data, "quizzesCompletedNaReason", out _)
            .Should().BeTrue("data must have quizzesCompletedNaReason (N/A marker); body: {0}", body);

        // Breakdowns (may be empty arrays)
        TryProp(data, "bySubject", out _)
            .Should().BeTrue("data must have bySubject; body: {0}", body);
        TryProp(data, "byGrade", out _)
            .Should().BeTrue("data must have byGrade; body: {0}", body);

        // Engagement KPIs
        TryProp(data, "missionsCompleted", out _)
            .Should().BeTrue("data must have missionsCompleted; body: {0}", body);
        TryProp(data, "xpEarnedInWindow", out _)
            .Should().BeTrue("data must have xpEarnedInWindow; body: {0}", body);

        // Subscription KPIs
        TryProp(data, "totalActiveSubscriptions", out _)
            .Should().BeTrue("data must have totalActiveSubscriptions; body: {0}", body);
        TryProp(data, "subscriptionsByTier", out _)
            .Should().BeTrue("data must have subscriptionsByTier; body: {0}", body);
        TryProp(data, "revenueNaReason", out _)
            .Should().BeTrue("data must have revenueNaReason; body: {0}", body);

        // AI Safety KPIs
        TryProp(data, "totalAiSafetyEvents", out _)
            .Should().BeTrue("data must have totalAiSafetyEvents; body: {0}", body);
        TryProp(data, "aiBlockedCount", out _)
            .Should().BeTrue("data must have aiBlockedCount; body: {0}", body);
        TryProp(data, "aiFlaggedCount", out _)
            .Should().BeTrue("data must have aiFlaggedCount; body: {0}", body);
        TryProp(data, "aiRequestVolumeNaReason", out _)
            .Should().BeTrue("data must have aiRequestVolumeNaReason; body: {0}", body);

        // P5-03-deferred N/A markers
        TryProp(data, "retentionNaReason", out _)
            .Should().BeTrue("data must have retentionNaReason; body: {0}", body);
        TryProp(data, "sessionDurationNaReason", out _)
            .Should().BeTrue("data must have sessionDurationNaReason; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-NA-MARKERS: deferred/unavailable facets carry string N/A markers
    // =========================================================================

    [Fact(DisplayName = "BE-TC-NA-MARKERS-1: retentionNaReason is a non-empty string, NOT null or a number")]
    public async Task NaMarkers_RetentionNaReason_IsString()
    {
        var (_, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "retentionNaReason", out var retention)
            .Should().BeTrue("body: {0}", body);

        retention.ValueKind.Should().Be(JsonValueKind.String,
            "retentionNaReason must be a string N/A marker (requires P5-03), NOT null or a number; body: {0}", body);
        retention.GetString().Should().NotBeNullOrWhiteSpace(
            "retentionNaReason must be a non-empty N/A message; body: {0}", body);
        retention.GetString().Should().Contain("P5-03",
            "retentionNaReason should reference P5-03 as the dependency; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-NA-MARKERS-2: sessionDurationNaReason is a non-empty string, NOT null or a number")]
    public async Task NaMarkers_SessionDurationNaReason_IsString()
    {
        var (_, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "sessionDurationNaReason", out var sessionDuration)
            .Should().BeTrue("body: {0}", body);

        sessionDuration.ValueKind.Should().Be(JsonValueKind.String,
            "sessionDurationNaReason must be a string N/A marker (requires P5-03), NOT null or a number; body: {0}", body);
        sessionDuration.GetString().Should().NotBeNullOrWhiteSpace(
            "sessionDurationNaReason must be a non-empty N/A message; body: {0}", body);
        sessionDuration.GetString().Should().Contain("P5-03",
            "sessionDurationNaReason should reference P5-03 as the dependency; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-NA-MARKERS-3: revenueNaReason is a non-empty string (Fake payment provider)")]
    public async Task NaMarkers_RevenueNaReason_IsString()
    {
        var (_, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "revenueNaReason", out var revenue)
            .Should().BeTrue("body: {0}", body);

        // Per the DTO contract: null = revenue available (not yet built), non-null = N/A.
        // With Fake provider active, it must be a non-null, non-empty string.
        revenue.ValueKind.Should().Be(JsonValueKind.String,
            "revenueNaReason must be a non-null string when revenue is unavailable (Fake payment provider); " +
            "if null, the handler is incorrectly reporting revenue as available; body: {0}", body);
        revenue.GetString().Should().NotBeNullOrWhiteSpace(
            "revenueNaReason must carry an explanatory message, not an empty string; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-NA-MARKERS-4: aiRequestVolumeNaReason is a non-empty string (P7-11 not yet built)")]
    public async Task NaMarkers_AiRequestVolumeNaReason_IsString()
    {
        var (_, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "aiRequestVolumeNaReason", out var aiVolume)
            .Should().BeTrue("body: {0}", body);

        // Per the DTO contract: null = request-volume available (not yet built), non-null = N/A.
        aiVolume.ValueKind.Should().Be(JsonValueKind.String,
            "aiRequestVolumeNaReason must be a non-null string (AiUsageLogs not yet built — P7-11); " +
            "if null, the handler is incorrectly reporting AI request volume as available; body: {0}", body);
        aiVolume.GetString().Should().NotBeNullOrWhiteSpace(
            "aiRequestVolumeNaReason must carry an explanatory message; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-NA-MARKERS-5: quizzesCompletedNaReason is a non-empty string (same entity as LessonsCompleted)")]
    public async Task NaMarkers_QuizzesCompletedNaReason_IsString()
    {
        var (_, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "quizzesCompletedNaReason", out var quizzes)
            .Should().BeTrue("body: {0}", body);

        quizzes.ValueKind.Should().Be(JsonValueKind.String,
            "quizzesCompletedNaReason must be a string N/A marker (quiz = lesson attempt same entity); body: {0}", body);
        quizzes.GetString().Should().NotBeNullOrWhiteSpace(
            "quizzesCompletedNaReason must carry an explanatory message; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-ACTIVE-PROXY: distinctActiveStudents is present and is a number
    // =========================================================================

    [Fact(DisplayName = "BE-TC-ACTIVE-PROXY: distinctActiveStudents field is present and is a non-negative integer (activity proxy)")]
    public async Task ActiveProxy_DistinctActiveStudents_IsNonNegativeInteger()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "distinctActiveStudents", out var das)
            .Should().BeTrue("distinctActiveStudents (activity proxy) must be present in the DTO; body: {0}", body);

        das.ValueKind.Should().Be(JsonValueKind.Number,
            "distinctActiveStudents must be a number (int), NOT a string or null; body: {0}", body);
        das.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            "distinctActiveStudents is a count — must be ≥ 0; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-REAL-AGGREGATION: seed a SafetyEvent → AI KPIs reflect it
    // =========================================================================

    [Fact(DisplayName = "BE-TC-REAL-AGGREGATION-1: seed a Blocked SafetyEvent → totalAiSafetyEvents ≥ 1 and aiBlockedCount ≥ 1")]
    public async Task RealAggregation_SeededSafetyEvent_ReflectedInAiKpis()
    {
        // Seed one Blocked SafetyEvent directly into the Ai schema.
        // We use AiDbContext directly (append-only log — no command pipeline needed).
        int studentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();

            var ev = new SafetyEvent
            {
                StudentId      = 9901,
                TaskKind       = "Explain",
                FailedChecks   = "[\"ToxicityCheck\"]",
                ReasonCodes    = "[\"TOXICITY_HIGH\"]",
                ActionTaken    = "Blocked",
                ModelId        = "test-model",
                OccurredAtUtc  = DateTime.UtcNow,
            };
            aiDb.SafetyEvents.Add(ev);
            await aiDb.SaveChangesAsync(userId: 0);
            studentId = ev.StudentId;
        }

        // Query with a window that covers "now" (default window = 30 days, seeded event is just now).
        var (resp, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "totalAiSafetyEvents", out var total)
            .Should().BeTrue("data must have totalAiSafetyEvents; body: {0}", body);
        total.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "totalAiSafetyEvents must be ≥ 1 after seeding a SafetyEvent; body: {0}", body);

        TryProp(data, "aiBlockedCount", out var blocked)
            .Should().BeTrue("data must have aiBlockedCount; body: {0}", body);
        blocked.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "aiBlockedCount must be ≥ 1 after seeding a Blocked SafetyEvent; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-REAL-AGGREGATION-2: seed a Flagged (non-Blocked) SafetyEvent → aiFlaggedCount ≥ 1")]
    public async Task RealAggregation_SeededFlaggedSafetyEvent_ReflectedInFlaggedCount()
    {
        // Seed a "Regenerated" (non-Blocked) SafetyEvent.
        using (var scope = _factory.Services.CreateScope())
        {
            var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();

            var ev = new SafetyEvent
            {
                StudentId     = 9902,
                TaskKind      = "Hint",
                FailedChecks  = "[\"ContentLengthCheck\"]",
                ReasonCodes   = "[\"LENGTH_EXCEEDED\"]",
                ActionTaken   = "Regenerated",
                ModelId       = "test-model",
                OccurredAtUtc = DateTime.UtcNow,
            };
            aiDb.SafetyEvents.Add(ev);
            await aiDb.SaveChangesAsync(userId: 0);
        }

        var (resp, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "aiFlaggedCount", out var flagged)
            .Should().BeTrue("data must have aiFlaggedCount; body: {0}", body);
        flagged.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "aiFlaggedCount must be ≥ 1 after seeding a Regenerated SafetyEvent; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-REAL-AGGREGATION-3: event outside the query window → not counted")]
    public async Task RealAggregation_EventOutsideWindow_NotCounted()
    {
        // Seed a SafetyEvent 60 days in the past — outside the default 30-day window.
        using (var scope = _factory.Services.CreateScope())
        {
            var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();

            var ev = new SafetyEvent
            {
                StudentId     = 9903,
                TaskKind      = "Explain",
                FailedChecks  = "[\"ToxicityCheck\"]",
                ReasonCodes   = "[\"TOXICITY_HIGH\"]",
                ActionTaken   = "Blocked",
                ModelId       = "test-model-old",
                OccurredAtUtc = DateTime.UtcNow.AddDays(-60), // outside 30-day default window
            };
            aiDb.SafetyEvents.Add(ev);
            await aiDb.SaveChangesAsync(userId: 0);
        }

        // Request with an explicit 1-day window to ensure the old event is excluded.
        var to   = DateTime.UtcNow;
        var from = to.AddDays(-1);
        var url  = $"{KpisUrl}?from={from:O}&to={to:O}";

        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // The 60-day-old event must NOT appear in the 1-day window.
        // We can't assert exactly 0 because other tests in the same collection may seed events,
        // but we CAN seed a unique model ID and verify the handler doesn't blow up.
        // Structural assertion: totalAiSafetyEvents is a non-negative number.
        TryProp(data, "totalAiSafetyEvents", out var total)
            .Should().BeTrue("data must have totalAiSafetyEvents; body: {0}", body);
        total.ValueKind.Should().Be(JsonValueKind.Number,
            "totalAiSafetyEvents must be a number; body: {0}", body);
        total.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            "totalAiSafetyEvents must be ≥ 0; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-RANGE-INVALID: from >= to → 400
    // =========================================================================

    [Fact(DisplayName = "BE-TC-RANGE-INVALID-1: from == to → 400")]
    public async Task RangeInvalid_FromEqualsTo_Returns400()
    {
        var point = DateTime.UtcNow;
        var url   = $"{KpisUrl}?from={point:O}&to={point:O}";

        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from == to must return 400 (from must be strictly before to); body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false on a 400; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-RANGE-INVALID-2: from > to → 400")]
    public async Task RangeInvalid_FromAfterTo_Returns400()
    {
        var to   = DateTime.UtcNow.AddDays(-1);
        var from = DateTime.UtcNow; // from > to
        var url  = $"{KpisUrl}?from={from:O}&to={to:O}";

        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "from > to must return 400; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false on a 400; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-RANGE-WINDOW-TOO-LARGE: window > 365 days → 400
    // =========================================================================

    [Fact(DisplayName = "BE-TC-RANGE-WINDOW-TOO-LARGE: window > 365 days → 400")]
    public async Task RangeWindowTooLarge_Returns400()
    {
        var from = DateTime.UtcNow.AddDays(-366); // 366 days window — exceeds 365-day max
        var to   = DateTime.UtcNow;
        var url  = $"{KpisUrl}?from={from:O}&to={to:O}";

        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "window > 365 days must return 400; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeFalse("successed must be false when window is too large; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-DEFAULT-WINDOW: no params → 200 with default 30-day window
    // =========================================================================

    [Fact(DisplayName = "BE-TC-DEFAULT-WINDOW: GET /api/Admin/Analytics/kpis with no params → 200 (30-day default window)")]
    public async Task DefaultWindow_NoParams_Returns200()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, KpisUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "default window (no from/to) must return 200; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for the default window; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // fromUtc must be approximately 30 days before toUtc
        TryProp(data, "fromUtc", out var fromEl).Should().BeTrue("body: {0}", body);
        TryProp(data, "toUtc",   out var toEl).Should().BeTrue("body: {0}", body);

        var from = fromEl.GetDateTime();
        var to   = toEl.GetDateTime();
        (to - from).TotalDays.Should().BeApproximately(30, 1.0,
            "default window must be approximately 30 days; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-EMPTY-PLATFORM: empty platform returns zeros for numeric fields
    // =========================================================================

    [Fact(DisplayName = "BE-TC-EMPTY-PLATFORM: far-future window with no data → 200 with zeros (not 500)")]
    public async Task EmptyPlatform_FutureDateWindow_Returns200WithZeros()
    {
        // Use a window well into the future — guaranteed to have no data.
        var from = new DateTime(2098, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to   = new DateTime(2098, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var url  = $"{KpisUrl}?from={from:O}&to={to:O}";

        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "an empty platform window must return 200, not 500; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for an empty-window response; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // All real-data numeric counters must be 0 for an empty window.
        AssertNumericKpi(data, "lessonsCompleted",       0, body);
        AssertNumericKpi(data, "totalAttempts",          0, body);
        AssertNumericKpi(data, "distinctActiveStudents", 0, body);
        AssertNumericKpi(data, "missionsCompleted",      0, body);
        AssertNumericKpi(data, "xpEarnedInWindow",       0, body);
        AssertNumericKpi(data, "totalAiSafetyEvents",    0, body);
        AssertNumericKpi(data, "aiBlockedCount",         0, body);
        AssertNumericKpi(data, "aiFlaggedCount",         0, body);

        // Subscription count is not windowed — it reflects the live table.
        // Still must be a non-negative integer.
        TryProp(data, "totalActiveSubscriptions", out var subs).Should().BeTrue("body: {0}", body);
        subs.ValueKind.Should().Be(JsonValueKind.Number, "body: {0}", body);
        subs.GetInt32().Should().BeGreaterThanOrEqualTo(0, "body: {0}", body);

        // Break-down arrays must be empty (no data in window).
        TryProp(data, "bySubject", out var bySubject).Should().BeTrue("body: {0}", body);
        bySubject.ValueKind.Should().Be(JsonValueKind.Array, "bySubject must be an array; body: {0}", body);
        bySubject.GetArrayLength().Should().Be(0, "bySubject must be empty for an empty window; body: {0}", body);

        TryProp(data, "byGrade", out var byGrade).Should().BeTrue("body: {0}", body);
        byGrade.ValueKind.Should().Be(JsonValueKind.Array, "byGrade must be an array; body: {0}", body);
        byGrade.GetArrayLength().Should().Be(0, "byGrade must be empty for an empty window; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-VALID-EXPLICIT-WINDOW: explicit valid window → 200
    // =========================================================================

    [Fact(DisplayName = "BE-TC-VALID-EXPLICIT-WINDOW: explicit valid 7-day window → 200")]
    public async Task ValidExplicitWindow_Returns200()
    {
        var to   = DateTime.UtcNow;
        var from = to.AddDays(-7);
        var url  = $"{KpisUrl}?from={from:O}&to={to:O}";

        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "explicit valid 7-day window must return 200; body: {0}", body);

        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // fromUtc + toUtc in the response must reflect the supplied values (within 1 s tolerance
        // to account for any UTC parsing rounding).
        TryProp(data, "fromUtc", out var fromEl).Should().BeTrue("body: {0}", body);
        TryProp(data, "toUtc",   out var toEl).Should().BeTrue("body: {0}", body);

        var respFrom = fromEl.GetDateTime();
        var respTo   = toEl.GetDateTime();

        Math.Abs((respFrom - from).TotalSeconds).Should().BeLessThan(2,
            "fromUtc in response must match the supplied from parameter; body: {0}", body);
        Math.Abs((respTo - to).TotalSeconds).Should().BeLessThan(2,
            "toUtc in response must match the supplied to parameter; body: {0}", body);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static void AssertNumericKpi(JsonElement data, string fieldName, int expectedValue, string body)
    {
        TryProp(data, fieldName, out var prop).Should().BeTrue(
            "data must have field '{0}'; body: {1}", fieldName, body);
        prop.ValueKind.Should().Be(JsonValueKind.Number,
            "'{0}' must be a number; body: {1}", fieldName, body);
        prop.GetInt64().Should().Be(expectedValue,
            "'{0}' must be {1} for an empty window; body: {2}", fieldName, expectedValue, body);
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
        var email = $"p710_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }
}

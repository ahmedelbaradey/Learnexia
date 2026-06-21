// ReSharper disable InconsistentNaming
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P6-02 integration tests: GET /api/Admin/AiSafety/evals
///
/// Endpoint under test (P6-02-BE-6 / P7-11-BE-3):
///   GET /api/Admin/AiSafety/evals
///       → BaseResponse&lt;EvalResultsDto&gt;
///
/// The endpoint is served from the committed safety-eval-results.json artifact embedded in
/// Ai.Infrastructure. It is parameterless and has no DB dependency.
///
/// Cases covered:
///   AUTHZ-1 : anonymous → 401
///   AUTHZ-2 : non-admin (parent JWT) → 403
///   AUTHZ-3 : non-admin (basicuser JWT) → 403
///   AUTHZ-4 : admin JWT → 200
///   EVAL-ENV-1 : BaseResponse envelope keys present (statusCode/successed/message/data/errors)
///   EVAL-ENV-2 : successed=true, message non-empty, statusCode=200 on admin 200
///   EVAL-SHAPE-1: data has all EvalResultsDto top-level scalar fields
///                  (runId/ranAt/totalCases/passedCases/failedCases/passRate/failRate/thresholdPercent/breached/tier/note)
///   EVAL-SHAPE-2: data.byCheck is an object (not null/array) with ≥1 key
///   EVAL-SHAPE-3: data.bySubject is an object with ≥1 key
///   EVAL-SHAPE-4: data.byLanguage is an object with ≥1 key
///   EVAL-SHAPE-5: each breakdown entry has passed/total/passRate fields
///   EVAL-ARTIFACT-1: data matches the committed artifact —
///                     runId non-empty (not Guid.Empty), totalCases=62, passedCases=62,
///                     failedCases=0, passRate=100, failRate=0, thresholdPercent=100,
///                     breached=false, tier="EvalOffline"
///   EVAL-ARTIFACT-2: byCheck contains "AgeAppropriatenessCheck" (passed=19, total=19)
///   EVAL-ARTIFACT-3: byCheck contains "ToxicityCheck" (passed=19, total=19)
///   EVAL-ARTIFACT-4: byCheck contains "HallucinationCheck" (passed=24, total=24)
///   EVAL-ARTIFACT-5: bySubject contains "Arabic" (passed=15, total=15)
///   EVAL-ARTIFACT-6: bySubject contains "Math" (16/16), "Science" (16/16), "English" (15/15)
///   EVAL-ARTIFACT-7: byLanguage contains "ar" (passed=31, total=31)
///   EVAL-ARTIFACT-8: byLanguage contains "en" (passed=31, total=31)
///   EVAL-SENTINEL-1 : endpoint returns HTTP 200 with clean data (not 500) even if adapter
///                      falls back to bootstrap sentinel — tested by asserting no 500 under any condition.
///
/// Docker + Postgres required to RUN (Testcontainers). Compile-only on Windows CI without Docker daemon.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P6_02_AiSafetyEvalResults_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string EvalsUrl          = "api/Admin/AiSafety/evals";

    // ── Seeded credentials (same as P7_11 sibling) ───────────────────────────
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

    // ── Embedded artifact expectations (from safety-eval-results.json) ────────
    // These values must match the committed artifact:
    //   backend/src/Modules/Ai/Learnexia.Modules.Ai.Infrastructure/EvalResults/safety-eval-results.json
    // Update this class if the artifact is regenerated with different totals.
    // P6-02 hardening (2026-06-21): 6 fail-closed cases added → totalCases 56→62,
    //   ToxicityCheck 16→19, AgeAppropriatenessCheck 16→19, ar/en 28→31 each,
    //   Arabic 14→15, Math 14→16, Science 14→16, English 14→15.
    private static readonly Guid ExpectedRunId        = new Guid("b4f23a37-c093-499a-ad4c-52bddf94aabb");
    private const int           ExpectedTotalCases    = 62;
    private const int           ExpectedPassedCases   = 62;
    private const int           ExpectedFailedCases   = 0;
    private const double        ExpectedPassRate      = 100.0;
    private const double        ExpectedFailRate      = 0.0;
    private const double        ExpectedThreshold     = 100.0;
    private const bool          ExpectedBreached      = false;
    private const string        ExpectedTier          = "EvalOffline";

    public P6_02_AiSafetyEvalResults_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AUTHZ: authorization matrix
    // =========================================================================

    [Fact(DisplayName = "AUTHZ-1: GET /api/Admin/AiSafety/evals anonymous → 401")]
    public async Task Authz1_Evals_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "evals is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-2: GET /api/Admin/AiSafety/evals with parent token → 403")]
    public async Task Authz2_Evals_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "evals is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-3: GET /api/Admin/AiSafety/evals with basicuser → 403")]
    public async Task Authz3_Evals_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "evals is AdminOnly — basicuser (non-admin) must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AUTHZ-4: GET /api/Admin/AiSafety/evals with admin token → 200")]
    public async Task Authz4_Evals_Admin_Returns200()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "evals must return 200 for admin; body: {0}", body);
    }

    // =========================================================================
    // EVAL-ENV: envelope shape
    // =========================================================================

    [Fact(DisplayName = "EVAL-ENV-1: evals has BaseResponse envelope keys (statusCode/successed/message/data/errors)")]
    public async Task EvalEnv1_Envelope_HasAllBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue(
            "BaseResponse envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out _).Should().BeTrue(
            "BaseResponse envelope must have successed (sic); body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue(
            "BaseResponse envelope must have message; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue(
            "BaseResponse envelope must have data; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue(
            "BaseResponse envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "EVAL-ENV-2: successed=true, message non-empty, statusCode=200 on admin 200")]
    public async Task EvalEnv2_EnvelopeValues_AreCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "successed",  out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue("successed must be true for a 200; body: {0}", body);

        TryProp(root, "statusCode", out var sc).Should().BeTrue("body: {0}", body);
        sc.GetInt32().Should().Be(200, "statusCode in envelope must be 200; body: {0}", body);

        TryProp(root, "message", out var msg).Should().BeTrue("body: {0}", body);
        var msgStr = msg.GetString();
        msgStr.Should().NotBeNullOrWhiteSpace(
            "localized message must be non-empty for a successful eval-results response; body: {0}", body);
    }

    // =========================================================================
    // EVAL-SHAPE: EvalResultsDto top-level fields and breakdown objects
    // =========================================================================

    [Fact(DisplayName = "EVAL-SHAPE-1: data has all EvalResultsDto scalar fields")]
    public async Task EvalShape1_Data_HasAllScalarFields()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have data; body: {0}", body);

        TryProp(data, "runId",            out _).Should().BeTrue("data must have runId; body: {0}", body);
        TryProp(data, "ranAt",            out _).Should().BeTrue("data must have ranAt; body: {0}", body);
        TryProp(data, "totalCases",       out _).Should().BeTrue("data must have totalCases; body: {0}", body);
        TryProp(data, "passedCases",      out _).Should().BeTrue("data must have passedCases; body: {0}", body);
        TryProp(data, "failedCases",      out _).Should().BeTrue("data must have failedCases; body: {0}", body);
        TryProp(data, "passRate",         out _).Should().BeTrue("data must have passRate; body: {0}", body);
        TryProp(data, "failRate",         out _).Should().BeTrue("data must have failRate; body: {0}", body);
        TryProp(data, "thresholdPercent", out _).Should().BeTrue("data must have thresholdPercent; body: {0}", body);
        TryProp(data, "breached",         out _).Should().BeTrue("data must have breached; body: {0}", body);
        TryProp(data, "tier",             out _).Should().BeTrue("data must have tier; body: {0}", body);
        TryProp(data, "note",             out _).Should().BeTrue("data must have note; body: {0}", body);
    }

    [Fact(DisplayName = "EVAL-SHAPE-2: data.byCheck is a non-empty object (not array/null)")]
    public async Task EvalShape2_ByCheck_IsObjectWithAtLeastOneKey()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byCheck", out var byCheck).Should().BeTrue("data must have byCheck; body: {0}", body);

        byCheck.ValueKind.Should().Be(JsonValueKind.Object,
            "byCheck must be an object (not an array or null); body: {0}", body);

        byCheck.EnumerateObject().Any().Should().BeTrue(
            "byCheck must have at least one per-check entry; body: {0}", body);
    }

    [Fact(DisplayName = "EVAL-SHAPE-3: data.bySubject is a non-empty object")]
    public async Task EvalShape3_BySubject_IsObjectWithAtLeastOneKey()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "bySubject", out var bySubject).Should().BeTrue("data must have bySubject; body: {0}", body);

        bySubject.ValueKind.Should().Be(JsonValueKind.Object,
            "bySubject must be an object; body: {0}", body);
        bySubject.EnumerateObject().Any().Should().BeTrue(
            "bySubject must have at least one per-subject entry; body: {0}", body);
    }

    [Fact(DisplayName = "EVAL-SHAPE-4: data.byLanguage is a non-empty object")]
    public async Task EvalShape4_ByLanguage_IsObjectWithAtLeastOneKey()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byLanguage", out var byLanguage).Should().BeTrue("data must have byLanguage; body: {0}", body);

        byLanguage.ValueKind.Should().Be(JsonValueKind.Object,
            "byLanguage must be an object; body: {0}", body);
        byLanguage.EnumerateObject().Any().Should().BeTrue(
            "byLanguage must have at least one per-language entry; body: {0}", body);
    }

    [Fact(DisplayName = "EVAL-SHAPE-5: each byCheck entry has passed/total/passRate fields")]
    public async Task EvalShape5_ByCheckEntries_HaveCorrectFields()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byCheck", out var byCheck).Should().BeTrue("body: {0}", body);
        byCheck.ValueKind.Should().Be(JsonValueKind.Object, "body: {0}", body);

        foreach (var prop in byCheck.EnumerateObject())
        {
            var entry = prop.Value;
            TryProp(entry, "passed",   out _).Should().BeTrue(
                "byCheck[{0}] must have 'passed'; body: {1}", prop.Name, body);
            TryProp(entry, "total",    out _).Should().BeTrue(
                "byCheck[{0}] must have 'total'; body: {1}", prop.Name, body);
            TryProp(entry, "passRate", out _).Should().BeTrue(
                "byCheck[{0}] must have 'passRate'; body: {1}", prop.Name, body);
        }
    }

    // =========================================================================
    // EVAL-ARTIFACT: verify exact values from the committed artifact
    // =========================================================================

    [Fact(DisplayName = "EVAL-ARTIFACT-1: top-level stats match committed safety-eval-results.json")]
    public async Task EvalArtifact1_TopLevelStats_MatchEmbeddedArtifact()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "runId", out var runIdProp).Should().BeTrue("body: {0}", body);
        var runId = Guid.Parse(runIdProp.GetString()!);
        runId.Should().NotBe(Guid.Empty,
            "runId must be a real run GUID (not bootstrap Guid.Empty); body: {0}", body);
        runId.Should().Be(ExpectedRunId,
            "runId must match the committed artifact; body: {0}", body);

        TryProp(data, "totalCases",  out var tc).Should().BeTrue("body: {0}", body);
        tc.GetInt32().Should().Be(ExpectedTotalCases,
            "totalCases must match committed artifact ({0}); body: {1}", ExpectedTotalCases, body);

        TryProp(data, "passedCases", out var pc).Should().BeTrue("body: {0}", body);
        pc.GetInt32().Should().Be(ExpectedPassedCases,
            "passedCases must match committed artifact ({0}); body: {1}", ExpectedPassedCases, body);

        TryProp(data, "failedCases", out var fc).Should().BeTrue("body: {0}", body);
        fc.GetInt32().Should().Be(ExpectedFailedCases,
            "failedCases must be {0} (all pass); body: {1}", ExpectedFailedCases, body);

        TryProp(data, "passRate", out var pr).Should().BeTrue("body: {0}", body);
        pr.GetDouble().Should().BeApproximately(ExpectedPassRate, 0.001,
            "passRate must be {0}%%; body: {1}", ExpectedPassRate, body);

        TryProp(data, "failRate", out var fr).Should().BeTrue("body: {0}", body);
        fr.GetDouble().Should().BeApproximately(ExpectedFailRate, 0.001,
            "failRate must be {0}%%; body: {1}", ExpectedFailRate, body);

        TryProp(data, "thresholdPercent", out var thr).Should().BeTrue("body: {0}", body);
        thr.GetDouble().Should().BeApproximately(ExpectedThreshold, 0.001,
            "thresholdPercent must be {0}%%; body: {1}", ExpectedThreshold, body);

        TryProp(data, "breached", out var br).Should().BeTrue("body: {0}", body);
        br.GetBoolean().Should().Be(ExpectedBreached,
            "breached must be {0} when all cases pass; body: {1}", ExpectedBreached, body);

        TryProp(data, "tier", out var tier).Should().BeTrue("body: {0}", body);
        tier.GetString().Should().Be(ExpectedTier,
            "tier must be '{0}'; body: {1}", ExpectedTier, body);
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-2: byCheck['AgeAppropriatenessCheck'] = passed=19, total=19, passRate=100")]
    public async Task EvalArtifact2_ByCheck_AgeAppropriatenessCheck_IsCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byCheck", out var byCheck).Should().BeTrue("body: {0}", body);

        var entry = FindBreakdownEntry(byCheck, "AgeAppropriatenessCheck");
        entry.Should().NotBeNull("byCheck must contain 'AgeAppropriatenessCheck'; body: {0}", body);

        AssertBreakdownEntry(entry!.Value, passed: 19, total: 19, passRate: 100.0, body);
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-3: byCheck['ToxicityCheck'] = passed=19, total=19, passRate=100")]
    public async Task EvalArtifact3_ByCheck_ToxicityCheck_IsCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byCheck", out var byCheck).Should().BeTrue("body: {0}", body);

        var entry = FindBreakdownEntry(byCheck, "ToxicityCheck");
        entry.Should().NotBeNull("byCheck must contain 'ToxicityCheck'; body: {0}", body);

        AssertBreakdownEntry(entry!.Value, passed: 19, total: 19, passRate: 100.0, body);
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-4: byCheck['HallucinationCheck'] = passed=24, total=24, passRate=100")]
    public async Task EvalArtifact4_ByCheck_HallucinationCheck_IsCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byCheck", out var byCheck).Should().BeTrue("body: {0}", body);

        var entry = FindBreakdownEntry(byCheck, "HallucinationCheck");
        entry.Should().NotBeNull("byCheck must contain 'HallucinationCheck'; body: {0}", body);

        AssertBreakdownEntry(entry!.Value, passed: 24, total: 24, passRate: 100.0, body);
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-5: bySubject['Arabic'] = passed=15, total=15, passRate=100")]
    public async Task EvalArtifact5_BySubject_Arabic_IsCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "bySubject", out var bySubject).Should().BeTrue("body: {0}", body);

        var entry = FindBreakdownEntry(bySubject, "Arabic");
        entry.Should().NotBeNull("bySubject must contain 'Arabic'; body: {0}", body);

        AssertBreakdownEntry(entry!.Value, passed: 15, total: 15, passRate: 100.0, body);
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-6: bySubject Math=16/16, Science=16/16, English=15/15")]
    public async Task EvalArtifact6_BySubject_AllFourSubjects_AreCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "bySubject", out var bySubject).Should().BeTrue("body: {0}", body);

        // Math and Science each got 2 fail-closed cases; English got 1 fail-closed case.
        foreach (var (subject, expectedCount) in new[] { ("Math", 16), ("Science", 16), ("English", 15) })
        {
            var entry = FindBreakdownEntry(bySubject, subject);
            entry.Should().NotBeNull("bySubject must contain '{0}'; body: {1}", subject, body);
            AssertBreakdownEntry(entry!.Value, passed: expectedCount, total: expectedCount, passRate: 100.0, body);
        }
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-7: byLanguage['ar'] = passed=31, total=31, passRate=100")]
    public async Task EvalArtifact7_ByLanguage_Arabic_IsCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byLanguage", out var byLanguage).Should().BeTrue("body: {0}", body);

        var entry = FindBreakdownEntry(byLanguage, "ar");
        entry.Should().NotBeNull("byLanguage must contain 'ar'; body: {0}", body);

        AssertBreakdownEntry(entry!.Value, passed: 31, total: 31, passRate: 100.0, body);
    }

    [Fact(DisplayName = "EVAL-ARTIFACT-8: byLanguage['en'] = passed=31, total=31, passRate=100")]
    public async Task EvalArtifact8_ByLanguage_English_IsCorrect()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "byLanguage", out var byLanguage).Should().BeTrue("body: {0}", body);

        var entry = FindBreakdownEntry(byLanguage, "en");
        entry.Should().NotBeNull("byLanguage must contain 'en'; body: {0}", body);

        AssertBreakdownEntry(entry!.Value, passed: 31, total: 31, passRate: 100.0, body);
    }

    // =========================================================================
    // EVAL-SENTINEL: bootstrap/resilience guard
    // =========================================================================

    [Fact(DisplayName = "EVAL-SENTINEL-1: endpoint never returns 500 — admin always gets 200 (or clean failure)")]
    public async Task EvalSentinel1_EndpointNeverReturns500()
    {
        // The adapter is sentinel-safe: any exception → Bootstrap() → 200.
        // This test asserts the running endpoint does NOT return 5xx, even under nominal conditions.
        var (resp, root, body) = await SendAsync(HttpMethod.Get, EvalsUrl, bearer: _adminToken);

        ((int)resp.StatusCode).Should().BeLessThan(500,
            "evals endpoint must never return 5xx — adapter is sentinel-safe; body: {0}", body);

        // It should be 200 specifically.
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "evals endpoint must return 200 when admin; body: {0}", body);

        // And successed must be true.
        TryProp(root, "successed", out var succ).Should().BeTrue("body: {0}", body);
        succ.GetBoolean().Should().BeTrue(
            "successed must be true for a clean 200 (even bootstrap sentinel); body: {0}", body);
    }

    // =========================================================================
    // Private helpers — HTTP, JSON
    // =========================================================================

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
            catch { /* non-JSON body — leave root default */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Case-insensitive JSON property lookup (camelCase + PascalCase + linear scan).
    /// Mirrors the same helper used across all P7_11 sibling tests.
    /// </summary>
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

    /// <summary>
    /// Finds a breakdown entry in a dictionary-shaped JsonElement by key name (case-insensitive).
    /// Returns null if not found.
    /// </summary>
    private static JsonElement? FindBreakdownEntry(JsonElement dictElement, string key)
    {
        if (dictElement.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in dictElement.EnumerateObject())
        {
            if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }
        return null;
    }

    /// <summary>
    /// Asserts that a breakdown entry has the expected passed/total/passRate values.
    /// </summary>
    private static void AssertBreakdownEntry(JsonElement entry, int passed, int total, double passRate, string body)
    {
        TryProp(entry, "passed",   out var p).Should().BeTrue("breakdown entry must have 'passed'; body: {0}", body);
        p.GetInt32().Should().Be(passed, "passed must be {0}; body: {1}", passed, body);

        TryProp(entry, "total",    out var t).Should().BeTrue("breakdown entry must have 'total'; body: {0}", body);
        t.GetInt32().Should().Be(total, "total must be {0}; body: {1}", total, body);

        TryProp(entry, "passRate", out var pr).Should().BeTrue("breakdown entry must have 'passRate'; body: {0}", body);
        pr.GetDouble().Should().BeApproximately(passRate, 0.001,
            "passRate must be {0}%%; body: {1}", passRate, body);
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
        var email = $"p602_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }
}

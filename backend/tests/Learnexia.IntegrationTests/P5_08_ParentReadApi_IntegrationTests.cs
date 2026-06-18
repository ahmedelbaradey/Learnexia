// ReSharper disable InconsistentNaming
// P5-08 Parent-Scoped Read API — Integration Tests (E1..E9)
//
// Tests in this file exercise the 9 parent analytics read endpoints at HTTP level:
//   E1  GET /api/Parent/Children/{id}/Progress
//   E2  GET /api/Parent/Family/Summary
//   E3  GET /api/Parent/Children/{id}/WeeklyKpis
//   E4  GET /api/Parent/Children/{id}/SubjectMastery
//   E5  GET /api/Parent/Children/{id}/WeakAreas
//   E6  GET /api/Parent/Children/{id}/Reports
//   E7  GET /api/Parent/Children/{id}/Energy
//   E8  GET /api/Parent/Children/{id}/Activity
//   E9  GET /api/Parent/Children/{id}/WeeklyReport (+ optional ?week=)
//
// Coverage map (keyed to acceptance criteria):
//
//   E1-HAPPY        E1 own child → 200 + successed=true + expected DTO fields
//   E1-IDOR         E1 parent A requests parent B's child → 403
//   E1-ANON         E1 anonymous → 401
//   E1-STUDENT      E1 child/Student JWT → 403
//   E1-ZERO         E1 freshly-linked child (zero activity) → 200 + well-formed zeroed DTO
//
//   E2-HAPPY        E2 family summary owns only caller's children
//   E2-ISOLATION    E2 parent B cannot see parent A's children aggregated
//   E2-ANON         E2 anonymous → 401
//   E2-NO-CHILDREN  E2 parent with no children → 200 empty zero-state
//
//   E3-HAPPY        E3 own child → 200 + all KPI fields present
//   E3-IDOR         E3 cross-family → 403
//   E3-ZERO         E3 no-activity child → 200 zeroed KPIs (not 500)
//
//   E4-HAPPY        E4 own child → 200 + BySubject array
//   E4-IDOR         E4 cross-family → 403
//
//   E5-HAPPY        E5 own child → 200 + Areas array (may be empty)
//   E5-IDOR         E5 cross-family → 403
//
//   E6-HAPPY        E6 own child → 200 + report chart fields present
//   E6-IDOR         E6 cross-family → 403
//
//   E7-HAPPY        E7 own child → 200 + energy fields present
//   E7-IDOR         E7 cross-family → 403
//   E7-ZERO         E7 no-billing child → 200 zeroed energy (not 500)
//
//   E8-HAPPY        E8 own child → 200 + Events array
//   E8-IDOR         E8 cross-family → 403
//
//   E9-HAPPY        E9 own child → 200 + well-formed DTO
//   E9-NO-REPORT    E9 no report yet → 200 + ReportFound=false (not 500)
//   E9-WEEK-PARAM   E9 ?week= query param accepted (latest when absent)
//   E9-IDEMPOTENT   E9 generator idempotency: two calls same week → one DB row
//   E9-IDOR         E9 cross-family → 403

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P5-08 parent-scoped read API integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so
/// the Testcontainers Postgres container is shared across all integration test classes.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in InitializeAsync.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P5_08_ParentReadApi_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────────
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";

    // E1..E9 read endpoints
    private const string ProgressUrl      = "api/Parent/Children/{0}/Progress";
    private const string FamilySummaryUrl = "api/Parent/Family/Summary";
    private const string WeeklyKpisUrl    = "api/Parent/Children/{0}/WeeklyKpis";
    private const string SubjectMasteryUrl= "api/Parent/Children/{0}/SubjectMastery";
    private const string WeakAreasUrl     = "api/Parent/Children/{0}/WeakAreas";
    private const string ReportsUrl       = "api/Parent/Children/{0}/Reports";
    private const string EnergyUrl        = "api/Parent/Children/{0}/Energy";
    private const string ActivityUrl      = "api/Parent/Children/{0}/Activity";
    private const string WeeklyReportUrl  = "api/Parent/Children/{0}/WeeklyReport";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P5_08_ParentReadApi_IntegrationTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"p508_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive property lookup (handles camelCase and PascalCase).</summary>
    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private async Task<(HttpResponseMessage Response, string Body, JsonElement Root)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var resp    = await _client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { /* non-JSON */ }
        return (resp, bodyStr, root);
    }

    /// <summary>Registers a parent and returns (parentId, JWT).</summary>
    private async Task<(int ParentId, string Token)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email         = email,
            Password      = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        return (SeatTestSupport.DecodeUserId(token), token);
    }

    /// <summary>
    /// Adds a child to the parent (handles seat provisioning automatically via SeatTestSupport).
    /// Returns (childId, childToken).
    /// </summary>
    private async Task<(int ChildId, string ChildToken)> AddChildAsync(
        int parentId, string parentToken, string tag = "")
    {
        // Ensure parent has at least one seat (Free plan allows 1; more needed for multiple children).
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId);

        var childEmail = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = $"Child {tag}",
            Email            = childEmail,
            Password         = "Child@Pass1",
            Grade            = 4,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);

        addResp.IsSuccessStatusCode.Should().BeTrue(
            "add-child must succeed; body={0}", addBody);

        // Sign in as child to get child token
        var (signResp, signBody, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = childEmail,
            Password = "Child@Pass1",
        });
        signResp.IsSuccessStatusCode.Should().BeTrue(
            "child sign-in must succeed; body={0}", signBody);

        TryProp(signRoot, "data", out var signData);
        TryProp(signData, "accessToken", out var ctok);
        var childToken = ctok.GetString()!;
        return (SeatTestSupport.DecodeUserId(childToken), childToken);
    }

    /// <summary>Asserts the standard BaseResponse envelope shape on a 200 response.</summary>
    private static void AssertSuccessEnvelope(JsonElement root, string body)
    {
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must have statusCode; body={0}", body);
        TryProp(root, "successed", out var succEl).Should().BeTrue(
            "envelope must have 'successed' (sic); body={0}", body);
        succEl.GetBoolean().Should().BeTrue(
            "successed must be true for a 200 success; body={0}", body);
        TryProp(root, "data", out _).Should().BeTrue(
            "envelope must have 'data'; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E1 — GET /api/Parent/Children/{id}/Progress
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E1-HAPPY: own child progress → 200 + successed=true + DTO fields present")]
    public async Task E1_Happy_OwnChildProgress_Returns200WithDtoFields()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E1H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E1H");

        var url = string.Format(ProgressUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E1-HAPPY: parent's own child must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "level",          out var levelEl).Should().BeTrue("data.level must exist; body={0}", body);
        TryProp(data, "totalXp",        out _)          .Should().BeTrue("data.totalXp must exist; body={0}", body);
        TryProp(data, "currentStreak",  out _)          .Should().BeTrue("data.currentStreak must exist; body={0}", body);
        TryProp(data, "bestStreak",     out _)          .Should().BeTrue("data.bestStreak must exist; body={0}", body);
        TryProp(data, "masteryPercent", out _)          .Should().BeTrue("data.masteryPercent must exist; body={0}", body);
        TryProp(data, "activeToday",    out _)          .Should().BeTrue("data.activeToday must exist; body={0}", body);
        TryProp(data, "energy",         out var energyEl).Should().BeTrue("data.energy must exist; body={0}", body);
        TryProp(energyEl, "remaining",  out _)          .Should().BeTrue("energy.remaining must exist; body={0}", body);

        // Zero-state: new child has level≥1 (never 0)
        levelEl.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "E1-HAPPY: new child must have level >= 1; body={0}", body);
    }

    [Fact(DisplayName = "E1-IDOR (CRITICAL): parent A requests parent B's child → 403, no data leak")]
    public async Task E1_IDOR_CrossFamilyChild_Returns403()
    {
        // Parent A — owns child A
        var (parentAId, parentAToken) = await RegisterParentAsync("E1IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E1IDOR_CA");

        // Parent B — owns a different child
        var (parentBId, parentBToken) = await RegisterParentAsync("E1IDOR_B");
        // Parent B attempts to read Parent A's child
        var url = string.Format(ProgressUrl, childAId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E1-IDOR CRITICAL: Parent B must get 403 when requesting Parent A's child; body={0}", body);

        // Verify no data leaks — successed must be false and data must not contain child progress
        TryProp(root, "successed", out var succEl);
        succEl.ValueKind.Should().NotBe(JsonValueKind.Undefined, "envelope must still be present");
        if (succEl.ValueKind == JsonValueKind.True || succEl.ValueKind == JsonValueKind.False)
        {
            succEl.GetBoolean().Should().BeFalse(
                "E1-IDOR CRITICAL: successed must be false on a 403; body={0}", body);
        }
    }

    [Fact(DisplayName = "E1-ANON: anonymous request → 401")]
    public async Task E1_Anonymous_Returns401()
    {
        var url = string.Format(ProgressUrl, 999);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "E1-ANON: anonymous must get 401; body={0}", body);
    }

    [Fact(DisplayName = "E1-STUDENT: child/Student JWT → 403 (role gate)")]
    public async Task E1_StudentToken_Returns403()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E1ST");
        var (childId, childToken) = await AddChildAsync(parentId, parentToken, "E1STC");

        var url = string.Format(ProgressUrl, childId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: childToken);

        ((int)resp.StatusCode).Should().BeOneOf([401, 403],
            "E1-STUDENT: Student role must be denied (401 or 403); body={0}", body);
    }

    [Fact(DisplayName = "E1-ZERO: freshly-linked child with no activity → 200 + zeroed DTO (not 500)")]
    public async Task E1_ZeroState_FreshChild_Returns200WithZeroedDto()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E1Z");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E1ZC");

        var url = string.Format(ProgressUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E1-ZERO: fresh child must return 200 (not 500); body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "level", out var levelEl);
        levelEl.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "E1-ZERO: level must be ≥1 even for zero-state child; body={0}", body);

        TryProp(data, "energy", out var energyEl);
        TryProp(energyEl, "remaining", out var remainingEl);
        remainingEl.GetInt32().Should().Be(0,
            "E1-ZERO: fresh child has no energy allocated; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E2 — GET /api/Parent/Family/Summary
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E2-HAPPY: family summary → 200 + successed=true + DTO fields present")]
    public async Task E2_Happy_FamilySummary_Returns200WithDtoFields()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E2H");
        await AddChildAsync(parentId, parentToken, "E2HC");

        var (resp, body, root) = await SendAsync(HttpMethod.Get, FamilySummaryUrl, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E2-HAPPY: family summary must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "activeLearners",   out _).Should().BeTrue("data.activeLearners must exist; body={0}", body);
        TryProp(data, "lessonsCompleted", out _).Should().BeTrue("data.lessonsCompleted must exist; body={0}", body);
        TryProp(data, "totalXp",          out _).Should().BeTrue("data.totalXp must exist; body={0}", body);
        TryProp(data, "bestStreakDays",   out _).Should().BeTrue("data.bestStreakDays must exist; body={0}", body);
        TryProp(data, "badgesEarned",     out _).Should().BeTrue("data.badgesEarned must exist; body={0}", body);
    }

    [Fact(DisplayName = "E2-ISOLATION: family summary aggregates only caller's own children (no IDOR)")]
    public async Task E2_Isolation_OnlyOwnChildren_NoIDOR()
    {
        // Parent A — has one child
        var (parentAId, parentAToken) = await RegisterParentAsync("E2ISO_A");
        await AddChildAsync(parentAId, parentAToken, "E2ISO_CA");

        // Parent B — no children
        var (_, parentBToken) = await RegisterParentAsync("E2ISO_B");

        var (resp, body, root) = await SendAsync(HttpMethod.Get, FamilySummaryUrl, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E2-ISOLATION: Parent B must get 200 even with no children; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var bData);
        TryProp(bData, "activeLearners", out var alEl);
        alEl.GetInt32().Should().Be(0,
            "E2-ISOLATION DEFECT: Parent B must see 0 activeLearners (cannot see Parent A's child); body={0}", body);
    }

    [Fact(DisplayName = "E2-ANON: anonymous → 401")]
    public async Task E2_Anonymous_Returns401()
    {
        var (resp, body, _) = await SendAsync(HttpMethod.Get, FamilySummaryUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "E2-ANON: anonymous must get 401; body={0}", body);
    }

    [Fact(DisplayName = "E2-NO-CHILDREN: parent with no children → 200 empty zero-state DTO")]
    public async Task E2_NoChildren_Returns200EmptyDto()
    {
        var (_, parentToken) = await RegisterParentAsync("E2NC");

        var (resp, body, root) = await SendAsync(HttpMethod.Get, FamilySummaryUrl, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E2-NO-CHILDREN: parent with no children must get 200 (not 500); body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "activeLearners", out var alEl);
        alEl.GetInt32().Should().Be(0,
            "E2-NO-CHILDREN: activeLearners must be 0 when parent has no children; body={0}", body);
        TryProp(data, "totalXp", out var xpEl);
        xpEl.GetInt32().Should().Be(0,
            "E2-NO-CHILDREN: totalXp must be 0 when parent has no children; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E3 — GET /api/Parent/Children/{id}/WeeklyKpis
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E3-HAPPY: weekly KPIs → 200 + all KPI fields present")]
    public async Task E3_Happy_WeeklyKpis_Returns200WithAllFields()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E3H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E3HC");

        var url = string.Format(WeeklyKpisUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E3-HAPPY: weekly KPIs must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "timeLearningMinutes",      out _).Should().BeTrue("data.timeLearningMinutes must exist; body={0}", body);
        TryProp(data, "xpEarned",                 out _).Should().BeTrue("data.xpEarned must exist; body={0}", body);
        TryProp(data, "lessonsDone",               out _).Should().BeTrue("data.lessonsDone must exist; body={0}", body);
        TryProp(data, "streakDays",                out _).Should().BeTrue("data.streakDays must exist; body={0}", body);
        TryProp(data, "timeLearningDeltaMinutes",  out _).Should().BeTrue("data.timeLearningDeltaMinutes must exist; body={0}", body);
        TryProp(data, "xpDelta",                   out _).Should().BeTrue("data.xpDelta must exist; body={0}", body);
        TryProp(data, "lessonsDelta",              out _).Should().BeTrue("data.lessonsDelta must exist; body={0}", body);
    }

    [Fact(DisplayName = "E3-IDOR: cross-family WeeklyKpis → 403")]
    public async Task E3_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E3IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E3IDOR_CA");

        var (_, parentBToken) = await RegisterParentAsync("E3IDOR_B");
        var url = string.Format(WeeklyKpisUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E3-IDOR: cross-family WeeklyKpis must return 403; body={0}", body);
    }

    [Fact(DisplayName = "E3-ZERO: fresh child with no activity → 200 zeroed KPIs (not 500)")]
    public async Task E3_ZeroState_NoActivity_Returns200WithZeroKpis()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E3Z");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E3ZC");

        var url = string.Format(WeeklyKpisUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E3-ZERO: fresh child must return 200 (not 500); body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "xpEarned", out var xpEl);
        xpEl.GetInt32().Should().Be(0,
            "E3-ZERO: fresh child must have xpEarned=0; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E4 — GET /api/Parent/Children/{id}/SubjectMastery
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E4-HAPPY: subject mastery → 200 + BySubject array present")]
    public async Task E4_Happy_SubjectMastery_Returns200WithBySubjectArray()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E4H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E4HC");

        var url = string.Format(SubjectMasteryUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E4-HAPPY: subject mastery must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "overallPercent", out _).Should().BeTrue("data.overallPercent must exist; body={0}", body);
        TryProp(data, "bySubject",      out var bySubjectEl).Should().BeTrue("data.bySubject must exist; body={0}", body);
        bySubjectEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E4-HAPPY: bySubject must be an array; body={0}", body);
    }

    [Fact(DisplayName = "E4-IDOR: cross-family SubjectMastery → 403")]
    public async Task E4_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E4IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E4IDOR_CA");

        var (_, parentBToken) = await RegisterParentAsync("E4IDOR_B");
        var url = string.Format(SubjectMasteryUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E4-IDOR: cross-family SubjectMastery must return 403; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E5 — GET /api/Parent/Children/{id}/WeakAreas
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E5-HAPPY: weak areas → 200 + Areas array (may be empty)")]
    public async Task E5_Happy_WeakAreas_Returns200WithAreasArray()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E5H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E5HC");

        var url = string.Format(WeakAreasUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E5-HAPPY: weak areas must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "areas", out var areasEl).Should().BeTrue(
            "data.areas must exist; body={0}", body);
        areasEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E5-HAPPY: areas must be an array; body={0}", body);
        // Empty list is valid for a new child — assert no 500 or crash
    }

    [Fact(DisplayName = "E5-IDOR: cross-family WeakAreas → 403")]
    public async Task E5_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E5IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E5IDOR_CA");

        var (_, parentBToken) = await RegisterParentAsync("E5IDOR_B");
        var url = string.Format(WeakAreasUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E5-IDOR: cross-family WeakAreas must return 403; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E6 — GET /api/Parent/Children/{id}/Reports
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E6-HAPPY: reports → 200 + chart arrays present")]
    public async Task E6_Happy_Reports_Returns200WithChartArrays()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E6H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E6HC");

        var url = string.Format(ReportsUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E6-HAPPY: reports must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "dailyXpSeries",    out var dailyEl).Should().BeTrue("data.dailyXpSeries must exist; body={0}", body);
        TryProp(data, "xpTrend20Day",     out var trendEl).Should().BeTrue("data.xpTrend20Day must exist; body={0}", body);
        TryProp(data, "timeOfDayBuckets", out var bucketEl).Should().BeTrue("data.timeOfDayBuckets must exist; body={0}", body);
        dailyEl.ValueKind.Should().Be(JsonValueKind.Array,   "dailyXpSeries must be an array; body={0}", body);
        trendEl.ValueKind.Should().Be(JsonValueKind.Array,   "xpTrend20Day must be an array; body={0}", body);
        bucketEl.ValueKind.Should().Be(JsonValueKind.Array,  "timeOfDayBuckets must be an array; body={0}", body);

        // Spec guarantees exactly 7 days, 20 trend, 4 buckets
        dailyEl.GetArrayLength().Should().Be(7,
            "E6-HAPPY: dailyXpSeries must have exactly 7 entries (rolling-7-day); body={0}", body);
        trendEl.GetArrayLength().Should().Be(20,
            "E6-HAPPY: xpTrend20Day must have exactly 20 entries; body={0}", body);
        bucketEl.GetArrayLength().Should().Be(4,
            "E6-HAPPY: timeOfDayBuckets must have exactly 4 entries; body={0}", body);
    }

    [Fact(DisplayName = "E6-IDOR: cross-family Reports → 403")]
    public async Task E6_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E6IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E6IDOR_CA");

        var (_, parentBToken) = await RegisterParentAsync("E6IDOR_B");
        var url = string.Format(ReportsUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E6-IDOR: cross-family Reports must return 403; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E7 — GET /api/Parent/Children/{id}/Energy
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E7-HAPPY: child energy → 200 + energy fields present")]
    public async Task E7_Happy_ChildEnergy_Returns200WithEnergyFields()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E7H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E7HC");

        var url = string.Format(EnergyUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E7-HAPPY: energy must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "remaining",       out _).Should().BeTrue("data.remaining must exist; body={0}", body);
        TryProp(data, "allocated",        out _).Should().BeTrue("data.allocated must exist; body={0}", body);
        TryProp(data, "spent",            out _).Should().BeTrue("data.spent must exist; body={0}", body);
        TryProp(data, "purchasedBalance", out _).Should().BeTrue("data.purchasedBalance must exist; body={0}", body);
        TryProp(data, "weeklyUsage",      out var weeklyUsageEl).Should().BeTrue("data.weeklyUsage must exist; body={0}", body);
        weeklyUsageEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E7-HAPPY: weeklyUsage must be an array; body={0}", body);
    }

    [Fact(DisplayName = "E7-IDOR: cross-family Energy → 403")]
    public async Task E7_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E7IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E7IDOR_CA");

        var (_, parentBToken) = await RegisterParentAsync("E7IDOR_B");
        var url = string.Format(EnergyUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E7-IDOR: cross-family Energy must return 403; body={0}", body);
    }

    [Fact(DisplayName = "E7-ZERO: fresh child with no billing data → 200 zeroed energy (not 500)")]
    public async Task E7_ZeroState_NoBilling_Returns200WithZeroedEnergy()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E7Z");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E7ZC");

        var url = string.Format(EnergyUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E7-ZERO: fresh child must return 200 (not 500); body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "remaining", out var remainingEl);
        remainingEl.GetInt32().Should().Be(0,
            "E7-ZERO: fresh child must have remaining=0; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E8 — GET /api/Parent/Children/{id}/Activity
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E8-HAPPY: activity feed → 200 + Events array")]
    public async Task E8_Happy_ActivityFeed_Returns200WithEventsArray()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E8H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E8HC");

        var url = string.Format(ActivityUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E8-HAPPY: activity feed must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "events", out var eventsEl).Should().BeTrue(
            "data.events must exist; body={0}", body);
        eventsEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E8-HAPPY: events must be an array; body={0}", body);
        // Empty list is valid for a new child — assert no 500
    }

    [Fact(DisplayName = "E8-IDOR: cross-family Activity → 403")]
    public async Task E8_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E8IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E8IDOR_CA");

        var (_, parentBToken) = await RegisterParentAsync("E8IDOR_B");
        var url = string.Format(ActivityUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E8-IDOR: cross-family Activity must return 403; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // E9 — GET /api/Parent/Children/{id}/WeeklyReport
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E9-HAPPY: weekly report → 200 + well-formed DTO")]
    public async Task E9_Happy_WeeklyReport_Returns200WithDto()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E9H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E9HC");

        // Seed a weekly report via the generator service (simulates the Hangfire job)
        var weekStart = GetLastMonday();
        using (var scope = _factory.Services.CreateScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IWeeklyReportGeneratorService>();
            await generator.GenerateAsync(childId, weekStart);
        }

        var url = string.Format(WeeklyReportUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E9-HAPPY: weekly report must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "reportFound", out var reportFoundEl).Should().BeTrue(
            "data.reportFound must exist; body={0}", body);
        reportFoundEl.GetBoolean().Should().BeTrue(
            "E9-HAPPY: reportFound must be true when report was generated; body={0}", body);
        TryProp(data, "xpEarned",        out _).Should().BeTrue("data.xpEarned must exist; body={0}", body);
        TryProp(data, "skillsImproved",  out _).Should().BeTrue("data.skillsImproved must exist; body={0}", body);
        TryProp(data, "weakAreas",       out var waEl).Should().BeTrue("data.weakAreas must exist; body={0}", body);
        TryProp(data, "recommendations", out var recsEl).Should().BeTrue("data.recommendations must exist; body={0}", body);
        waEl.ValueKind.Should().Be(JsonValueKind.Array,   "E9-HAPPY: weakAreas must be an array; body={0}", body);
        recsEl.ValueKind.Should().Be(JsonValueKind.Array, "E9-HAPPY: recommendations must be an array; body={0}", body);
    }

    [Fact(DisplayName = "E9-NO-REPORT: no report yet → 200 + ReportFound=false (not 500, not 404)")]
    public async Task E9_NoReport_Returns200WithReportFoundFalse()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E9NR");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E9NRC");

        // Do NOT generate any report — fresh child
        var url = string.Format(WeeklyReportUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E9-NO-REPORT: no-report state must return 200 (not 404, not 500); body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "reportFound", out var reportFoundEl).Should().BeTrue(
            "data.reportFound must exist; body={0}", body);
        reportFoundEl.GetBoolean().Should().BeFalse(
            "E9-NO-REPORT: reportFound must be false when no report exists; body={0}", body);
    }

    [Fact(DisplayName = "E9-WEEK-PARAM: ?week= query param filters to specific week")]
    public async Task E9_WeekParam_FiltersToSpecificWeek()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E9WP");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E9WPC");

        var weekStart = GetLastMonday();
        using (var scope = _factory.Services.CreateScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IWeeklyReportGeneratorService>();
            await generator.GenerateAsync(childId, weekStart);
        }

        // Request with ?week=<weekStart> — should find the report
        var weekParam = weekStart.ToString("yyyy-MM-dd");
        var url = string.Format(WeeklyReportUrl, childId) + $"?week={weekParam}";
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E9-WEEK-PARAM: explicit week request must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "reportFound", out var rfEl);
        rfEl.GetBoolean().Should().BeTrue(
            "E9-WEEK-PARAM: reportFound must be true for the week that was generated; body={0}", body);
    }

    [Fact(DisplayName = "E9-IDEMPOTENT: generating the same week twice → exactly one DB row")]
    public async Task E9_Idempotent_SameWeekTwice_OneDbRow()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E9ID");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E9IDC");

        var weekStart = GetLastMonday();

        using (var scope = _factory.Services.CreateScope())
        {
            var gen = scope.ServiceProvider.GetRequiredService<IWeeklyReportGeneratorService>();
            // Generate twice for the same (childId, weekStart)
            await gen.GenerateAsync(childId, weekStart);
            await gen.GenerateAsync(childId, weekStart);
        }

        // Verify only one row in the DB
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ParentDbContext>();
        var rowCount = await db.WeeklyReports
            .Where(r => r.ChildId == childId && r.WeekStartUtc == weekStart)
            .CountAsync();

        rowCount.Should().Be(1,
            "E9-IDEMPOTENT DEFECT: two GenerateAsync calls for the same (childId, week) must produce exactly one DB row; actual={0}",
            rowCount);
    }

    [Fact(DisplayName = "E9-IDOR: cross-family WeeklyReport → 403")]
    public async Task E9_IDOR_CrossFamily_Returns403()
    {
        var (parentAId, parentAToken) = await RegisterParentAsync("E9IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E9IDOR_CA");

        // Seed a report for child A so the IDOR test isn't short-circuited by "no report"
        var weekStart = GetLastMonday();
        using (var scope = _factory.Services.CreateScope())
        {
            var gen = scope.ServiceProvider.GetRequiredService<IWeeklyReportGeneratorService>();
            await gen.GenerateAsync(childAId, weekStart);
        }

        var (_, parentBToken) = await RegisterParentAsync("E9IDOR_B");
        var url = string.Format(WeeklyReportUrl, childAId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E9-IDOR: Parent B must get 403 when requesting Parent A's child weekly report; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the most recent Monday at 00:00:00 UTC.
    /// ISO week: Monday = DayOfWeek 1.
    /// </summary>
    private static DateTime GetLastMonday()
    {
        var today = DateTime.UtcNow.Date;
        var daysBack = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        // If today is Monday, daysBack = 0 (returns today), which is valid for an "in-progress week".
        // For testing we want last week's Monday to ensure the window is always in the past.
        if (daysBack == 0) daysBack = 7;
        return today.AddDays(-daysBack);
    }
}

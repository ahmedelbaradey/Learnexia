// ReSharper disable InconsistentNaming
// P5-09 Parent Recommendations Endpoint — Integration Tests (E10)
//
// Tests in this file exercise the single parent analytics endpoint added in P5-09:
//   E10  GET /api/Parent/Children/{id}/Recommendations
//
// Coverage map (keyed to acceptance criteria):
//
//   E10-HAPPY         own child, cold-start → 200 + successed=true + well-formed envelope + Items array
//   E10-HAPPY-SEEDED  own child after IRecommendationService.ComputeAndUpsertAsync → 200 + Items non-empty + field mapping
//   E10-IDOR          (CRITICAL) parent A reads parent B's child → 403, no data leak
//   E10-ANON          anonymous request → 401
//   E10-STUDENT       child/Student JWT → 403 (role gate)
//   E10-EMPTY         freshly-linked child, no recommendations computed → 200 + Items empty (not 500, not 404)

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P5-09 Recommendations endpoint integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so
/// the Testcontainers Postgres container is shared across all integration test classes.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in InitializeAsync.
/// The new <c>20260618142549_AddStudentRecommendation</c> migration is applied as part of
/// <c>LearningDbContext.MigrateAsync()</c>.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P5_09_Recommendations_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────────
    private const string RegisterParentUrl   = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl         = "api/Parent/Add-Child";
    private const string SignInUrl           = "api/Users/Authentication/Sign-In";
    private const string RecommendationsUrl  = "api/Parent/Children/{0}/Recommendations";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P5_09_Recommendations_IntegrationTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ────────────────────────────────────────────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"p509_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
    // E10 — GET /api/Parent/Children/{id}/Recommendations
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "E10-HAPPY: own child, cold-start → 200 + well-formed envelope + Items array present")]
    public async Task E10_Happy_OwnChild_ColdStart_Returns200WithWellFormedEnvelope()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E10H");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E10HC");

        var url = string.Format(RecommendationsUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E10-HAPPY: own child recommendations must return 200; body={0}", body);
        AssertSuccessEnvelope(root, body);

        // data.Items must exist and be an array
        TryProp(root, "data", out var data);
        TryProp(data, "items", out var itemsEl).Should().BeTrue(
            "E10-HAPPY: data.items must exist in RecommendationsDto; body={0}", body);
        itemsEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E10-HAPPY: items must be a JSON array; body={0}", body);
    }

    [Fact(DisplayName = "E10-EMPTY: freshly-linked child with no stored recommendations → 200 + Items empty (not 500, not 404)")]
    public async Task E10_Empty_NoRecommendations_Returns200WithEmptyItems()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E10MT");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E10MTC");

        // Explicitly verify no recommendation row exists for this child yet
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var rowCount = await db.StudentRecommendations
                .Where(r => r.StudentId == childId)
                .CountAsync();
            rowCount.Should().Be(0,
                "E10-EMPTY: fresh child must have 0 recommendation rows before the job runs");
        }

        var url = string.Format(RecommendationsUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E10-EMPTY: no-recommendations state must return 200 (not 500, not 404); body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "items", out var itemsEl).Should().BeTrue(
            "E10-EMPTY: data.items must exist even when list is empty; body={0}", body);
        itemsEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E10-EMPTY: items must be a JSON array; body={0}", body);
        itemsEl.GetArrayLength().Should().Be(0,
            "E10-EMPTY: items must be empty (cold-start / no job run yet); body={0}", body);
    }

    [Fact(DisplayName = "E10-HAPPY-SEEDED: after IRecommendationService.ComputeAndUpsertAsync → 200 + Items non-empty + DTO field mapping correct")]
    public async Task E10_Happy_AfterCompute_Returns200WithNonEmptyItemsAndFieldMapping()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E10HS");
        var (childId, _) = await AddChildAsync(parentId, parentToken, "E10HSC");

        // Trigger the recommendation computation via the in-process service (mirrors what the Hangfire job does).
        // Per the RecommendationRecomputeJob pattern: ComputeAndUpsertAsync STAGES the write;
        // the caller must call SaveChangesAsync to commit. We get the LearningDbContext from the same
        // scope so the tracked entities are the same instance.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
            var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            await svc.ComputeAndUpsertAsync(childId, today);
            await db.SaveChangesAsync(userId: 0);
        }

        var url = string.Format(RecommendationsUrl, childId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "E10-HAPPY-SEEDED: must return 200 after compute; body={0}", body);
        AssertSuccessEnvelope(root, body);

        TryProp(root, "data", out var data);
        TryProp(data, "items", out var itemsEl).Should().BeTrue(
            "E10-HAPPY-SEEDED: data.items must exist; body={0}", body);
        itemsEl.ValueKind.Should().Be(JsonValueKind.Array,
            "E10-HAPPY-SEEDED: items must be a JSON array; body={0}", body);

        // RecommendationEngine.Compute guarantees at least one Celebrate item for a cold-start student
        itemsEl.GetArrayLength().Should().BeGreaterThan(0,
            "E10-HAPPY-SEEDED: engine guarantees >=1 item (cold-start Celebrate); body={0}", body);

        // Verify the DTO field mapping for the first item
        var firstItem = itemsEl[0];
        TryProp(firstItem, "skillId",          out _).Should().BeTrue("item.skillId must exist; body={0}", body);
        TryProp(firstItem, "subjectCode",       out _).Should().BeTrue("item.subjectCode must exist; body={0}", body);
        TryProp(firstItem, "titleKey",          out var titleKeyEl).Should().BeTrue("item.titleKey must exist; body={0}", body);
        TryProp(firstItem, "bodyKey",           out _).Should().BeTrue("item.bodyKey must exist; body={0}", body);
        TryProp(firstItem, "ctaKey",            out _).Should().BeTrue("item.ctaKey must exist; body={0}", body);
        TryProp(firstItem, "severity",          out var severityEl).Should().BeTrue("item.severity must exist; body={0}", body);
        TryProp(firstItem, "actionType",        out var actionTypeEl).Should().BeTrue("item.actionType must exist; body={0}", body);
        TryProp(firstItem, "targetDifficulty",  out var targetDiffEl).Should().BeTrue("item.targetDifficulty must exist; body={0}", body);

        // Sanity checks on value ranges defined in RecommendationItemDto
        titleKeyEl.ValueKind.Should().Be(JsonValueKind.String,
            "E10-HAPPY-SEEDED: titleKey must be a string (i18n key); body={0}", body);
        titleKeyEl.GetString().Should().NotBeNullOrWhiteSpace(
            "E10-HAPPY-SEEDED: titleKey must not be blank; body={0}", body);
        severityEl.GetInt32().Should().BeInRange(1, 3,
            "E10-HAPPY-SEEDED: severity must be 1=Low, 2=Medium, or 3=High; body={0}", body);
        actionTypeEl.GetInt32().Should().BeInRange(1, 4,
            "E10-HAPPY-SEEDED: actionType must be 1=Practice, 2=Review, 3=KeepStreak, 4=Celebrate; body={0}", body);
        targetDiffEl.GetInt32().Should().BeInRange(1, 3,
            "E10-HAPPY-SEEDED: targetDifficulty must be 1=Easy, 2=Medium, 3=Hard; body={0}", body);
    }

    [Fact(DisplayName = "E10-IDOR (CRITICAL): parent A authenticated, requests parent B's child id → 403, no data leak")]
    public async Task E10_IDOR_CrossFamilyChild_Returns403_NoDataLeak()
    {
        // Parent A — owns child A; seed a recommendation row so there is data to potentially leak
        var (parentAId, parentAToken) = await RegisterParentAsync("E10IDOR_A");
        var (childAId, _) = await AddChildAsync(parentAId, parentAToken, "E10IDOR_CA");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
            var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            await svc.ComputeAndUpsertAsync(childAId, today);
            await db.SaveChangesAsync(userId: 0);
        }

        // Parent B — authenticated, but does NOT own child A
        var (parentBId, parentBToken) = await RegisterParentAsync("E10IDOR_B");
        // Parent B does not need to add a child — just needs to be authenticated

        var url = string.Format(RecommendationsUrl, childAId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, url, bearer: parentBToken);

        // CRITICAL: must be 403 Forbidden — not 200, not 404
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "E10-IDOR CRITICAL: Parent B must get 403 when requesting Parent A's child recommendations; body={0}", body);

        // Verify no data leak: successed must be false, data must not contain the child's recommendations
        if (root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var succEl);
            if (succEl.ValueKind == JsonValueKind.True || succEl.ValueKind == JsonValueKind.False)
            {
                succEl.GetBoolean().Should().BeFalse(
                    "E10-IDOR CRITICAL: successed must be false on a 403; body={0}", body);
            }

            // data must not contain a recommendations items array with actual data
            if (TryProp(root, "data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
            {
                if (TryProp(dataEl, "items", out var itemsEl) &&
                    itemsEl.ValueKind == JsonValueKind.Array)
                {
                    itemsEl.GetArrayLength().Should().Be(0,
                        "E10-IDOR CRITICAL: data.items must be empty or absent on a 403 — data leak detected; body={0}",
                        body);
                }
            }
        }
    }

    [Fact(DisplayName = "E10-ANON: anonymous request → 401")]
    public async Task E10_Anonymous_Returns401()
    {
        // Use a plausible child id; the auth gate fires before any handler logic
        var url = string.Format(RecommendationsUrl, 99999);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "E10-ANON: anonymous request must get 401; body={0}", body);
    }

    [Fact(DisplayName = "E10-STUDENT: child/Student JWT → 403 (role gate — only Parent/Admin/SuperAdmin allowed)")]
    public async Task E10_StudentToken_Returns403()
    {
        var (parentId, parentToken) = await RegisterParentAsync("E10ST");
        var (childId, childToken) = await AddChildAsync(parentId, parentToken, "E10STC");

        var url = string.Format(RecommendationsUrl, childId);
        var (resp, body, _) = await SendAsync(HttpMethod.Get, url, bearer: childToken);

        // The controller declares [Authorize(Roles = "Parent,Admin,SuperAdmin")] — a Student JWT
        // does not carry any of those roles, so ASP.NET Core should return 403 Forbidden.
        // Some JWT middleware configurations return 401 for an invalid/insufficient token —
        // accept either 401 or 403 as a denial (same pattern as the P5-08 E1-STUDENT test).
        ((int)resp.StatusCode).Should().BeOneOf([401, 403],
            "E10-STUDENT: Student role must be denied (401 or 403); body={0}", body);
    }
}

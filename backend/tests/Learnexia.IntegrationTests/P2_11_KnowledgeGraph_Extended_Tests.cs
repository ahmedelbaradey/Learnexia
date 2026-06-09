using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-11 Extended integration tests — QC catalog cases not covered by the base P2_11_KnowledgeGraph_Tests.
///
/// NEW cases from docs/qc/P2-11/backend-test-cases.md:
///   BE-TC-04  (UnlockedBy of unknown nodeId → 404)
///   BE-TC-07  (node DTO field contract + envelope spelling)
///   BE-TC-08  (cross-grade prereq edge is queryable)
///   BE-TC-09  (Parent token → 200, not 403)
///   BE-TC-10  (Student token → 200, not 403)
///   BE-TC-11  (fan-in node — multiple prereqs; N/A if seed is linear)
///   BE-TC-12  (root node → 200 empty Prerequisites)
///   BE-TC-13  (leaf node → 200 empty UnlockedBy)
///   BE-TC-14  (non-integer nodeId 'abc' → 404, not 500)
///   BE-TC-15  (nodeId=0/-1 → handler 404 envelope)
///   BE-TC-16..22  BLOCKED (authoring surface not built — plan Q2)
///
/// Existing: BE-TC-01..06 (T1..T6 in base file).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_11_KnowledgeGraph_Extended_Tests : IAsyncLifetime
{
    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string _superAdminToken = string.Empty;
    private int _addSingleDigitNodeId;     // Add Single-Digit Numbers (G1)
    private int _compareOrderNodeId;       // Compare and Order Numbers (G1)
    private int _subtractG2NodeId;         // Subtract Within 100 (G2)
    private int _multiplyG3NodeId;         // Multiply Single-Digit Factors (G3)
    private int _countTo1000NodeId;        // Count to 1000 (G1) — chain root
    private int _convertDecimalsNodeId;    // Convert Fractions to Decimals (G6) — chain leaf

    public P2_11_KnowledgeGraph_Extended_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Sign in as superadmin
        _superAdminToken = await GetTokenAsync(SuperAdminUserName, SuperAdminPassword);

        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        _addSingleDigitNodeId = (await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Name == "Add Single-Digit Numbers (G1)"))?.Id ?? 0;
        _compareOrderNodeId = (await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Name == "Compare and Order Numbers (G1)"))?.Id ?? 0;
        _subtractG2NodeId = (await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Name == "Subtract Within 100 (G2)"))?.Id ?? 0;
        _multiplyG3NodeId = (await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Name == "Multiply Single-Digit Factors (G3)"))?.Id ?? 0;
        _countTo1000NodeId = (await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Name == "Count to 1000 (G1)"))?.Id ?? 0;
        _convertDecimalsNodeId = (await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Name == "Convert Fractions to Decimals (G6)"))?.Id ?? 0;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string UniqueEmail(string tag = "")
        => $"p211x_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in el.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = prop.Value; return true; }
        value = default;
        return false;
    }

    private static async Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr)) try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, root, bodyStr);
    }

    private async Task<string> GetTokenAsync(string userName, string password)
    {
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        r.StatusCode.Should().Be(HttpStatusCode.OK, $"sign-in as {userName}; body={b}");
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    private async Task<string> CreateParentTokenAsync()
    {
        var email = UniqueEmail("par");
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)r.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"parent reg; body={b}");
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    private async Task<string> CreateStudentTokenAsync()
    {
        var parentToken = await CreateParentTokenAsync();
        var childEmail = UniqueEmail("ch");
        var (aR, aRoot, aBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new { FullName = "TC P211x", Email = childEmail, Password = ValidChildPassword,
                  Grade = 1, Language = "ar", Country = "EG", LearningLanguage = "en" },
            parentToken);
        ((int)aR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"add-child; body={aBody}");

        var (sR, sRoot, sBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        sR.StatusCode.Should().Be(HttpStatusCode.OK, $"sign-in; body={sBody}");
        TryProp(sRoot, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-04: UnlockedBy of unknown nodeId=999999 → 404 (not 500)")]
    public async Task BeTc04_UnlockedBy_UnknownNode_Returns404()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            "api/Learning/KnowledgeGraph/UnlockedBy/999999", null, _superAdminToken);

        ((int)resp.StatusCode).Should().NotBe(500, $"must not 500; body={body}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-07: Node DTO has all required fields + envelope uses 'successed' spelling")]
    public async Task BeTc07_NodeDto_FieldContract_And_EnvelopeSpelling()
    {
        _addSingleDigitNodeId.Should().BeGreaterThan(0,
            "Add Single-Digit Numbers (G1) must be seeded");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/Prerequisites/{_addSingleDigitNodeId}", null, _superAdminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");

        // Envelope spelling
        body.Should().Contain("\"successed\":", $"envelope must use 'successed' camelCase; body={body}");

        TryProp(root, "data", out var data);
        data.ValueKind.Should().Be(JsonValueKind.Array, $"data must be array; body={body}");
        data.GetArrayLength().Should().BeGreaterThan(0, $"Add Single-Digit must have at least one prereq; body={body}");

        // Inspect first item's DTO contract
        var first = data.EnumerateArray().First();
        string[] requiredFields = { "id", "name", "nodeType", "subjectId", "gradeId", "difficulty", "skillId" };
        foreach (var field in requiredFields)
        {
            bool found = TryProp(first, field, out _);
            found.Should().BeTrue($"node DTO must have '{field}' field; body={body}");
        }

        TryProp(first, "id", out var id);
        id.GetInt32().Should().BeGreaterThan(0, $"id must be positive; body={body}");

        TryProp(first, "name", out var name);
        name.GetString().Should().NotBeNullOrEmpty($"name must be non-empty; body={body}");
    }

    [Fact(DisplayName = "BE-TC-08: Cross-grade prereq edge — Multiply G3 has Subtract G2 as prereq")]
    public async Task BeTc08_CrossGradeEdge_Queryable()
    {
        if (_multiplyG3NodeId <= 0 || _subtractG2NodeId <= 0) return; // skip if not seeded

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/Prerequisites/{_multiplyG3NodeId}", null, _superAdminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);

        bool hasSubtractG2 = data.EnumerateArray()
            .Any(n => { TryProp(n, "id", out var id); return id.GetInt32() == _subtractG2NodeId; });

        hasSubtractG2.Should().BeTrue(
            $"Multiply G3 must have Subtract G2 as a cross-grade prereq; " +
            $"multiplyId={_multiplyG3NodeId}, subtractId={_subtractG2NodeId}; body={body}");

        // Verify grades differ
        var subtractNode = data.EnumerateArray()
            .First(n => { TryProp(n, "id", out var id); return id.GetInt32() == _subtractG2NodeId; });

        // Resolve G3's gradeId from a separate call
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var g3Node = await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == _multiplyG3NodeId);
        var g2Node = await db.KnowledgeNodes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == _subtractG2NodeId);

        if (g3Node != null && g2Node != null)
            g3Node.GradeId.Should().NotBe(g2Node.GradeId,
                "cross-grade edge must span different grades");
    }

    [Fact(DisplayName = "BE-TC-09: Parent JWT → 200 on Prerequisites (not 403 — any auth role)")]
    public async Task BeTc09_ParentJwt_Prerequisites_Returns200()
    {
        _addSingleDigitNodeId.Should().BeGreaterThan(0);
        var parentToken = await CreateParentTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/Prerequisites/{_addSingleDigitNodeId}", null, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Parent JWT must get 200 on read-only KnowledgeGraph endpoint; body={body}");
    }

    [Fact(DisplayName = "BE-TC-10: Student JWT → 200 on UnlockedBy (not 403 — any auth role)")]
    public async Task BeTc10_StudentJwt_UnlockedBy_Returns200()
    {
        _compareOrderNodeId.Should().BeGreaterThan(0);
        var studentToken = await CreateStudentTokenAsync();

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/UnlockedBy/{_compareOrderNodeId}", null, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Student JWT must get 200 on read-only KnowledgeGraph endpoint; body={body}");
    }

    [Fact(DisplayName = "BE-TC-11: Fan-in — node with multiple prerequisites (N/A if linear seed)")]
    public async Task BeTc11_FanIn_MultiplePrereqs()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Find a node that is the target of >1 Prerequisite edges
        var fanInNode = await db.KnowledgeEdges.AsNoTracking()
            .Where(e => e.RelationshipType == EdgeRelationshipType.Prerequisite)
            .GroupBy(e => e.TargetNodeId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .FirstOrDefaultAsync();

        if (fanInNode == 0)
        {
            // No fan-in in the seed — this is N/A for a linear chain. Document.
            return;
        }

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/Prerequisites/{fanInNode}", null, _superAdminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        data.GetArrayLength().Should().BeGreaterThan(1,
            $"Fan-in node must have >1 prerequisites; body={body}");

        // No duplicates
        var ids = data.EnumerateArray()
            .Select(n => { TryProp(n, "id", out var id); return id.GetInt32(); })
            .ToList();
        ids.Should().OnlyHaveUniqueItems($"No duplicate prereq nodes; body={body}");
    }

    [Fact(DisplayName = "BE-TC-12: Root node (Count to 1000) has empty Prerequisites → 200 []")]
    public async Task BeTc12_RootNode_EmptyPrerequisites_Returns200()
    {
        _countTo1000NodeId.Should().BeGreaterThan(0, "Count to 1000 (G1) must be seeded");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/Prerequisites/{_countTo1000NodeId}", null, _superAdminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Root node with no prerequisites must return 200 (EmptyCollection), not 404; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeTrue($"body={body}");
        TryProp(root, "data", out var data);
        data.ValueKind.Should().Be(JsonValueKind.Array, $"body={body}");
        data.GetArrayLength().Should().Be(0, $"Root node must have empty prerequisites; body={body}");
    }

    [Fact(DisplayName = "BE-TC-13: Leaf node (Convert Fractions to Decimals G6) has empty UnlockedBy → 200 []")]
    public async Task BeTc13_LeafNode_EmptyUnlockedBy_Returns200()
    {
        if (_convertDecimalsNodeId <= 0) return; // not in seed — skip

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/KnowledgeGraph/UnlockedBy/{_convertDecimalsNodeId}", null, _superAdminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Leaf node must return 200 with empty UnlockedBy, not 404; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeTrue($"body={body}");
        TryProp(root, "data", out var data);
        data.ValueKind.Should().Be(JsonValueKind.Array, $"body={body}");
        data.GetArrayLength().Should().Be(0, $"Leaf node must have empty UnlockedBy; body={body}");
    }

    [Fact(DisplayName = "BE-TC-14: Non-integer nodeId 'abc' → 404 from routing (not 500)")]
    public async Task BeTc14_NonIntegerNodeId_Returns404()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get,
            "api/Learning/KnowledgeGraph/Prerequisites/abc", null, _superAdminToken);

        ((int)resp.StatusCode).Should().NotBe(500, $"Must not 500; body={body}");
        ((int)resp.StatusCode).Should().Be(404, $"Route constraint mismatch must return 404; body={body}");
    }

    [Fact(DisplayName = "BE-TC-15: nodeId=0 and nodeId=-1 → handler 404 NotFound (not 500)")]
    public async Task BeTc15_ZeroAndNegativeNodeId_Returns404()
    {
        var (r0, root0, b0) = await SendAsync(_client, HttpMethod.Get,
            "api/Learning/KnowledgeGraph/Prerequisites/0", null, _superAdminToken);

        ((int)r0.StatusCode).Should().NotBe(500, $"nodeId=0 must not 500; body={b0}");
        r0.StatusCode.Should().Be(HttpStatusCode.NotFound, $"nodeId=0 must return 404; body={b0}");
        TryProp(root0, "successed", out var suc0);
        suc0.GetBoolean().Should().BeFalse($"body={b0}");

        var (rNeg, rootNeg, bNeg) = await SendAsync(_client, HttpMethod.Get,
            "api/Learning/KnowledgeGraph/Prerequisites/-1", null, _superAdminToken);

        ((int)rNeg.StatusCode).Should().NotBe(500, $"nodeId=-1 must not 500; body={bNeg}");
        // -1 may be routed with a constraint or reach the handler
        ((int)rNeg.StatusCode).Should().BeOneOf(new[] { 404, 400 },
            $"nodeId=-1 must return 404 or 400; body={bNeg}");
    }

    // §B cases — BLOCKED (authoring surface not built, plan Q2)
    [Fact(Skip = "BE-TC-16..22: BLOCKED — add/remove prerequisite-edge endpoints not built (plan Q2). Cycle/acyclic invariant covered by SkillGraphValidatorTests unit tests.")]
    public Task BeTc16_22_AuthoringBlocked() => Task.CompletedTask;
}

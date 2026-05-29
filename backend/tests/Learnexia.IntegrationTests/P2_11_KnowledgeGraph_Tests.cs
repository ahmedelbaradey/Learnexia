using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-11 — Knowledge-graph endpoint integration tests.
///
/// Endpoints under test:
///   GET /api/Learning/KnowledgeGraph/Prerequisites/{nodeId}  [Authorize]
///   GET /api/Learning/KnowledgeGraph/UnlockedBy/{nodeId}     [Authorize]
///
/// The <see cref="LearnexiaWebAppFactory"/> harness (Testcontainers PostgreSQL, Testing
/// environment, rate-limit bypass) is reused via the shared "IntegrationTests" xUnit collection.
///
/// Seeding note: <c>LearningModule.InitializeAsync</c> calls <c>LearningSeeder.SeedAsync</c>
/// only in Development, NOT in Testing.  <see cref="InitializeAsync"/> therefore calls the seeder
/// directly after applying migrations so the graph data is present during the tests.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_11_KnowledgeGraph_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public P2_11_KnowledgeGraph_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        // Apply migrations + seed identity (idempotent across all tests sharing this fixture).
        await _factory.ApplyMigrationsAndSeedAsync();

        // The Learning seeder runs only in Development inside LearningModule.InitializeAsync.
        // Call it directly here so the skill-graph data (KnowledgeNodes + KnowledgeEdges) is
        // present when the tests run.
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Case-insensitive property lookup — copes with the project's dual-serializer situation
    /// (Newtonsoft camelCase from controllers, System.Text.Json PascalCase from middleware/422).
    /// </summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
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
    /// Sign in as superadmin and return the bearer token.
    /// </summary>
    private async Task<string> GetSuperAdminTokenAsync()
    {
        var body = new { UserName = "superadmin", Password = "123Pa$$word!" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/Users/Authentication/Sign-In");
        request.Content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "superadmin sign-in prerequisite must succeed; P2-11 tests need a bearer token");

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        TryProp(root, "data", out var data).Should().BeTrue(
            "sign-in response must have 'data'; body: {0}", content);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue(
            "sign-in 'data.accessToken' must be present; body: {0}", content);

        var token = tokenProp.GetString();
        token.Should().NotBeNullOrWhiteSpace("sign-in must return a non-empty JWT");
        return token!;
    }

    /// <summary>
    /// Sends an authenticated GET request and returns (response, root JsonElement, raw body string).
    /// </summary>
    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        AuthenticatedGetAsync(string url, string bearerToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try { root = JsonDocument.Parse(body).RootElement; }
            catch { /* leave root default for non-JSON bodies */ }
        }
        return (response, root, body);
    }

    // -------------------------------------------------------------------------
    // Test 1 — Prerequisites: known node returns 200 with expected prereq
    // -------------------------------------------------------------------------

    /// <summary>
    /// AC-5 / Plan Batch-4 validation #1:
    /// GET Prerequisites for a node that HAS a seeded prereq returns HTTP 200,
    /// successed=true, and the data list contains the expected prerequisite node.
    ///
    /// The seeder builds the chain:
    ///   Count to 1000 (G1) → Compare and Order Numbers (G1) → Add Single-Digit Numbers (G1)
    ///
    /// "Add Single-Digit Numbers (G1)" has "Compare and Order Numbers (G1)" as its direct prereq.
    /// We find the KnowledgeNode by skill name, then query its prerequisites.
    /// </summary>
    [Fact(DisplayName = "T1: Prerequisites_KnownNode_Returns200_WithExpectedPrereq")]
    public async Task Prerequisites_KnownNode_Returns200_WithExpectedPrereq()
    {
        var token = await GetSuperAdminTokenAsync();

        // Resolve the target node's ID and the expected prereq node's ID from DB.
        int targetNodeId;
        int expectedPrereqNodeId;
        string expectedPrereqName;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

            // "Add Single-Digit Numbers (G1)" is the target; "Compare and Order Numbers (G1)" is the prereq.
            var targetNode = await db.KnowledgeNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Name == "Add Single-Digit Numbers (G1)");

            targetNode.Should().NotBeNull(
                "seeder must have created a KnowledgeNode for 'Add Single-Digit Numbers (G1)'; " +
                "check that LearningSeeder.SeedAsync ran in InitializeAsync");

            var prereqNode = await db.KnowledgeNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Name == "Compare and Order Numbers (G1)");

            prereqNode.Should().NotBeNull(
                "seeder must have created a KnowledgeNode for 'Compare and Order Numbers (G1)'");

            targetNodeId = targetNode!.Id;
            expectedPrereqNodeId = prereqNode!.Id;
            expectedPrereqName = prereqNode.Name;
        }

        // Act
        var (response, root, body) =
            await AuthenticatedGetAsync(
                $"/api/Learning/KnowledgeGraph/Prerequisites/{targetNodeId}", token);

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "known node with a seeded prereq must return HTTP 200; body: {0}", body);

        // Assert — envelope: successed=true
        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed'; body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true for a known node with prerequisites; body: {0}", body);

        // Assert — data is a non-empty list
        TryProp(root, "data", out var dataProp).Should().BeTrue(
            "response must contain 'data'; body: {0}", body);
        dataProp.ValueKind.Should().Be(JsonValueKind.Array,
            "data must be a JSON array; body: {0}", body);

        var items = dataProp.EnumerateArray().ToList();
        items.Should().NotBeEmpty(
            "data array must be non-empty when a prerequisite node exists; body: {0}", body);

        // Assert — the expected prereq node is present in the list (matched by id)
        var matchingItem = items.FirstOrDefault(el =>
            TryProp(el, "id", out var idProp) &&
            idProp.GetInt32() == expectedPrereqNodeId);

        matchingItem.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "expected prereq node (id={0}, name='{1}') must appear in the data list; body: {2}",
            expectedPrereqNodeId, expectedPrereqName, body);

        // Assert — each item has the required fields: id, name, nodeType, subjectId, gradeId, difficulty, skillId
        foreach (var item in items)
        {
            TryProp(item, "id", out _).Should().BeTrue(
                "each node DTO must have 'id'; body: {0}", body);
            TryProp(item, "name", out _).Should().BeTrue(
                "each node DTO must have 'name'; body: {0}", body);
            TryProp(item, "nodeType", out _).Should().BeTrue(
                "each node DTO must have 'nodeType'; body: {0}", body);
            TryProp(item, "subjectId", out _).Should().BeTrue(
                "each node DTO must have 'subjectId'; body: {0}", body);
            TryProp(item, "gradeId", out _).Should().BeTrue(
                "each node DTO must have 'gradeId'; body: {0}", body);
            TryProp(item, "difficulty", out _).Should().BeTrue(
                "each node DTO must have 'difficulty'; body: {0}", body);
            // skillId is nullable — key must be present even if value is null
            TryProp(item, "skillId", out _).Should().BeTrue(
                "each node DTO must have 'skillId' key (may be null for non-skill nodes); body: {0}", body);
        }
    }

    // -------------------------------------------------------------------------
    // Test 2 — UnlockedBy: known node returns 200 with expected next node
    // -------------------------------------------------------------------------

    /// <summary>
    /// AC-5 / Plan Batch-4 validation #2:
    /// GET UnlockedBy for "Compare and Order Numbers (G1)" returns HTTP 200,
    /// successed=true, and the data list contains "Add Single-Digit Numbers (G1)".
    /// </summary>
    [Fact(DisplayName = "T2: UnlockedBy_KnownNode_Returns200_WithExpectedNext")]
    public async Task UnlockedBy_KnownNode_Returns200_WithExpectedNext()
    {
        var token = await GetSuperAdminTokenAsync();

        int sourceNodeId;
        int expectedUnlockedNodeId;
        string expectedUnlockedName;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

            var sourceNode = await db.KnowledgeNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Name == "Compare and Order Numbers (G1)");

            sourceNode.Should().NotBeNull(
                "seeder must have created a KnowledgeNode for 'Compare and Order Numbers (G1)'");

            var unlockedNode = await db.KnowledgeNodes
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Name == "Add Single-Digit Numbers (G1)");

            unlockedNode.Should().NotBeNull(
                "seeder must have created a KnowledgeNode for 'Add Single-Digit Numbers (G1)'");

            sourceNodeId = sourceNode!.Id;
            expectedUnlockedNodeId = unlockedNode!.Id;
            expectedUnlockedName = unlockedNode.Name;
        }

        var (response, root, body) =
            await AuthenticatedGetAsync(
                $"/api/Learning/KnowledgeGraph/UnlockedBy/{sourceNodeId}", token);

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "known source node that unlocks another must return HTTP 200; body: {0}", body);

        // Assert — envelope: successed=true
        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response must contain 'successed'; body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "successed must be true for a known node; body: {0}", body);

        // Assert — data contains the expected unlocked node
        TryProp(root, "data", out var dataProp).Should().BeTrue(
            "response must contain 'data'; body: {0}", body);
        dataProp.ValueKind.Should().Be(JsonValueKind.Array,
            "data must be a JSON array; body: {0}", body);

        var items = dataProp.EnumerateArray().ToList();
        items.Should().NotBeEmpty(
            "data array must contain the unlocked node; body: {0}", body);

        var matchingItem = items.FirstOrDefault(el =>
            TryProp(el, "id", out var idProp) &&
            idProp.GetInt32() == expectedUnlockedNodeId);

        matchingItem.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "expected unlocked node (id={0}, name='{1}') must appear in data; body: {2}",
            expectedUnlockedNodeId, expectedUnlockedName, body);
    }

    // -------------------------------------------------------------------------
    // Test 3 — Prerequisites: unknown nodeId does not return 500
    // -------------------------------------------------------------------------

    /// <summary>
    /// AC-5 / Plan Batch-4 validation #3:
    /// GET Prerequisites for a nodeId that does not exist must return either:
    ///   - HTTP 404 with successed=false (handler's NotFound path), OR
    ///   - HTTP 200 with successed=true and an empty data array (EmptyCollection path).
    /// It MUST NOT return HTTP 500.
    /// </summary>
    [Fact(DisplayName = "T3: Prerequisites_UnknownNode_DoesNotReturn500")]
    public async Task Prerequisites_UnknownNode_DoesNotReturn500()
    {
        var token = await GetSuperAdminTokenAsync();

        var (response, root, body) =
            await AuthenticatedGetAsync(
                "/api/Learning/KnowledgeGraph/Prerequisites/999999", token);

        // Must NOT be 500
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "unknown nodeId must NOT produce a 500; it must return 404 (NotFound) or 200 (EmptyCollection). " +
            "Body: {0}", body);

        // Must be either 404 (NotFound) or 200 (EmptyCollection/Success)
        var statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(new[] { 404, 200 },
            "handler uses NotFound → 404 or EmptyCollection → 200 for missing/empty; got {0}; body: {1}",
            statusCode, body);

        // If there is a body, successed must match the status code:
        // 404 → successed=false, 200 → successed=true
        if (!string.IsNullOrWhiteSpace(body) && root.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(root, "successed", out var succeededProp).Should().BeTrue(
                "response body must contain 'successed'; body: {0}", body);

            if (response.StatusCode == HttpStatusCode.NotFound)
                succeededProp.GetBoolean().Should().BeFalse(
                    "404 response must have successed=false; body: {0}", body);
            else
                succeededProp.GetBoolean().Should().BeTrue(
                    "200 response must have successed=true; body: {0}", body);
        }
    }

    // -------------------------------------------------------------------------
    // Test 4 — Unauthenticated request returns 401
    // -------------------------------------------------------------------------

    /// <summary>
    /// AC-5 / Plan Batch-4 validation #4:
    /// GET Prerequisites without a bearer token → HTTP 401.
    /// If a body is present it must be parseable as JSON with camelCase keys
    /// (HANDOFF load-bearing note: ErrorHandlerMiddleWare serializes camelCase).
    /// </summary>
    [Fact(DisplayName = "T4: Prerequisites_Unauthenticated_Returns401")]
    public async Task Prerequisites_Unauthenticated_Returns401()
    {
        // Send request with NO Authorization header
        var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/Learning/KnowledgeGraph/Prerequisites/1");
        // Deliberately no Authorization header
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a request without a bearer token to an [Authorize] endpoint must return HTTP 401; " +
            "if 200 is returned the [Authorize] attribute is missing or not enforced");

        // If there is a response body, assert it is valid JSON with lowercase/camelCase keys
        var body = await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(body))
        {
            JsonElement root;
            try
            {
                root = JsonDocument.Parse(body).RootElement;
            }
            catch (JsonException)
            {
                // Non-JSON body is acceptable for a raw 401 from JWT middleware
                return;
            }

            // If the body is JSON (from ErrorHandlerMiddleWare), its keys must be camelCase.
            // Verify by checking that at least one known envelope key exists in camelCase form
            // (e.g. "statusCode" not "StatusCode").
            var hasLowerStatusCode = root.TryGetProperty("statusCode", out _);
            var hasUpperStatusCode = root.TryGetProperty("StatusCode", out _);

            if (hasLowerStatusCode || hasUpperStatusCode)
            {
                hasLowerStatusCode.Should().BeTrue(
                    "HANDOFF convention: ErrorHandlerMiddleWare uses camelCase — key must be 'statusCode' " +
                    "not 'StatusCode'; body: {0}", body);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Test 5 — Envelope key is literally "successed" (camelCase of Successed)
    // -------------------------------------------------------------------------

    /// <summary>
    /// AC CONVENTIONS §5 / Plan Batch-4 validation #6:
    /// The JSON response from a success call must contain the literal string <c>"successed":</c>
    /// (camelCase serialization of the intentionally-spelled <c>Successed</c> property).
    /// </summary>
    [Fact(DisplayName = "T5: SuccessEnvelope_UsesCapitalSSpelled_Successed")]
    public async Task SuccessEnvelope_UsesCapitalSSpelled_Successed()
    {
        var token = await GetSuperAdminTokenAsync();

        // Pick a well-known node that will return a success response.
        int anyNodeId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var node = await db.KnowledgeNodes.AsNoTracking().FirstOrDefaultAsync();
            node.Should().NotBeNull("at least one KnowledgeNode must exist after seeding");
            anyNodeId = node!.Id;
        }

        var (response, _, body) =
            await AuthenticatedGetAsync(
                $"/api/Learning/KnowledgeGraph/Prerequisites/{anyNodeId}", token);

        // The response can be 200 (success or empty) or 404 (no prereqs / node found empty).
        // Either way the raw JSON body must contain the "successed": literal.
        body.Should().NotBeNullOrWhiteSpace(
            "response must have a non-empty body; status={0}", (int)response.StatusCode);

        body.Should().Contain("\"successed\":",
            "the envelope property must serialize to camelCase 'successed' (not 'Successed' or 'succeeded'); " +
            "body: {0}", body);
    }

    // -------------------------------------------------------------------------
    // Test 6 — Seed smoke check: DB has nodes and Prerequisite edges
    // -------------------------------------------------------------------------

    /// <summary>
    /// Plan Batch-4 validation #5:
    /// After the seeder runs, the DB must contain:
    /// (a) At least one KnowledgeNode with a non-null SkillId (seeder mapped Skills → nodes).
    /// (b) At least one KnowledgeEdge with RelationshipType == Prerequisite (seeder authored edges).
    /// This proves the seeder ran cleanly and no cycle was detected (which would have caused it to
    /// skip saving edges).
    /// </summary>
    [Fact(DisplayName = "T6: SeedSmokeCheck_KnowledgeGraph_HasNodesAndEdges")]
    public async Task SeedSmokeCheck_KnowledgeGraph_HasNodesAndEdges()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // (a) At least one KnowledgeNode wrapping a Skill (SkillId non-null)
        var hasSkillNodes = await db.KnowledgeNodes
            .AnyAsync(n => n.SkillId != null);

        hasSkillNodes.Should().BeTrue(
            "LearningSeeder.SeedSkillGraphAsync must create KnowledgeNode rows with SkillId set " +
            "(Step 1 maps every seeded Skill to a KnowledgeNode); " +
            "0 rows means the seeder did not run or failed before saving nodes");

        // (b) At least one KnowledgeEdge of type Prerequisite
        var hasPrerequisiteEdges = await db.KnowledgeEdges
            .AnyAsync(e => e.RelationshipType == EdgeRelationshipType.Prerequisite);

        hasPrerequisiteEdges.Should().BeTrue(
            "LearningSeeder.SeedSkillGraphAsync must create Prerequisite KnowledgeEdge rows " +
            "(Step 2 authors the Math G1–G6 prereq chain); " +
            "0 edges means the seeder skipped saving (possible cycle detection false-positive, " +
            "or all candidate skill names failed to resolve — check seeder logs)");
    }
}

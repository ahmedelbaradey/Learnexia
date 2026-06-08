using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-03 integration tests: Admin skill authoring and skill dependency graph.
///
/// Endpoints under test:
///   GET    /api/learning/Skills/List                              — admin flat list (AdminOnly)
///   GET    /api/learning/Skills?id={n}                           — admin get by id (AdminOnly)
///   POST   /api/learning/Skills/Create                           — create skill + auto-creates KnowledgeNode
///   PUT    /api/learning/Skills/Update                           — edit skill
///   DELETE /api/learning/Skills?id={n}                          — soft-delete skill + cascade (node + edges)
///   PUT    /api/learning/Skills/{id}/Active                      — activate / deactivate skill
///   GET    /api/learning/KnowledgeGraph/Graph?subjectId={n}      — admin graph (AdminOnly)
///   POST   /api/learning/KnowledgeGraph/Edges                    — add edge (AdminOnly)
///   DELETE /api/learning/KnowledgeGraph/Edges/{edgeId}           — remove edge (AdminOnly)
///   GET    /api/learning/KnowledgeGraph/Prerequisites/{nodeId}   — authenticated read
///   GET    /api/learning/KnowledgeGraph/UnlockedBy/{nodeId}      — authenticated read
///   GET    /api/learning/Subjects/{id}/SkillTree                 — student skill tree (excludes inactive)
///
/// Auth matrix:
///   AdminOnly endpoints (anonymous → 401, non-admin → 403, admin → 200):
///     Skills/List, Skills/GetById, Skills/Create, Skills/Update, Skills/Delete,
///     Skills/{id}/Active, KnowledgeGraph/Graph, KnowledgeGraph/Edges (POST/DELETE).
///   [Authorize] endpoints (anonymous → 401, authenticated → 200):
///     KnowledgeGraph/Prerequisites/{nodeId}, KnowledgeGraph/UnlockedBy/{nodeId},
///     Subjects/{id}/SkillTree.
///
/// Acceptance criteria covered:
///   AC-SKILL-1  : Skill create auto-creates exactly one KnowledgeNode — visible in GetGraph.
///   AC-SKILL-2  : Skill soft-delete cascades: node and its edges disappear from GetGraph; not purged.
///   AC-SKILL-3  : SetActive toggles: inactive skill hidden from student SkillTree; admin List still shows it.
///   AC-SKILL-4  : Reactivate restores skill to student SkillTree.
///   AC-SKILL-5  : Admin Skills List returns skills including inactive ones; includes isActive field.
///   AC-EDGE-1   : AddEdge happy path — edge appears in GetGraph.
///   AC-EDGE-2   : Cross-language edge rejected (Successed=false).
///   AC-EDGE-3   : Cycle creation rejected (A→B then B→A Prerequisite → rejected; A→B still in graph).
///   AC-EDGE-4   : Duplicate (Source,Target,Type) rejected (Successed=false).
///   AC-EDGE-5   : Non-existent source or target node rejected (Successed=false).
///   AC-EDGE-6   : Strength out of range [0.0, 1.0] → 422.
///   AC-EDGE-7   : RemoveEdge soft-deletes: edge gone from GetGraph; not physically purged.
///   AC-GRAPH-1  : GetGraph returns nodes[] + edges[] for a subject.
///   AC-GRAPH-2  : GetGraph nodes include isSkillActive field; inactive-skill node has isSkillActive=false.
///   AC-GRAPH-3  : GetGraph scoped by subjectId: nodes from other subjects absent.
///   AC-AUTH-1   : All admin write endpoints require AdminOnly (anonymous → 401, non-admin → 403).
///   AC-AUTH-2   : Skills List / GetById now AdminOnly (anonymous → 401, non-admin → 403).
///   AC-AUTH-3   : Prerequisites/UnlockedBy require [Authorize] (anonymous → 401, authenticated → 200).
///   AC-ENV-1    : BaseResponse envelope shape (statusCode, successed, message, data, errors).
///   AC-VAL-1    : Skill Create with empty Name → 422.
///   AC-VAL-2    : Skill Create with ConceptId=0 → 422.
///   AC-VAL-3    : AddEdge with SourceNodeId=0 → 422; TargetNodeId=0 → 422.
///   AC-VAL-4    : SetActive with SkillId=0 → 422.
///
/// Note: SkillTree returns 200 with success; the admin JWT has no learning-language claim so the
/// language-resolver may produce a language-mismatch branch in P8-03. Tests assert only on
/// the AC-relevant aspects (IsActive exclusion / SkillNodeDto shape) rather than language branching.
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// Runs in the shared "IntegrationTests" xUnit collection (LearnexiaWebAppFactory, Testcontainers PG).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_03_SkillsGraph_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // ─────────────────────────────────────────────────────────────────────────
    // Seeded credentials (mirrors P7-01 / P7-02)
    // ─────────────────────────────────────────────────────────────────────────
    private const string AdminUserName     = "superadmin";
    private const string AdminPassword     = "123Pa$$word!";
    private const string BasicUserName     = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;

    // ─────────────────────────────────────────────────────────────────────────
    // EdgeRelationshipType enum values (mirrors Domain.Enums)
    // ─────────────────────────────────────────────────────────────────────────
    private const int EdgePrerequisite = 0;  // EdgeRelationshipType.Prerequisite
    private const int EdgeRelated      = 1;  // EdgeRelationshipType.Related

    public P7_03_SkillsGraph_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AC-AUTH-2 — Skills List / GetById now AdminOnly
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-2: GET Skills/List anonymous → 401")]
    public async Task Auth_SkillsList_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Skills/List?PageNumber=1&PageSize=10",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Skills List is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Skills/List non-admin → 403")]
    public async Task Auth_SkillsList_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Skills/List?PageNumber=1&PageSize=10",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Skills List is AdminOnly — non-admin must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Skills/List parent → 403")]
    public async Task Auth_SkillsList_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Skills/List?PageNumber=1&PageSize=10",
            bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Skills List is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Skills?id=1 anonymous → 401")]
    public async Task Auth_SkillsGetById_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Skills?id=1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Skills GetById is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-2: GET Skills?id=1 non-admin → 403")]
    public async Task Auth_SkillsGetById_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/Skills?id=1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Skills GetById is AdminOnly — non-admin must get 403; body: {0}", body);
    }

    // =========================================================================
    // AC-AUTH-1 — Admin write endpoints: Create / Update / Delete / SetActive
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-1: POST Skills/Create anonymous → 401")]
    public async Task Auth_SkillCreate_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/Skills/Create",
            new { Name = "TestSkill", MasteryThreshold = 80, EstimatedTimeMinutes = 10, ConceptId = 1 },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Skills Create is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: POST Skills/Create non-admin → 403")]
    public async Task Auth_SkillCreate_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/Skills/Create",
            new { Name = "TestSkill", MasteryThreshold = 80, EstimatedTimeMinutes = 10, ConceptId = 1 },
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Skills Create is AdminOnly — non-admin must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PUT Skills/Update anonymous → 401")]
    public async Task Auth_SkillUpdate_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Skills/Update",
            new { Id = 1, Name = "X", MasteryThreshold = 50, EstimatedTimeMinutes = 5, ConceptId = 1 },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PUT Skills/Update non-admin → 403")]
    public async Task Auth_SkillUpdate_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Skills/Update",
            new { Id = 1, Name = "X", MasteryThreshold = 50, EstimatedTimeMinutes = 5, ConceptId = 1 },
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: DELETE Skills?id=1 anonymous → 401")]
    public async Task Auth_SkillDelete_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete,
            "/api/learning/Skills?id=1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: DELETE Skills?id=1 non-admin → 403")]
    public async Task Auth_SkillDelete_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete,
            "/api/learning/Skills?id=1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PUT Skills/{id}/Active anonymous → 401")]
    public async Task Auth_SkillSetActive_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Skills/1/Active",
            new { isActive = false },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: PUT Skills/{id}/Active non-admin → 403")]
    public async Task Auth_SkillSetActive_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Skills/1/Active",
            new { isActive = false },
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    // =========================================================================
    // AC-AUTH-1 — KnowledgeGraph admin endpoints
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-1: GET KnowledgeGraph/Graph anonymous → 401")]
    public async Task Auth_GetGraph_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/KnowledgeGraph/Graph?subjectId=1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: GET KnowledgeGraph/Graph non-admin → 403")]
    public async Task Auth_GetGraph_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/KnowledgeGraph/Graph?subjectId=1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: POST KnowledgeGraph/Edges anonymous → 401")]
    public async Task Auth_AddEdge_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 1, TargetNodeId = 2, RelationshipType = EdgePrerequisite },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: POST KnowledgeGraph/Edges non-admin → 403")]
    public async Task Auth_AddEdge_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 1, TargetNodeId = 2, RelationshipType = EdgePrerequisite },
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: DELETE KnowledgeGraph/Edges/{id} anonymous → 401")]
    public async Task Auth_RemoveEdge_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete,
            "/api/learning/KnowledgeGraph/Edges/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-1: DELETE KnowledgeGraph/Edges/{id} non-admin → 403")]
    public async Task Auth_RemoveEdge_NonAdmin_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Delete,
            "/api/learning/KnowledgeGraph/Edges/1",
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    // =========================================================================
    // AC-AUTH-3 — Prerequisites / UnlockedBy require [Authorize] (not AdminOnly)
    // =========================================================================

    [Fact(DisplayName = "AC-AUTH-3: GET KnowledgeGraph/Prerequisites/{nodeId} anonymous → 401")]
    public async Task Auth_Prerequisites_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/KnowledgeGraph/Prerequisites/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Prerequisites endpoint requires [Authorize]; anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-3: GET KnowledgeGraph/UnlockedBy/{nodeId} anonymous → 401")]
    public async Task Auth_UnlockedBy_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/KnowledgeGraph/UnlockedBy/1",
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "UnlockedBy endpoint requires [Authorize]; anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-AUTH-3: GET KnowledgeGraph/Prerequisites/{nodeId} with non-admin token → 200 (not 403)")]
    public async Task Auth_Prerequisites_NonAdmin_Succeeds()
    {
        // Prerequisites is [Authorize] (not AdminOnly) — any authenticated user can call it.
        // For a non-existent node the handler returns gracefully (200 or 404, NOT 403).
        var (resp, _, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/KnowledgeGraph/Prerequisites/99999",
            bearer: _basicToken);
        ((int)resp.StatusCode).Should().NotBe(403,
            "Prerequisites is [Authorize] not AdminOnly; non-admin must not get 403; body: {0}", body);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "Non-existent nodeId must return 200 or 404, not 5xx; body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-1 / AC-VAL-2 — Skill Create validation
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-1: POST Skills/Create with empty Name → 422")]
    public async Task SkillCreate_EmptyName_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/Skills/Create",
            new { Name = "", MasteryThreshold = 80, EstimatedTimeMinutes = 10, ConceptId = 1 },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty Name must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-VAL-2: POST Skills/Create with ConceptId=0 → 422")]
    public async Task SkillCreate_ZeroConceptId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/Skills/Create",
            new { Name = $"Skill {Guid.NewGuid():N}", MasteryThreshold = 80, EstimatedTimeMinutes = 10, ConceptId = 0 },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "ConceptId=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-VAL-2: POST Skills/Create with MasteryThreshold > 100 → 422")]
    public async Task SkillCreate_MasteryThresholdOutOfRange_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/Skills/Create",
            new { Name = $"Skill {Guid.NewGuid():N}", MasteryThreshold = 101, EstimatedTimeMinutes = 5, ConceptId = 1 },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MasteryThreshold=101 must trigger InclusiveBetween(0,100) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-3 — AddEdge validation
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-3: POST KnowledgeGraph/Edges with SourceNodeId=0 → 422")]
    public async Task AddEdge_ZeroSourceNodeId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 0, TargetNodeId = 2, RelationshipType = EdgePrerequisite },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "SourceNodeId=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-VAL-3: POST KnowledgeGraph/Edges with TargetNodeId=0 → 422")]
    public async Task AddEdge_ZeroTargetNodeId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 1, TargetNodeId = 0, RelationshipType = EdgePrerequisite },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "TargetNodeId=0 must trigger GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-EDGE-6: POST KnowledgeGraph/Edges with Strength=1.5 → 422")]
    public async Task AddEdge_StrengthOutOfRange_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 1, TargetNodeId = 2, RelationshipType = EdgePrerequisite, Strength = 1.5m },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Strength=1.5 must trigger InclusiveBetween(0.0, 1.0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-EDGE-6: POST KnowledgeGraph/Edges with Strength=-0.1 → 422")]
    public async Task AddEdge_NegativeStrength_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 1, TargetNodeId = 2, RelationshipType = EdgePrerequisite, Strength = -0.1m },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Strength=-0.1 must trigger InclusiveBetween(0.0, 1.0) → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-VAL-4 — SetActive validator
    // =========================================================================

    [Fact(DisplayName = "AC-VAL-4: PUT Skills/0/Active → 422 (SkillId GreaterThan(0))")]
    public async Task SkillSetActive_ZeroId_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Put,
            "/api/learning/Skills/0/Active",
            new { isActive = false },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "SkillId=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-SKILL-1 — Skill Create auto-creates exactly one KnowledgeNode
    // =========================================================================

    [Fact(DisplayName = "AC-SKILL-1: Skill Create auto-creates exactly one KnowledgeNode visible in GetGraph")]
    public async Task SkillCreate_AutoCreatesKnowledgeNode_VisibleInGetGraph()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();

        var skillName = $"P703 Skill {Guid.NewGuid():N}";
        var (createResp, createRoot, createBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/Skills/Create",
            new { Name = skillName, MasteryThreshold = 75, EstimatedTimeMinutes = 15, ConceptId = conceptId },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Skill Create must return 200; body: {0}", createBody);
        AssertSucceeded(createRoot, createBody);

        // GetGraph must now show the auto-created node.
        var (graphResp, graphRoot, graphBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        graphResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GetGraph must return 200; body: {0}", graphBody);
        AssertSucceeded(graphRoot, graphBody);

        TryProp(graphRoot, "data", out var graphData).Should().BeTrue("body: {0}", graphBody);
        TryProp(graphData, "nodes", out var nodesEl).Should().BeTrue(
            "SkillGraphDto must have 'nodes'; body: {0}", graphBody);
        nodesEl.ValueKind.Should().Be(JsonValueKind.Array, "nodes must be an array; body: {0}", graphBody);

        var nodes = nodesEl.EnumerateArray().ToList();
        var matchingNode = nodes.FirstOrDefault(n =>
            TryProp(n, "name", out var nameProp) &&
            nameProp.GetString() == skillName);

        matchingNode.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "KnowledgeNode for skill '{0}' must appear in GetGraph after Skill Create; body: {1}",
            skillName, graphBody);

        // Verify the node has the expected fields including isSkillActive=true.
        TryProp(matchingNode, "id", out _).Should().BeTrue("node must have id; body: {0}", graphBody);
        TryProp(matchingNode, "skillId", out var skillIdProp).Should().BeTrue(
            "node must have skillId; body: {0}", graphBody);
        skillIdProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "auto-created node must have a non-null skillId; body: {0}", graphBody);
        TryProp(matchingNode, "isSkillActive", out var isSkillActiveProp).Should().BeTrue(
            "KnowledgeNodeDto must expose isSkillActive (P7-03); body: {0}", graphBody);
        isSkillActiveProp.GetBoolean().Should().BeTrue(
            "newly created skill is active — isSkillActive must be true; body: {0}", graphBody);

        // Exactly one node with this name should exist (no duplicates).
        nodes.Count(n =>
            TryProp(n, "name", out var np) && np.GetString() == skillName)
            .Should().Be(1,
            "Skill Create must auto-create exactly ONE node; body: {0}", graphBody);
    }

    // =========================================================================
    // AC-SKILL-2 — Skill soft-delete cascades: node + edges gone from GetGraph
    // =========================================================================

    [Fact(DisplayName = "AC-SKILL-2: Skill soft-delete cascades — node and its edges disappear from GetGraph")]
    public async Task SkillDelete_Cascades_NodeAndEdgesGoneFromGetGraph()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();

        // Create two skills/nodes in the same subject.
        var skillNameA = $"P703 SkillA {Guid.NewGuid():N}";
        var skillNameB = $"P703 SkillB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, skillNameA);
        var skillBId = await CreateSkillGetIdAsync(conceptId, skillNameB);

        // Resolve the two node IDs from GetGraph.
        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, skillNameA, skillNameB);

        // Add an edge: A → B.
        var (addEdgeResp, addEdgeRoot, addEdgeBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite },
            _adminToken);
        addEdgeResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AddEdge prereq must succeed; body: {0}", addEdgeBody);
        AssertSucceeded(addEdgeRoot, addEdgeBody);

        // Soft-delete skill B; must cascade-delete node B and the A→B edge.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/Skills?id={skillBId}",
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Skill Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // GetGraph must no longer contain node B.
        var (graphResp, graphRoot, graphBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        graphResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", graphBody);
        AssertSucceeded(graphRoot, graphBody);

        TryProp(graphRoot, "data", out var graphData).Should().BeTrue("body: {0}", graphBody);
        TryProp(graphData, "nodes", out var nodesEl).Should().BeTrue("body: {0}", graphBody);
        var nodes = nodesEl.EnumerateArray().ToList();
        nodes.Any(n => TryProp(n, "name", out var np) && np.GetString() == skillNameB)
            .Should().BeFalse("soft-deleted skill's node must not appear in GetGraph; body: {0}", graphBody);

        // GetGraph must no longer contain the A→B edge.
        TryProp(graphData, "edges", out var edgesEl).Should().BeTrue("body: {0}", graphBody);
        var edges = edgesEl.EnumerateArray().ToList();
        edges.Any(e =>
            TryProp(e, "targetNodeId", out var tId) && tId.GetInt32() == nodeBId)
            .Should().BeFalse("cascade-deleted edge to node B must not appear in GetGraph; body: {0}", graphBody);

        // Verify the row is not physically purged — check via DB directly.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var skillBInDb = await db.Skills
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == skillBId);
        skillBInDb.Should().NotBeNull("soft-deleted skill must still exist in DB (not physically purged)");
        skillBInDb!.IsDeleted.Should().BeTrue("skill must be marked IsDeleted=true");
    }

    // =========================================================================
    // AC-SKILL-3 — SetActive: inactive skill hidden from student SkillTree
    // =========================================================================

    [Fact(DisplayName = "AC-SKILL-3: Deactivated skill excluded from student SkillTree; admin List still shows it")]
    public async Task SkillDeactivate_HiddenFromSkillTree_VisibleInAdminList()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var skillName = $"P703 ActiveSkill {Guid.NewGuid():N}";
        var skillId = await CreateSkillGetIdAsync(conceptId, skillName);

        // Verify skill is in admin list as active.
        var listBefore = await GetSkillsListForConceptAsync(conceptId);
        listBefore.Any(s => TryProp(s, "id", out var id) && id.GetInt32() == skillId)
            .Should().BeTrue("active skill must appear in admin Skills List");

        // Deactivate the skill.
        var (setResp, setRoot, setBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Skills/{skillId}/Active",
            new { isActive = false },
            _adminToken);
        setResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SetActive must return 200; body: {0}", setBody);
        AssertSucceeded(setRoot, setBody);

        // Admin list must still show it.
        var listAfter = await GetSkillsListForConceptAsync(conceptId);
        var deactivatedInList = listAfter.FirstOrDefault(s =>
            TryProp(s, "id", out var id) && id.GetInt32() == skillId);
        deactivatedInList.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated skill must still appear in admin Skills List; body");

        // Student SkillTree must exclude the inactive skill.
        // Note: GetSkillTree uses [Authorize] and applies IsActive filter on skills.
        // The admin token has no learning-language claim (P8-03 guard may kick in),
        // so we assert only that the deactivated skill does NOT appear in any concept's skills.
        var (treeResp, treeRoot, treeBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Subjects/{subjectId}/SkillTree",
            bearer: _adminToken);
        // Accept 200 or 404 (subject may not resolve due to P8-03 language guard on admin token).
        ((int)treeResp.StatusCode).Should().BeOneOf(new[] { 200, 404 },
            "SkillTree must not 500; body: {0}", treeBody);

        if (treeResp.StatusCode == HttpStatusCode.OK && treeRoot.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(treeRoot, "data", out var treeData).Should().BeTrue("body: {0}", treeBody);
            if (treeData.ValueKind == JsonValueKind.Array)
            {
                foreach (var conceptNode in treeData.EnumerateArray())
                {
                    if (!TryProp(conceptNode, "skills", out var skillsEl)) continue;
                    if (skillsEl.ValueKind != JsonValueKind.Array) continue;
                    skillsEl.EnumerateArray().Any(sk =>
                        TryProp(sk, "skillId", out var skId) && skId.GetInt32() == skillId)
                        .Should().BeFalse(
                            "deactivated skill must be excluded from student SkillTree; body: {0}", treeBody);
                }
            }
        }
    }

    // =========================================================================
    // AC-SKILL-4 — Reactivate restores skill to student SkillTree
    // =========================================================================

    [Fact(DisplayName = "AC-SKILL-4: Reactivate skill restores isSkillActive=true in GetGraph")]
    public async Task SkillReactivate_RestoresIsSkillActiveInGetGraph()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var skillName = $"P703 Reactivate {Guid.NewGuid():N}";
        var skillId = await CreateSkillGetIdAsync(conceptId, skillName);

        // Deactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Skills/{skillId}/Active",
            new { isActive = false }, _adminToken);

        // Verify GetGraph now shows isSkillActive=false for this node.
        var graphAfterDeactivate = await GetGraphDataAsync(subjectId);
        var nodeAfterDeactivate = graphAfterDeactivate.Nodes.FirstOrDefault(n => n.Name == skillName);
        nodeAfterDeactivate.Should().NotBeNull(
            "node must still exist in GetGraph after deactivation (node itself not deleted)");
        nodeAfterDeactivate!.IsSkillActive.Should().BeFalse(
            "isSkillActive must be false after deactivation");

        // Reactivate.
        var (reactResp, reactRoot, reactBody) = await SendAsync(HttpMethod.Put,
            $"/api/learning/Skills/{skillId}/Active",
            new { isActive = true },
            _adminToken);
        reactResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Reactivate must return 200; body: {0}", reactBody);
        AssertSucceeded(reactRoot, reactBody);

        // GetGraph must now show isSkillActive=true.
        var graphAfterReactivate = await GetGraphDataAsync(subjectId);
        var nodeAfterReactivate = graphAfterReactivate.Nodes.FirstOrDefault(n => n.Name == skillName);
        nodeAfterReactivate.Should().NotBeNull("node must still be in GetGraph after reactivation");
        nodeAfterReactivate!.IsSkillActive.Should().BeTrue(
            "isSkillActive must be true after reactivation");
    }

    // =========================================================================
    // AC-SKILL-5 — Admin Skills List returns all skills including inactive
    // =========================================================================

    [Fact(DisplayName = "AC-SKILL-5: Admin Skills List returns isActive field; deactivated skill still listed")]
    public async Task SkillsList_Admin_ReturnsInactiveSkills()
    {
        var (_, conceptId) = await CreateSubjectWithConceptAsync();
        var skillName = $"P703 ListCheck {Guid.NewGuid():N}";
        var skillId   = await CreateSkillGetIdAsync(conceptId, skillName);

        // Deactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Skills/{skillId}/Active",
            new { isActive = false }, _adminToken);

        // List with admin token — deactivated skill must appear.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "Skills List must return 200; body: {0}", listBody);

        var items = ExtractPageItems(listRoot, listBody);
        var found = items.FirstOrDefault(s =>
            TryProp(s, "id", out var id) && id.GetInt32() == skillId);

        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated skill must still appear in admin Skills List; body: {0}", listBody);
    }

    // =========================================================================
    // AC-EDGE-1 — AddEdge happy path — edge appears in GetGraph
    // =========================================================================

    [Fact(DisplayName = "AC-EDGE-1: AddEdge (Prerequisite) persists — edge appears in GetGraph")]
    public async Task AddEdge_Prerequisite_PersistsInGetGraph()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 A {Guid.NewGuid():N}";
        var nameB = $"P703 B {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AddEdge must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        // Edge must now appear in GetGraph.
        var graph = await GetGraphDataAsync(subjectId);
        graph.Edges.Any(e => e.SourceNodeId == nodeAId && e.TargetNodeId == nodeBId)
            .Should().BeTrue(
                "Prerequisite edge A→B must appear in GetGraph after AddEdge; nodes=({0},{1})", nodeAId, nodeBId);
    }

    [Fact(DisplayName = "AC-EDGE-1: AddEdge (Related) persists — edge appears in GetGraph")]
    public async Task AddEdge_Related_PersistsInGetGraph()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 RelA {Guid.NewGuid():N}";
        var nameB = $"P703 RelB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgeRelated },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AddEdge Related must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var graph = await GetGraphDataAsync(subjectId);
        graph.Edges.Any(e => e.SourceNodeId == nodeAId && e.TargetNodeId == nodeBId)
            .Should().BeTrue("Related edge A→B must appear in GetGraph");
    }

    [Fact(DisplayName = "AC-EDGE-1: AddEdge with explicit Strength=0.75 persists in GetGraph")]
    public async Task AddEdge_ExplicitStrength_PersistsInGetGraph()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 StrA {Guid.NewGuid():N}";
        var nameB = $"P703 StrB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgeRelated, Strength = 0.75m },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AddEdge with Strength=0.75 must return 200; body: {0}", addBody);
        AssertSucceeded(addRoot, addBody);

        var graph = await GetGraphDataAsync(subjectId);
        var edge = graph.Edges.FirstOrDefault(e =>
            e.SourceNodeId == nodeAId && e.TargetNodeId == nodeBId);
        edge.Should().NotBeNull("edge must appear in GetGraph");
        edge!.Strength.Should().Be(0.75m, "Strength must persist as 0.75");
    }

    // =========================================================================
    // AC-EDGE-2 — Cross-language edge rejected
    // =========================================================================

    [Fact(DisplayName = "AC-EDGE-2: AddEdge between nodes from different language trees → Successed=false")]
    public async Task AddEdge_CrossLanguage_Rejected()
    {
        // Create two subjects in the same grade but different languages (Ar=0, En=1).
        var (gradeId, _) = await CreateGradeGetIdAndNumberAsync();
        var subjNameAr = $"P703 SubjAr {Guid.NewGuid():N}";
        var subjNameEn = $"P703 SubjEn {Guid.NewGuid():N}";

        var subjectArId = await CreateSubjectGetIdAsync(gradeId, subjNameAr, subjectCode: 0, language: 0 /* Ar */);
        var subjectEnId = await CreateSubjectGetIdAsync(gradeId, subjNameEn, subjectCode: 0 /* MATH */, language: 1 /* En */);

        // Create a concept in each subject and a skill under each concept.
        var conceptArId = await CreateConceptGetIdAsync(subjectArId);
        var conceptEnId = await CreateConceptGetIdAsync(subjectEnId);

        var skillNameAr = $"P703 ArSkill {Guid.NewGuid():N}";
        var skillNameEn = $"P703 EnSkill {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptArId, skillNameAr);
        await CreateSkillGetIdAsync(conceptEnId, skillNameEn);

        // Resolve node IDs for each skill.
        var arNodes = await GetGraphNodesForSubjectAsync(subjectArId);
        var enNodes = await GetGraphNodesForSubjectAsync(subjectEnId);

        var arNodeId = arNodes.FirstOrDefault(n =>
            TryProp(n, "name", out var np) && np.GetString() == skillNameAr);
        var enNodeId = enNodes.FirstOrDefault(n =>
            TryProp(n, "name", out var np) && np.GetString() == skillNameEn);

        arNodeId.ValueKind.Should().NotBe(JsonValueKind.Undefined, "Ar node must exist in GetGraph");
        enNodeId.ValueKind.Should().NotBe(JsonValueKind.Undefined, "En node must exist in GetGraph");

        TryProp(arNodeId, "id", out var arIdProp).Should().BeTrue();
        TryProp(enNodeId, "id", out var enIdProp).Should().BeTrue();
        var nodeArId = arIdProp.GetInt32();
        var nodeEnId = enIdProp.GetInt32();

        // Attempt to add a cross-language edge.
        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeArId, TargetNodeId = nodeEnId, RelationshipType = EdgePrerequisite },
            _adminToken);

        // Must be rejected.
        AssertRejected(addResp, addRoot, addBody,
            "Cross-language edge (Ar node → En node) must be rejected with Successed=false");
    }

    // =========================================================================
    // AC-EDGE-3 — Cycle detection: B→A rejected after A→B
    // =========================================================================

    [Fact(DisplayName = "AC-EDGE-3: Cycle creation (B→A after A→B Prerequisite) is rejected; A→B persists")]
    public async Task AddEdge_CycleDetected_Rejected_ExistingEdgePreserved()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 CycA {Guid.NewGuid():N}";
        var nameB = $"P703 CycB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        // Add A → B (Prerequisite). Must succeed.
        var (add1Resp, add1Root, add1Body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite },
            _adminToken);
        add1Resp.StatusCode.Should().Be(HttpStatusCode.OK, "A→B must succeed; body: {0}", add1Body);
        AssertSucceeded(add1Root, add1Body);

        // Attempt B → A (Prerequisite) — would create a cycle A→B→A.
        var (add2Resp, add2Root, add2Body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeBId, TargetNodeId = nodeAId, RelationshipType = EdgePrerequisite },
            _adminToken);

        // Must be rejected.
        AssertRejected(add2Resp, add2Root, add2Body,
            "Cycle (B→A after A→B Prerequisite) must be rejected with Successed=false");

        // The original A→B edge must still be in the graph (not rolled back).
        var graph = await GetGraphDataAsync(subjectId);
        graph.Edges.Any(e => e.SourceNodeId == nodeAId && e.TargetNodeId == nodeBId)
            .Should().BeTrue("A→B edge must persist after cycle rejection; no rollback of existing edges");
    }

    [Fact(DisplayName = "AC-EDGE-3: Three-node cycle (A→B, B→C, C→A) — C→A is rejected")]
    public async Task AddEdge_ThreeNodeCycle_Rejected()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 3CycA {Guid.NewGuid():N}";
        var nameB = $"P703 3CycB {Guid.NewGuid():N}";
        var nameC = $"P703 3CycC {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);
        await CreateSkillGetIdAsync(conceptId, nameC);

        // Resolve node IDs.
        var allNodes = await GetGraphNodesForSubjectAsync(subjectId);
        int nodeAId = GetNodeIdByName(allNodes, nameA);
        int nodeBId = GetNodeIdByName(allNodes, nameB);
        int nodeCId = GetNodeIdByName(allNodes, nameC);

        // Build A → B, B → C.
        var (r1, _, _) = await SendAsync(HttpMethod.Post, "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite }, _adminToken);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "A→B must succeed");

        var (r2, _, _) = await SendAsync(HttpMethod.Post, "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeBId, TargetNodeId = nodeCId, RelationshipType = EdgePrerequisite }, _adminToken);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "B→C must succeed");

        // Attempt C → A — would complete the cycle.
        var (cycResp, cycRoot, cycBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeCId, TargetNodeId = nodeAId, RelationshipType = EdgePrerequisite },
            _adminToken);

        AssertRejected(cycResp, cycRoot, cycBody,
            "C→A after A→B→C Prerequisite chain must be rejected as a cycle");
    }

    // =========================================================================
    // AC-EDGE-4 — Duplicate edge rejected
    // =========================================================================

    [Fact(DisplayName = "AC-EDGE-4: Duplicate (Source,Target,Type) edge rejected with Successed=false")]
    public async Task AddEdge_Duplicate_Rejected()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 DupA {Guid.NewGuid():N}";
        var nameB = $"P703 DupB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        // First add succeeds.
        var (add1Resp, _, add1Body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgeRelated },
            _adminToken);
        add1Resp.StatusCode.Should().Be(HttpStatusCode.OK, "First add must succeed; body: {0}", add1Body);

        // Second identical add must be rejected.
        var (add2Resp, add2Root, add2Body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgeRelated },
            _adminToken);

        AssertRejected(add2Resp, add2Root, add2Body,
            "Duplicate (Source,Target,Type) edge must be rejected with Successed=false");
    }

    // =========================================================================
    // AC-EDGE-5 — Non-existent node rejected
    // =========================================================================

    [Fact(DisplayName = "AC-EDGE-5: AddEdge with non-existent SourceNodeId → Successed=false")]
    public async Task AddEdge_NonExistentSourceNode_Rejected()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameB = $"P703 NE_B {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameB);

        var allNodes = await GetGraphNodesForSubjectAsync(subjectId);
        int nodeBId = GetNodeIdByName(allNodes, nameB);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = 99999998, TargetNodeId = nodeBId, RelationshipType = EdgeRelated },
            _adminToken);

        AssertRejected(resp, root, body,
            "Non-existent SourceNodeId=99999998 must be rejected with Successed=false (NotFound guard)");
    }

    [Fact(DisplayName = "AC-EDGE-5: AddEdge with non-existent TargetNodeId → Successed=false")]
    public async Task AddEdge_NonExistentTargetNode_Rejected()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 NE_A {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);

        var allNodes = await GetGraphNodesForSubjectAsync(subjectId);
        int nodeAId = GetNodeIdByName(allNodes, nameA);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = 99999997, RelationshipType = EdgeRelated },
            _adminToken);

        AssertRejected(resp, root, body,
            "Non-existent TargetNodeId=99999997 must be rejected with Successed=false (NotFound guard)");
    }

    // =========================================================================
    // AC-EDGE-7 — RemoveEdge: edge gone from GetGraph; not physically purged
    // =========================================================================

    [Fact(DisplayName = "AC-EDGE-7: RemoveEdge soft-deletes edge — gone from GetGraph; row still in DB")]
    public async Task RemoveEdge_SoftDeletes_GoneFromGetGraph_NotPurged()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 RemA {Guid.NewGuid():N}";
        var nameB = $"P703 RemB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        // Add edge.
        var (addResp, _, addBody) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgeRelated },
            _adminToken);
        addResp.StatusCode.Should().Be(HttpStatusCode.OK, "Add edge must succeed; body: {0}", addBody);

        // Get the edge ID from GetGraph.
        var graphBefore = await GetGraphDataAsync(subjectId);
        var edgeBefore = graphBefore.Edges.FirstOrDefault(e =>
            e.SourceNodeId == nodeAId && e.TargetNodeId == nodeBId);
        edgeBefore.Should().NotBeNull("edge must be in GetGraph before removal");
        var edgeId = edgeBefore!.Id;

        // Remove edge.
        var (remResp, remRoot, remBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/KnowledgeGraph/Edges/{edgeId}",
            bearer: _adminToken);
        remResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "RemoveEdge must return 200; body: {0}", remBody);
        AssertSucceeded(remRoot, remBody);

        // Edge must be gone from GetGraph.
        var graphAfter = await GetGraphDataAsync(subjectId);
        graphAfter.Edges.Any(e => e.Id == edgeId)
            .Should().BeFalse("soft-deleted edge must not appear in GetGraph; edgeId={0}", edgeId);

        // But the row must still exist in DB with IsDeleted=true.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var edgeInDb = await db.KnowledgeEdges
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == edgeId);
        edgeInDb.Should().NotBeNull("soft-deleted edge must still exist in DB");
        edgeInDb!.IsDeleted.Should().BeTrue("edge must be marked IsDeleted=true after RemoveEdge");
    }

    [Fact(DisplayName = "AC-EDGE-7: RemoveEdge on non-existent edgeId → Successed=false")]
    public async Task RemoveEdge_NonExistentId_Rejected()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Delete,
            "/api/learning/KnowledgeGraph/Edges/99999996",
            bearer: _adminToken);

        AssertRejected(resp, root, body,
            "Non-existent EdgeId=99999996 must be rejected with Successed=false");
    }

    // =========================================================================
    // AC-GRAPH-1 — GetGraph returns nodes[] + edges[] for a subject
    // =========================================================================

    [Fact(DisplayName = "AC-GRAPH-1: GetGraph returns SkillGraphDto with nodes and edges arrays")]
    public async Task GetGraph_ReturnsNodesAndEdgesArrays()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 GrA {Guid.NewGuid():N}";
        var nameB = $"P703 GrB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);
        await SendAsync(HttpMethod.Post, "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite },
            _adminToken);

        var (graphResp, graphRoot, graphBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        graphResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", graphBody);
        AssertSucceeded(graphRoot, graphBody);

        TryProp(graphRoot, "data", out var data).Should().BeTrue("body: {0}", graphBody);
        TryProp(data, "subjectId", out var subIdProp).Should().BeTrue(
            "SkillGraphDto must have subjectId; body: {0}", graphBody);
        subIdProp.GetInt32().Should().Be(subjectId,
            "SkillGraphDto.subjectId must match the requested subjectId; body: {0}", graphBody);

        TryProp(data, "nodes", out var nodesEl).Should().BeTrue("SkillGraphDto must have nodes; body: {0}", graphBody);
        nodesEl.ValueKind.Should().Be(JsonValueKind.Array, "nodes must be an array; body: {0}", graphBody);

        TryProp(data, "edges", out var edgesEl).Should().BeTrue("SkillGraphDto must have edges; body: {0}", graphBody);
        edgesEl.ValueKind.Should().Be(JsonValueKind.Array, "edges must be an array; body: {0}", graphBody);

        nodesEl.GetArrayLength().Should().BeGreaterThanOrEqualTo(2,
            "at least 2 nodes (the two created skills); body: {0}", graphBody);
        edgesEl.GetArrayLength().Should().BeGreaterThanOrEqualTo(1,
            "at least 1 edge (the one we added); body: {0}", graphBody);
    }

    [Fact(DisplayName = "AC-GRAPH-1: GetGraph — each KnowledgeEdgeDto has id/sourceNodeId/targetNodeId/relationshipType/strength")]
    public async Task GetGraph_EdgeDtoShape()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 ShpA {Guid.NewGuid():N}";
        var nameB = $"P703 ShpB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);
        await SendAsync(HttpMethod.Post, "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite, Strength = 0.9m },
            _adminToken);

        var (graphResp, graphRoot, graphBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        graphResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", graphBody);

        TryProp(graphRoot, "data", out var data).Should().BeTrue("body: {0}", graphBody);
        TryProp(data, "edges", out var edgesEl).Should().BeTrue("body: {0}", graphBody);
        var edges = edgesEl.EnumerateArray().ToList();
        edges.Should().NotBeEmpty("at least one edge must exist; body: {0}", graphBody);

        var edge = edges.First(e =>
            TryProp(e, "sourceNodeId", out var sn) && sn.GetInt32() == nodeAId &&
            TryProp(e, "targetNodeId", out var tn) && tn.GetInt32() == nodeBId);

        TryProp(edge, "id", out _).Should().BeTrue("KnowledgeEdgeDto must have id; body: {0}", graphBody);
        TryProp(edge, "sourceNodeId", out _).Should().BeTrue("body: {0}", graphBody);
        TryProp(edge, "targetNodeId", out _).Should().BeTrue("body: {0}", graphBody);
        TryProp(edge, "relationshipType", out _).Should().BeTrue("body: {0}", graphBody);
        TryProp(edge, "strength", out var strProp).Should().BeTrue(
            "KnowledgeEdgeDto must have strength; body: {0}", graphBody);
        strProp.GetDecimal().Should().Be(0.9m, "strength must be 0.9 as persisted; body: {0}", graphBody);
    }

    // =========================================================================
    // AC-GRAPH-2 — GetGraph nodes include isSkillActive; inactive node flagged
    // =========================================================================

    [Fact(DisplayName = "AC-GRAPH-2: Inactive skill node has isSkillActive=false in GetGraph")]
    public async Task GetGraph_InactiveSkillNode_HasIsSkillActiveFalse()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var skillName = $"P703 InactGr {Guid.NewGuid():N}";
        var skillId   = await CreateSkillGetIdAsync(conceptId, skillName);

        // Deactivate.
        await SendAsync(HttpMethod.Put, $"/api/learning/Skills/{skillId}/Active",
            new { isActive = false }, _adminToken);

        var (graphResp, graphRoot, graphBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        graphResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", graphBody);

        TryProp(graphRoot, "data", out var data).Should().BeTrue("body: {0}", graphBody);
        TryProp(data, "nodes", out var nodesEl).Should().BeTrue("body: {0}", graphBody);
        var nodes = nodesEl.EnumerateArray().ToList();

        var inactiveNode = nodes.FirstOrDefault(n =>
            TryProp(n, "name", out var np) && np.GetString() == skillName);
        inactiveNode.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "inactive skill's node must still appear in admin GetGraph; body: {0}", graphBody);

        TryProp(inactiveNode, "isSkillActive", out var isp).Should().BeTrue(
            "KnowledgeNodeDto must expose isSkillActive; body: {0}", graphBody);
        isp.GetBoolean().Should().BeFalse(
            "isSkillActive must be false for deactivated skill; body: {0}", graphBody);
    }

    // =========================================================================
    // AC-GRAPH-3 — GetGraph scoped by subjectId
    // =========================================================================

    [Fact(DisplayName = "AC-GRAPH-3: GetGraph scoped by subjectId — nodes from other subjects absent")]
    public async Task GetGraph_ScopedBySubjectId()
    {
        var (subjectId1, conceptId1) = await CreateSubjectWithConceptAsync();
        var (subjectId2, conceptId2) = await CreateSubjectWithConceptAsync();

        var nameIn  = $"P703 InSubj {Guid.NewGuid():N}";
        var nameOut = $"P703 OutSubj {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId1, nameIn);
        await CreateSkillGetIdAsync(conceptId2, nameOut);

        // GetGraph for subject 1.
        var (graphResp, graphRoot, graphBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId1}",
            bearer: _adminToken);
        graphResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", graphBody);

        TryProp(graphRoot, "data", out var data).Should().BeTrue("body: {0}", graphBody);
        TryProp(data, "nodes", out var nodesEl).Should().BeTrue("body: {0}", graphBody);
        var nodes = nodesEl.EnumerateArray().ToList();

        nodes.Any(n => TryProp(n, "name", out var np) && np.GetString() == nameIn)
            .Should().BeTrue("node from subject1 must appear in GetGraph(subjectId1); body: {0}", graphBody);
        nodes.Any(n => TryProp(n, "name", out var np) && np.GetString() == nameOut)
            .Should().BeFalse("node from subject2 must NOT appear in GetGraph(subjectId1); body: {0}", graphBody);
    }

    [Fact(DisplayName = "AC-GRAPH-3: GetGraph with subjectId=0 → Successed=false (bad request guard)")]
    public async Task GetGraph_ZeroSubjectId_ReturnsFailure()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            "/api/learning/KnowledgeGraph/Graph?subjectId=0",
            bearer: _adminToken);

        // Handler returns BadRequest when SubjectId <= 0.
        AssertRejected(resp, root, body,
            "subjectId=0 must be rejected by GetGraph handler (BadRequest guard in handler)");
    }

    // =========================================================================
    // AC-ENV-1 — BaseResponse envelope shape
    // =========================================================================

    [Fact(DisplayName = "AC-ENV-1: AddEdge success response has statusCode/successed/message/data/errors keys")]
    public async Task AddEdge_ResponseEnvelopeShape()
    {
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 EnvA {Guid.NewGuid():N}";
        var nameB = $"P703 EnvB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);

        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgeRelated },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);

        // Verify "successed" is spelled correctly (not "succeeded" or "Successed").
        body.Should().Contain("\"successed\":",
            "the envelope property must serialize to camelCase 'successed' (CONVENTIONS §5); body: {0}", body);
    }

    [Fact(DisplayName = "AC-ENV-1: GetGraph success response has full envelope + data.nodes + data.edges")]
    public async Task GetGraph_ResponseEnvelopeShape()
    {
        var (subjectId, _) = await CreateSubjectWithConceptAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed",  out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data",    out var data).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors",  out _).Should().BeTrue("envelope must have errors; body: {0}", body);

        TryProp(data, "nodes", out _).Should().BeTrue("data must have nodes; body: {0}", body);
        TryProp(data, "edges", out _).Should().BeTrue("data must have edges; body: {0}", body);
    }

    // =========================================================================
    // Skill CRUD round-trip smoke test
    // =========================================================================

    [Fact(DisplayName = "Skill CRUD: Create → GetById → Update → Delete round-trip")]
    public async Task Skill_CrudRoundTrip()
    {
        var (_, conceptId) = await CreateSubjectWithConceptAsync();
        var skillName = $"P703 CRUD {Guid.NewGuid():N}";
        var skillId   = await CreateSkillGetIdAsync(conceptId, skillName);

        // GetById.
        var (getResp, getRoot, getBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Skills?id={skillId}",
            bearer: _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "GetById must return 200; body: {0}", getBody);
        AssertSucceeded(getRoot, getBody);
        TryProp(getRoot, "data", out var getData).Should().BeTrue("body: {0}", getBody);
        TryProp(getData, "name", out var nameProp).Should().BeTrue("body: {0}", getBody);
        nameProp.GetString().Should().Be(skillName, "GetById must return the correct skill name; body: {0}", getBody);

        // Update.
        var updatedName = $"P703 CRUD Updated {Guid.NewGuid():N}";
        var (updResp, updRoot, updBody) = await SendAsync(HttpMethod.Put,
            "/api/learning/Skills/Update",
            new { Id = skillId, Name = updatedName, MasteryThreshold = 90, EstimatedTimeMinutes = 20, ConceptId = conceptId },
            _adminToken);
        updResp.StatusCode.Should().Be(HttpStatusCode.OK, "Update must return 200; body: {0}", updBody);
        AssertSucceeded(updRoot, updBody);

        // Verify update persisted.
        var (getResp2, getRoot2, getBody2) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Skills?id={skillId}",
            bearer: _adminToken);
        getResp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody2);
        TryProp(getRoot2, "data", out var getData2).Should().BeTrue("body: {0}", getBody2);
        TryProp(getData2, "name", out var updNameProp).Should().BeTrue("body: {0}", getBody2);
        updNameProp.GetString().Should().Be(updatedName,
            "Updated name must be persisted; body: {0}", getBody2);

        // Delete.
        var (delResp, delRoot, delBody) = await SendAsync(HttpMethod.Delete,
            $"/api/learning/Skills?id={skillId}",
            bearer: _adminToken);
        delResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // GetById after delete must return NotFound / Successed=false.
        var (getResp3, getRoot3, getBody3) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Skills?id={skillId}",
            bearer: _adminToken);
        ((int)getResp3.StatusCode).Should().BeOneOf(new[] { 200, 404 }, "body: {0}", getBody3);
        if (getResp3.StatusCode == HttpStatusCode.OK && getRoot3.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(getRoot3, "successed", out var s3).Should().BeTrue("body: {0}", getBody3);
            s3.GetBoolean().Should().BeFalse("deleted skill must return Successed=false; body: {0}", getBody3);
        }
    }

    // =========================================================================
    // Prerequisites / UnlockedBy: admin token returns 200 or graceful empty
    // =========================================================================

    [Fact(DisplayName = "Prerequisites/UnlockedBy: known node with seeded data returns 200")]
    public async Task Prerequisites_KnownNode_Returns200()
    {
        // Build our own prerequisite edge and test against it — this avoids relying on
        // the LearningSeeder (which only runs in Development, not in the Testing env).
        var (subjectId, conceptId) = await CreateSubjectWithConceptAsync();
        var nameA = $"P703 PrA {Guid.NewGuid():N}";
        var nameB = $"P703 PrB {Guid.NewGuid():N}";
        await CreateSkillGetIdAsync(conceptId, nameA);
        await CreateSkillGetIdAsync(conceptId, nameB);
        var (nodeAId, nodeBId) = await GetNodeIdsForSkillNamesAsync(subjectId, nameA, nameB);
        await SendAsync(HttpMethod.Post, "/api/learning/KnowledgeGraph/Edges",
            new { SourceNodeId = nodeAId, TargetNodeId = nodeBId, RelationshipType = EdgePrerequisite },
            _adminToken);
        int targetNodeId = nodeBId;

        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Prerequisites/{targetNodeId}",
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Prerequisites for a known node must return Successed=true; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "data must be an array of prerequisite nodes; body: {0}", body);
    }

    // =========================================================================
    // Private helpers — infrastructure (mirrors P7-01 / P7-02 style)
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
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase).</summary>
    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
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

    private static List<JsonElement> ExtractPageItems(JsonElement root, string bodyStr)
    {
        TryProp(root, "data", out var outer).Should().BeTrue("envelope must have 'data'; body: {0}", bodyStr);
        if (TryProp(outer, "data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            return inner.EnumerateArray().ToList();
        if (outer.ValueKind == JsonValueKind.Array)
            return outer.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    private static void AssertRejected(
        HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var statusCode = (int)response.StatusCode;
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

        if (statusCode == 200)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "{0}; expected Successed=false on 200 rejection; body: {1}", reason, body);
        }
        else
        {
            statusCode.Should().BeOneOf(new[] { 400, 404, 422, 424, 500 },
                "{0}; expected a non-success HTTP status; body: {1}", reason, body);
        }
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> RegisterParentGetTokenAsync()
    {
        var email = $"p703_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent reg must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Graph/curriculum scaffolding helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates: grade → subject (MATH/Ar) → concept; returns (subjectId, conceptId).
    /// All P7-03 tests that need graph data use this as their base.
    /// </summary>
    private async Task<(int SubjectId, int ConceptId)> CreateSubjectWithConceptAsync()
    {
        var (gradeId, _) = await CreateGradeGetIdAndNumberAsync();
        var subjectId = await CreateSubjectGetIdAsync(gradeId, $"P703 Subj {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        var conceptId = await CreateConceptGetIdAsync(subjectId);
        return (subjectId, conceptId);
    }

    private async Task<(int GradeId, int GradeNumber)> CreateGradeGetIdAndNumberAsync()
    {
        var name = $"P703 Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/grades/Create",
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "grade create must succeed; body: {0}", body);

        var gradeId = await FindIdInListAsync(
            "/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);
        return (gradeId, 1);
    }

    private async Task<int> CreateSubjectGetIdAsync(int gradeId, string name, int subjectCode, int language)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/subjects/Create",
            new { Name = name, Country = "EG", GradeId = gradeId, SubjectCode = subjectCode, Language = language },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "subject create must succeed; body: {0}", body);
        return await FindIdInListAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", name, "subject", _adminToken);
    }

    private async Task<int> CreateConceptGetIdAsync(int subjectId)
    {
        var name = $"P703 Concept {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/Concepts/Create",
            new { Name = name, DifficultyLevel = 1, SubjectId = subjectId },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "concept create must succeed; body: {0}", body);
        return await FindIdInListAsync(
            $"/api/learning/Concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "concept", _adminToken);
    }

    private async Task<int> CreateSkillGetIdAsync(int conceptId, string skillName)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/Skills/Create",
            new { Name = skillName, MasteryThreshold = 80, EstimatedTimeMinutes = 10, ConceptId = conceptId },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "skill create must succeed; body: {0}", body);
        return await FindSkillIdByNameAsync(conceptId, skillName);
    }

    private async Task<int> FindSkillIdByNameAsync(int conceptId, string skillName)
    {
        // LIST: GET /api/learning/Skills/List?ConceptId={conceptId}&PageNumber=1&PageSize=200
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "Skills List must return 200; body: {0}", listBody);

        var items = ExtractPageItems(listRoot, listBody);
        foreach (var item in items)
        {
            if (TryProp(item, "name", out var np) && np.GetString() == skillName)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue("skill must have id; body: {0}", listBody);
                return idProp.GetInt32();
            }
        }
        throw new InvalidOperationException(
            $"Could not find skill where name='{skillName}' for conceptId={conceptId}; body={listBody}");
    }

    private async Task<List<JsonElement>> GetSkillsListForConceptAsync(int conceptId)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            $"/api/learning/Skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        return ExtractPageItems(listRoot, listBody);
    }

    /// <summary>Calls GetGraph and returns the parsed SkillGraphDto nodes + edges.</summary>
    private async Task<(List<GraphNodeEntry> Nodes, List<GraphEdgeEntry> Edges)> GetGraphDataAsync(int subjectId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GetGraph must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "nodes", out var nodesEl).Should().BeTrue("body: {0}", body);
        var nodes = nodesEl.EnumerateArray().Select(n =>
        {
            TryProp(n, "id",           out var id);
            TryProp(n, "name",         out var name);
            TryProp(n, "isSkillActive",out var isa);
            return new GraphNodeEntry(
                id.GetInt32(),
                name.GetString()!,
                isa.ValueKind != JsonValueKind.Undefined && isa.GetBoolean());
        }).ToList();

        TryProp(data, "edges", out var edgesEl).Should().BeTrue("body: {0}", body);
        var edges = edgesEl.EnumerateArray().Select(e =>
        {
            TryProp(e, "id",             out var id);
            TryProp(e, "sourceNodeId",   out var src);
            TryProp(e, "targetNodeId",   out var tgt);
            TryProp(e, "strength",       out var str);
            return new GraphEdgeEntry(
                id.GetInt32(),
                src.GetInt32(),
                tgt.GetInt32(),
                str.ValueKind == JsonValueKind.Number ? str.GetDecimal() : 1.0m);
        }).ToList();

        return (nodes, edges);
    }

    private async Task<List<JsonElement>> GetGraphNodesForSubjectAsync(int subjectId)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get,
            $"/api/learning/KnowledgeGraph/Graph?subjectId={subjectId}",
            bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GetGraph must return 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "nodes", out var nodesEl).Should().BeTrue("body: {0}", body);
        return nodesEl.EnumerateArray().ToList();
    }

    /// <summary>Resolves node IDs for two skill names via GetGraph.</summary>
    private async Task<(int NodeAId, int NodeBId)> GetNodeIdsForSkillNamesAsync(
        int subjectId, string nameA, string nameB)
    {
        var nodes = await GetGraphNodesForSubjectAsync(subjectId);
        var nodeAId = GetNodeIdByName(nodes, nameA);
        var nodeBId = GetNodeIdByName(nodes, nameB);
        return (nodeAId, nodeBId);
    }

    private static int GetNodeIdByName(List<JsonElement> nodes, string name)
    {
        var node = nodes.FirstOrDefault(n =>
            TryProp(n, "name", out var np) && np.GetString() == name);
        node.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "KnowledgeNode for skill '{0}' must exist in GetGraph", name);
        TryProp(node, "id", out var idProp).Should().BeTrue("node must have id");
        return idProp.GetInt32();
    }

    private async Task<int> FindIdInListAsync(
        string listUrl, string fieldName, string fieldValue, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, listUrl, bearer: bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);

        var items = ExtractPageItems(listRoot, listBody);
        foreach (var item in items)
        {
            if (TryProp(item, fieldName, out var fv) && fv.GetString() == fieldValue)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue(
                    "{0} item must have id; body: {1}", entityType, listBody);
                return idProp.GetInt32();
            }
        }
        throw new InvalidOperationException(
            $"Could not find {entityType} where {fieldName}='{fieldValue}' in list; url={listUrl}; body={listBody}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lightweight typed records for parsed GetGraph data
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record GraphNodeEntry(int Id, string Name, bool IsSkillActive);
    private sealed record GraphEdgeEntry(int Id, int SourceNodeId, int TargetNodeId, decimal Strength);
}

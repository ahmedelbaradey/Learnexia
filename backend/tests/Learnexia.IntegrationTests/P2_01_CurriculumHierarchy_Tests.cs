using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-01 integration tests: Curriculum hierarchy module (Learning).
///
/// Endpoints under test:
///   Grades    : GET /api/learning/grades/List  |  GET /api/learning/grades?id=  |  POST /api/learning/grades/Create  |  PUT /api/learning/grades/Update  |  DELETE /api/learning/grades?id=
///   Subjects  : same pattern with optional ?GradeId= on List
///   Units     : same pattern with optional ?SubjectId= on List
///   Lessons   : same pattern with optional ?UnitId= on List
///   Concepts  : same pattern with optional ?SubjectId= on List
///   Skills    : same pattern with optional ?ConceptId= on List
///
/// Auth contract (post-gate):
///   Grades List / GetById     — requires any authenticated user (class-level [Authorize]);
///                               anonymous → 401.
///   Grades Create/Update/Delete — requires Admin or SuperAdmin
///                               ([Authorize(Policy = AdminOnly)]);
///                               anonymous → 401, non-admin → 403.
///   Subjects/Units/Lessons/Concepts/Skills Create/Update/Delete — requires Admin or
///                               SuperAdmin ([Authorize(Policy = AdminOnly)]);
///                               anonymous → 401, non-admin → 403.
///   Subjects/Units/Lessons/Skills List — AdminOnly (expose admin DTOs; locked down post P7-01/P7-02/P7-03).
///   Concepts List / GetById — unchanged: anonymous access is permitted.
///   Skills List / GetById — AdminOnly (P7-SEC-2: exposes admin DTOs; locked down post P7-03).
///
/// JSON Response Structure (OBSERVED from actual API):
///   List → BaseResponse&lt;PaginatedResult&lt;T&gt;&gt;
///     root.statusCode, root.successed, root.message, root.data (=PaginatedResult), root.errors
///     root.data.currentPage, root.data.totalCount, root.data.totalPages, root.data.pageSize
///     root.data.data = List&lt;T&gt; (the actual items)
///
///   Create/Update/Delete → BaseResponse&lt;string&gt;
///     root.statusCode=200, root.successed=true, root.data="Record saved successfully"
///     NOTE: Handlers use Success() not Created() → HTTP 200 (not 201).
///
///   Get (single) → BaseResponse&lt;SingleXxxResponse&gt;
///     root.data = the entity DTO
///
/// Validation failures → HTTP 422 from FluentValidation middleware
///   Envelope: statusCode, successed=false, message, errors=[{propertyName, errorMessage}]
///
/// Coverage map:
///   AC-1 : CRUD round-trip — each aggregate; full hierarchy creation; nullable SkillId
///   AC-2 : BaseResponse envelope shape; PaginatedResult shape
///   AC-3 : Validation → 422 on empty Name, GradeId=0, out-of-range enum, out-of-range MasteryThreshold
///   AC-4 : Non-existent GradeId fails gracefully (not a naked exception, non-2xx)
///   AC-5 : DifficultyLevel enum persists and round-trips as int in JSON
///   AC-6 : Grades List requires authentication (401 anonymous); Concepts list endpoint is anonymous
///   AC-6b: Subjects/Units List endpoints now require AdminOnly (P7-SEC post P7-01)
///   AC-6c: Lessons List endpoint now requires AdminOnly (P7-SEC-1 post P7-02 security audit)
///   AC-6d: Skills List/GetById endpoints now require AdminOnly (P7-SEC-2 post P7-03)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_01_CurriculumHierarchy_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // -------------------------------------------------------------------------
    // Seeded admin credentials (mirrored from P1_05_RBAC_Tests)
    // -------------------------------------------------------------------------
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    /// <summary>Lazy admin token — fetched once per test class instance.</summary>
    private string? _adminToken;

    public P2_01_CurriculumHierarchy_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Ensure LearningDbContext migrations are applied (LearningModule.InitializeAsync is also called
        // by Program.cs host startup, so this is idempotent).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        // Pre-fetch admin token so all write + read helpers can reuse it.
        _adminToken = await FetchAdminTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Auth helpers
    // =========================================================================

    /// <summary>Signs in as the seeded superadmin and returns the JWT access token.</summary>
    private async Task<string> FetchAdminTokenAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, SignInUrl);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { UserName = AdminUserName, Password = AdminPassword }),
            Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin sign-in prerequisite must succeed; body: {0}", body);

        var root = JsonDocument.Parse(body).RootElement;
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>Builds an HttpRequestMessage with an optional bearer token.</summary>
    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return request;
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        using var request = BuildRequest(method, url, body, bearer);
        var response = await _client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON */ }
        }
        return (response, root, bodyStr);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>Case-insensitive JSON property lookup (handles camelCase and PascalCase).</summary>
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

    // Authenticated variants used by test bodies
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostAsync(string url, object payload, string? bearer = null)
        => SendAsync(HttpMethod.Post, url, payload, bearer);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> GetAsync(string url, string? bearer = null)
        => SendAsync(HttpMethod.Get, url, null, bearer);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PutAsync(string url, object payload, string? bearer = null)
        => SendAsync(HttpMethod.Put, url, payload, bearer);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> DeleteAsync(string url, string? bearer = null)
        => SendAsync(HttpMethod.Delete, url, null, bearer);

    /// <summary>
    /// Extracts the inner list from a paged response.
    /// Structure: root.data (PaginatedResult) → root.data.data (List&lt;T&gt;)
    /// </summary>
    private static List<JsonElement> ExtractItems(JsonElement root, string bodyStr)
    {
        TryProp(root, "data", out var outerData).Should().BeTrue(
            "response must have outer 'data' field; body: {0}", bodyStr);
        TryProp(outerData, "data", out var innerData).Should().BeTrue(
            "PaginatedResult must have inner 'data' array; body: {0}", bodyStr);

        if (innerData.ValueKind == JsonValueKind.Null || innerData.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();

        if (innerData.ValueKind != JsonValueKind.Array)
        {
            // Should not happen, but defensive
            return new List<JsonElement>();
        }

        return innerData.EnumerateArray().ToList();
    }

    /// <summary>
    /// Asserts a successful create response: HTTP 200, successed=true.
    /// Note: handlers use BaseResponseHandler.Success() → HTTP 200, not 201.
    /// </summary>
    private static void AssertCreateSuccess(HttpResponseMessage response, JsonElement root, string body)
    {
        ((int)response.StatusCode).Should().Be(200,
            "Create handler uses Success() → HTTP 200 (not 201); body: {0}", body);

        // If root is not provided (default), parse from body
        var element = root.ValueKind == JsonValueKind.Undefined && !string.IsNullOrWhiteSpace(body)
            ? JsonDocument.Parse(body).RootElement
            : root;

        if (element.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(element, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
            succeededProp.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        }
    }

    // =========================================================================
    // AC-6 — Auth contract: Grades List requires auth; other lists are anonymous
    // =========================================================================

    [Fact(DisplayName = "AC-6: GET /api/learning/grades/List is protected — anonymous → 401")]
    public async Task AC6_GradesList_RequiresAuth()
    {
        // No token — grade list must challenge with 401 (class-level [Authorize] on GradesController).
        var (response, _, body) = await GetAsync("/api/learning/grades/List?PageNumber=1&PageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GradesController is [Authorize]; anonymous request must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "AC-6: GET /api/learning/grades/List with valid token → 200")]
    public async Task AC6_GradesList_Authenticated_Returns200()
    {
        var (response, _, body) = await GetAsync("/api/learning/grades/List?PageNumber=1&PageSize=10", _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Authenticated user must receive 200 on Grades List; body: {0}", body);
    }

    [Theory(DisplayName = "AC-6: Concepts List endpoint remains anonymous (200 without JWT)")]
    [InlineData("/api/learning/concepts/List?PageNumber=1&PageSize=10")]
    public async Task AC6_NonGradeListEndpoints_AreAnonymous(string url)
    {
        // No token — concepts list endpoint must still return 200 (unchanged contract).
        // NOTE: Subjects/List and Units/List were locked to AdminOnly in P7-SEC (P7-01).
        //       Lessons/List was locked to AdminOnly in P7-SEC-1 (P7-02 security audit).
        //       Skills/List was locked to AdminOnly in P7-SEC-2 (P7-03 security: exposes admin metadata).
        var (response, _, body) = await GetAsync(url);

        ((int)response.StatusCode).Should().NotBe(401,
            "endpoint {0} must not require authentication (unchanged); body: {1}", url, body);
        ((int)response.StatusCode).Should().NotBe(403,
            "endpoint {0} must not require a policy (unchanged); body: {1}", url, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Non-grade List endpoint {0} must return 200 for anonymous requests; body: {1}", url, body);
    }

    [Theory(DisplayName = "AC-6b: Subjects/Units List endpoints now require AdminOnly (P7-SEC post P7-01)")]
    [InlineData("/api/learning/subjects/List?PageNumber=1&PageSize=10")]
    [InlineData("/api/learning/units/List?PageNumber=1&PageSize=10")]
    public async Task AC6b_SubjectsUnitsListEndpoints_RequireAdminOnly(string url)
    {
        // Anonymous must get 401.
        var (anonResp, _, anonBody) = await GetAsync(url);
        ((int)anonResp.StatusCode).Should().Be(401,
            "P7-SEC: Subjects/Units List must require auth; url={0}; body: {1}", url, anonBody);
    }

    [Fact(DisplayName = "AC-6c: GET /api/learning/lessons/List requires AdminOnly (P7-SEC-1 post P7-02 security audit) — anonymous → 401")]
    public async Task AC6c_LessonsListEndpoint_RequiresAdminOnly_Anonymous401()
    {
        // No token — Lessons/List was locked to AdminOnly in P7-02 security-audit remediation.
        // It exposes admin metadata (EstimatedMinutes, UnitId, SequenceOrder, SkillId) and
        // returns inactive lessons. Students use GET /Subjects/{id}/Lessons instead.
        var (anonResp, _, anonBody) = await GetAsync("/api/learning/lessons/List?PageNumber=1&PageSize=10");
        ((int)anonResp.StatusCode).Should().Be(401,
            "P7-SEC-1: Lessons/List must require auth; anonymous → 401; body: {0}", anonBody);
    }

    [Fact(DisplayName = "AC-6c: GET /api/learning/lessons/List requires AdminOnly — admin token → 200")]
    public async Task AC6c_LessonsListEndpoint_AdminToken_Returns200()
    {
        var (adminResp, _, adminBody) = await GetAsync("/api/learning/lessons/List?PageNumber=1&PageSize=10", _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P7-SEC-1: Lessons/List with admin token must return 200; body: {0}", adminBody);
    }

    [Theory(DisplayName = "AC-6d: Skills List/GetById endpoints require AdminOnly (P7-SEC-2 post P7-03) — anonymous → 401")]
    [InlineData("/api/learning/skills/List?PageNumber=1&PageSize=10")]
    [InlineData("/api/learning/skills?id=1")]
    public async Task AC6d_SkillsListAndGetByIdEndpoints_RequireAdminOnly_Anonymous401(string url)
    {
        // P7-03 security: Skills List and GetById are now AdminOnly.
        // These endpoints return admin DTOs (MasteryThreshold, ConceptId, IsActive).
        var (anonResp, _, anonBody) = await GetAsync(url);
        ((int)anonResp.StatusCode).Should().Be(401,
            "P7-SEC-2: Skills List/GetById must require auth; anonymous → 401; url={0}; body: {1}", url, anonBody);
    }

    [Fact(DisplayName = "AC-6d: GET /api/learning/skills/List requires AdminOnly — admin token → 200")]
    public async Task AC6d_SkillsListEndpoint_AdminToken_Returns200()
    {
        var (adminResp, _, adminBody) = await GetAsync("/api/learning/skills/List?PageNumber=1&PageSize=10", _adminToken);
        adminResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "P7-SEC-2: Skills/List with admin token must return 200; body: {0}", adminBody);
    }

    // =========================================================================
    // AC-2 — BaseResponse envelope shape + PaginatedResult shape
    // =========================================================================

    [Fact(DisplayName = "AC-2: List Grades response outer envelope has statusCode/successed/message/data/errors")]
    public async Task AC2_GradesList_OuterEnvelopeShape()
    {
        // Grades List requires authentication — send admin token.
        var (response, root, body) = await GetAsync("/api/learning/grades/List?PageNumber=1&PageSize=10", _adminToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Outer BaseResponse<PaginatedResult<T>> keys
        TryProp(root, "statusCode", out _).Should().BeTrue("outer envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("outer envelope must have successed; body: {0}", body);
        succeedProp.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("outer envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("outer envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("outer envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2: List Grades PaginatedResult (root.data) has currentPage/totalCount/totalPages/pageSize/data")]
    public async Task AC2_GradesList_PaginatedResultShape()
    {
        // Grades List requires authentication — send admin token.
        var (response, root, body) = await GetAsync("/api/learning/grades/List?PageNumber=1&PageSize=10", _adminToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var paginatedResult).Should().BeTrue("body: {0}", body);

        // PaginatedResult (nested in root.data) must have pagination fields
        TryProp(paginatedResult, "currentPage", out _).Should().BeTrue(
            "root.data (PaginatedResult) must have currentPage; body: {0}", body);
        TryProp(paginatedResult, "totalCount", out _).Should().BeTrue(
            "root.data (PaginatedResult) must have totalCount; body: {0}", body);
        TryProp(paginatedResult, "totalPages", out _).Should().BeTrue(
            "root.data (PaginatedResult) must have totalPages; body: {0}", body);
        TryProp(paginatedResult, "pageSize", out _).Should().BeTrue(
            "root.data (PaginatedResult) must have pageSize; body: {0}", body);
        TryProp(paginatedResult, "data", out _).Should().BeTrue(
            "root.data (PaginatedResult) must have inner data array; body: {0}", body);
    }

    // =========================================================================
    // AC-1 — CRUD Round-trip: Grade
    // =========================================================================

    [Fact(DisplayName = "AC-1 CRUD Grade: Create → GetById → Update → Delete round-trip with verification")]
    public async Task AC1_Grade_CrudRoundTrip()
    {
        var uniqueName = $"Grade Test {Guid.NewGuid():N}";

        // CREATE — requires Admin JWT
        var (createResp, createRoot, createBody) = await PostAsync("/api/learning/grades/Create",
            new { Number = 2, DisplayName = uniqueName }, _adminToken);
        AssertCreateSuccess(createResp, createRoot, createBody);

        // LIST — authenticated read; find the created grade by unique name
        var (listResp, listRoot, listBody) = await GetAsync("/api/learning/grades/List?PageNumber=1&PageSize=200", _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var gradeItems = ExtractItems(listRoot, listBody);

        int createdGradeId = -1;
        foreach (var item in gradeItems)
        {
            if (TryProp(item, "displayName", out var dn) && dn.GetString() == uniqueName)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue("grade item must have id; body: {0}", listBody);
                createdGradeId = idProp.GetInt32();
                break;
            }
        }
        createdGradeId.Should().BeGreaterThan(0,
            "created grade must appear in the list with a positive id; listBody: {0}", listBody);

        // GET BY ID — authenticated read
        var (getResp, getRoot, getBody) = await GetAsync($"/api/learning/grades?id={createdGradeId}", _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);
        TryProp(getRoot, "successed", out var getSucceeded).Should().BeTrue("body: {0}", getBody);
        getSucceeded.GetBoolean().Should().BeTrue("GetById successed must be true; body: {0}", getBody);
        TryProp(getRoot, "data", out var getData).Should().BeTrue("body: {0}", getBody);
        TryProp(getData, "id", out var getIdProp).Should().BeTrue("data must have id; body: {0}", getBody);
        getIdProp.GetInt32().Should().Be(createdGradeId, "returned id must match; body: {0}", getBody);
        TryProp(getData, "displayName", out var getDnProp).Should().BeTrue("body: {0}", getBody);
        getDnProp.GetString().Should().Be(uniqueName, "displayName must match; body: {0}", getBody);

        // UPDATE — requires Admin JWT
        var updatedName = $"Updated {uniqueName}";
        var (updateResp, updateRoot, updateBody) = await PutAsync("/api/learning/grades/Update",
            new { Id = createdGradeId, Number = 2, DisplayName = updatedName }, _adminToken);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", updateBody);
        TryProp(updateRoot, "successed", out var updateSucceeded).Should().BeTrue("body: {0}", updateBody);
        updateSucceeded.GetBoolean().Should().BeTrue("Update successed must be true; body: {0}", updateBody);

        // GET after UPDATE — verify name changed; authenticated read
        var (getAfterUpdateResp, getAfterUpdateRoot, getAfterUpdateBody) = await GetAsync($"/api/learning/grades?id={createdGradeId}", _adminToken);
        getAfterUpdateResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getAfterUpdateBody);
        TryProp(getAfterUpdateRoot, "data", out var updatedData).Should().BeTrue("body: {0}", getAfterUpdateBody);
        TryProp(updatedData, "displayName", out var updatedDnProp).Should().BeTrue("body: {0}", getAfterUpdateBody);
        updatedDnProp.GetString().Should().Be(updatedName, "displayName must reflect update; body: {0}", getAfterUpdateBody);

        // DELETE — requires Admin JWT
        var (deleteResp, deleteRoot, deleteBody) = await DeleteAsync($"/api/learning/grades?id={createdGradeId}", _adminToken);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", deleteBody);
        TryProp(deleteRoot, "successed", out var deleteSucceeded).Should().BeTrue("body: {0}", deleteBody);
        deleteSucceeded.GetBoolean().Should().BeTrue("Delete successed must be true; body: {0}", deleteBody);
    }

    // =========================================================================
    // AC-1 — Full hierarchy round-trip: Grade→Subject→Unit→Lesson→Concept→Skill
    // =========================================================================

    [Fact(DisplayName = "AC-1 Hierarchy: Grade→Subject→Unit→Lesson→Concept→Skill full creation round-trip")]
    public async Task AC1_FullHierarchy_CreationRoundTrip()
    {
        // STEP 1: Create Grade — requires Admin JWT
        var gradeName = $"Hierarchy Grade {Guid.NewGuid():N}";
        var (gradeResp, gradeRoot, gradeBody) = await PostAsync("/api/learning/grades/Create",
            new { Number = 3, DisplayName = gradeName }, _adminToken);
        AssertCreateSuccess(gradeResp, gradeRoot, gradeBody);

        int gradeId = await FindIdInList("/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", gradeName, "grade", _adminToken);

        // STEP 2: Create Subject — requires Admin JWT
        var subjName = $"Math {Guid.NewGuid():N}";
        var (subjResp, subjRoot, subjBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = subjName, Country = "EG", GradeId = gradeId }, _adminToken);
        AssertCreateSuccess(subjResp, subjRoot, subjBody);

        int subjectId = await FindIdInList(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", subjName, "subject", _adminToken);

        // STEP 3: Create Unit — requires Admin JWT
        var unitName = $"Algebra {Guid.NewGuid():N}";
        var (unitResp, unitRoot, unitBody) = await PostAsync("/api/learning/units/Create",
            new { Name = unitName, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        AssertCreateSuccess(unitResp, unitRoot, unitBody);

        int unitId = await FindIdInList(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", unitName, "unit", _adminToken);

        // STEP 4: Create Concept — requires Admin JWT
        var conceptName = $"Variables {Guid.NewGuid():N}";
        var (conceptResp, conceptRoot, conceptBody) = await PostAsync("/api/learning/concepts/Create",
            new { Name = conceptName, Description = "Intro to variables", DifficultyLevel = 1 /* Easy */, SubjectId = subjectId }, _adminToken);
        AssertCreateSuccess(conceptResp, conceptRoot, conceptBody);

        int conceptId = await FindIdInList(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", conceptName, "concept");

        // STEP 5: Create Skill — requires Admin JWT
        var skillName = $"Solve Equations {Guid.NewGuid():N}";
        var (skillResp, skillRoot, skillBody) = await PostAsync("/api/learning/skills/Create",
            new { Name = skillName, MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = conceptId }, _adminToken);
        AssertCreateSuccess(skillResp, skillRoot, skillBody);

        int skillId = await FindIdInList(
            $"/api/learning/skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}",
            "name", skillName, "skill");

        // STEP 6: Create Lesson WITHOUT SkillId (null is allowed) — requires Admin JWT
        var lesson1Name = $"Intro Lesson {Guid.NewGuid():N}";
        var (lesson1Resp, lesson1Root, lesson1Body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = lesson1Name, Difficulty = 1 /* Easy */, SequenceOrder = 1, IsLocked = false, UnitId = unitId }, _adminToken);
        AssertCreateSuccess(lesson1Resp, lesson1Root, lesson1Body);

        // STEP 7: Create Lesson WITH SkillId — requires Admin JWT
        var lesson2Name = $"Linked Lesson {Guid.NewGuid():N}";
        var (lesson2Resp, lesson2Root, lesson2Body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = lesson2Name, Difficulty = 2 /* Medium */, SequenceOrder = 2, IsLocked = false, UnitId = unitId, SkillId = skillId }, _adminToken);
        AssertCreateSuccess(lesson2Resp, lesson2Root, lesson2Body);

        // Verify lessons appear in filtered list — admin token required (P7-SEC-1: Lessons/List is AdminOnly)
        var (lessonListResp, lessonListRoot, lessonListBody) = await GetAsync(
            $"/api/learning/lessons/List?PageNumber=1&PageSize=200&UnitId={unitId}", _adminToken);
        lessonListResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", lessonListBody);
        var lessons = ExtractItems(lessonListRoot, lessonListBody);
        lessons.Should().HaveCountGreaterThanOrEqualTo(2,
            "both lessons must appear in the filtered list; body: {0}", lessonListBody);
    }

    // =========================================================================
    // AC-5 — DifficultyLevel enum round-trip
    // =========================================================================

    [Fact(DisplayName = "AC-5 DifficultyLevel: Lesson Difficulty=Hard(3) persists and is returned as int in JSON")]
    public async Task AC5_Lesson_DifficultyLevel_RoundTrips()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int unitId = await CreateUnitGetId(subjectId);

        var lessonName = $"Hard Lesson {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await PostAsync("/api/learning/lessons/Create",
            new { Name = lessonName, Difficulty = 3 /* Hard */, SequenceOrder = 1, IsLocked = false, UnitId = unitId }, _adminToken);
        AssertCreateSuccess(createResp, default, createBody);

        // P7-SEC-1: Lessons/List is AdminOnly — must send admin token
        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/lessons/List?PageNumber=1&PageSize=200&UnitId={unitId}", _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var lessons = ExtractItems(listRoot, listBody);

        bool found = false;
        foreach (var lesson in lessons)
        {
            if (TryProp(lesson, "name", out var n) && n.GetString() == lessonName)
            {
                found = true;
                TryProp(lesson, "difficulty", out var diffProp).Should().BeTrue(
                    "lesson must have difficulty field; body: {0}", listBody);
                // DifficultyLevel is stored as int; serialized as number
                diffProp.ValueKind.Should().BeOneOf(
                    new[] { JsonValueKind.Number, JsonValueKind.String },
                    "difficulty must be a number or string; body: {0}", listBody);
                if (diffProp.ValueKind == JsonValueKind.Number)
                    diffProp.GetInt32().Should().Be(3, "Hard=3; body: {0}", listBody);
                break;
            }
        }
        found.Should().BeTrue("'Hard Lesson' must appear in lessons list; body: {0}", listBody);
    }

    [Fact(DisplayName = "AC-5 DifficultyLevel: Concept DifficultyLevel=Medium(2) persists as int in JSON")]
    public async Task AC5_Concept_DifficultyLevel_RoundTrips()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);

        var conceptName = $"Medium Concept {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await PostAsync("/api/learning/concepts/Create",
            new { Name = conceptName, DifficultyLevel = 2 /* Medium */, SubjectId = subjectId }, _adminToken);
        AssertCreateSuccess(createResp, default, createBody);

        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var concepts = ExtractItems(listRoot, listBody);

        bool found = false;
        foreach (var concept in concepts)
        {
            if (TryProp(concept, "name", out var n) && n.GetString() == conceptName)
            {
                found = true;
                TryProp(concept, "difficultyLevel", out var diffProp).Should().BeTrue(
                    "concept must have difficultyLevel field; body: {0}", listBody);
                diffProp.ValueKind.Should().BeOneOf(
                    new[] { JsonValueKind.Number, JsonValueKind.String },
                    "difficultyLevel must be a number or string; body: {0}", listBody);
                if (diffProp.ValueKind == JsonValueKind.Number)
                    diffProp.GetInt32().Should().Be(2, "Medium=2; body: {0}", listBody);
                break;
            }
        }
        found.Should().BeTrue("'Medium Concept' must appear in concepts list; body: {0}", listBody);
    }

    // =========================================================================
    // AC-3 — Validation: 422 on invalid commands
    // =========================================================================

    [Fact(DisplayName = "AC-3 Validation: Grade Create with empty DisplayName → 422")]
    public async Task AC3_Grade_EmptyDisplayName_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = 1, DisplayName = "" }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty DisplayName must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
        TryProp(root, "errors", out var errorsProp).Should().BeTrue("422 must include errors[]; body: {0}", body);
        errorsProp.EnumerateArray().Should().NotBeEmpty("errors[] must be populated; body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Grade Create with Number=0 (out of 1–6 range) → 422")]
    public async Task AC3_Grade_InvalidNumber_Zero_Returns422()
    {
        // Admin JWT required so FluentValidation fires (not the auth gate).
        var (response, root, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = 0, DisplayName = "Valid Name" }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Number=0 out of valid range 1–6 → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Grade Create with Number=7 (out of 1–6 range) → 422")]
    public async Task AC3_Grade_InvalidNumber_TooLarge_Returns422()
    {
        // Admin JWT required so FluentValidation fires (not the auth gate).
        var (response, root, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = 7, DisplayName = "Valid Name" }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Number=7 out of valid range 1–6 → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Subject Create with empty Name → 422")]
    public async Task AC3_Subject_EmptyName_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/subjects/Create",
            new { Name = "", GradeId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty Name must trigger validation → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Subject Create with GradeId=0 → 422 (GreaterThan(0) rule)")]
    public async Task AC3_Subject_ZeroGradeId_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/subjects/Create",
            new { Name = "Science", GradeId = 0 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "GradeId=0 violates GreaterThan(0) → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
        TryProp(root, "errors", out var errorsProp).Should().BeTrue("errors[] must be present; body: {0}", body);
        errorsProp.EnumerateArray().Should().NotBeEmpty("errors[] must not be empty; body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Unit Create with empty Name → 422")]
    public async Task AC3_Unit_EmptyName_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/units/Create",
            new { Name = "", SequenceOrder = 1, SubjectId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty Name must trigger validation → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Lesson Create with empty Name → 422")]
    public async Task AC3_Lesson_EmptyName_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = "", Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty Name must trigger validation → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Lesson Create with Difficulty=0 (not in DifficultyLevel enum) → 422")]
    public async Task AC3_Lesson_InvalidDifficulty_Zero_Returns422()
    {
        // DifficultyLevel: Easy=1, Medium=2, Hard=3 — 0 is outside the enum.
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = "Valid Lesson", Difficulty = 0, SequenceOrder = 1, IsLocked = false, UnitId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Difficulty=0 not in DifficultyLevel enum (Easy=1,Medium=2,Hard=3) → IsInEnum() → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Concept Create with empty Name → 422")]
    public async Task AC3_Concept_EmptyName_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/concepts/Create",
            new { Name = "", DifficultyLevel = 1, SubjectId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty Name must trigger validation → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Concept Create with DifficultyLevel=99 (not in enum) → 422")]
    public async Task AC3_Concept_InvalidDifficultyLevel_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/concepts/Create",
            new { Name = "Valid Concept", DifficultyLevel = 99, SubjectId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "DifficultyLevel=99 not valid → IsInEnum() → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Skill Create with empty Name → 422")]
    public async Task AC3_Skill_EmptyName_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/skills/Create",
            new { Name = "", MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty Name must trigger validation → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-3 Validation: Skill Create with MasteryThreshold=101 (out of 0–100) → 422")]
    public async Task AC3_Skill_MasteryThresholdOutOfRange_Returns422()
    {
        // Admin JWT required so the auth gate passes and FluentValidation fires.
        var (response, root, body) = await PostAsync("/api/learning/skills/Create",
            new { Name = "Valid Skill", MasteryThreshold = 101, EstimatedTimeMinutes = 30, ConceptId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MasteryThreshold=101 out of InclusiveBetween(0,100) → 422; body: {0}", body);
        TryProp(root, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-2 — Validation envelope shape: 422 has proper keys
    // =========================================================================

    [Fact(DisplayName = "AC-2 Validation Envelope: 422 has statusCode, successed=false, message, errors[]")]
    public async Task AC2_ValidationEnvelope_Has422Shape()
    {
        // Admin JWT required so FluentValidation runs (multiple violations → 422).
        var (response, root, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = 0, DisplayName = "" }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "multiple violations must return 422; body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("422 envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var succeededProp).Should().BeTrue("422 envelope must have successed; body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("422 envelope must have message; body: {0}", body);
        TryProp(root, "errors", out var errorsProp).Should().BeTrue("422 envelope must have errors; body: {0}", body);
        errorsProp.EnumerateArray().Should().NotBeEmpty("errors[] must be populated; body: {0}", body);

        // Each error item must have PropertyName and ErrorMessage
        foreach (var err in errorsProp.EnumerateArray())
        {
            TryProp(err, "propertyName", out _).Should().BeTrue(
                "each error item must have 'propertyName'; body: {0}", body);
            TryProp(err, "errorMessage", out _).Should().BeTrue(
                "each error item must have 'errorMessage'; body: {0}", body);
        }
    }

    // =========================================================================
    // AC-4 — FK behavior: non-existent GradeId fails gracefully
    // =========================================================================

    [Fact(DisplayName = "AC-4 FK: Subject Create with non-existent GradeId=999999 fails gracefully (non-2xx, valid JSON envelope)")]
    public async Task AC4_Subject_NonExistentGradeId_FailsGracefully()
    {
        // GradeId=999999 passes validator (GreaterThan(0)) but doesn't exist in DB.
        // FK violation raised at SaveChanges in UnitOfWorkBehavior → propagates to handler catch → ServerError() → HTTP 500.
        // Admin JWT required — Create now has [Authorize(Policy = AdminOnly)].
        var (response, root, body) = await PostAsync("/api/learning/subjects/Create",
            new { Name = "Orphan Subject", GradeId = 999999 }, _adminToken);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().NotBe(200, "non-existent GradeId must not return 200; body: {0}", body);
        statusCode.Should().NotBe(201, "non-existent GradeId must not return 201; body: {0}", body);

        // Response body must be valid JSON (envelope), not a naked exception page
        body.Should().NotBeNullOrWhiteSpace("response body must not be empty on FK failure; body: {0}", body);

        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("FK failure response must be valid JSON, not a raw exception page; body: {0}", body);

        // Envelope must have successed=false
        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var succeededProp))
        {
            succeededProp.GetBoolean().Should().BeFalse(
                "successed must be false when FK constraint is violated; body: {0}", body);
        }
    }

    // =========================================================================
    // AC-1 — Nullable SkillId: Lesson accepts null/absent SkillId
    // =========================================================================

    [Fact(DisplayName = "AC-1 Nullable SkillId: Lesson created without SkillId succeeds (field omitted)")]
    public async Task AC1_Lesson_WithoutSkillId_IsAccepted()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int unitId = await CreateUnitGetId(subjectId);

        var (createResp, createRoot, createBody) = await PostAsync("/api/learning/lessons/Create",
            new { Name = $"No-Skill Lesson {Guid.NewGuid():N}", Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = unitId }, _adminToken);

        AssertCreateSuccess(createResp, createRoot, createBody);
    }

    [Fact(DisplayName = "AC-1 Nullable SkillId: Lesson created with explicit null SkillId succeeds")]
    public async Task AC1_Lesson_ExplicitNullSkillId_IsAccepted()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int unitId = await CreateUnitGetId(subjectId);

        var payload = new
        {
            Name = $"Null SkillId Lesson {Guid.NewGuid():N}",
            Difficulty = 2,
            SequenceOrder = 1,
            IsLocked = false,
            UnitId = unitId,
            SkillId = (int?)null
        };
        var (createResp, createRoot, createBody) = await PostAsync("/api/learning/lessons/Create", payload, _adminToken);

        AssertCreateSuccess(createResp, createRoot, createBody);
    }

    // =========================================================================
    // AC-1 — Skill fields: MasteryThreshold + EstimatedTimeMinutes present in response
    // =========================================================================

    [Fact(DisplayName = "AC-1 Skill fields: MasteryThreshold and EstimatedTimeMinutes round-trip in list response")]
    public async Task AC1_Skill_MasteryAndTimeFields_PresentInResponse()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int conceptId = await CreateConceptGetId(subjectId);

        var skillName = $"Field Skill {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await PostAsync("/api/learning/skills/Create",
            new { Name = skillName, MasteryThreshold = 75, EstimatedTimeMinutes = 45, ConceptId = conceptId }, _adminToken);
        AssertCreateSuccess(createResp, default, createBody);

        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var skills = ExtractItems(listRoot, listBody);

        bool found = false;
        foreach (var skill in skills)
        {
            if (TryProp(skill, "name", out var n) && n.GetString() == skillName)
            {
                found = true;
                TryProp(skill, "masteryThreshold", out var masteryProp).Should().BeTrue(
                    "skill must have masteryThreshold; body: {0}", listBody);
                masteryProp.GetInt32().Should().Be(75, "masteryThreshold must round-trip; body: {0}", listBody);

                TryProp(skill, "estimatedTimeMinutes", out var timeProp).Should().BeTrue(
                    "skill must have estimatedTimeMinutes; body: {0}", listBody);
                timeProp.GetInt32().Should().Be(45, "estimatedTimeMinutes must round-trip; body: {0}", listBody);
                break;
            }
        }
        found.Should().BeTrue("skill must appear in list; body: {0}", listBody);
    }

    // =========================================================================
    // AC-1 — GetById non-existent returns error (not 2xx)
    // =========================================================================

    [Fact(DisplayName = "AC-1 GetById: non-existent Grade id returns non-2xx with successed=false")]
    public async Task AC1_Grade_GetById_NonExistent_ReturnsNon2xx()
    {
        // LearningRepository.GetByIdAsync throws InvalidOperationException when entity not found.
        // The handler's catch block returns ServerError() → HTTP 500 with successed=false.
        // Authenticated read required.
        var (response, root, body) = await GetAsync("/api/learning/grades?id=99999999", _adminToken);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().NotBe(200,
            "GetById for non-existent id must not return 200; body: {0}", body);
        statusCode.Should().NotBe(201,
            "GetById for non-existent id must not return 201; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var succeededProp))
        {
            succeededProp.GetBoolean().Should().BeFalse(
                "successed must be false for non-existent entity; body: {0}", body);
        }
    }

    // =========================================================================
    // AC-1 — Subject filtered list
    // =========================================================================

    [Fact(DisplayName = "AC-1 Subject Filter: List with GradeId filter returns only that grade's subjects")]
    public async Task AC1_Subjects_FilterByGradeId_Works()
    {
        // Create two distinct grades — requires Admin JWT
        var gradeAId = await CreateGradeGetId();
        var gradeBId = await CreateGradeGetId();

        // Create one subject per grade with unique names — requires Admin JWT
        var nameA = $"SubjA {Guid.NewGuid():N}";
        var nameB = $"SubjB {Guid.NewGuid():N}";

        var (sAResp, sARoot, sABody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = nameA, GradeId = gradeAId }, _adminToken);
        AssertCreateSuccess(sAResp, sARoot, sABody);

        var (sBResp, sBRoot, sBBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = nameB, GradeId = gradeBId }, _adminToken);
        AssertCreateSuccess(sBResp, sBRoot, sBBody);

        // List subjects filtered by GradeA — AdminOnly (P7-SEC: locked down from anonymous)
        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeAId}", _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var subjectsA = ExtractItems(listRoot, listBody);

        bool foundA = subjectsA.Any(s => TryProp(s, "name", out var n) && n.GetString() == nameA);
        foundA.Should().BeTrue("GradeA filter must include SubjectA; body: {0}", listBody);

        bool foundB = subjectsA.Any(s => TryProp(s, "name", out var n) && n.GetString() == nameB);
        foundB.Should().BeFalse("GradeA filter must NOT include SubjectB; body: {0}", listBody);
    }

    // =========================================================================
    // Private setup helpers
    // =========================================================================

    /// <summary>
    /// Searches the paged list at the given URL for an item where field==value, returns its id.
    /// </summary>
    private async Task<int> FindIdInList(string listUrl, string field, string value, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await GetAsync(listUrl, bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);

        var items = ExtractItems(listRoot, listBody);
        foreach (var item in items)
        {
            if (TryProp(item, field, out var fieldVal) && fieldVal.GetString() == value)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue(
                    "{0} item must have id; body: {1}", entityType, listBody);
                return idProp.GetInt32();
            }
        }
        throw new InvalidOperationException(
            $"Could not find {entityType} where {field}='{value}' in list; url={listUrl}; listBody={listBody}");
    }

    /// <summary>Creates a grade using Admin JWT and returns its id.</summary>
    private async Task<int> CreateGradeGetId()
    {
        var name = $"Setup Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq grade create failed; body: {0}", body);
        return await FindIdInList("/api/learning/grades/List?PageNumber=1&PageSize=200", "displayName", name, "grade", _adminToken);
    }

    private async Task<int> CreateSubjectGetId(int gradeId)
    {
        var name = $"Setup Subject {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/subjects/Create",
            new { Name = name, GradeId = gradeId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq subject create failed; body: {0}", body);
        return await FindIdInList(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", name, "subject", _adminToken);
    }

    private async Task<int> CreateUnitGetId(int subjectId)
    {
        var name = $"Setup Unit {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/units/Create",
            new { Name = name, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq unit create failed; body: {0}", body);
        return await FindIdInList(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "unit", _adminToken);
    }

    private async Task<int> CreateConceptGetId(int subjectId)
    {
        var name = $"Setup Concept {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/concepts/Create",
            new { Name = name, DifficultyLevel = 1, SubjectId = subjectId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq concept create failed; body: {0}", body);
        return await FindIdInList(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "concept");
    }
}

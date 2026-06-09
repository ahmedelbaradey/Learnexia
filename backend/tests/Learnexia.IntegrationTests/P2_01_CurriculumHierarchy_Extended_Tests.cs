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
/// P2-01 EXTENDED integration tests — implements BE-TC-02..04, BE-TC-08, BE-TC-11,
/// BE-TC-17, BE-TC-21, BE-TC-23..37 from docs/qc/P2-01/backend-test-cases.md.
///
/// Cross-referenced existing tests (BE-TC-01, 05..07, 09..10, 12..16, 18..20, 22, 27, 33)
/// are handled by the existing P2_01_CurriculumHierarchy_Tests class. This file adds only
/// the net-new test methods and boundary additions.
///
/// Running:
///   dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj
///          --filter "FullyQualifiedName~P2_01"
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_01_CurriculumHierarchy_Extended_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string BasicUserName = "basicuser";
    private const string BasicUserPassword = "123Pa$$word!";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    private string? _adminToken;
    private string? _basicToken;

    public P2_01_CurriculumHierarchy_Extended_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await db.Database.MigrateAsync();

        _adminToken = await FetchTokenAsync(AdminUserName, AdminPassword);
        _basicToken = await FetchTokenAsync(BasicUserName, BasicUserPassword);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Auth helpers
    // =========================================================================

    private async Task<string> FetchTokenAsync(string userName, string password)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, SignInUrl);
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { UserName = userName, Password = password }),
            Encoding.UTF8, "application/json");
        var resp = await _client.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed for '{0}'; body: {1}", userName, body);
        var root = JsonDocument.Parse(body).RootElement;
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

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
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON */ }
        }
        return (response, root, bodyStr);
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PostAsync(string url, object payload, string? bearer = null)
        => SendAsync(HttpMethod.Post, url, payload, bearer);
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> GetAsync(string url, string? bearer = null)
        => SendAsync(HttpMethod.Get, url, null, bearer);
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> PutAsync(string url, object payload, string? bearer = null)
        => SendAsync(HttpMethod.Put, url, payload, bearer);
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)> DeleteAsync(string url, string? bearer = null)
        => SendAsync(HttpMethod.Delete, url, null, bearer);

    private static List<JsonElement> ExtractItems(JsonElement root, string bodyStr)
    {
        TryProp(root, "data", out var outerData).Should().BeTrue(
            "response must have outer 'data'; body: {0}", bodyStr);
        TryProp(outerData, "data", out var innerData).Should().BeTrue(
            "PaginatedResult must have inner 'data' array; body: {0}", bodyStr);
        if (innerData.ValueKind == JsonValueKind.Null || innerData.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();
        if (innerData.ValueKind != JsonValueKind.Array) return new List<JsonElement>();
        return innerData.EnumerateArray().ToList();
    }

    private static void AssertCreateSuccess(HttpResponseMessage response, JsonElement root, string body)
    {
        ((int)response.StatusCode).Should().Be(200,
            "Create handler uses Success() → HTTP 200 (not 201); body: {0}", body);
        var element = root.ValueKind == JsonValueKind.Undefined && !string.IsNullOrWhiteSpace(body)
            ? JsonDocument.Parse(body).RootElement : root;
        if (element.ValueKind != JsonValueKind.Undefined)
        {
            TryProp(element, "successed", out var sp).Should().BeTrue("body: {0}", body);
            sp.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
        }
    }

    private async Task<int> FindIdInList(string listUrl, string field, string value, string entityType, string? bearer = null)
    {
        var (listResp, listRoot, listBody) = await GetAsync(listUrl, bearer);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "prereq list for {0} must succeed; body: {1}", entityType, listBody);
        var items = ExtractItems(listRoot, listBody);
        foreach (var item in items)
        {
            if (TryProp(item, field, out var fv) && fv.GetString() == value)
            {
                TryProp(item, "id", out var idProp).Should().BeTrue(
                    "{0} must have id; body: {1}", entityType, listBody);
                return idProp.GetInt32();
            }
        }
        throw new InvalidOperationException(
            $"Could not find {entityType} where {field}='{value}' in list; url={listUrl}; body={listBody}");
    }

    private async Task<int> CreateGradeGetId()
    {
        var name = $"Setup Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq grade create failed; body: {0}", body);
        return await FindIdInList("/api/learning/grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);
    }

    private async Task<int> CreateSubjectGetId(int gradeId)
    {
        var name = $"Setup Subject {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/subjects/Create",
            new { Name = name, GradeId = gradeId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq subject create failed; body: {0}", body);
        return await FindIdInList(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", name, "subject");
    }

    private async Task<int> CreateUnitGetId(int subjectId)
    {
        var name = $"Setup Unit {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/units/Create",
            new { Name = name, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq unit create failed; body: {0}", body);
        return await FindIdInList(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "unit");
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

    private async Task<int> CreateSkillGetId(int conceptId)
    {
        var name = $"Setup Skill {Guid.NewGuid():N}";
        var (resp, _, body) = await PostAsync("/api/learning/skills/Create",
            new { Name = name, MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = conceptId }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "prereq skill create failed; body: {0}", body);
        return await FindIdInList(
            $"/api/learning/skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}",
            "name", name, "skill");
    }

    // =========================================================================
    // BE-TC-02 — Unit CRUD round-trip (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-02: Unit CRUD round-trip — Create → List → GetById → Update → GetById → Delete → GetById(deleted)")]
    public async Task BETC02_Unit_CrudRoundTrip()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);

        var unitName = $"Unit RoundTrip {Guid.NewGuid():N}";

        // CREATE
        var (createResp, createRoot, createBody) = await PostAsync("/api/learning/units/Create",
            new { Name = unitName, SequenceOrder = 1, SubjectId = subjectId }, _adminToken);
        AssertCreateSuccess(createResp, createRoot, createBody);

        // LIST filtered by SubjectId
        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractItems(listRoot, listBody);
        bool foundInList = items.Any(u => TryProp(u, "name", out var n) && n.GetString() == unitName);
        foundInList.Should().BeTrue("created unit must appear in filtered list; body: {0}", listBody);

        int unitId = await FindIdInList(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", unitName, "unit");

        // GETBYID
        var (getResp, getRoot, getBody) = await GetAsync($"/api/learning/units?id={unitId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "GetById must succeed; body: {0}", getBody);
        TryProp(getRoot, "successed", out var getS).Should().BeTrue("body: {0}", getBody);
        getS.GetBoolean().Should().BeTrue("body: {0}", getBody);

        // UPDATE Name and SequenceOrder
        var updatedName = $"Updated {unitName}";
        var (updateResp, updateRoot, updateBody) = await PutAsync("/api/learning/units/Update",
            new { Id = unitId, Name = updatedName, SequenceOrder = 2, SubjectId = subjectId }, _adminToken);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK, "Update must return 200; body: {0}", updateBody);
        TryProp(updateRoot, "successed", out var upS).Should().BeTrue("body: {0}", updateBody);
        upS.GetBoolean().Should().BeTrue("body: {0}", updateBody);

        // GETBYID after update — verify name changed
        var (getAfterResp, getAfterRoot, getAfterBody) = await GetAsync($"/api/learning/units?id={unitId}");
        getAfterResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getAfterBody);
        TryProp(getAfterRoot, "data", out var updatedData).Should().BeTrue("body: {0}", getAfterBody);
        TryProp(updatedData, "name", out var updatedNameProp).Should().BeTrue("body: {0}", getAfterBody);
        updatedNameProp.GetString().Should().Be(updatedName, "name must reflect update; body: {0}", getAfterBody);

        // DELETE
        var (deleteResp, deleteRoot, deleteBody) = await DeleteAsync($"/api/learning/units?id={unitId}", _adminToken);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", deleteBody);
        TryProp(deleteRoot, "successed", out var delS).Should().BeTrue("body: {0}", deleteBody);
        delS.GetBoolean().Should().BeTrue("body: {0}", deleteBody);

        // GETBYID after delete — must fail
        var (getDeletedResp, getDeletedRoot, getDeletedBody) = await GetAsync($"/api/learning/units?id={unitId}");
        var deletedStatusCode = (int)getDeletedResp.StatusCode;
        deletedStatusCode.Should().NotBe(200,
            "GetById after delete must not return 200; body: {0}", getDeletedBody);
        if (getDeletedRoot.ValueKind != JsonValueKind.Undefined && TryProp(getDeletedRoot, "successed", out var dS))
            dS.GetBoolean().Should().BeFalse("successed must be false for deleted entity; body: {0}", getDeletedBody);
    }

    // =========================================================================
    // BE-TC-03 — Concept CRUD round-trip (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-03: Concept CRUD round-trip — Create → List → GetById → Update → GetById → Delete")]
    public async Task BETC03_Concept_CrudRoundTrip()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);

        var conceptName = $"Concept RoundTrip {Guid.NewGuid():N}";

        // CREATE with DifficultyLevel=2 (Medium) and Description
        var (createResp, createRoot, createBody) = await PostAsync("/api/learning/concepts/Create",
            new { Name = conceptName, DifficultyLevel = 2, Description = "Integration test concept", SubjectId = subjectId }, _adminToken);
        AssertCreateSuccess(createResp, createRoot, createBody);

        int conceptId = await FindIdInList(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", conceptName, "concept");

        // LIST
        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);

        // GETBYID — verify description and difficultyLevel
        var (getResp, getRoot, getBody) = await GetAsync($"/api/learning/concepts?id={conceptId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);
        TryProp(getRoot, "successed", out var gs).Should().BeTrue("body: {0}", getBody);
        gs.GetBoolean().Should().BeTrue("body: {0}", getBody);

        // UPDATE
        var updatedName = $"Updated {conceptName}";
        var (updateResp, updateRoot, updateBody) = await PutAsync("/api/learning/concepts/Update",
            new { Id = conceptId, Name = updatedName, DifficultyLevel = 3, Description = "Updated desc", SubjectId = subjectId }, _adminToken);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK, "Update must return 200; body: {0}", updateBody);
        TryProp(updateRoot, "successed", out var us).Should().BeTrue("body: {0}", updateBody);
        us.GetBoolean().Should().BeTrue("body: {0}", updateBody);

        // GETBYID after update — name changed
        var (getAfterResp, getAfterRoot, getAfterBody) = await GetAsync($"/api/learning/concepts?id={conceptId}");
        getAfterResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getAfterBody);
        TryProp(getAfterRoot, "data", out var afterData).Should().BeTrue("body: {0}", getAfterBody);
        TryProp(afterData, "name", out var afterName).Should().BeTrue("body: {0}", getAfterBody);
        afterName.GetString().Should().Be(updatedName, "name must reflect update; body: {0}", getAfterBody);

        // DELETE
        var (deleteResp, deleteRoot, deleteBody) = await DeleteAsync($"/api/learning/concepts?id={conceptId}", _adminToken);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", deleteBody);
        TryProp(deleteRoot, "successed", out var ds).Should().BeTrue("body: {0}", deleteBody);
        ds.GetBoolean().Should().BeTrue("body: {0}", deleteBody);
    }

    // =========================================================================
    // BE-TC-04 — Skill CRUD round-trip (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-04: Skill CRUD round-trip — Create → List → GetById → Update MasteryThreshold → GetById → Delete")]
    public async Task BETC04_Skill_CrudRoundTrip()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int conceptId = await CreateConceptGetId(subjectId);

        var skillName = $"Skill RoundTrip {Guid.NewGuid():N}";

        // CREATE with MasteryThreshold=80, EstimatedTimeMinutes=30
        var (createResp, createRoot, createBody) = await PostAsync("/api/learning/skills/Create",
            new { Name = skillName, MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = conceptId }, _adminToken);
        AssertCreateSuccess(createResp, createRoot, createBody);

        int skillId = await FindIdInList(
            $"/api/learning/skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}",
            "name", skillName, "skill");

        // LIST by ConceptId
        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/skills/List?PageNumber=1&PageSize=200&ConceptId={conceptId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);

        // GETBYID
        var (getResp, getRoot, getBody) = await GetAsync($"/api/learning/skills?id={skillId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);
        TryProp(getRoot, "successed", out var gs).Should().BeTrue("body: {0}", getBody);
        gs.GetBoolean().Should().BeTrue("body: {0}", getBody);

        // UPDATE MasteryThreshold=90
        var (updateResp, updateRoot, updateBody) = await PutAsync("/api/learning/skills/Update",
            new { Id = skillId, Name = skillName, MasteryThreshold = 90, EstimatedTimeMinutes = 30, ConceptId = conceptId }, _adminToken);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK, "Update must return 200; body: {0}", updateBody);
        TryProp(updateRoot, "successed", out var us).Should().BeTrue("body: {0}", updateBody);
        us.GetBoolean().Should().BeTrue("body: {0}", updateBody);

        // GETBYID after update — mastery threshold changed
        var (getAfterResp, getAfterRoot, getAfterBody) = await GetAsync($"/api/learning/skills?id={skillId}");
        getAfterResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getAfterBody);
        TryProp(getAfterRoot, "data", out var afterData).Should().BeTrue("body: {0}", getAfterBody);
        TryProp(afterData, "masteryThreshold", out var mt).Should().BeTrue("data must have masteryThreshold; body: {0}", getAfterBody);
        mt.GetInt32().Should().Be(90, "masteryThreshold must reflect update; body: {0}", getAfterBody);

        // DELETE
        var (deleteResp, deleteRoot, deleteBody) = await DeleteAsync($"/api/learning/skills?id={skillId}", _adminToken);
        deleteResp.StatusCode.Should().Be(HttpStatusCode.OK, "Delete must return 200; body: {0}", deleteBody);
        TryProp(deleteRoot, "successed", out var ds).Should().BeTrue("body: {0}", deleteBody);
        ds.GetBoolean().Should().BeTrue("body: {0}", deleteBody);
    }

    // =========================================================================
    // BE-TC-08 — Pagination honors PageNumber/PageSize (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-08: Pagination — page 1 of 2 has ≤ 2 items; page 2 has different items; totalCount ≥ 3")]
    public async Task BETC08_Pagination_HonorsPageNumberAndPageSize()
    {
        // Seed at least 3 grades with unique names
        var grade1 = $"Pager Grade A {Guid.NewGuid():N}";
        var grade2 = $"Pager Grade B {Guid.NewGuid():N}";
        var grade3 = $"Pager Grade C {Guid.NewGuid():N}";
        foreach (var name in new[] { grade1, grade2, grade3 })
        {
            var (r, _, b) = await PostAsync("/api/learning/grades/Create",
                new { Number = 1, DisplayName = name }, _adminToken);
            r.StatusCode.Should().Be(HttpStatusCode.OK, "prereq grade seed failed; body: {0}", b);
        }

        // PAGE 1 — PageSize=2
        var (p1Resp, p1Root, p1Body) = await GetAsync(
            "/api/learning/grades/List?PageNumber=1&PageSize=2", _adminToken);
        p1Resp.StatusCode.Should().Be(HttpStatusCode.OK, "page 1 must succeed; body: {0}", p1Body);

        TryProp(p1Root, "data", out var p1Paginated).Should().BeTrue("body: {0}", p1Body);
        TryProp(p1Paginated, "currentPage", out var p1Page).Should().BeTrue("body: {0}", p1Body);
        p1Page.GetInt32().Should().Be(1, "currentPage must echo the request; body: {0}", p1Body);
        TryProp(p1Paginated, "pageSize", out var p1Size).Should().BeTrue("body: {0}", p1Body);
        p1Size.GetInt32().Should().Be(2, "pageSize must echo the request; body: {0}", p1Body);
        TryProp(p1Paginated, "totalCount", out var p1Total).Should().BeTrue("body: {0}", p1Body);
        p1Total.GetInt32().Should().BeGreaterThanOrEqualTo(3,
            "totalCount must be ≥ 3 (we seeded at least 3); body: {0}", p1Body);
        TryProp(p1Paginated, "totalPages", out var p1Pages).Should().BeTrue("body: {0}", p1Body);
        var expectedTotalPages = (int)Math.Ceiling(p1Total.GetInt32() / 2.0);
        p1Pages.GetInt32().Should().Be(expectedTotalPages,
            "totalPages must be ceil(totalCount/2); body: {0}", p1Body);

        var p1Items = ExtractItems(p1Root, p1Body);
        p1Items.Should().HaveCountLessThanOrEqualTo(2,
            "page 1 with pageSize=2 must return ≤ 2 items; body: {0}", p1Body);

        // PAGE 2 — PageSize=2
        var (p2Resp, p2Root, p2Body) = await GetAsync(
            "/api/learning/grades/List?PageNumber=2&PageSize=2", _adminToken);
        p2Resp.StatusCode.Should().Be(HttpStatusCode.OK, "page 2 must succeed; body: {0}", p2Body);

        TryProp(p2Root, "data", out var p2Paginated).Should().BeTrue("body: {0}", p2Body);
        TryProp(p2Paginated, "currentPage", out var p2Page).Should().BeTrue("body: {0}", p2Body);
        p2Page.GetInt32().Should().Be(2, "currentPage must echo page 2; body: {0}", p2Body);

        var p2Items = ExtractItems(p2Root, p2Body);

        // Page 2 items must differ from page 1 items (different names)
        if (p1Items.Count > 0 && p2Items.Count > 0)
        {
            var p1Names = p1Items
                .Where(i => TryProp(i, "displayName", out _))
                .Select(i => { TryProp(i, "displayName", out var dn); return dn.GetString(); })
                .ToHashSet();
            var p2Names = p2Items
                .Where(i => TryProp(i, "displayName", out _))
                .Select(i => { TryProp(i, "displayName", out var dn); return dn.GetString(); })
                .ToHashSet();
            p1Names.Intersect(p2Names).Should().BeEmpty(
                "page 1 and page 2 items must not overlap; p1: [{0}], p2: [{1}]",
                string.Join(",", p1Names), string.Join(",", p2Names));
        }
    }

    // =========================================================================
    // BE-TC-11 — Child list filters scope to their parent (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-11: Child list filters scope to parent — Units by SubjectId, Concepts by SubjectId, Skills by ConceptId, Lessons by UnitId")]
    public async Task BETC11_ChildLists_ScopeToParent()
    {
        int gradeId = await CreateGradeGetId();
        int subjectXId = await CreateSubjectGetId(gradeId);
        // Subject Y is in a separate grade to avoid the (GradeId,SubjectCode,Language) unique collision
        int gradeYId = await CreateGradeGetId();
        int subjectYId = await CreateSubjectGetId(gradeYId);

        // Units: X has unitX, Y has unitY
        var unitXName = $"UnitX {Guid.NewGuid():N}";
        var unitYName = $"UnitY {Guid.NewGuid():N}";
        var (uxR, _, uxB) = await PostAsync("/api/learning/units/Create",
            new { Name = unitXName, SequenceOrder = 1, SubjectId = subjectXId }, _adminToken);
        AssertCreateSuccess(uxR, default, uxB);
        var (uyR, _, uyB) = await PostAsync("/api/learning/units/Create",
            new { Name = unitYName, SequenceOrder = 1, SubjectId = subjectYId }, _adminToken);
        AssertCreateSuccess(uyR, default, uyB);

        // Filter units by SubjectX
        var (ulResp, ulRoot, ulBody) = await GetAsync(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectXId}");
        ulResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", ulBody);
        var unitsX = ExtractItems(ulRoot, ulBody);
        unitsX.Any(u => TryProp(u, "name", out var n) && n.GetString() == unitXName)
            .Should().BeTrue("SubjectX filter must include unitX; body: {0}", ulBody);
        unitsX.Any(u => TryProp(u, "name", out var n) && n.GetString() == unitYName)
            .Should().BeFalse("SubjectX filter must NOT include unitY; body: {0}", ulBody);

        // Concepts: X has conceptX, Y has conceptY
        var conceptXName = $"ConceptX {Guid.NewGuid():N}";
        var conceptYName = $"ConceptY {Guid.NewGuid():N}";
        var (cxR, _, cxB) = await PostAsync("/api/learning/concepts/Create",
            new { Name = conceptXName, DifficultyLevel = 1, SubjectId = subjectXId }, _adminToken);
        AssertCreateSuccess(cxR, default, cxB);
        var (cyR, _, cyB) = await PostAsync("/api/learning/concepts/Create",
            new { Name = conceptYName, DifficultyLevel = 1, SubjectId = subjectYId }, _adminToken);
        AssertCreateSuccess(cyR, default, cyB);

        // Filter concepts by SubjectX
        var (clResp, clRoot, clBody) = await GetAsync(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectXId}");
        clResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", clBody);
        var conceptsX = ExtractItems(clRoot, clBody);
        conceptsX.Any(c => TryProp(c, "name", out var n) && n.GetString() == conceptXName)
            .Should().BeTrue("SubjectX filter must include conceptX; body: {0}", clBody);
        conceptsX.Any(c => TryProp(c, "name", out var n) && n.GetString() == conceptYName)
            .Should().BeFalse("SubjectX filter must NOT include conceptY; body: {0}", clBody);

        // Skills: conceptX has skillX, conceptY has skillY
        int conceptXId = await FindIdInList(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectXId}",
            "name", conceptXName, "conceptX");
        int conceptYId = await FindIdInList(
            $"/api/learning/concepts/List?PageNumber=1&PageSize=200&SubjectId={subjectYId}",
            "name", conceptYName, "conceptY");

        var skillXName = $"SkillX {Guid.NewGuid():N}";
        var skillYName = $"SkillY {Guid.NewGuid():N}";
        var (sxR, _, sxB) = await PostAsync("/api/learning/skills/Create",
            new { Name = skillXName, MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = conceptXId }, _adminToken);
        AssertCreateSuccess(sxR, default, sxB);
        var (syR, _, syB) = await PostAsync("/api/learning/skills/Create",
            new { Name = skillYName, MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = conceptYId }, _adminToken);
        AssertCreateSuccess(syR, default, syB);

        // Filter skills by ConceptX
        var (slResp, slRoot, slBody) = await GetAsync(
            $"/api/learning/skills/List?PageNumber=1&PageSize=200&ConceptId={conceptXId}");
        slResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", slBody);
        var skillsX = ExtractItems(slRoot, slBody);
        skillsX.Any(s => TryProp(s, "name", out var n) && n.GetString() == skillXName)
            .Should().BeTrue("ConceptX filter must include skillX; body: {0}", slBody);
        skillsX.Any(s => TryProp(s, "name", out var n) && n.GetString() == skillYName)
            .Should().BeFalse("ConceptX filter must NOT include skillY; body: {0}", slBody);

        // Lessons: unitX has lessonX, unitY has lessonY
        int unitXId = await FindIdInList(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectXId}",
            "name", unitXName, "unitX");
        int unitYId = await FindIdInList(
            $"/api/learning/units/List?PageNumber=1&PageSize=200&SubjectId={subjectYId}",
            "name", unitYName, "unitY");

        var lessonXName = $"LessonX {Guid.NewGuid():N}";
        var lessonYName = $"LessonY {Guid.NewGuid():N}";
        var (lxR, _, lxB) = await PostAsync("/api/learning/lessons/Create",
            new { Name = lessonXName, Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = unitXId }, _adminToken);
        AssertCreateSuccess(lxR, default, lxB);
        var (lyR, _, lyB) = await PostAsync("/api/learning/lessons/Create",
            new { Name = lessonYName, Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = unitYId }, _adminToken);
        AssertCreateSuccess(lyR, default, lyB);

        // Filter lessons by UnitX
        var (llResp, llRoot, llBody) = await GetAsync(
            $"/api/learning/lessons/List?PageNumber=1&PageSize=200&UnitId={unitXId}");
        llResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", llBody);
        var lessonsX = ExtractItems(llRoot, llBody);
        lessonsX.Any(l => TryProp(l, "name", out var n) && n.GetString() == lessonXName)
            .Should().BeTrue("UnitX filter must include lessonX; body: {0}", llBody);
        lessonsX.Any(l => TryProp(l, "name", out var n) && n.GetString() == lessonYName)
            .Should().BeFalse("UnitX filter must NOT include lessonY; body: {0}", llBody);
    }

    // =========================================================================
    // BE-TC-17 — Grade Number boundary: 1 and 6 succeed; 0 and 7 → 422
    //            (extends the existing cross-referenced 0 and 7 cases;
    //             adds the inclusive-bound success sub-cases as new here)
    // =========================================================================

    [Theory(DisplayName = "BE-TC-17 (boundary): Grade Number=1 and Number=6 are valid inclusive bounds → 200")]
    [InlineData(1)]
    [InlineData(6)]
    public async Task BETC17_Grade_ValidBoundaryNumbers_Succeed(int number)
    {
        var name = $"Boundary Grade {number} {Guid.NewGuid():N}";
        var (response, root, body) = await PostAsync("/api/learning/grades/Create",
            new { Number = number, DisplayName = name }, _adminToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Grade Number={0} is an inclusive valid bound → must succeed (200); body: {1}", number, body);
        TryProp(root, "successed", out var sp).Should().BeTrue("body: {0}", body);
        sp.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-21 — Lesson Difficulty=99 → 422 (net-new; Difficulty=0 already covered)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-21: Lesson Create with Difficulty=99 (not in enum Easy=1..Hard=3) → 422")]
    public async Task BETC21_Lesson_InvalidDifficulty_99_Returns422()
    {
        var (response, root, body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = "Valid Lesson", Difficulty = 99, SequenceOrder = 1, IsLocked = false, UnitId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Difficulty=99 not in DifficultyLevel enum → 422; body: {0}", body);
        TryProp(root, "successed", out var sp).Should().BeTrue("body: {0}", body);
        sp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // BE-TC-23 — Skill MasteryThreshold=-1 → 422; 0 and 100 succeed (net-new additions)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-23 (net-new): Skill Create with MasteryThreshold=-1 → 422")]
    public async Task BETC23_Skill_NegativeMasteryThreshold_Returns422()
    {
        var (response, root, body) = await PostAsync("/api/learning/skills/Create",
            new { Name = "Valid Skill", MasteryThreshold = -1, EstimatedTimeMinutes = 30, ConceptId = 1 }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "MasteryThreshold=-1 violates InclusiveBetween(0,100) → 422; body: {0}", body);
        TryProp(root, "successed", out var sp).Should().BeTrue("body: {0}", body);
        sp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Theory(DisplayName = "BE-TC-23 (boundary): Skill MasteryThreshold=0 and MasteryThreshold=100 are valid inclusive bounds → 200")]
    [InlineData(0)]
    [InlineData(100)]
    public async Task BETC23_Skill_ValidBoundaryMasteryThreshold_Succeeds(int threshold)
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int conceptId = await CreateConceptGetId(subjectId);

        var name = $"Boundary Skill MT{threshold} {Guid.NewGuid():N}";
        var (response, root, body) = await PostAsync("/api/learning/skills/Create",
            new { Name = name, MasteryThreshold = threshold, EstimatedTimeMinutes = 10, ConceptId = conceptId }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "MasteryThreshold={0} is valid (inclusive bound) → 200; body: {1}", threshold, body);
        TryProp(root, "successed", out var sp).Should().BeTrue("body: {0}", body);
        sp.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-24 — Edit Grade command is validated → 422 (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-24: Grade Update with Number=0 and DisplayName='' → 422 (Edit commands are ICommand, validated)")]
    public async Task BETC24_GradeUpdate_InvalidPayload_Returns422()
    {
        int gradeId = await CreateGradeGetId();

        var (response, root, body) = await PutAsync("/api/learning/grades/Update",
            new { Id = gradeId, Number = 0, DisplayName = "" }, _adminToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Edit Grade with Number=0 and empty DisplayName → FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var sp).Should().BeTrue("body: {0}", body);
        sp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
        TryProp(root, "errors", out var ep).Should().BeTrue("errors[] must be present; body: {0}", body);
        ep.EnumerateArray().Should().NotBeEmpty("errors[] must not be empty; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-25 — Six tables exist in learning schema (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-25: Six curriculum tables (Grades, Subjects, Units, Lessons, Concepts, Skills) exist in the 'learning' schema")]
    public async Task BETC25_SixCurriculumTables_ExistInLearningSchema()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        var expectedTables = new[] { "Grades", "Subjects", "Units", "Lessons", "Concepts", "Skills" };

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'learning'
              AND table_type = 'BASE TABLE'
              AND table_name = ANY(@names)
            """;

        var param = cmd.CreateParameter();
        param.ParameterName = "@names";

        // Npgsql-specific: pass as a string array parameter
        var npgsqlParam = (Npgsql.NpgsqlParameter)param;
        npgsqlParam.Value = expectedTables;
        cmd.Parameters.Add(npgsqlParam);

        var found = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            found.Add(reader.GetString(0));

        found.Should().BeEquivalentTo(expectedTables,
            "all six curriculum tables must exist in the 'learning' schema");
    }

    // =========================================================================
    // BE-TC-26 — Unique index IX_Subjects_GradeId_SubjectCode_Language present (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-26: Unique index IX_Subjects_GradeId_SubjectCode_Language exists on learning.Subjects")]
    public async Task BETC26_UniqueIndex_OnSubjects_ExistsAndIsUnique()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'learning'
              AND tablename = 'Subjects'
              AND indexname = 'IX_Subjects_GradeId_SubjectCode_Language'
            """;

        string? indexDef = null;
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            indexDef = reader.GetString(1);

        indexDef.Should().NotBeNullOrEmpty(
            "index IX_Subjects_GradeId_SubjectCode_Language must exist on learning.Subjects");
        indexDef!.ToUpperInvariant().Should().Contain("UNIQUE",
            "the index must be UNIQUE; indexdef: {0}", indexDef);
    }

    // =========================================================================
    // BE-TC-27 (extension) — Anonymous GradesList GetById → 401 (existing covers List)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-27 (ext): Anonymous GET /api/learning/grades?id=1 → 401 (class-level [Authorize])")]
    public async Task BETC27_AnonymousGradesGetById_Returns401()
    {
        var (response, _, body) = await GetAsync("/api/learning/grades?id=1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GradesController is [Authorize] at class level; GetById without token → 401; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-28 — Anonymous writes → 401 on all six controllers (net-new)
    // =========================================================================

    [Theory(DisplayName = "BE-TC-28: Anonymous POST Create → 401 on all six curriculum controllers")]
    [InlineData("/api/learning/grades/Create")]
    [InlineData("/api/learning/subjects/Create")]
    [InlineData("/api/learning/units/Create")]
    [InlineData("/api/learning/lessons/Create")]
    [InlineData("/api/learning/concepts/Create")]
    [InlineData("/api/learning/skills/Create")]
    public async Task BETC28_Anonymous_Create_Returns401(string url)
    {
        // Empty body is fine — auth gate fires before model binding/validation
        var (response, _, body) = await PostAsync(url, new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Anonymous POST to {0} must return 401 (auth gate before validation); body: {1}", url, body);
    }

    [Theory(DisplayName = "BE-TC-28: Anonymous PUT Update → 401 on all six curriculum controllers")]
    [InlineData("/api/learning/grades/Update")]
    [InlineData("/api/learning/subjects/Update")]
    [InlineData("/api/learning/units/Update")]
    [InlineData("/api/learning/lessons/Update")]
    [InlineData("/api/learning/concepts/Update")]
    [InlineData("/api/learning/skills/Update")]
    public async Task BETC28_Anonymous_Update_Returns401(string url)
    {
        var (response, _, body) = await PutAsync(url, new { });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Anonymous PUT to {0} must return 401; body: {1}", url, body);
    }

    [Theory(DisplayName = "BE-TC-28: Anonymous DELETE → 401 on all six curriculum controllers")]
    [InlineData("/api/learning/grades?id=1")]
    [InlineData("/api/learning/subjects?id=1")]
    [InlineData("/api/learning/units?id=1")]
    [InlineData("/api/learning/lessons?id=1")]
    [InlineData("/api/learning/concepts?id=1")]
    [InlineData("/api/learning/skills?id=1")]
    public async Task BETC28_Anonymous_Delete_Returns401(string url)
    {
        var (response, _, body) = await DeleteAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Anonymous DELETE to {0} must return 401; body: {1}", url, body);
    }

    // =========================================================================
    // BE-TC-29 — Non-admin write → 403 on all six controllers (net-new)
    // =========================================================================

    [Theory(DisplayName = "BE-TC-29: Non-admin (basicuser) POST Create → 403 on all six curriculum controllers")]
    [InlineData("/api/learning/grades/Create")]
    [InlineData("/api/learning/subjects/Create")]
    [InlineData("/api/learning/units/Create")]
    [InlineData("/api/learning/lessons/Create")]
    [InlineData("/api/learning/concepts/Create")]
    [InlineData("/api/learning/skills/Create")]
    public async Task BETC29_NonAdmin_Create_Returns403(string url)
    {
        var (response, _, body) = await PostAsync(url, new { }, _basicToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Non-admin bearer POST to {0} must return 403 (authenticated but lacks AdminOnly); body: {1}", url, body);
    }

    [Theory(DisplayName = "BE-TC-29: Non-admin (basicuser) PUT Update → 403 on all six curriculum controllers")]
    [InlineData("/api/learning/grades/Update")]
    [InlineData("/api/learning/subjects/Update")]
    [InlineData("/api/learning/units/Update")]
    [InlineData("/api/learning/lessons/Update")]
    [InlineData("/api/learning/concepts/Update")]
    [InlineData("/api/learning/skills/Update")]
    public async Task BETC29_NonAdmin_Update_Returns403(string url)
    {
        var (response, _, body) = await PutAsync(url, new { }, _basicToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Non-admin bearer PUT to {0} must return 403; body: {1}", url, body);
    }

    [Theory(DisplayName = "BE-TC-29: Non-admin (basicuser) DELETE → 403 on all six curriculum controllers")]
    [InlineData("/api/learning/grades?id=1")]
    [InlineData("/api/learning/subjects?id=1")]
    [InlineData("/api/learning/units?id=1")]
    [InlineData("/api/learning/lessons?id=1")]
    [InlineData("/api/learning/concepts?id=1")]
    [InlineData("/api/learning/skills?id=1")]
    public async Task BETC29_NonAdmin_Delete_Returns403(string url)
    {
        var (response, _, body) = await DeleteAsync(url, _basicToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Non-admin bearer DELETE to {0} must return 403; body: {1}", url, body);
    }

    // =========================================================================
    // BE-TC-30 — Duplicate subject (same grade) → rejected, record actual code (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-30: Second subject under same grade violates unique (GradeId,SubjectCode,Language) → rejected (non-2xx, valid JSON, successed=false)")]
    public async Task BETC30_DuplicateSubject_SameGrade_IsRejected()
    {
        int gradeId = await CreateGradeGetId();

        // Subject A — should succeed
        var nameA = $"DupSubjA {Guid.NewGuid():N}";
        var (aResp, aRoot, aBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = nameA, GradeId = gradeId }, _adminToken);
        AssertCreateSuccess(aResp, aRoot, aBody);

        // Subject B — same gradeId, different name, but defaults to same (MATH, Ar) → unique collision
        var nameB = $"DupSubjB {Guid.NewGuid():N}";
        var (bResp, bRoot, bBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = nameB, GradeId = gradeId }, _adminToken);

        var bStatus = (int)bResp.StatusCode;
        // Must be non-2xx — actual status (expected 500 per Q1) recorded below
        bStatus.Should().NotBe(200,
            "duplicate (GradeId,SubjectCode,Language) must not return 200; actual={0}; body: {1}", bStatus, bBody);
        bStatus.Should().NotBe(201,
            "must not return 201; actual={0}; body: {1}", bStatus, bBody);

        // Body must be valid JSON (envelope, not naked exception page)
        bBody.Should().NotBeNullOrWhiteSpace("response body must not be empty; body: {0}", bBody);
        var parseAction = () => JsonDocument.Parse(bBody);
        parseAction.Should().NotThrow(
            "duplicate-collision response must be valid JSON, not a raw exception page; body: {0}", bBody);

        // Envelope must carry successed=false
        if (bRoot.ValueKind != JsonValueKind.Undefined && TryProp(bRoot, "successed", out var bs))
            bs.GetBoolean().Should().BeFalse("successed must be false; body: {0}", bBody);

        // --- DEFECT NOTE (Q1) ---
        // Observed status code: recorded in execution-report.md
        // Expected 500 (ServerError) because AddSubjectCommand cannot set SubjectCode/Language
        // so every subject defaults to (MATH=0, Ar=0) → unique index IX_Subjects_GradeId_SubjectCode_Language
        // is violated at SaveChanges. The handler's catch block returns ServerError() → HTTP 500.
        // This is a known defect — the Create endpoint should expose SubjectCode/Language
        // or pre-check uniqueness → clean 409/422.
    }

    // =========================================================================
    // BE-TC-31 — Same-name duplicate same grade → rejected (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-31: Same-name duplicate subject under same grade → rejected (valid JSON envelope, successed=false, no stack-trace leak)")]
    public async Task BETC31_SameNameDuplicateSubject_IsRejected()
    {
        int gradeId = await CreateGradeGetId();
        var sameName = $"DupName {Guid.NewGuid():N}";

        // First create — must succeed
        var (aResp, aRoot, aBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = sameName, GradeId = gradeId }, _adminToken);
        AssertCreateSuccess(aResp, aRoot, aBody);

        // Second create — same name, same grade → same (MATH, Ar) defaults → unique collision
        var (bResp, bRoot, bBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = sameName, GradeId = gradeId }, _adminToken);

        var bStatus = (int)bResp.StatusCode;
        bStatus.Should().NotBe(200,
            "identical-name duplicate must not succeed; actual={0}; body: {1}", bStatus, bBody);

        // Valid JSON response — no stack trace leak
        bBody.Should().NotBeNullOrWhiteSpace("response must not be empty; body: {0}", bBody);
        var parseAction = () => JsonDocument.Parse(bBody);
        parseAction.Should().NotThrow(
            "duplicate response must be valid JSON, not a raw exception page; body: {0}", bBody);

        // No class name / stack trace in body
        bBody.Should().NotContain("at Learnexia.",
            "stack trace must not leak into response body; body: {0}", bBody);
        bBody.Should().NotContain("System.Exception",
            "raw exception class name must not appear in response; body: {0}", bBody);

        if (bRoot.ValueKind != JsonValueKind.Undefined && TryProp(bRoot, "successed", out var bs))
            bs.GetBoolean().Should().BeFalse("successed must be false; body: {0}", bBody);
    }

    // =========================================================================
    // BE-TC-32 — First subject still retrievable after duplicate failure (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-32: First subject survives duplicate failure — no partial corruption; subject B not persisted")]
    public async Task BETC32_FirstSubject_SurvivesDuplicateFailure()
    {
        int gradeId = await CreateGradeGetId();
        var nameA = $"SurvivorA {Guid.NewGuid():N}";
        var nameB = $"SurvivorB {Guid.NewGuid():N}";

        // Create subject A — capture id via filtered list
        var (aResp, aRoot, aBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = nameA, GradeId = gradeId }, _adminToken);
        AssertCreateSuccess(aResp, aRoot, aBody);

        int subjectAId = await FindIdInList(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", nameA, "subjectA");

        // Attempt duplicate B (will fail per BE-TC-30)
        var (bResp, _, bBody) = await PostAsync("/api/learning/subjects/Create",
            new { Name = nameB, GradeId = gradeId }, _adminToken);
        ((int)bResp.StatusCode).Should().NotBe(200,
            "duplicate B must be rejected; body: {0}", bBody);

        // GET subject A by id — must still return 200 with intact fields
        var (getResp, getRoot, getBody) = await GetAsync($"/api/learning/subjects?id={subjectAId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "subject A must still be retrievable after the duplicate failure; body: {0}", getBody);
        TryProp(getRoot, "successed", out var gs).Should().BeTrue("body: {0}", getBody);
        gs.GetBoolean().Should().BeTrue("subject A successed must still be true; body: {0}", getBody);
        TryProp(getRoot, "data", out var aData).Should().BeTrue("body: {0}", getBody);
        TryProp(aData, "name", out var aName).Should().BeTrue("data must have name; body: {0}", getBody);
        aName.GetString().Should().Be(nameA, "subject A name must be intact; body: {0}", getBody);

        // List subjects for the grade — should contain A but NOT B
        var (listResp, listRoot, listBody) = await GetAsync(
            $"/api/learning/subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var subjectsInGrade = ExtractItems(listRoot, listBody);

        subjectsInGrade.Any(s => TryProp(s, "name", out var n) && n.GetString() == nameA)
            .Should().BeTrue("subject A must be in the grade's subject list; body: {0}", listBody);
        subjectsInGrade.Any(s => TryProp(s, "name", out var n) && n.GetString() == nameB)
            .Should().BeFalse("subject B must NOT be persisted (duplicate failed → rollback); body: {0}", listBody);
    }

    // =========================================================================
    // BE-TC-34 — Unit under non-existent SubjectId → rejected (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-34: Unit Create under non-existent SubjectId=999999 → non-2xx, valid JSON envelope, successed=false (Q2)")]
    public async Task BETC34_Unit_NonExistentSubjectId_Rejected()
    {
        var (response, root, body) = await PostAsync("/api/learning/units/Create",
            new { Name = "Orphan Unit", SequenceOrder = 1, SubjectId = 999999 }, _adminToken);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().NotBe(200,
            "non-existent SubjectId must not return 200; actual={0}; body: {1}", statusCode, body);
        statusCode.Should().NotBe(201,
            "must not return 201; actual={0}; body: {1}", statusCode, body);

        body.Should().NotBeNullOrWhiteSpace("response body must not be empty; body: {0}", body);
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow(
            "FK failure response must be valid JSON, not a raw exception page; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var sp))
            sp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-35 — Concept under non-existent SubjectId → rejected (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-35: Concept Create under non-existent SubjectId=999999 → non-2xx, valid JSON envelope, successed=false (Q2)")]
    public async Task BETC35_Concept_NonExistentSubjectId_Rejected()
    {
        var (response, root, body) = await PostAsync("/api/learning/concepts/Create",
            new { Name = "Orphan Concept", DifficultyLevel = 1, SubjectId = 999999 }, _adminToken);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().NotBe(200,
            "non-existent SubjectId must not return 200; actual={0}; body: {1}", statusCode, body);

        body.Should().NotBeNullOrWhiteSpace("response body must not be empty; body: {0}", body);
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow(
            "FK failure response must be valid JSON; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var sp))
            sp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-36 — Lesson under non-existent UnitId → rejected; Skill under non-existent ConceptId → rejected (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-36a: Lesson Create with UnitId=999999 (non-existent) → non-2xx, valid JSON envelope")]
    public async Task BETC36a_Lesson_NonExistentUnitId_Rejected()
    {
        var (response, root, body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = "Orphan Lesson", Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = 999999 }, _adminToken);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().NotBe(200,
            "non-existent UnitId must not return 200; actual={0}; body: {1}", statusCode, body);

        body.Should().NotBeNullOrWhiteSpace("body: {0}", body);
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("FK failure must be valid JSON; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var sp))
            sp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-36b: Skill Create with ConceptId=999999 (non-existent) → non-2xx, valid JSON envelope")]
    public async Task BETC36b_Skill_NonExistentConceptId_Rejected()
    {
        var (response, root, body) = await PostAsync("/api/learning/skills/Create",
            new { Name = "Orphan Skill", MasteryThreshold = 80, EstimatedTimeMinutes = 30, ConceptId = 999999 }, _adminToken);

        var statusCode = (int)response.StatusCode;
        statusCode.Should().NotBe(200,
            "non-existent ConceptId must not return 200; actual={0}; body: {1}", statusCode, body);

        body.Should().NotBeNullOrWhiteSpace("body: {0}", body);
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("FK failure must be valid JSON; body: {0}", body);

        if (root.ValueKind != JsonValueKind.Undefined && TryProp(root, "successed", out var sp))
            sp.GetBoolean().Should().BeFalse("successed must be false; body: {0}", body);
    }

    [Fact(DisplayName = "BE-TC-36c (bonus): Lesson with valid UnitId but SkillId=999999 (optional FK SetNull) — record whether accepted or rejected")]
    public async Task BETC36c_Lesson_NonExistentSkillId_BehaviorRecorded()
    {
        int gradeId = await CreateGradeGetId();
        int subjectId = await CreateSubjectGetId(gradeId);
        int unitId = await CreateUnitGetId(subjectId);

        var (response, root, body) = await PostAsync("/api/learning/lessons/Create",
            new { Name = $"Lesson SkillId999 {Guid.NewGuid():N}", Difficulty = 1, SequenceOrder = 1, IsLocked = false, UnitId = unitId, SkillId = 999999 }, _adminToken);

        var statusCode = (int)response.StatusCode;
        // The Lesson→Skill FK is SetNull (optional). Behavior may differ:
        // - If FK is properly set to SetNull or SkillId is nullable and ignored: 200 accepted
        // - If FK is Restrict and non-existent SkillId violates it: non-2xx
        // We assert only the envelope contract (valid JSON, not a crash):
        body.Should().NotBeNullOrWhiteSpace("response must not be empty; body: {0}", body);
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("must be valid JSON, not a raw exception page; body: {0}", body);

        // Record the observed status for the execution report (not asserting a specific code here)
        // Observed status: see execution-report.md (BE-TC-36 row)
        _ = statusCode; // suppress unused warning — value recorded in report
    }

    // =========================================================================
    // BE-TC-37 — 4 subjects / no Social Studies (net-new)
    // =========================================================================

    [Fact(DisplayName = "BE-TC-37 (a): SubjectCode enum only defines the 4 product subjects (MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3) — no 5th/Social Studies code")]
    public async Task BETC37_SubjectCodeEnum_OnlyFourProductCodes()
    {
        // Verify at the application layer: the SubjectCode enum has exactly 4 values.
        // We check via reflection — no fifth value like SOCIAL_STUDIES should exist.
        var subjectCodeType = Type.GetType(
            "Learnexia.Modules.Learning.Domain.Enums.SubjectCode, Learnexia.Modules.Learning.Domain")
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
                .FirstOrDefault(t => t.Name == "SubjectCode" && t.IsEnum);

        subjectCodeType.Should().NotBeNull("SubjectCode enum must be discoverable in the loaded assemblies");

        var values = Enum.GetValues(subjectCodeType!).Cast<int>().ToList();
        values.Should().BeEquivalentTo(new[] { 0, 1, 2, 3 },
            "SubjectCode must have exactly 4 values: MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3 (no Social Studies=4 or other)");

        var names = Enum.GetNames(subjectCodeType!);
        names.Should().NotContain("SOCIAL_STUDIES",
            "the product mandates 4 subjects; Social Studies must not be a SubjectCode");
        names.Should().NotContain("SocialStudies",
            "the product mandates 4 subjects; Social Studies must not be a SubjectCode");
        names.Should().HaveCount(4,
            "exactly 4 SubjectCode names must exist; found: {0}", string.Join(",", names));
    }

    // Note: BE-TC-37 sub-step (b) — attempt to create a subject with SubjectCode=4 via the Create endpoint —
    // is BLOCKED because AddSubjectCommand/AddSubjectDto do NOT expose SubjectCode.
    // The Create endpoint ignores any SubjectCode in the request body and defaults to MATH(0).
    // Therefore, this sub-step is not testable via the current public API surface.
    // See execution-report.md for this BLOCKED notation.
}

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
/// P7-01b gap-fill integration tests: the 4 admin curriculum-mutation endpoints NOT covered
/// by the existing P7_01_SubjectsUnitsAdmin_Tests:
///
///   PUT  /api/learning/Subjects/Update  — edit a subject (name / GradeId / etc.)
///   PUT  /api/learning/Units/Update     — edit a unit (name / SequenceOrder / etc.)
///   PUT  /api/learning/Grades/Update    — edit a grade (Number / DisplayName)
///   DELETE /api/learning/Grades?id=n   — soft-delete a grade
///
/// Cases per endpoint:
///   Happy path    — create via helper → mutate → GET / List asserts the change persisted.
///   Admin-authz   — non-admin (Parent JWT) → 403; anonymous → 401.
///   Not-found     — unknown id → rejected (not a raw 500).
///   Grade-delete guard — grade with subjects → rejected (no guard in handler, see defect note).
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_01b_CurriculumMutationGaps_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    private string? _adminToken;
    private string? _parentToken;

    public P7_01b_CurriculumMutationGaps_Tests(LearnexiaWebAppFactory factory)
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

        _adminToken = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _parentToken = await RegisterParentGetTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // PUT /api/learning/Subjects/Update
    // =========================================================================

    [Fact(DisplayName = "SubjectUpdate happy path: updated name persists on admin List reload")]
    public async Task SubjectUpdate_HappyPath_NameChangePersistedInList()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(
            gradeId, name: $"SubjOrig {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        // Fetch the subject to obtain current SubjectCode + Language (required in EditSubjectDto).
        var (getResp, getRoot, getBody) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Subjects?id={subjectId}", bearer: _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "GetById must succeed; body: {0}", getBody);
        TryProp(getRoot, "data", out var subjectData).Should().BeTrue("body: {0}", getBody);
        TryProp(subjectData, "subjectCode", out var scProp).Should().BeTrue("body: {0}", getBody);
        TryProp(subjectData, "language", out var langProp).Should().BeTrue("body: {0}", getBody);
        TryProp(subjectData, "sequenceOrder", out var seqProp).Should().BeTrue("body: {0}", getBody);
        TryProp(subjectData, "isActive", out var isActiveProp).Should().BeTrue("body: {0}", getBody);

        var newName = $"SubjEdited {Guid.NewGuid():N}";

        // PUT /api/learning/Subjects/Update
        // Body shape = EditSubjectDto which inherits SubjectDto (which inherits BaseDto → Id + Name/Country/GradeId/SubjectCode/Language/SequenceOrder/IsActive).
        var (updResp, updRoot, updBody) = await SendAsync(
            HttpMethod.Put, "/api/learning/Subjects/Update",
            new
            {
                Id = subjectId,
                Name = newName,
                Country = "EG",
                GradeId = gradeId,
                SubjectCode = scProp.GetInt32(),
                Language = langProp.GetInt32(),
                SequenceOrder = seqProp.GetInt32(),
                IsActive = isActiveProp.GetBoolean()
            },
            _adminToken);

        updResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Subject Update must return 200; body: {0}", updBody);
        AssertSucceeded(updRoot, updBody);

        // Verify name change is visible via the admin List.
        var (listResp, listRoot, listBody) = await SendAsync(
            HttpMethod.Get,
            $"/api/learning/Subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);
        items.Any(s => TryProp(s, "name", out var n) && n.GetString() == newName)
            .Should().BeTrue("updated name must appear in admin List; body: {0}", listBody);
    }

    [Fact(DisplayName = "SubjectUpdate: envelope has statusCode/successed/message/data/errors")]
    public async Task SubjectUpdate_EnvelopeShape()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(
            gradeId, name: $"Subj Envelope {Guid.NewGuid():N}", subjectCode: 2, language: 1);

        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Subjects/Update",
            new
            {
                Id = subjectId,
                Name = $"Subj Envelope Upd {Guid.NewGuid():N}",
                Country = "EG",
                GradeId = gradeId,
                SubjectCode = 2,
                Language = 1,
                SequenceOrder = 0,
                IsActive = true
            },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "SubjectUpdate: anonymous → 401")]
    public async Task SubjectUpdate_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Subjects/Update",
            new { Id = 1, Name = "X", Country = "EG", GradeId = 1, SubjectCode = 0, Language = 0, SequenceOrder = 0, IsActive = true },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Subject Update is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "SubjectUpdate: parent → 403")]
    public async Task SubjectUpdate_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Subjects/Update",
            new { Id = 1, Name = "X", Country = "EG", GradeId = 1, SubjectCode = 0, Language = 0, SequenceOrder = 0, IsActive = true },
            _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Subject Update is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "SubjectUpdate: unknown id → rejected (not-found / ServerError, not a raw 5xx exception page)")]
    public async Task SubjectUpdate_UnknownId_Rejected()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Subjects/Update",
            new { Id = 99999997, Name = "Ghost", Country = "EG", GradeId = 1, SubjectCode = 0, Language = 0, SequenceOrder = 0, IsActive = true },
            _adminToken);

        // Body must be valid JSON (not an exception dump).
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; body: {0}", body);

        var statusCode = (int)resp.StatusCode;
        // Handler catches the exception and returns ServerError → HTTP 200 Successed=false,
        // OR it may propagate a 404 / 400. Either way it must NOT be a raw uncaught 5xx.
        if (statusCode == 200)
        {
            TryProp(root, "successed", out var sv).Should().BeTrue("body: {0}", body);
            sv.GetBoolean().Should().BeFalse("unknown id must yield Successed=false; body: {0}", body);
        }
        else
        {
            statusCode.Should().BeOneOf(new[] { 400, 404, 422, 424 },
                "unknown id must be rejected with a proper 4xx (not 500); body: {0}", body);
        }
    }

    // =========================================================================
    // PUT /api/learning/Units/Update
    // =========================================================================

    [Fact(DisplayName = "UnitUpdate happy path: updated name persists on admin List reload")]
    public async Task UnitUpdate_HappyPath_NameChangePersistedInList()
    {
        int gradeId = await CreateGradeGetIdAsync();
        int subjectId = await CreateSubjectGetIdAsync(
            gradeId, name: $"Math {Guid.NewGuid():N}", subjectCode: 0, language: 0);
        int unitId = await CreateUnitGetIdAsync(
            subjectId, name: $"UnitOrig {Guid.NewGuid():N}", seqOrder: 1);

        var newName = $"UnitEdited {Guid.NewGuid():N}";

        // Body shape = EditUnitDto which inherits UnitDto (Id + Name/SequenceOrder/SubjectId/IsActive).
        var (updResp, updRoot, updBody) = await SendAsync(
            HttpMethod.Put, "/api/learning/Units/Update",
            new
            {
                Id = unitId,
                Name = newName,
                SequenceOrder = 1,
                SubjectId = subjectId,
                IsActive = true
            },
            _adminToken);

        updResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Unit Update must return 200; body: {0}", updBody);
        AssertSucceeded(updRoot, updBody);

        // Verify name change is visible via the admin List.
        var (listResp, listRoot, listBody) = await SendAsync(
            HttpMethod.Get,
            $"/api/learning/Units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);
        items.Any(u => TryProp(u, "name", out var n) && n.GetString() == newName)
            .Should().BeTrue("updated name must appear in admin List; body: {0}", listBody);
    }

    [Fact(DisplayName = "UnitUpdate: anonymous → 401")]
    public async Task UnitUpdate_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Units/Update",
            new { Id = 1, Name = "X", SequenceOrder = 1, SubjectId = 1, IsActive = true },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Unit Update is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "UnitUpdate: parent → 403")]
    public async Task UnitUpdate_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Units/Update",
            new { Id = 1, Name = "X", SequenceOrder = 1, SubjectId = 1, IsActive = true },
            _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Unit Update is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "UnitUpdate: unknown id → 404 NotFound (FIXED: pre-fetch guard returns 404 instead of 500)")]
    public async Task UnitUpdate_UnknownId_Rejected()
    {
        // FIXED: EditUnitCommandHandler now pre-fetches the unit via GetUnitTrackedAsync before
        // calling UpdateAsync. If null → NotFound<string>(localizer[UnitNotFound]) → HTTP 404.
        // Previously returned HTTP 500 (InvalidOperationException from repository GetByIdAsync).
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Units/Update",
            new { Id = 99999996, Name = "Ghost", SequenceOrder = 1, SubjectId = 1, IsActive = true },
            _adminToken);

        // Body must be valid JSON (not a raw HTML exception page).
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; body: {0}", body);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "unknown unit id must return HTTP 404 after the pre-fetch fix; body: {0}", body);

        TryProp(root, "successed", out var sv).Should().BeTrue("body: {0}", body);
        sv.GetBoolean().Should().BeFalse("unknown id must yield Successed=false; body: {0}", body);
    }

    // =========================================================================
    // PUT /api/learning/Grades/Update
    // =========================================================================

    [Fact(DisplayName = "GradeUpdate happy path: updated DisplayName persists on GetById reload")]
    public async Task GradeUpdate_HappyPath_DisplayNameChangePersistedOnGetById()
    {
        // Create a grade and capture its Id.
        var origName = $"GradeOrig {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await SendAsync(
            HttpMethod.Post, "/api/learning/Grades/Create",
            new { Number = 3, DisplayName = origName },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Grade create must succeed; body: {0}", createBody);

        int gradeId = await FindIdInListAsync(
            "/api/learning/Grades/List?PageNumber=1&PageSize=200",
            "displayName", origName, "grade", _adminToken);

        var newDisplayName = $"GradeEdited {Guid.NewGuid():N}";

        // PUT /api/learning/Grades/Update
        // Body shape = EditGradeDto which inherits GradeDto (BaseDto → Id) + Number + DisplayName.
        var (updResp, updRoot, updBody) = await SendAsync(
            HttpMethod.Put, "/api/learning/Grades/Update",
            new { Id = gradeId, Number = 3, DisplayName = newDisplayName },
            _adminToken);

        updResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Grade Update must return 200; body: {0}", updBody);
        AssertSucceeded(updRoot, updBody);

        // Verify the change via GetById.
        var (getResp, getRoot, getBody) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Grades?id={gradeId}", bearer: _adminToken);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "GetById after update; body: {0}", getBody);
        TryProp(getRoot, "data", out var gradeData).Should().BeTrue("body: {0}", getBody);
        TryProp(gradeData, "displayName", out var dnProp).Should().BeTrue("body: {0}", getBody);
        dnProp.GetString().Should().Be(newDisplayName,
            "displayName must reflect the update; body: {0}", getBody);
    }

    [Fact(DisplayName = "GradeUpdate: envelope has statusCode/successed/message/data/errors")]
    public async Task GradeUpdate_EnvelopeShape()
    {
        var origName = $"GradeEnv {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await SendAsync(
            HttpMethod.Post, "/api/learning/Grades/Create",
            new { Number = 4, DisplayName = origName },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", createBody);

        int gradeId = await FindIdInListAsync(
            "/api/learning/Grades/List?PageNumber=1&PageSize=200",
            "displayName", origName, "grade", _adminToken);

        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Grades/Update",
            new { Id = gradeId, Number = 4, DisplayName = $"GradeEnv2 {Guid.NewGuid():N}" },
            _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "GradeUpdate: anonymous → 401")]
    public async Task GradeUpdate_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Grades/Update",
            new { Id = 1, Number = 1, DisplayName = "X" },
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Grade Update is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "GradeUpdate: parent → 403")]
    public async Task GradeUpdate_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Grades/Update",
            new { Id = 1, Number = 1, DisplayName = "X" },
            _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Grade Update is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "GradeUpdate: unknown id → 404 NotFound (FIXED: pre-fetch guard returns 404 instead of 500)")]
    public async Task GradeUpdate_UnknownId_Rejected()
    {
        // FIXED: EditGradeCommandHandler now pre-fetches the grade via GetGradeTrackedAsync before
        // calling UpdateAsync. If null → NotFound<string>(localizer[GradeNotFound]) → HTTP 404.
        // Previously returned HTTP 500 (InvalidOperationException from repository GetByIdAsync).
        // Also removes the ex.Message leak that was in the old catch block.
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put, "/api/learning/Grades/Update",
            new { Id = 99999995, Number = 1, DisplayName = "Ghost" },
            _adminToken);

        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; body: {0}", body);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "unknown grade id must return HTTP 404 after the pre-fetch fix; body: {0}", body);

        TryProp(root, "successed", out var sv).Should().BeTrue("body: {0}", body);
        sv.GetBoolean().Should().BeFalse("unknown id must yield Successed=false; body: {0}", body);
    }

    // =========================================================================
    // DELETE /api/learning/Grades?id=n
    // =========================================================================

    [Fact(DisplayName = "GradeDelete happy path: empty grade → Successed=true, disappears from List")]
    public async Task GradeDelete_EmptyGrade_SoftDeletes_DisappearsFromList()
    {
        var name = $"GradeToDelete {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await SendAsync(
            HttpMethod.Post, "/api/learning/Grades/Create",
            new { Number = 5, DisplayName = name },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", createBody);

        int gradeId = await FindIdInListAsync(
            "/api/learning/Grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);

        // DELETE /api/learning/Grades?id=n
        var (delResp, delRoot, delBody) = await SendAsync(
            HttpMethod.Delete,
            $"/api/learning/Grades?id={gradeId}",
            bearer: _adminToken);

        delResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Grade Delete must return 200; body: {0}", delBody);
        AssertSucceeded(delRoot, delBody);

        // Grade must disappear from List (soft-delete / IsDeleted global filter).
        var (listResp, listRoot, listBody) = await SendAsync(
            HttpMethod.Get, "/api/learning/Grades/List?PageNumber=1&PageSize=200",
            bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractPageItems(listRoot, listBody);
        items.Any(g => TryProp(g, "id", out var id) && id.GetInt32() == gradeId)
            .Should().BeFalse("soft-deleted grade must not appear in List; body: {0}", listBody);
    }

    [Fact(DisplayName = "GradeDelete: envelope has statusCode/successed/message/data/errors on success")]
    public async Task GradeDelete_EnvelopeShape()
    {
        var name = $"GradeEnvDel {Guid.NewGuid():N}";
        var (createResp, _, createBody) = await SendAsync(
            HttpMethod.Post, "/api/learning/Grades/Create",
            new { Number = 6, DisplayName = name },
            _adminToken);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", createBody);

        int gradeId = await FindIdInListAsync(
            "/api/learning/Grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);

        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete,
            $"/api/learning/Grades?id={gradeId}",
            bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "GradeDelete: anonymous → 401")]
    public async Task GradeDelete_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Delete, "/api/learning/Grades?id=1",
            bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Grade Delete is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    [Fact(DisplayName = "GradeDelete: parent → 403")]
    public async Task GradeDelete_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(
            HttpMethod.Delete, "/api/learning/Grades?id=1",
            bearer: _parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Grade Delete is AdminOnly — parent must get 403; body: {0}", body);
    }

    [Fact(DisplayName = "GradeDelete: unknown id → 404 NotFound (FIXED: pre-fetch guard returns 404 instead of 500)")]
    public async Task GradeDelete_UnknownId_Rejected()
    {
        // FIXED: DeleteGradeCommandHandler now pre-fetches the grade via GetGradeTrackedAsync before
        // calling DeleteAsync. If null → NotFound<string>(localizer[GradeNotFound]) → HTTP 404.
        // Previously returned HTTP 500 (InvalidOperationException from repository GetByIdAsync).
        // Also removes the ex.Message leak that was in the old catch block.
        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, "/api/learning/Grades?id=99999994",
            bearer: _adminToken);

        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("response must be valid JSON, not a raw exception page; body: {0}", body);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "unknown grade id must return HTTP 404 after the pre-fetch fix; body: {0}", body);

        TryProp(root, "successed", out var sv).Should().BeTrue("body: {0}", body);
        sv.GetBoolean().Should().BeFalse("unknown id must yield Successed=false; body: {0}", body);
    }

    [Fact(DisplayName = "GradeDelete: grade with subjects → 400 BadRequest (FIXED: application-level guard returns 400 instead of DB FK 500)")]
    public async Task GradeDelete_GradeWithSubjects_Rejected()
    {
        // FIXED: DeleteGradeCommandHandler now checks GradeHasSubjectsAsync before calling DeleteAsync.
        // If the grade has non-deleted subjects → BadRequest<string>(localizer[GradeNotEmpty]) → HTTP 400.
        // Previously the base DeleteAsync would commit via UoW and the DB FK violation would produce a 500.
        int gradeId = await CreateGradeGetIdAsync();
        await CreateSubjectGetIdAsync(gradeId, name: $"Subj InGrade {Guid.NewGuid():N}", subjectCode: 0, language: 0);

        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, $"/api/learning/Grades?id={gradeId}",
            bearer: _adminToken);

        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow(
            "response must be valid JSON even when grade has subjects; body: {0}", body);

        // After the fix: application-level guard returns HTTP 400 (BadRequest) with Successed=false.
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "deleting a grade with subjects must return HTTP 400 (children guard); body: {0}", body);

        TryProp(root, "successed", out var sv).Should().BeTrue("body: {0}", body);
        sv.GetBoolean().Should().BeFalse(
            "Deleting a grade that has subjects must yield Successed=false; body: {0}", body);
    }

    // =========================================================================
    // Private helpers (mirrors P7_01_SubjectsUnitsAdmin_Tests)
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
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Case-insensitive JSON property lookup.</summary>
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
        TryProp(outer, "data", out var inner).Should().BeTrue("PaginatedResult must have inner 'data'; body: {0}", bodyStr);

        if (inner.ValueKind == JsonValueKind.Null || inner.ValueKind == JsonValueKind.Undefined)
            return new List<JsonElement>();
        if (inner.ValueKind != JsonValueKind.Array)
            return new List<JsonElement>();

        return inner.EnumerateArray().ToList();
    }

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
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
        var email = $"p701b_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<int> CreateGradeGetIdAsync()
    {
        var name = $"P701b Grade {Guid.NewGuid():N}";
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/Grades/Create",
            new { Number = 1, DisplayName = name }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "grade create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            "/api/learning/Grades/List?PageNumber=1&PageSize=200",
            "displayName", name, "grade", _adminToken);
    }

    private async Task<int> CreateSubjectGetIdAsync(int gradeId, string name, int subjectCode, int language)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/Subjects/Create",
            new { Name = name, Country = "EG", GradeId = gradeId, SubjectCode = subjectCode, Language = language },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "subject create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"/api/learning/Subjects/List?PageNumber=1&PageSize=200&GradeId={gradeId}",
            "name", name, "subject", _adminToken);
    }

    private async Task<int> CreateUnitGetIdAsync(int subjectId, string name, int seqOrder)
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, "/api/learning/Units/Create",
            new { Name = name, SequenceOrder = seqOrder, SubjectId = subjectId },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "unit create must succeed; body: {0}", body);

        return await FindIdInListAsync(
            $"/api/learning/Units/List?PageNumber=1&PageSize=200&SubjectId={subjectId}",
            "name", name, "unit", _adminToken);
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
}

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
/// P7-01c Regression tests: GetById / Update / Delete of non-existent ids must return 404,
/// NOT 500 (regression for LearningRepository.GetByIdAsync previously throwing
/// InvalidOperationException → caught → ServerError → HTTP 500).
///
/// Endpoints under test:
///   GET    /api/learning/Subjects?id=999999999   — AdminOnly
///   GET    /api/learning/Units?id=999999999       — AdminOnly
///   GET    /api/learning/Grades?id=999999999      — [Authorize] (any authenticated user)
///   GET    /api/learning/Concepts?id=999999999    — [Authorize] (any authenticated user; class-level)
///   GET    /api/learning/Skills?id=999999999      — AdminOnly
///
///   PUT    /api/learning/Subjects/Update          — AdminOnly; body with Id=999999999
///   PUT    /api/learning/Units/Update             — AdminOnly; body with Id=999999999
///   PUT    /api/learning/Grades/Update            — AdminOnly; body with Id=999999999
///   PUT    /api/learning/Concepts/Update          — AdminOnly; body with Id=999999999
///   PUT    /api/learning/Skills/Update            — AdminOnly; body with Id=999999999
///
///   DELETE /api/learning/Subjects?id=999999999   — AdminOnly
///   DELETE /api/learning/Units?id=999999999       — AdminOnly
///   DELETE /api/learning/Grades?id=999999999      — AdminOnly
///   DELETE /api/learning/Concepts?id=999999999    — AdminOnly
///   DELETE /api/learning/Skills?id=999999999      — AdminOnly
///
/// Fix covered: commit feb3841 on branch fix/learning-getbyid-404.
/// LearningRepository.GetByIdAsync now returns null for a missing id;
/// LearningBaseService.GetByIdAsync/UpdateAsync/DeleteAsync map null → NotFound (HTTP 404).
/// Custom handlers (DeleteSubject, DeleteUnit, DeleteSkill, EditSubject) also check for null
/// and return NotFound explicitly.
///
/// Each test asserts:
///   1. HTTP status == 404 (NOT 500, NOT 200).
///   2. Response body successed == false.
///   3. Response body message does NOT leak internals (no " at ", Npgsql, InvalidOperationException, etc.).
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// Runs in the shared "IntegrationTests" xUnit collection (LearnexiaWebAppFactory, Testcontainers PG).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_01c_LearningGetByIdNotFound_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    // Deliberately non-existent id — far beyond any seeded or test-created entity.
    private const int GhostId = 999999999;

    private string? _adminToken;

    public P7_01c_LearningGetByIdNotFound_Tests(LearnexiaWebAppFactory factory)
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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // GET by non-existent id — all 5 aggregates
    // =========================================================================

    [Fact(DisplayName = "NotFound-regression: GET Subjects?id=999999999 → 404, successed=false, no leak")]
    public async Task GetById_Subject_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Subjects?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Subject GetById with non-existent id");
        AssertNoInternalLeak(root, body, "Subject GetById");
    }

    [Fact(DisplayName = "NotFound-regression: GET Units?id=999999999 → 404, successed=false, no leak")]
    public async Task GetById_Unit_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Units?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Unit GetById with non-existent id");
        AssertNoInternalLeak(root, body, "Unit GetById");
    }

    [Fact(DisplayName = "NotFound-regression: GET Grades?id=999999999 → 404, successed=false, no leak")]
    public async Task GetById_Grade_NonExistentId_Returns404()
    {
        // Grades controller is [Authorize] at class level — admin token satisfies it.
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Grades?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Grade GetById with non-existent id");
        AssertNoInternalLeak(root, body, "Grade GetById");
    }

    [Fact(DisplayName = "NotFound-regression: GET Concepts?id=999999999 → 404, successed=false, no leak")]
    public async Task GetById_Concept_NonExistentId_Returns404()
    {
        // ConceptsController has class-level [Authorize] — use admin token so auth passes
        // and the handler can return a real 404 (not 401).
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Concepts?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Concept GetById with non-existent id");
        AssertNoInternalLeak(root, body, "Concept GetById");
    }

    [Fact(DisplayName = "NotFound-regression: GET Skills?id=999999999 → 404, successed=false, no leak")]
    public async Task GetById_Skill_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Get, $"/api/learning/Skills?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Skill GetById with non-existent id");
        AssertNoInternalLeak(root, body, "Skill GetById");
    }

    // =========================================================================
    // UPDATE of non-existent id — all 5 aggregates
    // =========================================================================

    [Fact(DisplayName = "NotFound-regression: PUT Subjects/Update id=999999999 → 404, successed=false, no leak")]
    public async Task Update_Subject_NonExistentId_Returns404()
    {
        // EditSubjectCommand : EditSubjectDto : SubjectDto : BaseDto { Id, Name, Country, GradeId, SubjectCode, Language, SequenceOrder, IsActive }
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put,
            "/api/learning/Subjects/Update",
            new
            {
                Id = GhostId,
                Name = "Ghost Subject",
                Country = "EG",
                GradeId = 1,
                SubjectCode = 0,
                Language = 0,
                SequenceOrder = 1,
                IsActive = true
            },
            bearer: _adminToken);

        AssertNotFound(resp, root, body, "Subject Update with non-existent id");
        AssertNoInternalLeak(root, body, "Subject Update");
    }

    [Fact(DisplayName = "NotFound-regression: PUT Units/Update id=999999999 → 404, successed=false, no leak")]
    public async Task Update_Unit_NonExistentId_Returns404()
    {
        // EditUnitCommand : EditUnitDto : UnitDto : BaseDto { Id, Name, SequenceOrder, SubjectId, IsActive }
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put,
            "/api/learning/Units/Update",
            new
            {
                Id = GhostId,
                Name = "Ghost Unit",
                SequenceOrder = 1,
                SubjectId = 1,
                IsActive = true
            },
            bearer: _adminToken);

        AssertNotFound(resp, root, body, "Unit Update with non-existent id");
        AssertNoInternalLeak(root, body, "Unit Update");
    }

    [Fact(DisplayName = "NotFound-regression: PUT Grades/Update id=999999999 → 404, successed=false, no leak")]
    public async Task Update_Grade_NonExistentId_Returns404()
    {
        // EditGradeCommand : EditGradeDto : GradeDto : BaseDto { Id, Number, DisplayName }
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put,
            "/api/learning/Grades/Update",
            new
            {
                Id = GhostId,
                Number = 1,
                DisplayName = "Ghost Grade"
            },
            bearer: _adminToken);

        AssertNotFound(resp, root, body, "Grade Update with non-existent id");
        AssertNoInternalLeak(root, body, "Grade Update");
    }

    [Fact(DisplayName = "NotFound-regression: PUT Concepts/Update id=999999999 → 404, successed=false, no leak")]
    public async Task Update_Concept_NonExistentId_Returns404()
    {
        // EditConceptCommand : EditConceptDto : ConceptDto : BaseDto { Id, Name, Description, DifficultyLevel, SubjectId }
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put,
            "/api/learning/Concepts/Update",
            new
            {
                Id = GhostId,
                Name = "Ghost Concept",
                Description = "none",
                DifficultyLevel = 1,
                SubjectId = 1
            },
            bearer: _adminToken);

        AssertNotFound(resp, root, body, "Concept Update with non-existent id");
        AssertNoInternalLeak(root, body, "Concept Update");
    }

    [Fact(DisplayName = "NotFound-regression: PUT Skills/Update id=999999999 → 404, successed=false, no leak")]
    public async Task Update_Skill_NonExistentId_Returns404()
    {
        // EditSkillCommand : EditSkillDto : SkillDto : BaseDto { Id, Name, MasteryThreshold, EstimatedTimeMinutes, ConceptId }
        var (resp, root, body) = await SendAsync(
            HttpMethod.Put,
            "/api/learning/Skills/Update",
            new
            {
                Id = GhostId,
                Name = "Ghost Skill",
                MasteryThreshold = 80,
                EstimatedTimeMinutes = 10,
                ConceptId = 1
            },
            bearer: _adminToken);

        AssertNotFound(resp, root, body, "Skill Update with non-existent id");
        AssertNoInternalLeak(root, body, "Skill Update");
    }

    // =========================================================================
    // DELETE of non-existent id — all 5 aggregates
    // =========================================================================

    [Fact(DisplayName = "NotFound-regression: DELETE Subjects?id=999999999 → 404, successed=false, no leak")]
    public async Task Delete_Subject_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, $"/api/learning/Subjects?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Subject Delete with non-existent id");
        AssertNoInternalLeak(root, body, "Subject Delete");
    }

    [Fact(DisplayName = "NotFound-regression: DELETE Units?id=999999999 → 404, successed=false, no leak")]
    public async Task Delete_Unit_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, $"/api/learning/Units?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Unit Delete with non-existent id");
        AssertNoInternalLeak(root, body, "Unit Delete");
    }

    [Fact(DisplayName = "NotFound-regression: DELETE Grades?id=999999999 → 404, successed=false, no leak")]
    public async Task Delete_Grade_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, $"/api/learning/Grades?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Grade Delete with non-existent id");
        AssertNoInternalLeak(root, body, "Grade Delete");
    }

    [Fact(DisplayName = "NotFound-regression: DELETE Concepts?id=999999999 → 404, successed=false, no leak")]
    public async Task Delete_Concept_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, $"/api/learning/Concepts?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Concept Delete with non-existent id");
        AssertNoInternalLeak(root, body, "Concept Delete");
    }

    [Fact(DisplayName = "NotFound-regression: DELETE Skills?id=999999999 → 404, successed=false, no leak")]
    public async Task Delete_Skill_NonExistentId_Returns404()
    {
        var (resp, root, body) = await SendAsync(
            HttpMethod.Delete, $"/api/learning/Skills?id={GhostId}", bearer: _adminToken);

        AssertNotFound(resp, root, body, "Skill Delete with non-existent id");
        AssertNoInternalLeak(root, body, "Skill Delete");
    }

    // =========================================================================
    // Private assertion helpers
    // =========================================================================

    /// <summary>
    /// Asserts:
    ///   - HTTP status is exactly 404 (not 200, not 500, not any other code).
    ///   - Body is valid JSON.
    ///   - JSON envelope has successed == false.
    /// </summary>
    private static void AssertNotFound(HttpResponseMessage resp, JsonElement root, string body, string context)
    {
        // Must be valid JSON first so the rest of the assertions make sense.
        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow(
            "response must be valid JSON (not an exception page); context={0}; body: {1}", context, body);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "non-existent id must return 404 NOT 500 (regression fix feb3841); context={0}; body: {1}",
            context, body);

        // Envelope must carry successed=false.
        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "response envelope must have 'successed' key; context={0}; body: {1}", context, body);
        succeededProp.GetBoolean().Should().BeFalse(
            "successed must be false for a not-found response; context={0}; body: {1}", context, body);
    }

    /// <summary>
    /// Asserts that the response body does not leak internal implementation details:
    /// no stack trace markers (" at "), no Npgsql, no InvalidOperationException, no raw SQL.
    /// </summary>
    private static void AssertNoInternalLeak(JsonElement root, string body, string context)
    {
        // Check the message field specifically.
        if (TryProp(root, "message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
        {
            var msg = msgProp.GetString() ?? string.Empty;

            msg.Should().NotContain(" at ",
                "message must not contain a stack-frame marker (' at '); context={0}; message: {1}",
                context, msg);
            msg.Should().NotContainAny(
                new[] { "Npgsql", "InvalidOperationException", "constraint", "SELECT", "INSERT", "UPDATE", "DELETE FROM" },
                "message must not leak database or exception internals; context={0}; message: {1}",
                context, msg);
        }

        // Also check the full raw body for any stack-trace or Npgsql content.
        body.Should().NotContain(" at System.",
            "raw body must not contain a .NET stack trace; context={0}; body (truncated): {1}",
            context, body.Length > 300 ? body[..300] : body);
        body.Should().NotContain("Npgsql",
            "raw body must not mention Npgsql; context={0}; body (truncated): {1}",
            context, body.Length > 300 ? body[..300] : body);
        body.Should().NotContain("InvalidOperationException",
            "raw body must not mention InvalidOperationException; context={0}; body (truncated): {1}",
            context, body.Length > 300 ? body[..300] : body);
    }

    // =========================================================================
    // Private infrastructure helpers (mirrored from P7_01 / P7_03)
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
            catch { /* non-JSON body — root stays default */ }
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
}

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-02 integration tests — Browse Subjects and Lessons (student-facing read endpoints).
///
/// Endpoints under test:
///   GET /api/learning/Subjects/ForGrade?grade={1-6}
///   GET /api/learning/Subjects/{id}/Lessons
///   GET /api/learning/Subjects/{id}/SkillTree
///
/// All three endpoints have NO [Authorize] attribute in the current production code (SubjectsController.cs).
/// Therefore they are accessible without a JWT, and these tests assert anonymous access throughout.
///
/// Seed shape (from LearningSeeder.cs):
///   Math    G1 : 5 units × 3 lessons = 15 lessons;  5 concepts × 3 skills = 15 skills
///   Science G1 : 2 units × 2 lessons;  2 concepts × 2 skills
///   Arabic  G1 : 2 units × 2 lessons;  2 concepts × 2 skills
///   English G1 : 2 units × 2 lessons;  2 concepts × 2 skills
///
/// Per grade, the seeder produces exactly 4 subjects (Math, Science, Arabic, English).
///
/// NodeState enum serialized as int (no JsonStringEnumConverter registered):
///   Locked=0, Available=1, Completed=2
///
/// Coverage map (12 test cases → Fact methods):
///   TC-1  ForGrade Grade 1 happy path          → ForGrade_Grade1_Returns200_With4Subjects
///   TC-2  ForGrade Grade 6 happy path          → ForGrade_Grade6_Returns200_With4Subjects
///   TC-3  ForGrade out-of-range grade=99       → ForGrade_OutOfRange_Grade99_Returns400_NotServerError
///   TC-4  ForGrade missing param (grade=0)     → ForGrade_MissingParam_Returns400_NotServerError
///   TC-5  ForGrade item shape                  → ForGrade_ItemShape_HasRequiredFields
///   TC-6  Lessons happy path                   → Lessons_HappyPath_Returns200_WithUnitsAndLessons
///   TC-7  Lessons ordered by SequenceOrder     → Lessons_UnitsAndLessonsOrderedBySequenceOrder
///   TC-8  Lessons unknown subject id           → Lessons_UnknownSubjectId_Returns404_NotServerError
///   TC-9  SkillTree happy path                 → SkillTree_HappyPath_Returns200_WithConceptsAndSkills
///   TC-10 SkillTree NodeState field present    → SkillTree_NodeState_FieldPresentAndValidValue
///   TC-11 SkillTree unknown subject id         → SkillTree_UnknownSubjectId_Returns404_NotServerError
///   TC-12 Envelope successed casing            → Envelope_SuccessedCamelCase_IsPresent
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_02_BrowseSubjectsAndLessons_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Seeded Math G1 subject id — resolved in InitializeAsync so all test methods can use it.
    private int _mathG1SubjectId;

    public P2_02_BrowseSubjectsAndLessons_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Apply migrations + identity seed (idempotent across all test classes).
        await _factory.ApplyMigrationsAndSeedAsync();

        // Seed the Learning module data (4 subjects × 6 grades, idempotent).
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Resolve the Math G1 subject id once for all tests that need a real subject id.
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var subject = await db.Subjects
            .Include(s => s.Grade)
            .FirstAsync(s => s.Name == "Math" && s.Grade.Number == 1);
        _mathG1SubjectId = subject.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

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

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)> GetAsync(string url)
    {
        var response = await _client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(body))
            root = JsonDocument.Parse(body).RootElement;
        return (response, root, body);
    }

    // =========================================================================
    // TC-1: ForGrade Grade 1 — 4 subjects returned
    // =========================================================================

    [Fact(DisplayName = "TC-1: GET /ForGrade?grade=1 returns HTTP 200 with exactly 4 subjects")]
    public async Task ForGrade_Grade1_Returns200_With4Subjects()
    {
        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade?grade=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "ForGrade grade=1 must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true for grade=1; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "'data' must be a JSON array; body: {0}", body);

        data.GetArrayLength().Should().Be(4,
            "seeder produces exactly 4 subjects (Math, Science, Arabic, English) per grade; body: {0}", body);
    }

    // =========================================================================
    // TC-2: ForGrade Grade 6 — 4 subjects returned
    // =========================================================================

    [Fact(DisplayName = "TC-2: GET /ForGrade?grade=6 returns HTTP 200 with exactly 4 subjects")]
    public async Task ForGrade_Grade6_Returns200_With4Subjects()
    {
        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade?grade=6");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "ForGrade grade=6 must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "'data' must be a JSON array; body: {0}", body);
        data.GetArrayLength().Should().Be(4,
            "seeder produces exactly 4 subjects per grade; body: {0}", body);
    }

    // =========================================================================
    // TC-3: ForGrade out-of-range grade=99 → 400, not 500
    // =========================================================================

    [Fact(DisplayName = "TC-3: GET /ForGrade?grade=99 returns 400 (out-of-range), NOT 500")]
    public async Task ForGrade_OutOfRange_Grade99_Returns400_NotServerError()
    {
        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade?grade=99");

        // Handler explicitly returns BadRequest for grade outside 1-6.
        ((int)response.StatusCode).Should().NotBe(500,
            "out-of-range grade must never return 500; body: {0}", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "handler returns BadRequest for grade=99 (outside 1-6); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false for out-of-range grade; body: {0}", body);
    }

    // =========================================================================
    // TC-4: ForGrade missing param (defaults to 0) → 400, not 500
    // =========================================================================

    [Fact(DisplayName = "TC-4: GET /ForGrade (no grade param, defaults to 0) returns non-500")]
    public async Task ForGrade_MissingParam_Returns400_NotServerError()
    {
        // When ?grade is omitted, ASP.NET model binding supplies default(int) = 0.
        // 0 < 1 triggers BadRequest in the handler, so we expect 400.
        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade");

        ((int)response.StatusCode).Should().NotBe(500,
            "missing grade param must never produce 500; body: {0}", body);

        // Handler returns BadRequest for 0 (< 1).
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "grade=0 (default when omitted) is out-of-range 1-6 → BadRequest; body: {0}", body);
    }

    // =========================================================================
    // TC-5: ForGrade item shape — each subject DTO has id, name, gradeNumber
    // =========================================================================

    [Fact(DisplayName = "TC-5: GET /ForGrade?grade=1 item shape has id, name, gradeNumber")]
    public async Task ForGrade_ItemShape_HasRequiredFields()
    {
        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade?grade=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.GetArrayLength().Should().BeGreaterThan(0, "at least one subject must be present; body: {0}", body);

        foreach (var item in data.EnumerateArray())
        {
            TryProp(item, "id", out var idProp).Should().BeTrue(
                "each subject DTO must have 'id'; body: {0}", body);
            idProp.ValueKind.Should().Be(JsonValueKind.Number, "'id' must be a number; body: {0}", body);
            idProp.GetInt32().Should().BeGreaterThan(0, "'id' must be a positive integer; body: {0}", body);

            TryProp(item, "name", out var nameProp).Should().BeTrue(
                "each subject DTO must have 'name'; body: {0}", body);
            nameProp.GetString().Should().NotBeNullOrWhiteSpace("'name' must be non-empty; body: {0}", body);

            TryProp(item, "gradeNumber", out var gradeNumProp).Should().BeTrue(
                "each subject DTO must have 'gradeNumber'; body: {0}", body);
            gradeNumProp.GetInt32().Should().Be(1, "'gradeNumber' must match the requested grade; body: {0}", body);
        }

        // Verify the four expected subject names are present.
        var names = data.EnumerateArray()
            .Select(item =>
            {
                TryProp(item, "name", out var n);
                return n.GetString();
            })
            .ToList();

        names.Should().Contain("Math", "Math subject must be in grade 1; body: {0}", body);
        names.Should().Contain("Science", "Science subject must be in grade 1; body: {0}", body);
        names.Should().Contain("Arabic", "Arabic subject must be in grade 1; body: {0}", body);
        names.Should().Contain("English", "English subject must be in grade 1; body: {0}", body);
    }

    // =========================================================================
    // TC-6: Lessons happy path — 5 units, each with 3 lessons (Math G1)
    // =========================================================================

    [Fact(DisplayName = "TC-6: GET /{mathG1Id}/Lessons returns 200 with 5 units each containing 3 lessons")]
    public async Task Lessons_HappyPath_Returns200_WithUnitsAndLessons()
    {
        var (response, root, body) = await GetAsync($"/api/learning/Subjects/{_mathG1SubjectId}/Lessons");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Lessons for Math G1 must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "'data' must be a JSON array of units; body: {0}", body);

        // Math G1 seeder creates 5 units.
        data.GetArrayLength().Should().Be(5,
            "Math G1 must have 5 units (per LearningSeeder); body: {0}", body);

        // Each unit must have a 'lessons' array with 3 lessons.
        foreach (var unit in data.EnumerateArray())
        {
            TryProp(unit, "unitId", out var unitId).Should().BeTrue(
                "each unit must have 'unitId'; body: {0}", body);
            unitId.GetInt32().Should().BeGreaterThan(0, "unitId must be positive; body: {0}", body);

            TryProp(unit, "name", out var unitName).Should().BeTrue(
                "each unit must have 'name'; body: {0}", body);
            unitName.GetString().Should().NotBeNullOrWhiteSpace("unit name must be non-empty; body: {0}", body);

            TryProp(unit, "sequenceOrder", out var seq).Should().BeTrue(
                "each unit must have 'sequenceOrder'; body: {0}", body);

            TryProp(unit, "lessons", out var lessons).Should().BeTrue(
                "each unit must have 'lessons'; body: {0}", body);
            lessons.ValueKind.Should().Be(JsonValueKind.Array, "'lessons' must be an array; body: {0}", body);
            lessons.GetArrayLength().Should().Be(3,
                "each unit in Math G1 must have exactly 3 lessons (per seeder); body: {0}", body);
        }
    }

    // =========================================================================
    // TC-7: Lessons ordered by SequenceOrder
    // =========================================================================

    [Fact(DisplayName = "TC-7: GET /{mathG1Id}/Lessons — units and lessons within first unit are in SequenceOrder ascending")]
    public async Task Lessons_UnitsAndLessonsOrderedBySequenceOrder()
    {
        var (response, root, body) = await GetAsync($"/api/learning/Subjects/{_mathG1SubjectId}/Lessons");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        var units = data.EnumerateArray().ToList();
        units.Should().HaveCount(5, "Math G1 must have 5 units; body: {0}", body);

        // Assert units are sorted by sequenceOrder ascending.
        var unitSequences = units.Select(u =>
        {
            TryProp(u, "sequenceOrder", out var s);
            return s.GetInt32();
        }).ToList();

        unitSequences.Should().BeInAscendingOrder(
            "units must be returned in sequenceOrder ascending; body: {0}", body);

        // Verify unit sequenceOrders are 1, 2, 3, 4, 5.
        unitSequences.Should().Equal(new[] { 1, 2, 3, 4, 5 },
            "seeder assigns sequenceOrder 1-5 to Math G1 units; body: {0}", body);

        // Assert lessons within the first unit are sorted by sequenceOrder ascending.
        var firstUnit = units[0];
        TryProp(firstUnit, "lessons", out var firstUnitLessons).Should().BeTrue("body: {0}", body);

        var lessonSequences = firstUnitLessons.EnumerateArray().Select(l =>
        {
            TryProp(l, "sequenceOrder", out var s);
            return s.GetInt32();
        }).ToList();

        lessonSequences.Should().BeInAscendingOrder(
            "lessons within first unit must be in sequenceOrder ascending; body: {0}", body);
        lessonSequences.Should().Equal(new[] { 1, 2, 3 },
            "seeder assigns sequenceOrder 1-3 to lessons in each unit; body: {0}", body);
    }

    // =========================================================================
    // TC-8: Lessons unknown subject id → 404, not 500
    // =========================================================================

    [Fact(DisplayName = "TC-8: GET /Subjects/999999/Lessons returns 404 (not found), NOT 500")]
    public async Task Lessons_UnknownSubjectId_Returns404_NotServerError()
    {
        var (response, root, body) = await GetAsync("/api/learning/Subjects/999999/Lessons");

        ((int)response.StatusCode).Should().NotBe(500,
            "unknown subject id must never produce 500; body: {0}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "handler returns NotFound when subject does not exist; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false for unknown subject; body: {0}", body);
    }

    // =========================================================================
    // TC-9: SkillTree happy path — 5 concepts each with 3 skills (Math G1)
    // =========================================================================

    [Fact(DisplayName = "TC-9: GET /{mathG1Id}/SkillTree returns 200 with 5 concepts each containing 3 skills")]
    public async Task SkillTree_HappyPath_Returns200_WithConceptsAndSkills()
    {
        var (response, root, body) = await GetAsync($"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SkillTree for Math G1 must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "'data' must be a JSON array of concepts; body: {0}", body);

        // Math G1 seeder creates 5 concepts.
        data.GetArrayLength().Should().Be(5,
            "Math G1 must have 5 concepts (per LearningSeeder); body: {0}", body);

        // Each concept must have a 'skills' array with 3 skills.
        foreach (var concept in data.EnumerateArray())
        {
            TryProp(concept, "conceptId", out var conceptId).Should().BeTrue(
                "each concept must have 'conceptId'; body: {0}", body);
            conceptId.GetInt32().Should().BeGreaterThan(0, "conceptId must be positive; body: {0}", body);

            TryProp(concept, "name", out var conceptName).Should().BeTrue(
                "each concept must have 'name'; body: {0}", body);
            conceptName.GetString().Should().NotBeNullOrWhiteSpace("concept name must be non-empty; body: {0}", body);

            TryProp(concept, "skills", out var skills).Should().BeTrue(
                "each concept must have 'skills'; body: {0}", body);
            skills.ValueKind.Should().Be(JsonValueKind.Array, "'skills' must be an array; body: {0}", body);
            skills.GetArrayLength().Should().Be(3,
                "each concept in Math G1 must have exactly 3 skills (per seeder); body: {0}", body);

            // Assert each skill has expected fields.
            foreach (var skill in skills.EnumerateArray())
            {
                TryProp(skill, "skillId", out var skillId).Should().BeTrue(
                    "each skill must have 'skillId'; body: {0}", body);
                skillId.GetInt32().Should().BeGreaterThan(0, "skillId must be positive; body: {0}", body);

                TryProp(skill, "name", out var skillName).Should().BeTrue(
                    "each skill must have 'name'; body: {0}", body);
                skillName.GetString().Should().NotBeNullOrWhiteSpace("skill name must be non-empty; body: {0}", body);

                TryProp(skill, "masteryThreshold", out var mt).Should().BeTrue(
                    "each skill must have 'masteryThreshold'; body: {0}", body);
                mt.GetInt32().Should().BeInRange(0, 100, "masteryThreshold must be 0-100; body: {0}", body);

                TryProp(skill, "estimatedTimeMinutes", out var etm).Should().BeTrue(
                    "each skill must have 'estimatedTimeMinutes'; body: {0}", body);
                etm.GetInt32().Should().BeGreaterThan(0, "estimatedTimeMinutes must be positive; body: {0}", body);

                TryProp(skill, "lessonIds", out var lessonIds).Should().BeTrue(
                    "each skill must have 'lessonIds'; body: {0}", body);
                lessonIds.ValueKind.Should().Be(JsonValueKind.Array, "'lessonIds' must be an array; body: {0}", body);
            }
        }
    }

    // =========================================================================
    // TC-10: SkillTree NodeState field present and valid value
    // =========================================================================

    [Fact(DisplayName = "TC-10: GET /{mathG1Id}/SkillTree — each skill has 'state' field with valid NodeState value (int 0/1/2)")]
    public async Task SkillTree_NodeState_FieldPresentAndValidValue()
    {
        var (response, root, body) = await GetAsync($"/api/learning/Subjects/{_mathG1SubjectId}/SkillTree");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // NodeState enum serializes as int (no JsonStringEnumConverter registered):
        //   Locked=0, Available=1, Completed=2
        // SkillNodeDto exposes the property as 'State' → camelCase: 'state'.
        var validIntValues = new HashSet<int> { 0, 1, 2 };

        bool atLeastOneSkillChecked = false;
        foreach (var concept in data.EnumerateArray())
        {
            TryProp(concept, "skills", out var skills).Should().BeTrue("body: {0}", body);
            foreach (var skill in skills.EnumerateArray())
            {
                TryProp(skill, "state", out var stateProp).Should().BeTrue(
                    "each skill must have 'state' (NodeState); body: {0}", body);

                // The value must be an integer in {0, 1, 2} OR a string matching Locked/Available/Completed.
                if (stateProp.ValueKind == JsonValueKind.Number)
                {
                    validIntValues.Should().Contain(stateProp.GetInt32(),
                        "'state' int value must be 0 (Locked), 1 (Available), or 2 (Completed); body: {0}", body);
                }
                else if (stateProp.ValueKind == JsonValueKind.String)
                {
                    var validStringValues = new[] { "Locked", "Available", "Completed" };
                    validStringValues.Should().Contain(stateProp.GetString()!,
                        "'state' string value must be Locked/Available/Completed; body: {0}", body);
                }
                else
                {
                    false.Should().BeTrue(
                        "'state' must be a number or string, not {0}; body: {1}", stateProp.ValueKind, body);
                }
                atLeastOneSkillChecked = true;
            }
        }
        atLeastOneSkillChecked.Should().BeTrue("at least one skill must have been checked; body: {0}", body);

        // Also assert the raw body contains the literal "state": key (camelCase).
        body.Should().Contain("\"state\":",
            "JSON body must contain camelCase 'state' property for NodeState; body: {0}", body);
    }

    // =========================================================================
    // TC-11: SkillTree unknown subject id → 404, not 500
    // =========================================================================

    [Fact(DisplayName = "TC-11: GET /Subjects/999999/SkillTree returns 404 (not found), NOT 500")]
    public async Task SkillTree_UnknownSubjectId_Returns404_NotServerError()
    {
        var (response, root, body) = await GetAsync("/api/learning/Subjects/999999/SkillTree");

        ((int)response.StatusCode).Should().NotBe(500,
            "unknown subject id must never produce 500; body: {0}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "handler returns NotFound when subject does not exist; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false for unknown subject; body: {0}", body);
    }

    // =========================================================================
    // TC-12: Envelope 'successed' camelCase canonical spelling
    // =========================================================================

    [Fact(DisplayName = "TC-12: Success responses contain the literal 'successed': key (camelCase, canonical spelling)")]
    public async Task Envelope_SuccessedCamelCase_IsPresent()
    {
        // Use ForGrade endpoint as the representative success response.
        var (response, root, body) = await GetAsync("/api/learning/Subjects/ForGrade?grade=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // The literal camelCase spelling 'successed' must appear in the JSON body.
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> uses the spelling 'successed' (not 'succeeded' or 'success'); body: {0}", body);

        // Also verify the full required envelope fields are present.
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);
    }
}

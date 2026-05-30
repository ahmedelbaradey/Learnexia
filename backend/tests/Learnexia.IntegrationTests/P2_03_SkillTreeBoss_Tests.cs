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
/// P2-03 integration tests — "Navigate the skill tree (boss flag)".
///
/// Story: FR-LR-2 fourth node category — boss lessons (end-of-unit challenge).
/// Batch 3 acceptance criteria:
///   - Each unit has exactly one boss lesson (the highest-SequenceOrder lesson).
///   - All other lessons have IsBoss = false.
///   - GET /api/Learning/Subjects/{id}/Lessons surfaces IsBoss per lesson.
///   - GET /api/Learning/Lessons/{id} surfaces IsBoss on the single-lesson DTO.
///   - GET /api/Learning/Dashboard continue target includes IsBoss.
///   - LearningSeeder.MarkBossLessonsAsync is idempotent (second run = zero updates).
///
/// Endpoints under test:
///   GET /api/Learning/Subjects/{id}/Lessons  [Authorize]
///   GET /api/Learning/Lessons/{id}           [Authorize]
///   GET /api/Learning/Dashboard              [Authorize]
///
/// Student authentication: parent-driven onboarding flow (Register Parent → Add-Child → Sign-In).
/// Each test creates its own unique student (unique email) to prevent cross-test pollution.
///
/// Seeding: LearningSeeder.SeedAsync is called in InitializeAsync, identical to P2-09 pattern.
///
/// Coverage map (5 test cases):
///   C01  Seeder_BossCountEqualsUnitCount
///   C02  GetSubjectLessons_G1Math_EachUnitHasExactlyOneBossLesson_WithHighestSequenceOrder
///   C03  GetLesson_BossLesson_ReturnsTrueIsBoss
///   C04  GetLesson_NonBossLesson_ReturnsFalseIsBoss
///   C05  Seeder_IsIdempotent_BossCountStableAcrossSecondSeedRun
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_03_SkillTreeBoss_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Seeded subject IDs resolved once during InitializeAsync.
    private int _mathG1SubjectId;
    private int _scienceG1SubjectId;

    // Boss lesson id from Math G1 Unit 1 (highest SequenceOrder = 3).
    private int _bossLessonId;
    // Non-boss lesson id from Math G1 Unit 1 (SequenceOrder = 1 — lowest, definitely not boss).
    private int _nonBossLessonId;

    public P2_03_SkillTreeBoss_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        // Apply migrations + seed identity (idempotent across all tests sharing this fixture).
        await _factory.ApplyMigrationsAndSeedAsync();

        // The Learning seeder runs only in Development inside LearningModule.InitializeAsync.
        // Call it directly here so Grade-1 subjects with their Lessons, Skills, and boss
        // markers are present for every test case.
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Resolve subject IDs and known lesson IDs for test assertions.
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var mathSubject = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.Name == "Math" && s.Grade.Number == 1);

        mathSubject.Should().NotBeNull(
            "LearningSeeder must seed a 'Math' subject for Grade 1; check LearningSeeder.SeedAsync.");
        _mathG1SubjectId = mathSubject!.Id;

        var scienceSubject = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s => s.Name == "Science" && s.Grade.Number == 1);

        scienceSubject.Should().NotBeNull(
            "LearningSeeder must seed a 'Science' subject for Grade 1; check LearningSeeder.SeedAsync.");
        _scienceG1SubjectId = scienceSubject!.Id;

        // Find the boss lesson for Math G1 Unit 1 — highest SequenceOrder in Numbers and Place Value unit.
        // Seeder plants: SequenceOrder=1 (Introduction to Counting), =2 (Place Value), =3 (Rounding Numbers).
        // Boss = SequenceOrder 3 = "Rounding Numbers (G1)".
        var mathG1Unit1 = await db.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.SubjectId == _mathG1SubjectId && u.SequenceOrder == 1);

        mathG1Unit1.Should().NotBeNull("Math G1 must have a first unit (SequenceOrder=1).");
        var mathG1Unit1Id = mathG1Unit1!.Id;

        // Boss: highest-SequenceOrder lesson in unit 1.
        var bossLesson = await db.Lessons
            .AsNoTracking()
            .Where(l => l.UnitId == mathG1Unit1Id)
            .OrderByDescending(l => l.SequenceOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefaultAsync();

        bossLesson.Should().NotBeNull("Math G1 Unit 1 must have at least one lesson.");
        bossLesson!.IsBoss.Should().BeTrue(
            "After seeding, the highest-SequenceOrder lesson in Math G1 Unit 1 must have IsBoss=true; " +
            "lesson: '{0}', SequenceOrder: {1}", bossLesson.Name, bossLesson.SequenceOrder);
        _bossLessonId = bossLesson.Id;

        // Non-boss: lowest-SequenceOrder lesson in unit 1.
        var nonBossLesson = await db.Lessons
            .AsNoTracking()
            .Where(l => l.UnitId == mathG1Unit1Id)
            .OrderBy(l => l.SequenceOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefaultAsync();

        nonBossLesson.Should().NotBeNull("Math G1 Unit 1 must have at least one non-boss lesson.");
        nonBossLesson!.IsBoss.Should().BeFalse(
            "The first lesson (lowest SequenceOrder) in Math G1 Unit 1 must NOT be a boss; " +
            "lesson: '{0}', SequenceOrder: {1}", nonBossLesson.Name, nonBossLesson.SequenceOrder);
        _nonBossLessonId = nonBossLesson.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p203_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive property lookup — handles both camelCase and PascalCase JSON.</summary>
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

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url,
            object? body = null, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// Registers a parent, adds a child via Add-Child, and signs in as that child.
    /// Returns the Student JWT and the child's userId.
    /// Mirrors CreateStudentViaParentFlowAsync from P2_09_HomeDashboard_Tests.cs exactly.
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(
        string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..6];

        // 1. Register parent
        var parentEmail = UniqueEmail($"parent_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTokProp.GetString()!;

        // 2. Add child
        var childEmail = UniqueEmail($"child_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student P203",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        // 3. Sign in as child
        var (signInResp, signInRoot, signInBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"child sign-in must succeed; body: {signInBody}");
        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var studentTokProp).Should().BeTrue("body: {0}", signInBody);

        return (studentTokProp.GetString()!, childId);
    }

    // =========================================================================
    // C01 — Seeder smoke: boss count equals unit count (one boss per unit)
    // =========================================================================

    [Fact(DisplayName = "P203-C01 Seeder smoke: boss count == unit count (exactly one boss lesson per unit across all grades/subjects)")]
    public async Task Seeder_BossCountEqualsUnitCount()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var bossCount = await db.Lessons.CountAsync(l => l.IsBoss);
        var unitCount = await db.Units.CountAsync();

        bossCount.Should().Be(unitCount,
            "LearningSeeder.MarkBossLessonsAsync must mark exactly one lesson per unit as boss. " +
            "Expected bossCount == unitCount == {0}; actual bossCount = {1}. " +
            "Check that MarkBossLessonsAsync ran and that no unit has 0 or 2+ boss lessons.",
            unitCount, bossCount);

        // Sanity: the expected tally per the plan (66 boss lessons, 66 units across 6 grades × 11 units-per-grade).
        // Math: 5 units/grade × 6 grades = 30; Science/Arabic/English: 2 units/grade × 6 grades × 3 subjects = 36.
        // Total = 66. Allow ± 5 in case the seeder schema evolves.
        bossCount.Should().BeInRange(60, 72,
            "The seeder plants 66 boss lessons (5 Math units × 6 grades + 2×3 Science/Arabic/English units × 6 grades); " +
            "allow ±5 tolerance for future curriculum additions; actual bossCount = {0}", bossCount);
    }

    // =========================================================================
    // C02 — GET /Subjects/{mathG1Id}/Lessons: each unit has exactly one boss lesson
    //        at the highest SequenceOrder, all others have isBoss == false
    // =========================================================================

    [Fact(DisplayName = "P203-C02 GET /api/Learning/Subjects/{g1MathId}/Lessons — each unit has exactly one boss (highest SequenceOrder); all others isBoss=false")]
    public async Task GetSubjectLessons_G1Math_EachUnitHasExactlyOneBossLesson_WithHighestSequenceOrder()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c02");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Subjects/{_mathG1SubjectId}/Lessons", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated GET on a seeded subject must return 200; body: {0}", body);

        // Envelope: successed must be true (camelCase, per CONVENTIONS §5).
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true for a 200 response; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must contain 'data'; body: {0}", body);

        data.ValueKind.Should().Be(JsonValueKind.Array,
            "data must be a JSON array of units; body: {0}", body);

        var units = data.EnumerateArray().ToList();
        units.Should().NotBeEmpty(
            "Math G1 subject must have at least one unit after seeding; body: {0}", body);

        // Math G1 is seeded with 5 units.
        units.Count.Should().BeGreaterThanOrEqualTo(5,
            "Math G1 must have >= 5 units per the seeder; body: {0}", body);

        var totalBossFound = 0;

        foreach (var unit in units)
        {
            TryProp(unit, "lessons", out var lessonsEl).Should().BeTrue(
                "each unit element must have a 'lessons' array; body: {0}", body);
            lessonsEl.ValueKind.Should().Be(JsonValueKind.Array,
                "unit.lessons must be a JSON array; body: {0}", body);

            var lessons = lessonsEl.EnumerateArray().ToList();
            lessons.Should().NotBeEmpty(
                "every unit in the response must have at least one lesson; body: {0}", body);

            // Boss count per unit must be exactly 1.
            var bossLessons = lessons
                .Where(l => TryProp(l, "isBoss", out var isBossEl) && isBossEl.GetBoolean())
                .ToList();

            bossLessons.Count.Should().Be(1,
                "each unit must have exactly one boss lesson (IsBoss=true); " +
                "found {0} boss(es) in unit; body: {1}", bossLessons.Count, body);

            totalBossFound++;

            // The boss lesson must have the highest SequenceOrder in this unit.
            var bossLesson = bossLessons.Single();
            TryProp(bossLesson, "sequenceOrder", out var bossSeqEl).Should().BeTrue(
                "boss lesson must have 'sequenceOrder'; body: {0}", body);
            var bossSeqOrder = bossSeqEl.GetInt32();

            var maxSeqOrder = lessons
                .Select(l =>
                {
                    TryProp(l, "sequenceOrder", out var seqEl);
                    return seqEl.GetInt32();
                })
                .Max();

            bossSeqOrder.Should().Be(maxSeqOrder,
                "the boss lesson's SequenceOrder ({0}) must equal the unit's max SequenceOrder ({1}); body: {2}",
                bossSeqOrder, maxSeqOrder, body);

            // All non-boss lessons must have isBoss == false.
            foreach (var lesson in lessons)
            {
                TryProp(lesson, "isBoss", out var isBossEl).Should().BeTrue(
                    "every lesson in the response must have the 'isBoss' field; body: {0}", body);

                TryProp(lesson, "sequenceOrder", out var seqEl);
                var seqOrder = seqEl.GetInt32();

                if (seqOrder != bossSeqOrder)
                {
                    isBossEl.GetBoolean().Should().BeFalse(
                        "non-boss lesson at SequenceOrder={0} must have isBoss=false; body: {1}",
                        seqOrder, body);
                }
            }
        }

        totalBossFound.Should().BeGreaterThan(0,
            "at least one unit with a boss lesson must be present; body: {0}", body);
    }

    // =========================================================================
    // C03 — GET /api/Learning/Lessons/{bossLessonId}: isBoss == true
    // =========================================================================

    [Fact(DisplayName = "P203-C03 GET /api/Learning/Lessons/{bossLessonId} → response has isBoss=true")]
    public async Task GetLesson_BossLesson_ReturnsTrueIsBoss()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c03");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_bossLessonId}", null, token);

        // The boss lesson in Math G1 Unit 1 is "Rounding Numbers (G1)" with IsLocked=true.
        // Per P2-05 behaviour, the endpoint returns 200 regardless of lock status (preview semantics).
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/Learning/Lessons/{id} must return 200 even for locked lessons (P2-05 preview semantics); " +
            "bossLessonId={0}; body: {1}", _bossLessonId, body);

        // Envelope: successed must be true.
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);

        // isBoss field must be present and true.
        TryProp(data, "isBoss", out var isBossEl).Should().BeTrue(
            "SingleLessonResponse must include the 'isBoss' field (P2-03-BE-3d); body: {0}", body);
        isBossEl.GetBoolean().Should().BeTrue(
            "the boss lesson (highest SequenceOrder in its unit) must have isBoss=true; " +
            "bossLessonId={0}; body: {1}", _bossLessonId, body);
    }

    // =========================================================================
    // C04 — GET /api/Learning/Lessons/{nonBossLessonId}: isBoss == false
    // =========================================================================

    [Fact(DisplayName = "P203-C04 GET /api/Learning/Lessons/{nonBossLessonId} → response has isBoss=false")]
    public async Task GetLesson_NonBossLesson_ReturnsFalseIsBoss()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c04");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_nonBossLessonId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/Learning/Lessons/{id} must return 200 for a non-boss lesson; " +
            "nonBossLessonId={0}; body: {1}", _nonBossLessonId, body);

        // Envelope: successed must be true.
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);

        // isBoss field must be present and false.
        TryProp(data, "isBoss", out var isBossEl).Should().BeTrue(
            "SingleLessonResponse must include the 'isBoss' field (P2-03-BE-3d); body: {0}", body);
        isBossEl.GetBoolean().Should().BeFalse(
            "the non-boss lesson (lowest SequenceOrder=1 in its unit) must have isBoss=false; " +
            "nonBossLessonId={0}; body: {1}", _nonBossLessonId, body);
    }

    // =========================================================================
    // C05 — Seeder idempotency: second SeedAsync call leaves boss count unchanged
    // =========================================================================

    [Fact(DisplayName = "P203-C05 Seeder idempotency: second LearningSeeder.SeedAsync call leaves IsBoss count unchanged (zero updates)")]
    public async Task Seeder_IsIdempotent_BossCountStableAcrossSecondSeedRun()
    {
        using var scope1 = _factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Count after the first seed (already done in InitializeAsync).
        var bossCountBefore = await db1.Lessons.CountAsync(l => l.IsBoss);
        var totalLessons    = await db1.Lessons.CountAsync();
        var unitCount       = await db1.Units.CountAsync();

        bossCountBefore.Should().Be(unitCount,
            "one boss per unit must be present after the first SeedAsync in InitializeAsync; " +
            "bossCount={0}, unitCount={1}", bossCountBefore, unitCount);

        // Run SeedAsync a second time — must be idempotent.
        using var scope2 = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope2.ServiceProvider);

        using var scope3 = _factory.Services.CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<LearningDbContext>();

        var bossCountAfter     = await db3.Lessons.CountAsync(l => l.IsBoss);
        var totalLessonsAfter  = await db3.Lessons.CountAsync();
        var nonBossCountAfter  = totalLessonsAfter - bossCountAfter;

        bossCountAfter.Should().Be(bossCountBefore,
            "second SeedAsync must not change the boss count (idempotent); " +
            "before={0}, after={1}", bossCountBefore, bossCountAfter);

        totalLessonsAfter.Should().Be(totalLessons,
            "second SeedAsync must not insert extra lessons; " +
            "before={0}, after={1}", totalLessons, totalLessonsAfter);

        // Final sanity: 66 boss, 96 non-boss (plan estimate).
        bossCountAfter.Should().BeInRange(60, 72,
            "expected ~66 boss lessons (5 Math × 6 grades + 2 × 3 subjects × 6 grades); actual={0}",
            bossCountAfter);
        nonBossCountAfter.Should().BeGreaterThan(bossCountAfter,
            "non-boss lessons must outnumber boss lessons; nonBoss={0}, boss={1}",
            nonBossCountAfter, bossCountAfter);
    }
}

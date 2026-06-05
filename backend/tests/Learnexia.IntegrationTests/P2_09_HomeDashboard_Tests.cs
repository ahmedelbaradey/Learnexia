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
/// P2-09 integration tests — "See the Home Dashboard".
///
/// Endpoint under test:
///   GET /api/Learning/Dashboard   [Authorize]
///
/// Route: parameterless from the caller's POV — StudentId is resolved from the JWT via
/// _currentUser.UserId. IDOR is structurally impossible: there is no client-supplied
/// studentId parameter in the route, query string, or request body. No dedicated IDOR
/// test case is included — the constraint is verified by inspection of DashboardController.cs.
///
/// Student authentication: parent-driven onboarding flow (Register Parent → Add-Child → Sign-In).
/// Each test creates its own unique student (unique email) to prevent cross-test pollution.
///
/// Seeding: LearningSeeder.SeedAsync is called in InitializeAsync (mirrors P2-04/P2-11 patterns)
/// so Grade-1 subjects (Math, Science, Arabic, English) with Lessons/Skills/KnowledgeGraph are
/// present for every test case.
///
/// Coverage map (11 test cases):
///   C01  Anonymous_Returns401
///   C02  FreshStudent_Returns200_WithContinuePopulated
///   C03  ContinueShape_WhenPresent_HasRequiredFields
///   C04  Phase2ZeroValues_XpZero_StreakZero
///   C05  Phase2Nulls_DailyMissionNull_LeaguePreviewNull
///   C06  Envelope_HasSuccessedTrue_CamelCase
///   C07  StudentWithMathAttempt_ContinueIsInMathSubject
///   C08  StudentWithScienceAttempt_ContinueIsInScienceSubject
///   C09  CrossStudentIsolation_StudentBNotAffectedByStudentAActivity
///   C10  Idempotent_TwoCallsReturnSameShape
///   C11  SeederSmoke_Grade1Has4Subjects
///
/// Phase-4 contract notes (deliberately NOT tested here — Phase-2 stubs):
///   xp will always be 0 until P4-02 wires the XP ledger.
///   streak will always be 0 until P4-03 wires the streak engine.
///   dailyMissions (P4-06 WIRED) — now populated; null for brand-new students without an XP profile.
///   leaguePreview will always be null until P4-07 wires the leagues engine.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_09_HomeDashboard_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string DashboardUrl       = "api/Learning/Dashboard";
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

    public P2_09_HomeDashboard_Tests(LearnexiaWebAppFactory factory)
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
        // Call it directly here so Grade-1 subjects (Math/Science/Arabic/English) with their
        // Lessons, Skills, Concepts, and KnowledgeGraph nodes/edges are present.
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Resolve subject IDs used across multiple test cases.
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // P8-02: query by SubjectCode + Language — names are now grade-suffixed ("Math (G1)" etc.)
        // and differ between language trees. Students use learning_language="en" so the En tree
        // is the relevant one for Math and Science (ARABIC→Ar pinned, ENGLISH→En pinned).
        var mathSubject = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s =>
                s.SubjectCode == SubjectCode.MATH &&
                s.Language == ContentLanguage.En &&
                s.Grade.Number == 1);

        mathSubject.Should().NotBeNull(
            "LearningSeeder must seed a MATH/En subject for Grade 1; check LearningSeeder.SeedAsync.");
        _mathG1SubjectId = mathSubject!.Id;

        var scienceSubject = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s =>
                s.SubjectCode == SubjectCode.SCIENCE &&
                s.Language == ContentLanguage.En &&
                s.Grade.Number == 1);

        scienceSubject.Should().NotBeNull(
            "LearningSeeder must seed a SCIENCE/En subject for Grade 1; check LearningSeeder.SeedAsync.");
        _scienceG1SubjectId = scienceSubject!.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p209_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetDashboardAsync(string? bearerToken = null)
        => SendAsync(_client, HttpMethod.Get, DashboardUrl, null, bearerToken);

    private async Task<string> SignInAndGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Registers a parent, adds a child via Add-Child (which creates a Student-role account),
    /// and signs in as that child. Returns the Student JWT and the child's userId.
    /// Mirrors CreateStudentViaParentFlowAsync from P2_06/P2_08 tests exactly.
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
                FullName = "Test Student P209",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "en", // P8-01: "en" so handler resolves MATH/En, SCIENCE/En trees
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        // 3. Sign in as child
        var studentToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    /// <summary>
    /// Seeds a Completed Attempt for the given student+lesson directly in the DB.
    /// Mirrors SeedCompletedAttemptAsync from P2_04_LearningPath_Tests.cs.
    /// Returns the attemptId.
    /// </summary>
    private async Task<int> SeedCompletedAttemptAsync(int studentId, int lessonId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;
        var attempt = new Attempt
        {
            StudentId          = studentId,
            LessonId           = lessonId,
            Status             = AttemptStatus.Completed,
            AccuracyPercentage = 0.0,
            DurationSeconds    = 60,
            StartedAt          = now,
            CompletedAt        = now,
            CreatedAt          = now,
            CreatedBy          = 0,
        };
        db.Attempts.Add(attempt);
        await db.SaveChangesAsync();
        return attempt.Id;
    }

    /// <summary>
    /// Returns the first lesson in the given subject (by SequenceOrder ASC, then Id ASC).
    /// </summary>
    private async Task<Lesson> GetFirstLessonInSubjectAsync(int subjectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var lesson = await db.Lessons
            .AsNoTracking()
            .Include(l => l.Unit)
            .Where(l => l.Unit.SubjectId == subjectId)
            .OrderBy(l => l.SequenceOrder)
            .ThenBy(l => l.Id)
            .FirstOrDefaultAsync();

        lesson.Should().NotBeNull(
            $"subject {subjectId} must have at least one lesson (check LearningSeeder.SeedAsync).");
        return lesson!;
    }

    // =========================================================================
    // C01 — Anonymous request → 401
    // =========================================================================

    [Fact(DisplayName = "P209-C01 Anonymous GET /api/Learning/Dashboard → 401")]
    public async Task Anonymous_Returns401()
    {
        // No bearer token — middleware must block with 401.
        var (response, _, body) = await GetDashboardAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "DashboardController.Get has [Authorize]; anonymous caller must receive 401; body: {0}", body);
    }

    // =========================================================================
    // C02 — Fresh student (no attempts) → 200 with Continue populated
    // =========================================================================

    [Fact(DisplayName = "P209-C02 Fresh student (no attempts) → 200, continue non-null (Grade-1 Math fallback)")]
    public async Task FreshStudent_Returns200_WithContinuePopulated()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c02");

        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated fresh student must receive 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must contain 'data'; body: {0}", body);

        // xp and streak must be 0 (Phase-2 stubs)
        TryProp(data, "xp", out var xp).Should().BeTrue("data.xp required; body: {0}", body);
        xp.GetInt32().Should().Be(0, "xp must be 0 in Phase 2; body: {0}", body);

        TryProp(data, "streak", out var streak).Should().BeTrue("data.streak required; body: {0}", body);
        streak.GetInt32().Should().Be(0, "streak must be 0 in Phase 2; body: {0}", body);

        // P4-06: dailyMissions (plural) replaces the old dailyMission stub.
        // Brand-new student has no XP profile, so IStudentMissionsQuery returns sentinel ([], null) → null.
        TryProp(data, "dailyMissions", out var dailyMissions).Should().BeTrue(
            "data.dailyMissions required (P4-06); body: {0}", body);
        dailyMissions.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Array },
            "dailyMissions must be null or an array for a brand-new student; body: {0}", body);

        TryProp(data, "leaguePreview", out var leaguePreview).Should().BeTrue(
            "data.leaguePreview required; body: {0}", body);
        leaguePreview.ValueKind.Should().Be(JsonValueKind.Null,
            "leaguePreview must be null in Phase 2; body: {0}", body);

        // continue must be non-null — Grade-1 Math fallback should resolve an Available lesson.
        TryProp(data, "continue", out var continueTarget).Should().BeTrue(
            "data.continue required; body: {0}", body);
        continueTarget.ValueKind.Should().NotBe(JsonValueKind.Null,
            "continue must not be null for a fresh student — Grade-1 Math fallback must find an Available lesson; body: {0}", body);
    }

    // =========================================================================
    // C03 — Continue shape: when present, all required fields are present
    // =========================================================================

    [Fact(DisplayName = "P209-C03 ContinueTargetDto when present has all required fields with correct nodeState=1 (Available)")]
    public async Task ContinueShape_WhenPresent_HasRequiredFields()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c03");

        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "continue", out var ct).Should().BeTrue("body: {0}", body);

        ct.ValueKind.Should().NotBe(JsonValueKind.Null,
            "continue must be non-null for a seeded fresh student; body: {0}", body);

        // subjectId — positive int
        TryProp(ct, "subjectId", out var subjectId).Should().BeTrue(
            "continue.subjectId required; body: {0}", body);
        subjectId.GetInt32().Should().BeGreaterThan(0,
            "continue.subjectId must be > 0; body: {0}", body);

        // subjectName — non-empty string
        TryProp(ct, "subjectName", out var subjectName).Should().BeTrue(
            "continue.subjectName required; body: {0}", body);
        subjectName.GetString().Should().NotBeNullOrWhiteSpace(
            "continue.subjectName must be non-empty; body: {0}", body);

        // unitId — positive int
        TryProp(ct, "unitId", out var unitId).Should().BeTrue(
            "continue.unitId required; body: {0}", body);
        unitId.GetInt32().Should().BeGreaterThan(0,
            "continue.unitId must be > 0; body: {0}", body);

        // unitName — non-empty string
        TryProp(ct, "unitName", out var unitName).Should().BeTrue(
            "continue.unitName required; body: {0}", body);
        unitName.GetString().Should().NotBeNullOrWhiteSpace(
            "continue.unitName must be non-empty; body: {0}", body);

        // lessonId — positive int
        TryProp(ct, "lessonId", out var lessonId).Should().BeTrue(
            "continue.lessonId required; body: {0}", body);
        lessonId.GetInt32().Should().BeGreaterThan(0,
            "continue.lessonId must be > 0; body: {0}", body);

        // lessonName — non-empty string
        TryProp(ct, "lessonName", out var lessonName).Should().BeTrue(
            "continue.lessonName required; body: {0}", body);
        lessonName.GetString().Should().NotBeNullOrWhiteSpace(
            "continue.lessonName must be non-empty; body: {0}", body);

        // nodeState — must be 1 (NodeState.Available) — serialized as int by System.Text.Json default
        TryProp(ct, "nodeState", out var nodeState).Should().BeTrue(
            "continue.nodeState required; body: {0}", body);

        // The system serializes NodeState enum as int (default System.Text.Json behaviour, no
        // JsonStringEnumConverter registered globally). Mirror the P2-04 assertion pattern.
        if (nodeState.ValueKind == JsonValueKind.Number)
        {
            nodeState.GetInt32().Should().Be((int)NodeState.Available,
                "continue.nodeState must be 1 (NodeState.Available); body: {0}", body);
        }
        else if (nodeState.ValueKind == JsonValueKind.String)
        {
            nodeState.GetString().Should().Be("Available",
                "continue.nodeState string must be 'Available'; body: {0}", body);
        }
        else
        {
            // Neither int nor string — fail with a helpful message.
            nodeState.ValueKind.Should().BeOneOf(new[] { JsonValueKind.Number, JsonValueKind.String },
                "continue.nodeState must serialize as int or string; body: {0}", body);
        }

        // skillId — nullable; if present and non-null must be > 0
        if (TryProp(ct, "skillId", out var skillId) && skillId.ValueKind != JsonValueKind.Null)
        {
            skillId.GetInt32().Should().BeGreaterThan(0,
                "continue.skillId when non-null must be > 0; body: {0}", body);
        }
        // skillName — nullable; allowed to be null when SkillId is null

        // isBoss — P2-03: field must be present and false for a fresh Grade-1 student.
        // The fresh-student fallback lands on "Introduction to Counting (G1)" (SequenceOrder=1),
        // which is the FIRST lesson in Math G1 Unit 1 — NOT the highest-SequenceOrder lesson,
        // so it is never marked as boss by LearningSeeder.MarkBossLessonsAsync.
        TryProp(ct, "isBoss", out var isBoss).Should().BeTrue("isBoss field must be present on ContinueTargetDto; body: {0}", body);
        isBoss.GetBoolean().Should().BeFalse("fresh Grade-1 student lands on Introduction to Counting (G1), which is SequenceOrder=1 — not a boss lesson; body: {0}", body);
    }

    // =========================================================================
    // C04 — Phase-2 zero values: xp == 0 AND streak == 0
    // =========================================================================

    [Fact(DisplayName = "P209-C04 Phase-2 stubs: xp == 0 and streak == 0 (literally, not just non-negative)")]
    public async Task Phase2ZeroValues_XpZero_StreakZero()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c04");

        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "xp", out var xp).Should().BeTrue("data.xp must be present; body: {0}", body);
        xp.GetInt32().Should().Be(0, "xp must be exactly 0 in Phase 2 (TODO P4-02); body: {0}", body);

        TryProp(data, "streak", out var streak).Should().BeTrue(
            "data.streak must be present; body: {0}", body);
        streak.GetInt32().Should().Be(0,
            "streak must be exactly 0 in Phase 2 (TODO P4-03); body: {0}", body);
    }

    // =========================================================================
    // C05 — Phase-2 null values: dailyMissions == null (brand-new student) AND leaguePreview == null
    // =========================================================================

    [Fact(DisplayName = "P209-C05 P4-06 wired: dailyMissions is JSON null for brand-new student (no missions instantiated); leaguePreview is JSON null")]
    public async Task Phase2Nulls_DailyMissionNull_LeaguePreviewNull()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c05");

        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // P4-06: dailyMissions (plural) is the new field replacing the old dailyMission stub.
        // Brand-new students: the sentinel returns an empty list which maps to null in the handler
        // (empty list is suppressed to null for JSON compactness — mirrors RecentBadges behaviour).
        // The MissionSeeder runs at startup and seeds catalog rows; however, a brand-new student
        // has no StudentXpProfile yet, so IStudentMissionsQuery returns the sentinel ([], null),
        // which handler maps to null.
        TryProp(data, "dailyMissions", out var dailyMissions).Should().BeTrue(
            "dailyMissions key must be present in the JSON; body: {0}", body);
        dailyMissions.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Null, JsonValueKind.Array },
            "dailyMissions must be JSON null or an array for a brand-new student (P4-06); body: {0}", body);

        // leaguePreview must be JSON null — not absent, not an empty object.
        TryProp(data, "leaguePreview", out var leaguePreview).Should().BeTrue(
            "leaguePreview key must be present in the JSON; body: {0}", body);
        leaguePreview.ValueKind.Should().Be(JsonValueKind.Null,
            "leaguePreview must be JSON null in Phase 2 (TODO P4-07); body: {0}", body);
    }

    // =========================================================================
    // C06 — Envelope shape: "successed" camelCase + value true
    // =========================================================================

    [Fact(DisplayName = "P209-C06 Success response envelope has 'successed': true (camelCase, per CONVENTIONS §5)")]
    public async Task Envelope_HasSuccessedTrue_CamelCase()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c06");

        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Critical: the literal string "successed": must appear in camelCase.
        // Per CONVENTIONS.md §5 the success flag is spelled 'Successed' (sic — load-bearing).
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> uses the canonical key 'successed' (camelCase of Successed); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "'successed' key must be present in the JSON envelope; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "successed must be true for a 200 response; body: {0}", body);

        // Full envelope shape (statusCode, message, data, errors).
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must have 'message'; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must have 'errors'; body: {0}", body);
    }

    // =========================================================================
    // C07 — Student with Math attempt → continue is in Math subject
    // =========================================================================

    [Fact(DisplayName = "P209-C07 Student with Completed Math attempt → continue.subjectId is Math G1 (most-recent-activity logic)")]
    public async Task StudentWithMathAttempt_ContinueIsInMathSubject()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c07");

        // Find the first available lesson in Math G1 to seed a Completed attempt.
        // We need a lesson that the engine would mark Available for a fresh student —
        // the first lesson by SequenceOrder (root skill or null-skill lesson).
        var completedLesson = await GetFirstLessonInSubjectAsync(_mathG1SubjectId);

        // Seed a Completed attempt directly in the DB (mirrors P2-04's SeedCompletedAttemptAsync).
        await SeedCompletedAttemptAsync(studentId, completedLesson.Id);

        // Hit the dashboard endpoint.
        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated student with a Math attempt must receive 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "continue", out var ct).Should().BeTrue("body: {0}", body);

        // continue may be null if every Math G1 lesson was just completed and there are no
        // more Available lessons. In practice the seeder has many lessons so this should not
        // be null. Assert non-null first.
        ct.ValueKind.Should().NotBe(JsonValueKind.Null,
            "continue must not be null — seeded Math G1 has multiple lessons; " +
            "the completed lesson is Completed, subsequent lessons should be Available; body: {0}", body);

        // The most-recent-activity logic should pick Math as the primary subject.
        TryProp(ct, "subjectId", out var subjectId).Should().BeTrue("body: {0}", body);
        subjectId.GetInt32().Should().Be(_mathG1SubjectId,
            "continue.subjectId must match the Math G1 subject where the attempt was seeded; body: {0}", body);

        // The returned lessonId must NOT be the just-completed lesson
        // (Completed lessons are not Available — engine marks them Completed, not Available).
        TryProp(ct, "lessonId", out var lessonId).Should().BeTrue("body: {0}", body);
        lessonId.GetInt32().Should().NotBe(completedLesson.Id,
            "the Completed lesson must not be returned as the continue target; body: {0}", body);
    }

    // =========================================================================
    // C08 — Student with Science attempt → continue is in Science subject
    // =========================================================================

    [Fact(DisplayName = "P209-C08 Student with Completed Science attempt → continue.subjectId is Science G1")]
    public async Task StudentWithScienceAttempt_ContinueIsInScienceSubject()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c08");

        // Find the first available lesson in Science G1 to seed a Completed attempt.
        var completedLesson = await GetFirstLessonInSubjectAsync(_scienceG1SubjectId);

        // Seed a Completed attempt for a Science lesson.
        await SeedCompletedAttemptAsync(studentId, completedLesson.Id);

        var (resp, root, body) = await GetDashboardAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated student with a Science attempt must receive 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "continue", out var ct).Should().BeTrue("body: {0}", body);

        ct.ValueKind.Should().NotBe(JsonValueKind.Null,
            "continue must not be null — Science G1 has Available lessons after the first is Completed; body: {0}", body);

        TryProp(ct, "subjectId", out var subjectId).Should().BeTrue("body: {0}", body);
        subjectId.GetInt32().Should().Be(_scienceG1SubjectId,
            "most-recent-activity logic must select Science (the subject where the Attempt was seeded); body: {0}", body);
    }

    // =========================================================================
    // C09 — Cross-student isolation: Student B not affected by Student A's activity
    // =========================================================================

    [Fact(DisplayName = "P209-C09 Cross-student isolation: Student B's dashboard ignores Student A's Science attempt")]
    public async Task CrossStudentIsolation_StudentBNotAffectedByStudentAActivity()
    {
        // Student A seeds a Science attempt.
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("c09a");
        var scienceLesson = await GetFirstLessonInSubjectAsync(_scienceG1SubjectId);
        await SeedCompletedAttemptAsync(studentIdA, scienceLesson.Id);

        // Student B has no attempts.
        var (tokenB, _) = await CreateStudentViaParentFlowAsync("c09b");

        // Student B's dashboard should use the Grade-1 Math fallback (no attempts → fallback).
        var (resp, root, body) = await GetDashboardAsync(tokenB);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student B's dashboard must return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "continue", out var ct).Should().BeTrue("body: {0}", body);

        ct.ValueKind.Should().NotBe(JsonValueKind.Null,
            "Student B's continue must not be null (Grade-1 Math fallback should apply); body: {0}", body);

        // Student B should default to Math (no attempts → Grade-1 Math fallback, not Science).
        TryProp(ct, "subjectId", out var subjectId).Should().BeTrue("body: {0}", body);
        subjectId.GetInt32().Should().Be(_mathG1SubjectId,
            "Student B (no attempts) must receive the Grade-1 Math fallback, " +
            "not Student A's Science subject — cross-student isolation; body: {0}", body);
    }

    // =========================================================================
    // C10 — Idempotent: two consecutive calls return the same Continue target
    // =========================================================================

    [Fact(DisplayName = "P209-C10 Idempotent: two calls to GET /Dashboard return the same continue target (no hidden side-effects)")]
    public async Task Idempotent_TwoCallsReturnSameShape()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c10");

        // First call.
        var (resp1, root1, body1) = await GetDashboardAsync(token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK,
            "first dashboard call must return 200; body: {0}", body1);

        TryProp(root1, "data", out var data1).Should().BeTrue("body: {0}", body1);
        TryProp(data1, "continue", out var ct1).Should().BeTrue("body: {0}", body1);

        // Second call — must return the same shape.
        var (resp2, root2, body2) = await GetDashboardAsync(token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second dashboard call must also return 200; body: {0}", body2);

        TryProp(root2, "data", out var data2).Should().BeTrue("body: {0}", body2);
        TryProp(data2, "continue", out var ct2).Should().BeTrue("body: {0}", body2);

        // Both calls must produce the same continue.lessonId (or both null).
        if (ct1.ValueKind == JsonValueKind.Null)
        {
            ct2.ValueKind.Should().Be(JsonValueKind.Null,
                "both calls must return null continue (or both non-null); body1: {0}", body1);
        }
        else
        {
            ct2.ValueKind.Should().NotBe(JsonValueKind.Null,
                "second call must also return non-null continue; body: {0}", body2);

            TryProp(ct1, "lessonId", out var lessonId1).Should().BeTrue("body: {0}", body1);
            TryProp(ct2, "lessonId", out var lessonId2).Should().BeTrue("body: {0}", body2);

            lessonId2.GetInt32().Should().Be(lessonId1.GetInt32(),
                "idempotent: both calls must return the same lessonId (no hidden writes); " +
                "body1: {0}, body2: {1}", body1, body2);
        }

        // xp and streak must be 0 in both calls.
        TryProp(data2, "xp", out var xp2).Should().BeTrue("body: {0}", body2);
        xp2.GetInt32().Should().Be(0, "xp must be 0 in both calls; body: {0}", body2);

        TryProp(data2, "streak", out var streak2).Should().BeTrue("body: {0}", body2);
        streak2.GetInt32().Should().Be(0, "streak must be 0 in both calls; body: {0}", body2);
    }

    // =========================================================================
    // C11 — Seeder smoke: Grade 1 has exactly 4 subjects after SeedAsync
    // =========================================================================

    [Fact(DisplayName = "P209-C11 Seeder smoke: Grade 1 has all 6 bilingual subject trees; 4 visible to an English-medium student")]
    public async Task SeederSmoke_Grade1Has4Subjects()
    {
        // P8-02: The bilingual seeder now creates 6 subject roots per grade
        // (Math/Ar, Math/En, Science/Ar, Science/En, Arabic/Ar, English/En).
        // The total DB count for Grade 1 is 6 — assert that.
        // The 4 subjects VISIBLE to an English-medium student (resolved by SubjectLanguageResolver):
        //   MATH    → En → "Math (G1)"
        //   SCIENCE → En → "Science (G1)"
        //   ARABIC  → Ar (pinned) → Arabic subject
        //   ENGLISH → En (pinned) → "English (G1)"
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var grade1SubjectCount = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .Where(s => s.Grade.Number == 1)
            .CountAsync();

        // Use >= 6 (not == 6) because P2_01_CurriculumHierarchy_Tests creates extra Grade rows
        // with Number=1 via CreateGradeGetId() (no unique constraint on Grade.Number). When
        // LearningSeeder.SeedAsync runs after those tests it may pick one of the extra Grade-1
        // rows and seed 6 subjects under it, giving 12 total for Grade.Number==1. The 4 stable
        // SubjectCode+Language assertions below (mathEn, scienceEn, arabicAr, englishEn) remain
        // the load-bearing idempotency check — they guarantee the seeder ran and seeded the
        // correct 6 bilingual trees at least once under some Grade-1 row.
        grade1SubjectCount.Should().BeGreaterThanOrEqualTo(6,
            "P8-02 bilingual seeder must seed at least 6 subject roots for Grade 1 " +
            "(Math/Ar, Math/En, Science/Ar, Science/En, Arabic/Ar, English/En); " +
            "allow > 6 when P2_01 creates extra Grade-1 rows (no unique constraint on Grade.Number). " +
            "If this fails, check that LearningSeeder.SeedAsync ran successfully in InitializeAsync.");

        // Verify all 4 stable SubjectCodes are seeded (MATH=0, SCIENCE=1, ARABIC=2, ENGLISH=3).
        var grade1SubjectCodes = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .Where(s => s.Grade.Number == 1)
            .Select(s => s.SubjectCode)
            .Distinct()
            .ToListAsync();

        grade1SubjectCodes.Should().Contain(SubjectCode.MATH,
            "MATH subject trees must be seeded for Grade 1");
        grade1SubjectCodes.Should().Contain(SubjectCode.SCIENCE,
            "SCIENCE subject trees must be seeded for Grade 1");
        grade1SubjectCodes.Should().Contain(SubjectCode.ARABIC,
            "ARABIC subject tree must be seeded for Grade 1");
        grade1SubjectCodes.Should().Contain(SubjectCode.ENGLISH,
            "ENGLISH subject tree must be seeded for Grade 1");

        // Verify that the 4 trees visible to an English-medium student are present.
        var mathEn = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .AnyAsync(s => s.SubjectCode == SubjectCode.MATH && s.Language == ContentLanguage.En && s.Grade.Number == 1);
        var scienceEn = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .AnyAsync(s => s.SubjectCode == SubjectCode.SCIENCE && s.Language == ContentLanguage.En && s.Grade.Number == 1);
        var arabicAr = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .AnyAsync(s => s.SubjectCode == SubjectCode.ARABIC && s.Language == ContentLanguage.Ar && s.Grade.Number == 1);
        var englishEn = await db.Subjects.AsNoTracking().Include(s => s.Grade)
            .AnyAsync(s => s.SubjectCode == SubjectCode.ENGLISH && s.Language == ContentLanguage.En && s.Grade.Number == 1);

        mathEn.Should().BeTrue("MATH/En tree must be seeded for Grade 1 (visible to English-medium student)");
        scienceEn.Should().BeTrue("SCIENCE/En tree must be seeded for Grade 1 (visible to English-medium student)");
        arabicAr.Should().BeTrue("ARABIC/Ar tree must be seeded for Grade 1 (always Ar per pinning rule)");
        englishEn.Should().BeTrue("ENGLISH/En tree must be seeded for Grade 1 (always En per pinning rule)");
    }
}

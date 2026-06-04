using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// Event-capture handler — scoped to this test class.
// Uses a separate static bag (P205_Captured) to isolate from P2_07 tests.
// =============================================================================

/// <summary>
/// Capturing handler for LessonCompletedIntegrationEvent used exclusively by
/// P2_05 tests.  Static ConcurrentBag is cleared in each test's arrange phase.
/// </summary>
public sealed class CapturingLessonCompletedHandlerP205
    : INotificationHandler<LessonCompletedIntegrationEvent>
{
    public static readonly ConcurrentBag<LessonCompletedIntegrationEvent> Captured = new();

    public Task Handle(LessonCompletedIntegrationEvent e, CancellationToken ct)
    {
        Captured.Add(e);
        return Task.CompletedTask;
    }
}

/// <summary>
/// P2-05 integration tests — "Open and Complete a Lesson".
///
/// Endpoints under test:
///   GET  /api/Learning/Lessons/{id}             [Authorize]   (NEW — P2-05)
///   GET  /api/Learning/Lessons?id={id}          (anonymous — back-compat)
///   POST /api/Learning/Quizzes/{lessonId}/Attempt    [Authorize(Roles="Student")]
///   POST /api/Learning/Quizzes/{attemptId}/Answers   [Authorize(Roles="Student")]
///   POST /api/Learning/Quizzes/{attemptId}/Complete  [Authorize(Roles="Student")]
///
/// Coverage map (11 test cases):
///   Case  1 → GetLesson_Anonymous_NewRoute_Returns401
///   Case  2 → GetLesson_SeededDemoLesson_ReturnsFullContent
///   Case  3 → GetLesson_QuickCheck_HasNoCorrectAnswerField
///   Case  4 → GetLesson_NonExistentId_Returns404
///   Case  5 → GetLesson_NonDemoLesson_ReturnsNullContentAndNullQuickCheck
///   Case  6 → GetLesson_Envelope_SuccessedCamelCase
///   Case  7 → GetLesson_BackCompatRoute_StillWorks
///   Case  8 → EndToEnd_OpenAndCompleteLesson_AC3
///   Case  9 → EndToEnd_LessonCompletedIntegrationEvent_Fires
///   Case 10 → EndToEnd_WrongAnswer_IsCorrectFalse_LessonStillCompletes
///   Case 11 → Seeder_FourLessonsHaveNonNullContentAndQuestions
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_05_OpenAndCompleteLesson_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;

    // Primary client — used for all cases except the event-capture case.
    private readonly HttpClient _client;

    // Fork that also has the capturing handler registered — used for Cases 8, 9, 10.
    private readonly HttpClient _eventClient;

    // Math G1 demo lesson id — resolved once in InitializeAsync.
    private int _mathG1DemoLessonId;
    // Math G1 subject id — resolved once in InitializeAsync for AC3 NodeState check.
    private int _mathG1SubjectId;
    // Correct answer for Math demo lesson: JsonSerializer.Serialize("6") == "\"6\""
    private string _mathDemoCorrectAnswer = "\"6\"";
    // A non-demo lesson id (non-null content check).
    private int _nonDemoLessonId;

    public P2_05_OpenAndCompleteLesson_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();

        // Build a fork with the capturing handler registered so we can catch events in Cases 9.
        var fork = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<INotificationHandler<LessonCompletedIntegrationEvent>,
                    CapturingLessonCompletedHandlerP205>();
            });
        });
        _eventClient = fork.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Apply migrations + seed identity (idempotent across all tests sharing the collection).
        await _factory.ApplyMigrationsAndSeedAsync();

        // Run the Learning seeder directly — it only runs in Development inside
        // LearningModule.InitializeAsync, so we call it here as P2-11 does.
        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Resolve the Math G1 demo lesson id + subject id from the DB.
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var demoLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");

        demoLesson.Should().NotBeNull(
            "LearningSeeder.SeedAsync must have seeded 'Introduction to Counting (G1)'; " +
            "ensure the seeder ran successfully");

        _mathG1DemoLessonId = demoLesson!.Id;

        // P8-02: query by SubjectCode + Language — names are now grade-suffixed ("Math (G1)" etc.).
        // Students use learning_language="en" so the MATH/En tree is the relevant one.
        var mathG1Subject = await db.Subjects
            .AsNoTracking()
            .Include(s => s.Grade)
            .FirstOrDefaultAsync(s =>
                s.SubjectCode == SubjectCode.MATH &&
                s.Language == ContentLanguage.En &&
                s.Grade.Number == 1);

        mathG1Subject.Should().NotBeNull("Math/En G1 subject must be seeded");
        _mathG1SubjectId = mathG1Subject!.Id;

        // Find a lesson WITHOUT demo content (Explanation IS NULL) in Math G1.
        // "Place Value: Tens and Hundreds (G1)" is the second lesson in unit 1 — never demo-seeded.
        var nonDemoLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Explanation == null && l.Name == "Place Value: Tens and Hundreds (G1)");

        nonDemoLesson.Should().NotBeNull(
            "'Place Value: Tens and Hundreds (G1)' must exist and have no Explanation (non-demo lesson)");
        _nonDemoLessonId = nonDemoLesson!.Id;

        // Clear the static event bag at startup.
        CapturingLessonCompletedHandlerP205.Captured.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers — mirrored from P2-07 / P2-08 patterns
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p205_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? bearerToken = null)
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

    private async Task<(string Token, int UserId)> RegisterParentAndGetTokenAsync()
    {
        var email = UniqueEmail("parent");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tok).Should().BeTrue("body: {0}", body);
        TryProp(data, "userId", out var uid).Should().BeTrue("body: {0}", body);
        return (tok.GetString()!, uid.GetInt32());
    }

    /// <summary>
    /// Registers a parent, adds a child via Add-Child, and signs in as that child.
    /// Returns the Student JWT and the child's userId.  Mirrors P2-08 exactly.
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(string? suffix = null)
    {
        var (parentToken, _) = await RegisterParentAndGetTokenAsync();
        var childEmail = UniqueEmail(suffix ?? "child");

        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student P205",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "en", // P8-01: "en" so handler resolves MATH/En tree
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        var studentToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    /// <summary>
    /// Overload that uses a specific client (e.g. _eventClient) for the Add-Child + Sign-In calls.
    /// Required for Case 9 so the student's attempt goes through the fork that has the capturing handler.
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(HttpClient client)
    {
        var parentEmail = UniqueEmail("evtparent");
        var (regResp, regRoot, regBody) = await SendAsync(client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", regBody);
        var parentTok = parentTokProp.GetString()!;

        var childEmail = UniqueEmail("evtchild");
        var (addResp, addRoot, addBody) = await SendAsync(client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Event Test Student",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "en", // P8-01: "en" so handler resolves MATH/En tree
            },
            parentTok);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var childIdProp).Should().BeTrue("body: {0}", addBody);
        var childId = childIdProp.GetInt32();

        var (signInResp, signInRoot, signInBody) = await SendAsync(client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in must succeed; body: {0}", signInBody);
        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var studentTokProp).Should().BeTrue("body: {0}", signInBody);

        return (studentTokProp.GetString()!, childId);
    }

    /// <summary>Starts an attempt via the API (using the given client). Returns the attemptId.</summary>
    private async Task<int> StartAttemptViaApiAsync(HttpClient client, int lessonId, string studentToken)
    {
        var (resp, root, body) = await SendAsync(client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SubmitAnswerAsync(HttpClient client, int attemptId, int questionId, string answerPayload, string? token)
        => SendAsync(client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answerPayload, TimeSpentSeconds = 5, HintUsed = false },
            token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        CompleteAttemptAsync(HttpClient client, int attemptId, string? token)
        => SendAsync(client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    // =========================================================================
    // Case 1: Anonymous GET /api/Learning/Lessons/{id} → 401
    // =========================================================================

    [Fact(DisplayName = "P205-C01 Anonymous GET /api/Learning/Lessons/{id} (new route) → 401")]
    public async Task GetLesson_Anonymous_NewRoute_Returns401()
    {
        // No Authorization header — the new [Authorize] action must reject this.
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET /api/Learning/Lessons/{{id}} is decorated with [Authorize] (P2-05 Q5 decision); " +
            "an unauthenticated request must return 401; body: {0}", body);
    }

    // =========================================================================
    // Case 2: Authenticated GET on seeded demo lesson → 200 with full content
    // =========================================================================

    [Fact(DisplayName = "P205-C02 Authenticated GET on seeded Math G1 demo lesson → 200 with non-null Explanation, Visual, QuickCheck")]
    public async Task GetLesson_SeededDemoLesson_ReturnsFullContent()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c02");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "authenticated GET on a seeded demo lesson must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);

        // Explanation — must be non-null for the seeded demo lesson.
        TryProp(data, "explanation", out var explanationProp).Should().BeTrue(
            "data must have 'explanation' field; body: {0}", body);
        explanationProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "explanation must be non-null for the Math G1 seeded demo lesson; body: {0}", body);
        explanationProp.GetString().Should().NotBeNullOrWhiteSpace(
            "explanation must be a non-empty string; body: {0}", body);

        // Visual — must be non-null for the seeded demo lesson.
        TryProp(data, "visual", out var visualProp).Should().BeTrue(
            "data must have 'visual' field; body: {0}", body);
        visualProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "visual must be non-null for the Math G1 seeded demo lesson; body: {0}", body);
        visualProp.GetString().Should().NotBeNullOrWhiteSpace(
            "visual must be a non-empty string; body: {0}", body);

        // QuickCheck — must be non-null for the seeded demo lesson.
        TryProp(data, "quickCheck", out var quickCheck).Should().BeTrue(
            "data must have 'quickCheck' field; body: {0}", body);
        quickCheck.ValueKind.Should().NotBe(JsonValueKind.Null,
            "quickCheck must be non-null for the seeded demo lesson (it has one QuizQuestion); body: {0}", body);

        // QuickCheck.id must be present and > 0.
        TryProp(quickCheck, "id", out var qcId).Should().BeTrue(
            "quickCheck must have 'id'; body: {0}", body);
        qcId.GetInt32().Should().BeGreaterThan(0,
            "quickCheck.id must be a positive int; body: {0}", body);

        // QuickCheck.questionText must be present and non-empty.
        TryProp(quickCheck, "questionText", out var qcText).Should().BeTrue(
            "quickCheck must have 'questionText'; body: {0}", body);
        qcText.GetString().Should().NotBeNullOrWhiteSpace(
            "quickCheck.questionText must be non-empty; body: {0}", body);

        // QuickCheck.options must be present (it's a JSON string of a JSON array).
        TryProp(quickCheck, "options", out var qcOptions).Should().BeTrue(
            "quickCheck must have 'options'; body: {0}", body);
        qcOptions.GetString().Should().NotBeNullOrWhiteSpace(
            "quickCheck.options must be non-empty; body: {0}", body);
    }

    // =========================================================================
    // Case 3: CorrectAnswer must NOT appear anywhere in the QuickCheck response
    // =========================================================================

    [Fact(DisplayName = "P205-C03 QuickCheck in GetLesson response NEVER contains 'correctAnswer' field (security guard)")]
    public async Task GetLesson_QuickCheck_HasNoCorrectAnswerField()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c03");

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "request must succeed to perform the security check; body: {0}", body);

        // Case-insensitive check — the JSON must NOT contain "correctAnswer" at any depth.
        // QuizProfile.cs excludes CorrectAnswer via ForSourceMember(...DoNotValidate()).
        body.ToLowerInvariant().Should().NotContain("\"correctanswer\"",
            "CorrectAnswer must NEVER appear in the QuickCheck DTO " +
            "(QuizProfile.cs excludes it via ForSourceMember DoNotValidate); " +
            "this is the AC1 security guard from P2-05 Q4; body: {0}", body);

        // Also walk the JSON document to catch any nested key at any depth.
        using var doc = JsonDocument.Parse(body);
        AssertNoCorrectAnswerKeyDeep(doc.RootElement, body);
    }

    /// <summary>Recursively asserts that no JSON element has a key named "correctAnswer" (case-insensitive).</summary>
    private static void AssertNoCorrectAnswerKeyDeep(JsonElement element, string rawBody)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                prop.Name.Should().NotBeEquivalentTo("correctAnswer",
                    "CorrectAnswer must never be serialized to the client; raw body: {0}", rawBody);
                AssertNoCorrectAnswerKeyDeep(prop.Value, rawBody);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                AssertNoCorrectAnswerKeyDeep(item, rawBody);
        }
    }

    // =========================================================================
    // Case 4: Non-existent lesson id → 404 (not 500)
    // =========================================================================

    [Fact(DisplayName = "P205-C04 GET /api/Learning/Lessons/999999 with valid JWT → 404 NotFound (not 500)")]
    public async Task GetLesson_NonExistentId_Returns404()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c04");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            "api/Learning/Lessons/999999", null, token);

        ((int)resp.StatusCode).Should().NotBe(500,
            "handler must never return 500 for a missing id; body: {0}", body);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "GetLessonQueryHandler must return NotFound (with SharedResourcesKey.LessonNotFound) " +
            "when the lesson id does not exist; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("successed must be false for 404; body: {0}", body);
    }

    // =========================================================================
    // Case 5: Non-demo lesson → 200 with explanation:null, visual:null, quickCheck:null
    // =========================================================================

    [Fact(DisplayName = "P205-C05 GET on non-demo lesson → 200 with explanation=null, visual=null, quickCheck=null")]
    public async Task GetLesson_NonDemoLesson_ReturnsNullContentAndNullQuickCheck()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c05");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_nonDemoLessonId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "a lesson without seeded content must still return 200 (content-absence is not an error); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // explanation must be null for a non-demo lesson.
        TryProp(data, "explanation", out var explanationProp).Should().BeTrue(
            "data must have 'explanation' key even when null; body: {0}", body);
        explanationProp.ValueKind.Should().Be(JsonValueKind.Null,
            "explanation must be null for a non-demo/unseeded lesson; body: {0}", body);

        // visual must be null.
        TryProp(data, "visual", out var visualProp).Should().BeTrue(
            "data must have 'visual' key even when null; body: {0}", body);
        visualProp.ValueKind.Should().Be(JsonValueKind.Null,
            "visual must be null for a non-demo/unseeded lesson; body: {0}", body);

        // quickCheck must be null (no QuizQuestions seeded for non-demo lessons).
        TryProp(data, "quickCheck", out var quickCheck).Should().BeTrue(
            "data must have 'quickCheck' key even when null; body: {0}", body);
        quickCheck.ValueKind.Should().Be(JsonValueKind.Null,
            "quickCheck must be null for a lesson that has no QuizQuestions; body: {0}", body);
    }

    // =========================================================================
    // Case 6: Envelope camelCase "successed" key is present
    // =========================================================================

    [Fact(DisplayName = "P205-C06 GET /api/Learning/Lessons/{id} response body contains '\"successed\":' (camelCase, sic spelling)")]
    public async Task GetLesson_Envelope_SuccessedCamelCase()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c06");

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "request must succeed to check the envelope; body: {0}", body);

        // Load-bearing CONVENTIONS.md check: the property is spelled "Successed" (not "Succeeded")
        // and serializes to camelCase "successed" (not "succeeded").
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> envelope must use the canonical 'successed' (sic, single-s) camelCase key; " +
            "do NOT rename to 'succeeded'; body: {0}", body);
    }

    // =========================================================================
    // Case 7: Back-compat route GET /api/Learning/Lessons?id={id} still works
    // =========================================================================

    [Fact(DisplayName = "P205-C07 Back-compat GET /api/Learning/Lessons?id={id} (anonymous, old route) → 200 with demo content populated")]
    public async Task GetLesson_BackCompatRoute_StillWorks()
    {
        // The old route is anonymous — no bearer token required.
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons?id={_mathG1DemoLessonId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "the back-compat ?id= route (kept for admin tooling) must still return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // The same handler is invoked — Explanation and Visual must be populated.
        TryProp(data, "explanation", out var explanationProp).Should().BeTrue(
            "back-compat route must return the same handler output; body: {0}", body);
        explanationProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "explanation must be non-null (same seeded demo lesson via the same handler); body: {0}", body);

        TryProp(data, "visual", out var visualProp).Should().BeTrue("body: {0}", body);
        visualProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "visual must be non-null via the back-compat route; body: {0}", body);

        // QuickCheck must also be populated.
        TryProp(data, "quickCheck", out var quickCheck).Should().BeTrue("body: {0}", body);
        quickCheck.ValueKind.Should().NotBe(JsonValueKind.Null,
            "quickCheck must be non-null via the back-compat route (same handler, same QuizQuestion); body: {0}", body);
    }

    // =========================================================================
    // Case 8: End-to-end AC3 completion flow
    //   GET Lessons/{id} → GET QuickCheck → StartAttempt → SubmitAnswer (correct)
    //   → CompleteAttempt → DB Attempt.Status==Completed → NodeState.Completed in Lessons API
    // =========================================================================

    [Fact(DisplayName = "P205-C08 E2E: Open lesson → start attempt → submit correct answer → complete → lesson NodeState.Completed (AC3)")]
    public async Task EndToEnd_OpenAndCompleteLesson_AC3()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("c08");

        // ── Step 1: GET lesson to obtain QuickCheck question id ──────────────
        var (getResp, getRoot, getBody) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}", null, token);

        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "lesson open (step 1) must succeed; body: {0}", getBody);

        TryProp(getRoot, "data", out var lessonData).Should().BeTrue("body: {0}", getBody);
        TryProp(lessonData, "quickCheck", out var quickCheck).Should().BeTrue("body: {0}", getBody);
        quickCheck.ValueKind.Should().NotBe(JsonValueKind.Null,
            "QuickCheck must be non-null on the Math demo lesson; body: {0}", getBody);

        TryProp(quickCheck, "id", out var qcIdProp).Should().BeTrue("body: {0}", getBody);
        var questionId = qcIdProp.GetInt32();

        // ── Step 2: Start attempt ─────────────────────────────────────────────
        var attemptId = await StartAttemptViaApiAsync(_client, _mathG1DemoLessonId, token);

        // ── Step 3: Submit correct answer ─────────────────────────────────────
        // The correct answer for Math "What number comes after 5?" is "6" → JSON-encoded: "\"6\""
        var (subResp, subRoot, subBody) = await SubmitAnswerAsync(_client, attemptId, questionId, _mathDemoCorrectAnswer, token);

        subResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer (correct) must return 200; body: {0}", subBody);
        TryProp(subRoot, "data", out var subData).Should().BeTrue("body: {0}", subBody);
        TryProp(subData, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", subBody);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "submitting the correct answer must yield isCorrect=true; body: {0}", subBody);

        // ── Step 4: Complete attempt ──────────────────────────────────────────
        var (complResp, complRoot, complBody) = await CompleteAttemptAsync(_client, attemptId, token);

        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", complBody);
        TryProp(complRoot, "successed", out var completedSucc).Should().BeTrue("body: {0}", complBody);
        completedSucc.GetBoolean().Should().BeTrue("successed must be true; body: {0}", complBody);

        TryProp(complRoot, "data", out var complData).Should().BeTrue("body: {0}", complBody);
        TryProp(complData, "status", out var statusProp).Should().BeTrue("body: {0}", complBody);
        statusProp.GetString().Should().Be("Completed",
            "CompleteAttempt response must surface status='Completed'; body: {0}", complBody);

        // ── Step 5: Verify Attempt row in DB has Status == Completed ─────────
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var attemptRow = await db.Attempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        attemptRow.Should().NotBeNull("Attempt row must exist in DB; attemptId: {0}", attemptId);
        attemptRow!.Status.Should().Be(AttemptStatus.Completed,
            "Attempt.Status must be Completed after CompleteAttempt call (AC3); attemptId: {0}", attemptId);

        // ── Step 6: Verify completed lesson set via a join-based LINQ query ───
        // Attempt has no Lesson navigation property (module isolation — plain int LessonId).
        // Replicate what GetCompletedLessonIdsForStudentInSubjectAsync does: join through
        // Lesson → Unit to resolve subjectId.
        var completedIds = await (
            from a in db.Attempts.AsNoTracking()
            join l in db.Lessons.AsNoTracking() on a.LessonId equals l.Id
            join u in db.Units.AsNoTracking() on l.UnitId equals u.Id
            where a.StudentId == studentId
               && a.Status == AttemptStatus.Completed
               && u.SubjectId == _mathG1SubjectId
            select a.LessonId
        ).Distinct().ToListAsync();

        completedIds.Should().Contain(_mathG1DemoLessonId,
            "after CompleteAttempt, the demo lesson id must appear in completed lessons for the student " +
            "(this is what LearningPathEngine.GetCompletedLessonIdsForStudentInSubjectAsync queries); " +
            "studentId={0}, subjectId={1}", studentId, _mathG1SubjectId);

        // ── Step 7: GET /Subjects/{subjectId}/Lessons → lesson must be NodeState.Completed ──
        var (lessonsResp, lessonsRoot, lessonsBody) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Subjects/{_mathG1SubjectId}/Lessons", null, token);

        lessonsResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET Subjects/.../Lessons must succeed; body: {0}", lessonsBody);

        TryProp(lessonsRoot, "data", out var lessonsData).Should().BeTrue("body: {0}", lessonsBody);

        int? completedLessonState = null;
        foreach (var unit in lessonsData.EnumerateArray())
        {
            if (TryProp(unit, "lessons", out var lessons))
            {
                foreach (var lesson in lessons.EnumerateArray())
                {
                    if (TryProp(lesson, "lessonId", out var lessonIdEl) &&
                        lessonIdEl.GetInt32() == _mathG1DemoLessonId)
                    {
                        if (TryProp(lesson, "state", out var stateProp))
                            completedLessonState = stateProp.ValueKind == JsonValueKind.Number
                                ? stateProp.GetInt32()
                                : -1;
                        break;
                    }
                }
            }
            if (completedLessonState.HasValue) break;
        }

        completedLessonState.Should().NotBeNull(
            "the demo lesson must appear in GET Subjects/{id}/Lessons; lessonId={0}, body: {1}",
            _mathG1DemoLessonId, lessonsBody);
        completedLessonState!.Value.Should().Be((int)NodeState.Completed,
            "after CompleteAttempt the lesson NodeState must be Completed=2 (LearningPathEngine reads Attempt.Status); " +
            "body: {0}", lessonsBody);
    }

    // =========================================================================
    // Case 9: LessonCompletedIntegrationEvent fires on completion
    // =========================================================================

    [Fact(DisplayName = "P205-C09 E2E: CompleteAttempt on a demo lesson fires LessonCompletedIntegrationEvent with correct StudentId/LessonId/SkillId")]
    public async Task EndToEnd_LessonCompletedIntegrationEvent_Fires()
    {
        // Use the event-capturing fork client for this test.
        CapturingLessonCompletedHandlerP205.Captured.Clear();

        var (token, studentId) = await CreateStudentViaParentFlowAsync(_eventClient);

        // Step 1: GET lesson via event client.
        var (getResp, getRoot, getBody) = await SendAsync(_eventClient, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}", null, token);

        getResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "lesson open (step 1) must succeed via event client; body: {0}", getBody);

        TryProp(getRoot, "data", out var lessonData).Should().BeTrue("body: {0}", getBody);
        TryProp(lessonData, "quickCheck", out var quickCheck).Should().BeTrue("body: {0}", getBody);
        quickCheck.ValueKind.Should().NotBe(JsonValueKind.Null,
            "QuickCheck must be non-null on the demo lesson; body: {0}", getBody);

        TryProp(quickCheck, "id", out var qcIdProp).Should().BeTrue("body: {0}", getBody);
        var questionId = qcIdProp.GetInt32();

        // Step 2: Start attempt.
        var attemptId = await StartAttemptViaApiAsync(_eventClient, _mathG1DemoLessonId, token);

        // Step 3: Submit correct answer.
        await SubmitAnswerAsync(_eventClient, attemptId, questionId, _mathDemoCorrectAnswer, token);

        // Clear the bag just before Complete to isolate the LessonCompleted event.
        CapturingLessonCompletedHandlerP205.Captured.Clear();

        // Step 4: Complete attempt — this should fire LessonCompletedIntegrationEvent.
        var (complResp, _, complBody) = await CompleteAttemptAsync(_eventClient, attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", complBody);

        // Step 5: Assert the event was captured.
        CapturingLessonCompletedHandlerP205.Captured.Should().HaveCount(1,
            "exactly one LessonCompletedIntegrationEvent must be captured when lesson has a SkillId; " +
            "the demo lesson 'Introduction to Counting (G1)' has SkillId set");

        var evt = CapturingLessonCompletedHandlerP205.Captured.Single();

        evt.StudentId.Should().Be(studentId,
            "event StudentId must match the JWT-derived student id");
        evt.LessonId.Should().Be(_mathG1DemoLessonId,
            "event LessonId must match the demo lesson id");
        evt.SkillId.Should().BeGreaterThan(0,
            "event SkillId must be a positive int (the lesson's linked skill)");
        evt.CorrectAnswerCount.Should().Be(1,
            "1 correct answer submitted → CorrectAnswerCount=1");
        evt.AccuracyPercentage.Should().BeInRange(0, 100,
            "AccuracyPercentage must be in 0..100; 1/1 correct = 100");
        evt.EventId.Should().NotBe(Guid.Empty,
            "EventId must be a fresh non-empty Guid");
        evt.OccurredOnUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(60),
            "OccurredOnUtc must be recent");
    }

    // =========================================================================
    // Case 10: Wrong answer → isCorrect=false, attempt still completes
    // =========================================================================

    [Fact(DisplayName = "P205-C10 E2E: Submit wrong answer → isCorrect=false; CompleteAttempt still returns Status=Completed")]
    public async Task EndToEnd_WrongAnswer_IsCorrectFalse_LessonStillCompletes()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("c10");

        // Step 1: GET lesson to obtain questionId.
        var (getResp, getRoot, getBody) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Lessons/{_mathG1DemoLessonId}", null, token);

        getResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", getBody);
        TryProp(getRoot, "data", out var lessonData).Should().BeTrue("body: {0}", getBody);
        TryProp(lessonData, "quickCheck", out var quickCheck).Should().BeTrue("body: {0}", getBody);
        TryProp(quickCheck, "id", out var qcIdProp).Should().BeTrue("body: {0}", getBody);
        var questionId = qcIdProp.GetInt32();

        // Step 2: Start attempt.
        var attemptId = await StartAttemptViaApiAsync(_client, _mathG1DemoLessonId, token);

        // Step 3: Submit WRONG answer.
        // Math demo Q: "What number comes after 5?" correct = "6"; wrong = "4"
        var wrongAnswer = "\"4\"";
        var (subResp, subRoot, subBody) = await SubmitAnswerAsync(_client, attemptId, questionId, wrongAnswer, token);

        subResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer with a wrong answer must still return 200 (wrong is not an API error); body: {0}", subBody);
        TryProp(subRoot, "data", out var subData).Should().BeTrue("body: {0}", subBody);
        TryProp(subData, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", subBody);
        isCorrectProp.GetBoolean().Should().BeFalse(
            "wrong answer must yield isCorrect=false; body: {0}", subBody);

        // correctAnswer must be populated (non-null) when wrong.
        TryProp(subData, "correctAnswer", out var correctAnswerProp).Should().BeTrue(
            "correctAnswer must be present in the response when isCorrect=false; body: {0}", subBody);
        correctAnswerProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "correctAnswer must not be null when isCorrect=false; body: {0}", subBody);
        correctAnswerProp.GetString().Should().NotBeNullOrWhiteSpace(
            "correctAnswer must be non-empty; body: {0}", subBody);

        // Step 4: Complete attempt — must still succeed (low accuracy does not block completion).
        var (complResp, complRoot, complBody) = await CompleteAttemptAsync(_client, attemptId, token);

        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200 even with 0% accuracy (AC3: completion != mastery); body: {0}", complBody);
        TryProp(complRoot, "data", out var complData).Should().BeTrue("body: {0}", complBody);
        TryProp(complData, "status", out var statusProp).Should().BeTrue("body: {0}", complBody);
        statusProp.GetString().Should().Be("Completed",
            "status must be Completed regardless of answer accuracy; body: {0}", complBody);

        // Step 5: Verify Attempt row in DB has Status == Completed.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var attemptRow = await db.Attempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attemptId);

        attemptRow.Should().NotBeNull("Attempt row must exist; attemptId: {0}", attemptId);
        attemptRow!.Status.Should().Be(AttemptStatus.Completed,
            "Attempt.Status must be Completed in the DB; attemptId: {0}", attemptId);
        attemptRow.AccuracyPercentage.Should().Be(0.0,
            "AccuracyPercentage must be 0 (0 out of 1 correct); attemptId: {0}", attemptId);
    }

    // =========================================================================
    // Case 11: Seeder smoke check — 4 lessons have non-null Explanation + Visual + 4 questions
    // =========================================================================

    [Fact(DisplayName = "P205-C11 Seeder smoke check: DB has >= 4 lessons with non-null Explanation+Visual and >= 4 QuizQuestions seeded by demo seeder")]
    public async Task Seeder_FourLessonsHaveNonNullContentAndQuestions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Count lessons that have both Explanation and Visual seeded.
        var lessonsWithContent = await db.Lessons
            .AsNoTracking()
            .CountAsync(l => l.Explanation != null && l.Visual != null);

        lessonsWithContent.Should().BeGreaterThanOrEqualTo(4,
            "LearningSeeder.SeedDemoLessonContentAsync must seed Explanation + Visual for exactly 4 demo lessons " +
            "(one per subject: Math/Science/Arabic/English Grade-1 root lessons); " +
            "allow >= 4 for future additions; count found: {0}", lessonsWithContent);

        // Verify all 4 named demo lessons specifically.
        var demoLessonNames = new[]
        {
            "Introduction to Counting (G1)",
            "What Are Living Things? (G1)",
            "Arabic Alphabet Review (G1)",
            "Sight Words and Fluency (G1)",
        };

        foreach (var name in demoLessonNames)
        {
            var lesson = await db.Lessons
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Name == name);

            lesson.Should().NotBeNull(
                "seeder must have created lesson '{0}' (P2-10 stable key)", name);
            lesson!.Explanation.Should().NotBeNullOrWhiteSpace(
                "seeder must have populated Explanation for '{0}'", name);
            lesson.Visual.Should().NotBeNullOrWhiteSpace(
                "seeder must have populated Visual for '{0}'", name);
        }

        // Each demo lesson must have exactly one QuizQuestion seeded.
        foreach (var name in demoLessonNames)
        {
            var lesson = await db.Lessons.AsNoTracking().FirstAsync(l => l.Name == name);
            var questionCount = await db.QuizQuestions
                .AsNoTracking()
                .CountAsync(q => q.LessonId == lesson.Id);

            questionCount.Should().BeGreaterThanOrEqualTo(1,
                "seeder must have inserted at least one QuizQuestion for demo lesson '{0}'", name);
        }

        // Total QuizQuestions from demo seeder should be >= 4.
        // We count only the questions attached to the 4 known demo lesson names.
        var demoLessonIds = await db.Lessons
            .AsNoTracking()
            .Where(l => demoLessonNames.Contains(l.Name))
            .Select(l => l.Id)
            .ToListAsync();

        var totalDemoQuestions = await db.QuizQuestions
            .AsNoTracking()
            .CountAsync(q => demoLessonIds.Contains(q.LessonId));

        totalDemoQuestions.Should().BeGreaterThanOrEqualTo(4,
            "each of the 4 demo lessons must have at least 1 QuizQuestion seeded; " +
            "total demo questions found: {0}", totalDemoQuestions);
    }
}

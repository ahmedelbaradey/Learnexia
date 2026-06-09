using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// ============================================================================
// Event-capture infrastructure — test-local INotificationHandler registrations
// These are registered via a tiny WebApplicationFactory subclass so they do NOT
// pollute LearnexiaWebAppFactory (shared by all other test suites).
// ============================================================================

/// <summary>
/// Capturing handler for AnswerSubmittedIntegrationEvent.
/// Static ConcurrentBag is cleared in each test's InitializeAsync.
/// </summary>
public sealed class CapturingAnswerSubmittedHandler
    : INotificationHandler<AnswerSubmittedIntegrationEvent>
{
    public static readonly ConcurrentBag<AnswerSubmittedIntegrationEvent> Captured = new();

    public Task Handle(AnswerSubmittedIntegrationEvent e, CancellationToken ct)
    {
        Captured.Add(e);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Capturing handler for LessonCompletedIntegrationEvent.
/// Static ConcurrentBag is cleared in each test's InitializeAsync.
/// </summary>
public sealed class CapturingLessonCompletedHandler
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
/// Deliberately-throwing handler — used by the handler-isolation test (Case 11).
/// Proves IsolatedNotificationPublisher lets other handlers run even when this one throws.
/// </summary>
public sealed class ThrowingAnswerSubmittedHandler
    : INotificationHandler<AnswerSubmittedIntegrationEvent>
{
    public Task Handle(AnswerSubmittedIntegrationEvent e, CancellationToken ct)
        => throw new InvalidOperationException("P2-07 test: deliberate throw from ThrowingAnswerSubmittedHandler");
}


/// <summary>
/// P2-07 integration tests — "Instant Answer Feedback".
///
/// Event capture infrastructure:
///   Uses LearnexiaWebAppFactory.WithWebHostBuilder(...) to register capturing INotificationHandler
///   implementations in the DI container for the duration of this test class.
///   The handlers capture events into static ConcurrentBags which are cleared in InitializeAsync.
///
/// This keeps LearnexiaWebAppFactory clean (no modification to the shared factory).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_07_InstantAnswerFeedback_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URLs (same as P2-08)
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;

    // A derived client that has the capturing handlers registered.
    // We use WithWebHostBuilder to layer extra DI registrations on top of the shared factory.
    private readonly HttpClient _client;

    public P2_07_InstantAnswerFeedback_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;

        // Build a client from a fork of the factory that adds the capturing handlers.
        // WithWebHostBuilder returns a new factory instance that inherits all the parent's
        // configuration (Postgres override, rate-limit override) and layers extras on top.
        var fork = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Register the capturing handlers as singletons so we can inspect the static bags.
                services.AddSingleton<INotificationHandler<AnswerSubmittedIntegrationEvent>,
                    CapturingAnswerSubmittedHandler>();
                services.AddSingleton<INotificationHandler<LessonCompletedIntegrationEvent>,
                    CapturingLessonCompletedHandler>();
            });
        });

        _client = fork.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Apply migrations + seed (idempotent — runs once across the collection).
        await _factory.ApplyMigrationsAndSeedAsync();

        // Clear the event bags so each test starts from a clean state.
        CapturingAnswerSubmittedHandler.Captured.Clear();
        CapturingLessonCompletedHandler.Captured.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers — mirror P2-08 helpers exactly
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p207_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
    /// Registers a parent, adds a child, signs in as that child.
    /// Returns the Student JWT and the child's userId.
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync()
    {
        var (parentToken, _) = await RegisterParentAndGetTokenAsync();
        var childEmail = UniqueEmail("child");

        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 3,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "ar", // P8-01: required
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

    /// <summary>Seeds Grade → Subject → Unit → Lesson hierarchy. Returns lessonId.</summary>
    private async Task<int> SeedLessonAsync(string lessonName = "Test Lesson", int? skillId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        var grade = new Grade { Number = 99, DisplayName = $"G_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject { Name = $"S_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Learnexia.Modules.Learning.Domain.Entities.Unit { Name = $"U_{Guid.NewGuid():N}", SequenceOrder = 1, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = lessonName,
            Difficulty     = DifficultyLevel.Easy,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
            SkillId        = skillId,   // null = no skill; non-null = lesson linked to a skill
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0,
        };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        return lesson.Id;
    }

    /// <summary>
    /// Seeds QuizQuestion rows for the given lessonId with explicit QuestionType support.
    /// Returns the list of created question IDs.
    /// </summary>
    private async Task<List<int>> SeedQuestionsAsync(
        int lessonId,
        IEnumerable<(string QuestionText, string Options, string CorrectAnswer, int? SkillId, QuestionType QuestionType)> questions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;
        var ids = new List<int>();
        foreach (var (qText, opts, correctAnswer, skillId, questionType) in questions)
        {
            var q = new QuizQuestion
            {
                LessonId       = lessonId,
                SkillId        = skillId,
                QuestionType   = questionType,
                QuestionText   = qText,
                Options        = opts,
                CorrectAnswer  = correctAnswer,
                Difficulty     = DifficultyLevel.Easy,
                GeneratedBy    = GeneratedBy.Curated,
                IsActive       = true,
                LifecycleState = LifecycleState.Published,
                CreatedAt      = now,
                CreatedBy      = 0,
            };
            await db.QuizQuestions.AddAsync(q);
            await db.SaveChangesAsync();
            ids.Add(q.Id);
        }
        return ids;
    }

    /// <summary>
    /// Overload that defaults to MCQ (backward-compatible with P2-08-style callers in this file).
    /// </summary>
    private Task<List<int>> SeedMcqQuestionsAsync(
        int lessonId,
        IEnumerable<(string QuestionText, string Options, string CorrectAnswer, int? SkillId)> questions)
        => SeedQuestionsAsync(lessonId,
            questions.Select(q => (q.QuestionText, q.Options, q.CorrectAnswer, q.SkillId, QuestionType.MCQ)));

    /// <summary>Seeds a Skill entity. Returns new Skill.Id.</summary>
    private async Task<int> SeedSkillAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        var grade = new Grade { Number = 66, DisplayName = $"SkillG_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject { Name = $"SkillS_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"SkillC_{Guid.NewGuid():N}",
            DifficultyLevel = DifficultyLevel.Easy,
            SubjectId       = subject.Id,
            CreatedAt       = now,
            CreatedBy       = 0,
        };
        await db.Concepts.AddAsync(concept);
        await db.SaveChangesAsync();

        var skill = new Skill
        {
            Name                 = $"Skill_{Guid.NewGuid():N}",
            MasteryThreshold     = 70,
            EstimatedTimeMinutes = 15,
            ConceptId            = concept.Id,
            CreatedAt            = now,
            CreatedBy            = 0,
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    /// <summary>Starts an attempt via the API. Returns the attemptId.</summary>
    private async Task<int> StartAttemptViaApiAsync(int lessonId, string studentToken)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SubmitAnswerAsync(int attemptId, int questionId, string answerPayload,
            int timeSpentSeconds, bool hintUsed, string? token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answerPayload, TimeSpentSeconds = timeSpentSeconds, HintUsed = hintUsed },
            token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        CompleteAttemptAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        AbandonAttemptAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Abandon", null, token);

    // =========================================================================
    // Case 1: MCQ correct → isCorrect=true, correctAnswer=null
    // =========================================================================

    [Fact(DisplayName = "P207-C01 SubmitAnswer MCQ correct → isCorrect=true, correctAnswer=null, AnswerSubmittedEvent captured with CorrectAnswerCount=1")]
    public async Task SubmitAnswer_MCQ_CorrectAnswer_IsCorrectTrue()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C01-MCQ-Correct");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Capital of Egypt?", "[\"Cairo\",\"Giza\",\"Alex\",\"Luxor\"]", "\"Cairo\"", (int?)skillId),
        });
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Clear bag just before the assertion window so we get a clean delta.
        CapturingAnswerSubmittedHandler.Captured.Clear();

        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"Cairo\"", 10, false, token);

        // --- HTTP 200 + envelope ---
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "correct MCQ must return 200; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // --- isCorrect=true ---
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", body);
        isCorrectProp.GetBoolean().Should().BeTrue("MCQ correct answer must set isCorrect=true; body: {0}", body);

        // --- correctAnswer=null when correct ---
        if (TryProp(data, "correctAnswer", out var caProp))
            caProp.ValueKind.Should().Be(JsonValueKind.Null,
                "correctAnswer must be null when isCorrect=true; body: {0}", body);

        // --- Event captured with CorrectAnswerCount=1 ---
        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1,
            "exactly one AnswerSubmittedIntegrationEvent must be captured for a question with SkillId");

        var evt = CapturingAnswerSubmittedHandler.Captured.Single();
        evt.CorrectAnswerCount.Should().Be(1, "correct MCQ must produce CorrectAnswerCount=1");
        evt.SkillId.Should().Be(skillId, "event SkillId must match the question's SkillId");
        evt.LessonId.Should().Be(lessonId, "event LessonId must match the attempt's LessonId");
        evt.StudentId.Should().Be(studentId, "event StudentId must match the student");
    }

    // =========================================================================
    // Case 2: MCQ wrong → isCorrect=false, correctAnswer populated, event with CorrectAnswerCount=0
    // =========================================================================

    [Fact(DisplayName = "P207-C02 SubmitAnswer MCQ wrong → isCorrect=false, correctAnswer populated, event with CorrectAnswerCount=0")]
    public async Task SubmitAnswer_MCQ_WrongAnswer_IsCorrectFalse()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C02-MCQ-Wrong");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Capital of France?", "[\"Berlin\",\"Paris\",\"Rome\",\"Madrid\"]", "\"Paris\"", (int?)skillId),
        });
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingAnswerSubmittedHandler.Captured.Clear();

        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"Berlin\"", 15, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "wrong MCQ must still return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", body);
        isCorrectProp.GetBoolean().Should().BeFalse("wrong MCQ must set isCorrect=false; body: {0}", body);

        // correctAnswer must be populated
        TryProp(data, "correctAnswer", out var caProp).Should().BeTrue(
            "correctAnswer must be present when isCorrect=false; body: {0}", body);
        caProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "correctAnswer must not be null when isCorrect=false; body: {0}", body);
        caProp.GetString().Should().NotBeNullOrWhiteSpace(
            "correctAnswer must be a non-empty string when wrong; body: {0}", body);

        // Event with CorrectAnswerCount=0
        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1,
            "AnswerSubmittedIntegrationEvent must still fire for a wrong answer with SkillId");
        CapturingAnswerSubmittedHandler.Captured.Single().CorrectAnswerCount.Should().Be(0,
            "wrong answer must produce CorrectAnswerCount=0");
    }

    // =========================================================================
    // Case 3: TrueFalse "true" vs stored "true" (JSON boolean) → isCorrect=true
    //
    // Storage note: CorrectAnswer is a jsonb column. For TrueFalse, the value
    // "true" (lowercase) is stored as a JSON boolean — valid JSON. Npgsql reads
    // it back as the C# string "true". bool.TryParse("true") = true ✓.
    // The student submits "true" (raw, no JSON string encoding).
    // =========================================================================

    [Fact(DisplayName = "P207-C03 SubmitAnswer TrueFalse 'true' vs stored 'true' (JSON bool) → isCorrect=true (bool.TryParse)")]
    public async Task SubmitAnswer_TrueFalse_CorrectAnswer_IsCorrectTrue()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C03-TrueFalse");
        // CorrectAnswer = "true" (valid JSON boolean — stored as jsonb, read back as "true")
        // Options = valid JSON array of strings (required by jsonb column — not validated for TrueFalse)
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("Is the Earth round?", "[\"true\",\"false\"]", "true", (int?)skillId, QuestionType.TrueFalse),
        });
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingAnswerSubmittedHandler.Captured.Clear();

        // Submit "true" — matches stored "true" via bool.TryParse (both parse to true)
        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "true", 8, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", body);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "TrueFalse 'true' vs stored 'true' must be isCorrect=true (both parse via bool.TryParse); body: {0}", body);

        // Event must have been captured
        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1,
            "AnswerSubmittedIntegrationEvent must be captured for TrueFalse correct answer with SkillId");
        CapturingAnswerSubmittedHandler.Captured.Single().CorrectAnswerCount.Should().Be(1,
            "CorrectAnswerCount must be 1 for a correct TrueFalse answer");
    }

    // =========================================================================
    // Case 4: FillInBlank OrdinalIgnoreCase — stored "\"cairo\"" (JSON string),
    //         student submits "\"CAIRO\"" (JSON string in different case) → isCorrect=true
    //
    // Storage note: CorrectAnswer is a jsonb column. The value is stored as a JSON
    // string "\"cairo\"" (C# literal), which Postgres stores as jsonb string "cairo".
    // Npgsql reads it back as C# string "\"cairo\"" (with embedded quotes).
    // The student submits "\"CAIRO\"" — also a JSON string. Both sides have matching
    // embedded quotes, so OrdinalIgnoreCase comparison succeeds after Trim().
    //
    // The unit-level trim test (spaces) is covered by AnswerComparatorTests; here we
    // confirm the end-to-end case-insensitive match works at the HTTP level.
    // =========================================================================

    [Fact(DisplayName = "P207-C04 SubmitAnswer FillInBlank '\"CAIRO\"' vs stored '\"cairo\"' → isCorrect=true (OrdinalIgnoreCase, JSON-encoded values)")]
    public async Task SubmitAnswer_FillInBlank_TrimAndCaseInsensitive_IsCorrectTrue()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C04-FillInBlank");
        // CorrectAnswer as jsonb: store JSON string "cairo" using C# literal "\"cairo\""
        // Options: empty JSON array is valid jsonb
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("The capital of Egypt is ___.", "[]", "\"cairo\"", (int?)skillId, QuestionType.FillInBlank),
        });
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingAnswerSubmittedHandler.Captured.Clear();

        // Submit "\"CAIRO\"" (JSON string, different case) — OrdinalIgnoreCase should match stored "\"cairo\""
        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"CAIRO\"", 12, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "isCorrect", out var isCorrectProp).Should().BeTrue("body: {0}", body);
        isCorrectProp.GetBoolean().Should().BeTrue(
            "FillInBlank OrdinalIgnoreCase: '\"CAIRO\"' vs stored '\"cairo\"' must be isCorrect=true; body: {0}", body);

        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1,
            "AnswerSubmittedIntegrationEvent must be captured for FillInBlank correct answer with SkillId");
    }

    // =========================================================================
    // Case 5: Question WITH SkillId → event captured with all correct fields
    // =========================================================================

    [Fact(DisplayName = "P207-C05 SubmitAnswer question WITH SkillId → AnswerSubmittedIntegrationEvent captured with correct StudentId, LessonId, SkillId, CorrectAnswerCount")]
    public async Task SubmitAnswer_WithSkillId_PublishesAnswerSubmittedEvent()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C05-EventFull");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Full event check?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)skillId),
        });
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingAnswerSubmittedHandler.Captured.Clear();

        var (resp, _, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"A\"", 10, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Assert event captured with ALL correct fields
        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1,
            "exactly one event must be captured; body: {0}", body);

        var evt = CapturingAnswerSubmittedHandler.Captured.Single();
        evt.StudentId.Should().Be(studentId, "event StudentId must match the JWT-derived student id");
        evt.LessonId.Should().Be(lessonId, "event LessonId must match the attempt's lesson");
        evt.SkillId.Should().Be(skillId, "event SkillId must match the question's SkillId");
        evt.CorrectAnswerCount.Should().Be(1, "correct answer → CorrectAnswerCount=1");
        evt.EventId.Should().NotBe(Guid.Empty, "EventId must be a fresh Guid");
        evt.OccurredOnUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30),
            "OccurredOnUtc must be recent");
    }

    // =========================================================================
    // Case 6: Question WITHOUT SkillId → NO event captured; 200 still returned
    // =========================================================================

    [Fact(DisplayName = "P207-C06 SubmitAnswer question with SkillId=null → NO AnswerSubmittedIntegrationEvent captured; HTTP 200 still returned")]
    public async Task SubmitAnswer_WithoutSkillId_DoesNotPublishEvent()
    {
        var lessonId = await SeedLessonAsync("C06-NullSkill");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("No-skill question?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
        });
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingAnswerSubmittedHandler.Captured.Clear();

        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"A\"", 5, false, token);

        // Must still return 200 (null SkillId is not a user error — fail-soft)
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "null-SkillId question must still return 200 (fail-soft skip, not an error); body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // NO event must have been captured
        CapturingAnswerSubmittedHandler.Captured.Should().BeEmpty(
            "AnswerSubmittedIntegrationEvent must NOT be published when question.SkillId is null");
    }

    // =========================================================================
    // Case 7: Rejection paths → NO event captured
    //   7a: Duplicate question submit (second call → 424) — bag grows by exactly 1 (first only)
    //   7b: Ownership violation → 0 events
    //   7c: Non-InProgress attempt → 0 events (tested via completed attempt)
    // =========================================================================

    [Fact(DisplayName = "P207-C07 Rejection paths (duplicate/ownership/non-InProgress) → NO AnswerSubmittedIntegrationEvent on rejected calls")]
    public async Task SubmitAnswer_RejectionPath_DoesNotPublishEvent()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C07-Rejection");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Rejection test Q1?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)skillId),
            ("Rejection test Q2?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)skillId),
        });
        var (tokenA, _) = await CreateStudentViaParentFlowAsync();
        var (tokenB, _) = await CreateStudentViaParentFlowAsync();

        // --- 7a: Duplicate question submit ---
        var attemptId = await StartAttemptViaApiAsync(lessonId, tokenA);

        CapturingAnswerSubmittedHandler.Captured.Clear();

        // First submit — must succeed and publish event
        var (resp1, _, body1) = await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 5, false, tokenA);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first submit must succeed; body: {0}", body1);

        var countAfterFirst = CapturingAnswerSubmittedHandler.Captured.Count;
        countAfterFirst.Should().Be(1, "first submit must produce exactly 1 event");

        // Second submit for the SAME question — must return 424 (re-answer guard)
        var (resp2, root2, body2) = await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 5, false, tokenA);
        ((int)resp2.StatusCode).Should().Be(424,
            "duplicate question submit must return 424; body: {0}", body2);
        TryProp(root2, "successed", out var succFail).Should().BeTrue("body: {0}", body2);
        succFail.GetBoolean().Should().BeFalse("424 must have successed=false; body: {0}", body2);

        // Bag must NOT have grown (the 424 rejection must not fire the event)
        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(countAfterFirst,
            "rejected duplicate-question submit must NOT publish an additional event");

        // --- 7b: Ownership violation (Student B tries to submit to Student A's attempt) ---
        CapturingAnswerSubmittedHandler.Captured.Clear();

        var attemptIdB_owned_by_A = attemptId; // still student A's attempt
        var (ownershipResp, _, ownershipBody) = await SubmitAnswerAsync(
            attemptIdB_owned_by_A, questionIds[1], "\"B\"", 5, false, tokenB);

        ownershipResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ownership violation must return 401; body: {0}", ownershipBody);

        CapturingAnswerSubmittedHandler.Captured.Should().BeEmpty(
            "ownership-violation rejection must NOT publish AnswerSubmittedIntegrationEvent");

        // --- 7c: Non-InProgress attempt (already completed) ---
        CapturingAnswerSubmittedHandler.Captured.Clear();

        // Complete the attempt first
        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, tokenA);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK, "Complete must succeed; body: {0}", complBody);

        // Clear again to isolate from any LessonCompleted sub-events
        CapturingAnswerSubmittedHandler.Captured.Clear();

        // Now try to submit to a completed attempt
        var (nonInProgResp, _, nonInProgBody) = await SubmitAnswerAsync(
            attemptId, questionIds[1], "\"B\"", 5, false, tokenA);
        ((int)nonInProgResp.StatusCode).Should().Be(424,
            "submitting to a Completed attempt must return 424; body: {0}", nonInProgBody);

        CapturingAnswerSubmittedHandler.Captured.Should().BeEmpty(
            "non-InProgress attempt rejection must NOT publish AnswerSubmittedIntegrationEvent");
    }

    // =========================================================================
    // Case 8: CompleteAttempt with lesson SkillId → LessonCompletedIntegrationEvent captured
    //         with correct AccuracyPercentage and CorrectAnswerCount
    // =========================================================================

    [Fact(DisplayName = "P207-C08 CompleteAttempt with lesson SkillId → LessonCompletedIntegrationEvent captured with AccuracyPercentage and CorrectAnswerCount")]
    public async Task CompleteAttempt_WithLessonSkillId_PublishesLessonCompletedEvent()
    {
        var skillId  = await SeedSkillAsync();
        // Lesson is linked to the skill — LessonCompletedIntegrationEvent must fire
        var lessonId = await SeedLessonAsync("C08-LessonComplete", skillId: skillId);
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Q1 for complete?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)skillId),
            ("Q2 for complete?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)skillId),
            ("Q3 for complete?", "[\"A\",\"B\",\"C\",\"D\"]", "\"C\"", (int?)skillId),
        });
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit 2 correct + 1 wrong → AccuracyPercentage = 2/3 * 100 ≈ 66.67 → int round = 67
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 10, false, token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"B\"", 8, false, token);  // correct
        await SubmitAnswerAsync(attemptId, questionIds[2], "\"A\"", 12, false, token); // wrong (A≠C)

        CapturingLessonCompletedHandler.Captured.Clear();

        var (resp, root, body) = await CompleteAttemptAsync(attemptId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", body);

        // Give the async publish a moment to complete (it's in-process but synchronous in this path)
        // The publish happens synchronously inside the handler before the response, so no await needed.

        // Assert LessonCompletedIntegrationEvent captured
        CapturingLessonCompletedHandler.Captured.Should().HaveCount(1,
            "exactly one LessonCompletedIntegrationEvent must be captured when lesson has SkillId");

        var evt = CapturingLessonCompletedHandler.Captured.Single();
        evt.StudentId.Should().Be(studentId, "event StudentId must match");
        evt.LessonId.Should().Be(lessonId, "event LessonId must match");
        evt.SkillId.Should().Be(skillId, "event SkillId must match the lesson's SkillId");
        evt.CorrectAnswerCount.Should().Be(2, "2 correct answers → CorrectAnswerCount=2");
        // AccuracyPercentage: 2/3 * 100 = 66.67 → (int)Math.Round(66.67) = 67
        evt.AccuracyPercentage.Should().Be(67, "AccuracyPercentage must be (int)Math.Round(2/3*100) = 67");
    }

    // =========================================================================
    // Case 9: CompleteAttempt without lesson SkillId → NO LessonCompletedIntegrationEvent
    // =========================================================================

    [Fact(DisplayName = "P207-C09 CompleteAttempt lesson with SkillId=null → NO LessonCompletedIntegrationEvent; HTTP 200 still returned")]
    public async Task CompleteAttempt_WithoutLessonSkillId_DoesNotPublishEvent()
    {
        // Lesson has no SkillId
        var lessonId = await SeedLessonAsync("C09-NoSkillLesson", skillId: null);
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingLessonCompletedHandler.Captured.Clear();

        var (resp, root, body) = await CompleteAttemptAsync(attemptId, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200 even when lesson has no SkillId; body: {0}", body);
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        // NO event must have been captured
        CapturingLessonCompletedHandler.Captured.Should().BeEmpty(
            "LessonCompletedIntegrationEvent must NOT be published when lesson.SkillId is null");
    }

    // =========================================================================
    // Case 10: CompleteAttempt idempotent — event fires exactly once, NOT twice
    // =========================================================================

    [Fact(DisplayName = "P207-C10 CompleteAttempt idempotent: event fires exactly once, second Complete does NOT re-publish")]
    public async Task CompleteAttempt_Idempotent_DoesNotRepublishEvent()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C10-IdempotentComplete", skillId: skillId);
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingLessonCompletedHandler.Captured.Clear();

        // First Complete — must publish once
        var (resp1, _, body1) = await CompleteAttemptAsync(attemptId, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first Complete must succeed; body: {0}", body1);

        CapturingLessonCompletedHandler.Captured.Should().HaveCount(1,
            "first Complete must publish exactly 1 LessonCompletedIntegrationEvent");

        // Second Complete (idempotent) — must NOT re-publish
        var (resp2, root2, body2) = await CompleteAttemptAsync(attemptId, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second Complete (idempotent) must return 200; body: {0}", body2);
        TryProp(root2, "successed", out var successed).Should().BeTrue("body: {0}", body2);
        successed.GetBoolean().Should().BeTrue("idempotent Complete must succeed; body: {0}", body2);

        // Bag must still be 1 — idempotent re-Complete does NOT re-fire the event
        CapturingLessonCompletedHandler.Captured.Should().HaveCount(1,
            "idempotent second Complete must NOT publish an additional LessonCompletedIntegrationEvent");
    }

    // =========================================================================
    // Case 11: Handler isolation — throwing subscriber does NOT fail API request;
    //          capturing handler still receives the event (IsolatedNotificationPublisher).
    // =========================================================================

    [Fact(DisplayName = "P207-C11 Handler isolation: throwing INotificationHandler does NOT fail HTTP 200; capturing handler still receives the event")]
    public async Task HandlerIsolation_ThrowingSubscriber_DoesNotFailApiRequest()
    {
        // Create a new fork that also registers the throwing handler alongside the capturing handler.
        var forkWithThrower = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Register both the capturing AND the throwing handler.
                services.AddSingleton<INotificationHandler<AnswerSubmittedIntegrationEvent>,
                    CapturingAnswerSubmittedHandler>();
                services.AddSingleton<INotificationHandler<AnswerSubmittedIntegrationEvent>,
                    ThrowingAnswerSubmittedHandler>();
            });
        });
        var clientWithThrower = forkWithThrower.CreateClient();

        // Build a new student + lesson using the forked client
        async Task<(HttpResponseMessage, JsonElement, string)> SendViaFork(HttpMethod m, string url, object? b = null, string? tok = null)
        {
            var req = new HttpRequestMessage(m, url);
            if (b is not null)
                req.Content = new StringContent(JsonSerializer.Serialize(b), Encoding.UTF8, "application/json");
            if (tok is not null)
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);
            var r = await clientWithThrower.SendAsync(req);
            var s = await r.Content.ReadAsStringAsync();
            JsonElement root = default;
            if (!string.IsNullOrWhiteSpace(s))
            {
                try { root = JsonDocument.Parse(s).RootElement; } catch { }
            }
            return (r, root, s);
        }

        // Register parent + child using forked client
        var parentEmail = UniqueEmail("c11parent");
        var (regResp, regRoot, regBody) = await SendViaFork(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        regResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent reg must succeed; body: {0}", regBody);
        TryProp(regRoot, "data", out var regData);
        TryProp(regData, "accessToken", out var parentTokProp);
        var parentTok = parentTokProp.GetString()!;

        var childEmail = UniqueEmail("c11child");
        var (addResp, addRoot, addBody) = await SendViaFork(HttpMethod.Post, AddChildUrl,
            new { FullName = "C11 Student", Email = childEmail, Password = ValidChildPassword, Grade = 3, Language = "ar", Country = "EG", LearningLanguage = "ar" },
            parentTok);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"Add-Child must succeed; body: {addBody}");

        var (signInResp, signInRoot, signInBody) = await SendViaFork(HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in must succeed; body: {0}", signInBody);
        TryProp(signInRoot, "data", out var signInData);
        TryProp(signInData, "accessToken", out var studentTokProp);
        var studentTok = studentTokProp.GetString()!;

        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("C11-Isolation");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Isolation test Q?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)skillId),
        });

        // Start attempt via forked client
        var (startResp, startRoot, startBody) = await SendViaFork(HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentTok);
        startResp.StatusCode.Should().Be(HttpStatusCode.OK, "StartAttempt must succeed; body: {0}", startBody);
        TryProp(startRoot, "data", out var startData);
        TryProp(startData, "attemptId", out var attemptIdProp);
        var attemptId = attemptIdProp.GetInt32();

        CapturingAnswerSubmittedHandler.Captured.Clear();

        // Submit answer via forked client — ThrowingHandler will throw, but IsolatedNotificationPublisher
        // must catch it and let CapturingHandler still run.
        var (submitResp, submitRoot, submitBody) = await SendViaFork(HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionIds[0], AnswerPayload = "\"A\"", TimeSpentSeconds = 5, HintUsed = false },
            studentTok);

        // HTTP 200 must be returned despite the throwing handler
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "IsolatedNotificationPublisher must ensure a throwing handler does NOT fail the API response; body: {0}", submitBody);
        TryProp(submitRoot, "successed", out var successed).Should().BeTrue("body: {0}", submitBody);
        successed.GetBoolean().Should().BeTrue(
            "successed must be true despite the throwing handler; body: {0}", submitBody);

        // Capturing handler must still have received the event (isolation fan-out works)
        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1,
            "the capturing handler must have received the event even though the throwing handler threw");
    }

    // =========================================================================
    // Case 12: Regression — P2-08 envelope contract intact
    //          Quick SubmitAnswer + CompleteAttempt flow confirms BaseResponse<T>
    //          "successed" key is present in both responses.
    // =========================================================================

    [Fact(DisplayName = "P207-C12 Regression: existing P2-08 envelope contract intact — 'successed' key present in SubmitAnswer and CompleteAttempt responses")]
    public async Task ExistingContract_EnvelopeShapeIntact_SuccessedKeyPresent()
    {
        var lessonId = await SeedLessonAsync("C12-Regression");
        var questionIds = await SeedMcqQuestionsAsync(lessonId, new[]
        {
            ("Regression Q?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
        });
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        var (submitResp, _, submitBody) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"A\"", 5, false, token);

        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must return 200; body: {0}", submitBody);

        // Confirm "successed" key (sic — the canonical spelling per CONVENTIONS.md)
        submitBody.Should().Contain("\"successed\":",
            "BaseResponse<T> envelope must contain 'successed' key (sic spelling); body: {0}", submitBody);

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", complBody);
        complBody.Should().Contain("\"successed\":",
            "BaseResponse<T> envelope must contain 'successed' key in CompleteAttempt response; body: {0}", complBody);
    }

    // =========================================================================
    // Bonus: Abandon does NOT publish LessonCompletedIntegrationEvent
    // =========================================================================

    [Fact(DisplayName = "P207-BONUS AbandonAttempt does NOT publish LessonCompletedIntegrationEvent")]
    public async Task AbandonAttempt_DoesNotPublishLessonCompletedEvent()
    {
        var skillId  = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("BONUS-Abandon", skillId: skillId);
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        CapturingLessonCompletedHandler.Captured.Clear();

        var (resp, root, body) = await AbandonAttemptAsync(attemptId, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AbandonAttempt must return 200; body: {0}", body);

        // Abandonment is NOT lesson completion — no LessonCompletedIntegrationEvent must fire.
        CapturingLessonCompletedHandler.Captured.Should().BeEmpty(
            "AbandonAttempt must NOT publish LessonCompletedIntegrationEvent — abandonment is not lesson completion");
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-06 integration tests — "Take a Quiz" (StartAttempt).
///
/// Endpoint under test:
///   POST /api/Learning/Quizzes/{lessonId}/Attempt   [Authorize(Roles = "Student")]
///
/// Returns BaseResponse&lt;StartAttemptResponse&gt; where StartAttemptResponse = { AttemptId, Questions[] }.
/// Questions omit CorrectAnswer (the key security assertion for this story).
///
/// Student authentication flow:
///   Register Parent → POST /api/Parent/Add-Child → sign in as that child → Student JWT.
///
/// Coverage map (acceptance-criteria → test):
///
///   T1  Unauthenticated → 401
///         StartAttempt_Unauthenticated_Returns401
///
///   T2  Parent/Admin JWT → 403 (role-scoped to Student only)
///         StartAttempt_ParentJwt_Returns403
///
///   T3  Student happy path → 200, Successed=true, positive AttemptId; Attempt row persisted
///         StartAttempt_HappyPath_Returns200_WithAttemptId
///         StartAttempt_HappyPath_AttemptRowPersisted
///
///   T4  Answer-leakage check: CorrectAnswer NOT in response body
///         StartAttempt_CorrectAnswer_NotLeakedInResponse
///
///   T5  Resume: second StartAttempt returns same AttemptId, attempt count stays 1
///         StartAttempt_Resume_SameAttemptId_And_RowCountStaysOne
///
///   T6  Non-existent lesson → 404, Successed=false, no Attempt row
///         StartAttempt_NonExistentLesson_Returns404
///
///   T7  Empty quiz (lesson with no questions) → 200, Questions=[]
///         StartAttempt_EmptyQuiz_Returns200_WithEmptyQuestions
///
///   T8  Envelope casing: 'successed' (camelCase canonical spelling)
///         StartAttempt_Envelope_HasAllBaseResponseKeys
///         StartAttempt_Envelope_UsesSuccessedCamelCase
///
///   T9  Schema: learning."QuizQuestions"/"Attempts"/"StudentAnswers" exist; NO assessment schema
///         Schema_QuizQuestions_Exists
///         Schema_Attempts_Exists
///         Schema_StudentAnswers_Exists
///         Schema_NoAssessmentSchema
///
///   T10 Questions include Id/QuestionType/QuestionText/Options
///         StartAttempt_HappyPath_QuestionsHaveRequiredFields
///
///   T11 StudentId from JWT (not client-supplied)
///         StartAttempt_StudentId_ResolvedFromJwt_NotClientSupplied
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_06_StartAttempt_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URLs
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";

    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P2_06_StartAttempt_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p206_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    /// <summary>POST /api/Learning/Quizzes/{lessonId}/Attempt</summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        StartAttemptAsync(int lessonId, string? token = null)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{lessonId}/Attempt", null, token);

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
    /// Registers a parent, adds a child via Add-Child (which creates a Student-role account),
    /// and signs in as that child. Returns the Student JWT and the child's userId.
    ///
    /// This is the canonical Student auth flow: parent-driven onboarding.
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync()
    {
        var (parentToken, _) = await RegisterParentAndGetTokenAsync();
        var childEmail = UniqueEmail("child");

        // Add-Child: creates a Student-role account and auto-links it to the parent.
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

        // Sign in as the child (UserName is the email per Identity convention).
        var studentToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    /// <summary>
    /// Seeds the full hierarchy Grade→Subject→Unit→Lesson and returns the newly-created Lesson.Id.
    /// Must be called before StartAttempt because the handler verifies lesson existence with AnyAsync.
    /// </summary>
    private async Task<int> SeedLessonAsync(string lessonName = "Test Lesson")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 99,
            DisplayName = $"Test Grade {Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0,
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"Test Subject {Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0,
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit
        {
            Name          = $"Test Unit {Guid.NewGuid():N}",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now,
            CreatedBy     = 0,
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = lessonName,
            Difficulty     = DifficultyLevel.Easy,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
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
    /// Seeds QuizQuestion rows for the given lessonId and returns the lesson unchanged.
    /// Stamps CreatedAt/CreatedBy manually (test bypasses the SaveChangesAsync(userId) override).
    /// </summary>
    private async Task SeedQuestionsAsync(
        int lessonId,
        IEnumerable<(string QuestionText, string Options, string CorrectAnswer)> questions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;
        foreach (var (qText, opts, correctAnswer) in questions)
        {
            var q = new QuizQuestion
            {
                LessonId       = lessonId,
                QuestionType   = QuestionType.MCQ,
                QuestionText   = qText,
                Options        = opts,
                CorrectAnswer  = correctAnswer,
                Difficulty     = DifficultyLevel.Easy,
                GeneratedBy    = GeneratedBy.Curated,   // enum has Curated=1, AI=2 — NOT Admin
                IsActive       = true,
                LifecycleState = LifecycleState.Published,
                CreatedAt      = now,
                CreatedBy      = 0,
            };
            await db.QuizQuestions.AddAsync(q);
        }
        await db.SaveChangesAsync();
    }

    // =========================================================================
    // T1 — Unauthenticated → 401
    // =========================================================================

    [Fact(DisplayName = "QZ-T1 StartAttempt: no token → 401 Unauthorized")]
    public async Task StartAttempt_Unauthenticated_Returns401()
    {
        // Use any integer — the auth middleware fires before the handler.
        var (resp, _, body) = await StartAttemptAsync(lessonId: 1, token: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "StartAttempt without a JWT must return 401; body: {0}", body);
    }

    // =========================================================================
    // T2 — Parent/Admin JWT → 403 (endpoint is Student-only)
    // =========================================================================

    [Fact(DisplayName = "QZ-T2 StartAttempt: Parent JWT → 403 Forbidden (Student role required)")]
    public async Task StartAttempt_ParentJwt_Returns403()
    {
        var (parentToken, _) = await RegisterParentAndGetTokenAsync();
        var lessonId = await SeedLessonAsync("T2 Lesson");

        var (resp, _, body) = await StartAttemptAsync(lessonId, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "A Parent JWT must get 403 on the Student-only StartAttempt endpoint; body: {0}", body);
    }

    [Fact(DisplayName = "QZ-T2 StartAttempt: SuperAdmin JWT → 403 Forbidden (Student role required)")]
    public async Task StartAttempt_SuperAdminJwt_Returns403()
    {
        var adminToken = await SignInAndGetTokenAsync(SuperAdminUserName, SuperAdminPassword);
        var lessonId   = await SeedLessonAsync("T2-Admin Lesson");

        var (resp, _, body) = await StartAttemptAsync(lessonId, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "A SuperAdmin JWT must get 403 on the Student-only StartAttempt endpoint; body: {0}", body);
    }

    // =========================================================================
    // T3 — Student happy path: 200, Successed=true, positive AttemptId
    // =========================================================================

    [Fact(DisplayName = "QZ-T3a StartAttempt: Student JWT → 200, Successed=true, positive AttemptId")]
    public async Task StartAttempt_HappyPath_Returns200_WithAttemptId()
    {
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var lessonId          = await SeedLessonAsync("T3a Lesson");

        var (resp, root, body) = await StartAttemptAsync(lessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student StartAttempt must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null; body: {0}", body);

        TryProp(data, "attemptId", out var attemptIdProp).Should().BeTrue("data.attemptId required; body: {0}", body);
        attemptIdProp.GetInt32().Should().BeGreaterThan(0,
            "AttemptId must be a positive integer after commit; body: {0}", body);
    }

    // =========================================================================
    // T3b — Attempt row persisted with correct StudentId / LessonId / Status=InProgress
    // =========================================================================

    [Fact(DisplayName = "QZ-T3b StartAttempt: Attempt row created in learning.\"Attempts\" with correct StudentId/LessonId/Status=InProgress")]
    public async Task StartAttempt_HappyPath_AttemptRowPersisted()
    {
        var (studentToken, studentId) = await CreateStudentViaParentFlowAsync();
        var lessonId = await SeedLessonAsync("T3b Lesson");

        var (resp, root, body) = await StartAttemptAsync(lessonId, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        var attemptId = idProp.GetInt32();
        attemptId.Should().BeGreaterThan(0, "body: {0}", body);

        // Verify the DB row
        using var scope = _factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var attempt = await db.Attempts.SingleOrDefaultAsync(a => a.Id == attemptId);

        attempt.Should().NotBeNull(
            "Attempt row must exist in learning.\"Attempts\" after StartAttempt; attemptId: {0}", attemptId);
        attempt!.StudentId.Should().Be(studentId,
            "StudentId must match the JWT caller's userId; attemptId: {0}", attemptId);
        attempt.LessonId.Should().Be(lessonId,
            "LessonId must match the route parameter; attemptId: {0}", attemptId);
        attempt.Status.Should().Be(AttemptStatus.InProgress,
            "Status must be InProgress after StartAttempt; attemptId: {0}", attemptId);
    }

    // =========================================================================
    // T4 — Answer-leakage: CorrectAnswer is NOT in the response body (KEY security check)
    // =========================================================================

    [Fact(DisplayName = "QZ-T4 NOLEAK StartAttempt: CorrectAnswer is NOT present in the response body")]
    public async Task StartAttempt_CorrectAnswer_NotLeakedInResponse()
    {
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var lessonId          = await SeedLessonAsync("T4 Leak Lesson");

        // Use a highly distinctive correct-answer value that can never appear innocently
        const string distinctiveCorrectAnswer = "SECRETANSWER_XYZZY_NOTFORCLIENTS";
        await SeedQuestionsAsync(lessonId, new[]
        {
            ("Leak test Q1?", "[\"A\",\"B\",\"C\",\"D\"]", $"\"{distinctiveCorrectAnswer}\""),
        });

        var (resp, _, rawBody) = await StartAttemptAsync(lessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed so we can inspect the body; body: {0}", rawBody);

        // The raw body must NOT contain the seeded CorrectAnswer value anywhere
        rawBody.Should().NotContain(distinctiveCorrectAnswer,
            "CorrectAnswer must NEVER be sent to the client — found it verbatim in the response body. " +
            "CRITICAL security defect (answer leakage). Body: {0}", rawBody);

        // The key name 'correctAnswer' / 'CorrectAnswer' must also be absent
        rawBody.ToLowerInvariant().Should().NotContain("\"correctanswer\"",
            "The key 'correctAnswer' must not appear in the response JSON (excluded from QuizQuestionDto); " +
            "Body: {0}", rawBody);
    }

    // =========================================================================
    // T5 — Resume: two StartAttempt calls → same AttemptId, attempt count == 1
    // =========================================================================

    [Fact(DisplayName = "QZ-T5 Resume: second StartAttempt returns the SAME AttemptId and attempt count stays 1")]
    public async Task StartAttempt_Resume_SameAttemptId_And_RowCountStaysOne()
    {
        var (studentToken, studentId) = await CreateStudentViaParentFlowAsync();
        var lessonId = await SeedLessonAsync("T5 Resume Lesson");

        // First call
        var (resp1, root1, body1) = await StartAttemptAsync(lessonId, studentToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first StartAttempt must succeed; body: {0}", body1);
        TryProp(root1, "data", out var d1).Should().BeTrue("body: {0}", body1);
        TryProp(d1, "attemptId", out var id1Prop).Should().BeTrue("body: {0}", body1);
        var attemptId1 = id1Prop.GetInt32();

        // Second call — same student, same lesson
        var (resp2, root2, body2) = await StartAttemptAsync(lessonId, studentToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "resume StartAttempt must also return 200; body: {0}", body2);
        TryProp(root2, "data", out var d2).Should().BeTrue("body: {0}", body2);
        TryProp(d2, "attemptId", out var id2Prop).Should().BeTrue("body: {0}", body2);
        var attemptId2 = id2Prop.GetInt32();

        // Same AttemptId
        attemptId2.Should().Be(attemptId1,
            "F4-resume: the same AttemptId must be returned on the second call; " +
            "body1: {0}, body2: {1}", body1, body2);

        // Exactly one Attempt row for this (student, lesson) pair
        using var scope = _factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var count = await db.Attempts.CountAsync(a => a.StudentId == studentId && a.LessonId == lessonId);

        count.Should().Be(1,
            "F4-resume: only ONE Attempt row must exist for (student={0}, lesson={1}) after two StartAttempt calls",
            studentId, lessonId);
    }

    // =========================================================================
    // T6 — Non-existent lesson → 404, Successed=false, no Attempt row created
    // =========================================================================

    [Fact(DisplayName = "QZ-T6 StartAttempt: non-existent lessonId → 404 NotFound, Successed=false")]
    public async Task StartAttempt_NonExistentLesson_Returns404()
    {
        var (studentToken, studentId) = await CreateStudentViaParentFlowAsync();
        // A lessonId that is astronomically unlikely to exist in the test DB
        const int phantomLessonId = 999_999_999;

        var (resp, root, body) = await StartAttemptAsync(phantomLessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "F7: a non-existent lessonId must return 404; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false for 404; body: {0}", body);

        // No Attempt row must have been created for this phantom lesson
        using var scope = _factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var count = await db.Attempts.CountAsync(a => a.StudentId == studentId && a.LessonId == phantomLessonId);

        count.Should().Be(0,
            "No Attempt row must be created when the lesson does not exist; studentId: {0}", studentId);
    }

    // =========================================================================
    // T7 — Empty quiz: lesson with no questions → 200, Questions=[]
    // =========================================================================

    [Fact(DisplayName = "QZ-T7 StartAttempt: lesson with no questions → 200, Questions=[]")]
    public async Task StartAttempt_EmptyQuiz_Returns200_WithEmptyQuestions()
    {
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var emptyLessonId     = await SeedLessonAsync("T7 Empty Lesson");
        // No questions seeded for this lesson

        var (resp, root, body) = await StartAttemptAsync(emptyLessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "An empty quiz must still return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true for empty quiz; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "questions", out var questions).Should().BeTrue("data.questions required; body: {0}", body);
        questions.ValueKind.Should().Be(JsonValueKind.Array, "questions must be an array; body: {0}", body);
        questions.GetArrayLength().Should().Be(0,
            "questions must be empty when the lesson has no questions; body: {0}", body);
    }

    // =========================================================================
    // T8 — Envelope shape: all BaseResponse keys present and correctly spelled
    // =========================================================================

    [Fact(DisplayName = "QZ-T8 StartAttempt: envelope has statusCode, successed, message, data, errors")]
    public async Task StartAttempt_Envelope_HasAllBaseResponseKeys()
    {
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var lessonId          = await SeedLessonAsync("T8 Envelope Lesson");

        var (resp, root, rawBody) = await StartAttemptAsync(lessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        TryProp(root, "statusCode", out var sc).Should().BeTrue("envelope must have 'statusCode'; body: {0}", rawBody);
        sc.GetInt32().Should().Be(200, "statusCode must be 200 on success; body: {0}", rawBody);

        TryProp(root, "successed", out _).Should().BeTrue("envelope must have 'successed'; body: {0}", rawBody);
        TryProp(root, "message",   out _).Should().BeTrue("envelope must have 'message'; body: {0}", rawBody);
        TryProp(root, "data",      out _).Should().BeTrue("envelope must have 'data'; body: {0}", rawBody);
        TryProp(root, "errors",    out _).Should().BeTrue("envelope must have 'errors'; body: {0}", rawBody);
    }

    [Fact(DisplayName = "QZ-T8 StartAttempt: response uses camelCase 'successed' (not 'succeeded')")]
    public async Task StartAttempt_Envelope_UsesSuccessedCamelCase()
    {
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var lessonId          = await SeedLessonAsync("T8 Casing Lesson");

        var (resp, _, rawBody) = await StartAttemptAsync(lessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", rawBody);

        // Raw JSON must contain the canonical (intentional) misspelling per API contract
        rawBody.Should().Contain("successed",
            "envelope must use the canonical 'successed' spelling; body: {0}", rawBody);
        rawBody.Should().NotContain("\"succeeded\"",
            "must not use the incorrect 'succeeded' spelling; body: {0}", rawBody);
    }

    // =========================================================================
    // T9 — Schema: learning tables exist; no 'assessment' schema
    // =========================================================================

    [Fact(DisplayName = "QZ-T9 Schema: learning.\"QuizQuestions\" table exists")]
    public async Task Schema_QuizQuestions_Exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var act = async () => { _ = await db.QuizQuestions.CountAsync(); };
        await act.Should().NotThrowAsync(
            "learning.\"QuizQuestions\" must exist after Learning migrations are applied");
    }

    [Fact(DisplayName = "QZ-T9 Schema: learning.\"Attempts\" table exists")]
    public async Task Schema_Attempts_Exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var act = async () => { _ = await db.Attempts.CountAsync(); };
        await act.Should().NotThrowAsync(
            "learning.\"Attempts\" must exist after Learning migrations are applied");
    }

    [Fact(DisplayName = "QZ-T9 Schema: learning.\"StudentAnswers\" table exists")]
    public async Task Schema_StudentAnswers_Exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var act = async () => { _ = await db.StudentAnswers.CountAsync(); };
        await act.Should().NotThrowAsync(
            "learning.\"StudentAnswers\" must exist after Learning migrations are applied");
    }

    [Fact(DisplayName = "QZ-T9 Schema: no 'assessment' schema exists — quiz tables are in 'learning'")]
    public async Task Schema_NoAssessmentSchema()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM information_schema.schemata " +
            "WHERE schema_name = 'assessment'";
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        await conn.CloseAsync();

        count.Should().Be(0,
            "there must be NO 'assessment' schema — quiz tables live in 'learning'; " +
            "found {0} row(s) in information_schema", count);
    }

    // =========================================================================
    // T10 — Questions include required fields (Id, QuestionType, QuestionText, Options)
    // =========================================================================

    [Fact(DisplayName = "QZ-T10 StartAttempt: returned questions have Id/QuestionType/QuestionText/Options")]
    public async Task StartAttempt_HappyPath_QuestionsHaveRequiredFields()
    {
        var (studentToken, _) = await CreateStudentViaParentFlowAsync();
        var lessonId          = await SeedLessonAsync("T10 Questions Lesson");

        await SeedQuestionsAsync(lessonId, new[]
        {
            ("What is 2+2?",                              "[\"1\",\"2\",\"3\",\"4\"]",          "\"4\""),
            ("What is the capital of Egypt?",             "[\"Cairo\",\"London\",\"Paris\",\"Berlin\"]", "\"Cairo\""),
        });

        var (resp, root, body) = await StartAttemptAsync(lessonId, studentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "questions", out var questions).Should().BeTrue("data.questions required; body: {0}", body);
        questions.ValueKind.Should().Be(JsonValueKind.Array, "questions must be an array; body: {0}", body);
        questions.GetArrayLength().Should().Be(2, "two questions were seeded; body: {0}", body);

        foreach (var q in questions.EnumerateArray())
        {
            TryProp(q, "id", out var qId).Should().BeTrue("each question must have 'id'; body: {0}", body);
            qId.GetInt32().Should().BeGreaterThan(0, "question id must be positive; body: {0}", body);

            TryProp(q, "questionType", out _).Should().BeTrue(
                "each question must have 'questionType'; body: {0}", body);
            TryProp(q, "questionText", out var qText).Should().BeTrue(
                "each question must have 'questionText'; body: {0}", body);
            qText.GetString().Should().NotBeNullOrWhiteSpace(
                "questionText must not be blank; body: {0}", body);

            TryProp(q, "options", out var opts).Should().BeTrue(
                "each question must have 'options'; body: {0}", body);
            opts.ValueKind.Should().NotBe(JsonValueKind.Null,
                "options must not be null; body: {0}", body);
        }
    }

    // =========================================================================
    // T11 — StudentId from JWT (not client-supplied)
    //        Two distinct callers → two distinct StudentIds in their respective Attempt rows
    // =========================================================================

    [Fact(DisplayName = "QZ-T11 StudentId resolved from JWT: two distinct callers produce Attempt rows with distinct StudentIds")]
    public async Task StartAttempt_StudentId_ResolvedFromJwt_NotClientSupplied()
    {
        var (token1, userId1) = await CreateStudentViaParentFlowAsync();
        var (token2, userId2) = await CreateStudentViaParentFlowAsync();

        userId1.Should().NotBe(userId2, "the two students must be distinct users");

        // Seed a shared lesson that both students can attempt
        var sharedLessonId = await SeedLessonAsync("T11 Shared Lesson");

        var (r1, root1, body1) = await StartAttemptAsync(sharedLessonId, token1);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "student1 StartAttempt must succeed; body: {0}", body1);
        TryProp(root1, "data", out var d1).Should().BeTrue("body: {0}", body1);
        TryProp(d1, "attemptId", out var id1Prop).Should().BeTrue("body: {0}", body1);

        var (r2, root2, body2) = await StartAttemptAsync(sharedLessonId, token2);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "student2 StartAttempt must succeed; body: {0}", body2);
        TryProp(root2, "data", out var d2).Should().BeTrue("body: {0}", body2);
        TryProp(d2, "attemptId", out var id2Prop).Should().BeTrue("body: {0}", body2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var attempt1 = await db.Attempts.SingleAsync(a => a.Id == id1Prop.GetInt32());
        var attempt2 = await db.Attempts.SingleAsync(a => a.Id == id2Prop.GetInt32());

        attempt1.StudentId.Should().Be(userId1,
            "Attempt1.StudentId must equal student1's JWT userId — StudentId must come from JWT, not client");
        attempt2.StudentId.Should().Be(userId2,
            "Attempt2.StudentId must equal student2's JWT userId — StudentId must come from JWT, not client");
        attempt1.StudentId.Should().NotBe(attempt2.StudentId,
            "two distinct callers must produce Attempt rows with distinct StudentIds");
    }
}

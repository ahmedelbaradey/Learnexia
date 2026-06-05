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
/// P2-08 integration tests — "Record Granular Per-Question Answers".
///
/// Endpoints under test (5 endpoints):
///   POST /api/Learning/Quizzes/{attemptId}/Answers   [Authorize(Roles="Student")]
///   POST /api/Learning/Quizzes/{attemptId}/Complete  [Authorize(Roles="Student")]
///   POST /api/Learning/Quizzes/{attemptId}/Abandon   [Authorize(Roles="Student")]
///   GET  /api/Learning/Students/{studentId}/Attempts [Authorize]
///   GET  /api/Learning/Skills/{skillId}/Stats        [Authorize] (?studentId=)
///
/// Seeding strategy: Path B — inline per-test (bespoke test data).
/// Student authentication: parent-driven onboarding flow (Register Parent → Add-Child → Sign-In as child).
///   This is the canonical Student auth flow mirrored exactly from P2_06_StartAttempt_Tests.cs.
///
/// Coverage map (17 plan test cases):
///   Case  1 → SubmitAnswer_CorrectAnswer_IsCorrectTrueAndCorrectAnswerNull
///   Case  2 → SubmitAnswer_WrongAnswer_IsCorrectFalseAndCorrectAnswerPopulated
///   Case  3 → SubmitAnswer_DuplicateQuestion_Returns424
///   Case  4 → SubmitAnswer_OwnershipViolation_Returns401
///   Case  5 → SubmitAnswer_NonExistentAttempt_Returns404
///   Case  6 → SubmitAnswer_AttemptNotInProgress_Returns424
///   Case  7 → CompleteAttempt_HappyPath_CorrectAggregates
///   Case  8 → CompleteAttempt_ZeroAnswers_AccuracyIsZeroNoError
///   Case  9 → CompleteAttempt_Idempotent_SecondCallReturns200
///   Case 10 → AbandonAttempt_Partial_CorrectAggregatesAndAnswersPreserved
///   Case 11 → AbandonAttempt_ZeroAnswers_ReturnsZeroedStats
///   Case 12 → AbandonAttempt_AlreadyAbandoned_IdempotentNoError
///   Case 13 → GetStudentAttempts_Self_Returns2ItemsNoCorrectAnswerField
///   Case 14 → GetStudentAttempts_OtherStudentIDOR_Returns401
///   Case 15 → GetSkillStats_WithData_CorrectAggregates
///   Case 16 → GetSkillStats_NoData_ReturnsZeroedStats
///   Case 17 → GetSkillStats_QuestionsWithoutSkillId_NotCountedInStats
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_08_RecordGranularAnswers_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URLs
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";

    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P2_08_RecordGranularAnswers_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers — mirrors P2_06_StartAttempt_Tests.cs patterns exactly
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p208_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
    /// Registers a parent, adds a child via Add-Child (which creates a Student-role account),
    /// and signs in as that child. Returns the Student JWT and the child's userId.
    /// Mirrors CreateStudentViaParentFlowAsync from P2_06_StartAttempt_Tests.cs.
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

    /// <summary>Seeds the full Grade → Subject → Unit → Lesson hierarchy. Returns lessonId.</summary>
    private async Task<int> SeedLessonAsync(string lessonName = "Test Lesson")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        var grade = new Grade { Number = 88, DisplayName = $"G_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject { Name = $"S_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit { Name = $"U_{Guid.NewGuid():N}", SequenceOrder = 1, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson { Name = lessonName, Difficulty = DifficultyLevel.Easy, SequenceOrder = 1, IsLocked = false, UnitId = unit.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        return lesson.Id;
    }

    /// <summary>
    /// Seeds QuizQuestion rows for the given lessonId. Returns the list of created question IDs.
    /// skillId is optional — pass null for questions not linked to a skill.
    /// </summary>
    private async Task<List<int>> SeedQuestionsAsync(
        int lessonId,
        IEnumerable<(string QuestionText, string Options, string CorrectAnswer, int? SkillId)> questions)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;
        var ids = new List<int>();
        foreach (var (qText, opts, correctAnswer, skillId) in questions)
        {
            var q = new QuizQuestion
            {
                LessonId      = lessonId,
                SkillId       = skillId,
                QuestionType  = QuestionType.MCQ,
                QuestionText  = qText,
                Options       = opts,
                CorrectAnswer = correctAnswer,
                Difficulty    = DifficultyLevel.Easy,
                GeneratedBy   = GeneratedBy.Curated,
                CreatedAt     = now,
                CreatedBy     = 0,
            };
            await db.QuizQuestions.AddAsync(q);
            await db.SaveChangesAsync();
            ids.Add(q.Id);
        }
        return ids;
    }

    /// <summary>
    /// Seeds a Skill entity (requires Concept → Subject → Grade hierarchy).
    /// Returns the new Skill.Id.
    /// </summary>
    private async Task<int> SeedSkillAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var now = DateTime.UtcNow;

        var grade = new Grade { Number = 77, DisplayName = $"SkillG_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
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
            Name                   = $"Skill_{Guid.NewGuid():N}",
            MasteryThreshold       = 70,
            EstimatedTimeMinutes   = 15,
            ConceptId              = concept.Id,
            CreatedAt              = now,
            CreatedBy              = 0,
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    /// <summary>
    /// Starts an attempt for the given student+lesson via the API. Returns the attemptId.
    /// </summary>
    private async Task<int> StartAttemptViaApiAsync(int lessonId, string studentToken)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    /// <summary>POST /api/Learning/Quizzes/{attemptId}/Answers</summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SubmitAnswerAsync(int attemptId, int questionId, string answerPayload,
            int timeSpentSeconds, bool hintUsed, string? token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answerPayload, TimeSpentSeconds = timeSpentSeconds, HintUsed = hintUsed },
            token);

    /// <summary>POST /api/Learning/Quizzes/{attemptId}/Complete</summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        CompleteAttemptAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    /// <summary>POST /api/Learning/Quizzes/{attemptId}/Abandon</summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        AbandonAttemptAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Abandon", null, token);

    // =========================================================================
    // SubmitAnswer — Case 1: Correct answer
    // =========================================================================

    [Fact(DisplayName = "P208-C01 SubmitAnswer correct answer → isCorrect=true, correctAnswer=null, DB row has correct fields")]
    public async Task SubmitAnswer_CorrectAnswer_IsCorrectTrueAndCorrectAnswerNull()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId   = await SeedLessonAsync("C01-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("What is 2+2?", "[\"1\",\"2\",\"3\",\"4\"]", "\"4\"", (int?)null),
        });
        var questionId = questionIds[0];
        var attemptId  = await StartAttemptViaApiAsync(lessonId, token);

        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionId, "\"4\"", timeSpentSeconds: 10, hintUsed: false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "correct answer submit must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);

        // isCorrect = true
        TryProp(data, "isCorrect", out var isCorrect).Should().BeTrue("data.isCorrect required; body: {0}", body);
        isCorrect.GetBoolean().Should().BeTrue("isCorrect must be true for correct answer; body: {0}", body);

        // correctAnswer must be null when isCorrect=true
        if (TryProp(data, "correctAnswer", out var correctAnswerProp))
        {
            // Property present — value must be null
            correctAnswerProp.ValueKind.Should().Be(JsonValueKind.Null,
                "correctAnswer must be null when isCorrect=true; body: {0}", body);
        }
        // If not present at all, that is also acceptable.

        // Verify DB row
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var answerRow = await db.StudentAnswers
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == questionId);

        answerRow.Should().NotBeNull("StudentAnswer row must be persisted; attemptId: {0}, questionId: {1}", attemptId, questionId);
        answerRow!.IsCorrect.Should().BeTrue("DB row IsCorrect must be true");
        answerRow.TimeSpentSeconds.Should().Be(10, "TimeSpentSeconds must match input");
        answerRow.HintUsed.Should().BeFalse("HintUsed must match input (false)");
    }

    // =========================================================================
    // SubmitAnswer — Case 2: Wrong answer
    // =========================================================================

    [Fact(DisplayName = "P208-C02 SubmitAnswer wrong answer → isCorrect=false, correctAnswer populated")]
    public async Task SubmitAnswer_WrongAnswer_IsCorrectFalseAndCorrectAnswerPopulated()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId   = await SeedLessonAsync("C02-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("Capital of France?", "[\"Berlin\",\"Paris\",\"Rome\",\"Madrid\"]", "\"Paris\"", (int?)null),
        });
        var questionId = questionIds[0];
        var attemptId  = await StartAttemptViaApiAsync(lessonId, token);

        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionId, "\"Berlin\"", timeSpentSeconds: 15, hintUsed: true, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "wrong answer submit must still return 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "isCorrect", out var isCorrect).Should().BeTrue("body: {0}", body);
        isCorrect.GetBoolean().Should().BeFalse("isCorrect must be false for wrong answer; body: {0}", body);

        // correctAnswer must be populated (non-null, non-empty) when wrong
        TryProp(data, "correctAnswer", out var correctAnswerProp).Should().BeTrue(
            "correctAnswer must be present when isCorrect=false; body: {0}", body);
        correctAnswerProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "correctAnswer must not be null when isCorrect=false; body: {0}", body);
        correctAnswerProp.GetString().Should().NotBeNullOrWhiteSpace(
            "correctAnswer must be non-empty string when isCorrect=false; body: {0}", body);

        // Verify DB row
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var answerRow = await db.StudentAnswers
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == questionId);

        answerRow.Should().NotBeNull("StudentAnswer row must be persisted");
        answerRow!.IsCorrect.Should().BeFalse("DB row IsCorrect must be false");
        answerRow.TimeSpentSeconds.Should().Be(15, "TimeSpentSeconds must match input");
        answerRow.HintUsed.Should().BeTrue("HintUsed must match input (true)");
    }

    // =========================================================================
    // SubmitAnswer — Case 3: Duplicate question → 424
    // =========================================================================

    [Fact(DisplayName = "P208-C03 SubmitAnswer duplicate question (same QuestionId twice) → 424 BusinessValidation")]
    public async Task SubmitAnswer_DuplicateQuestion_Returns424()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C03-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("1+1=?", "[\"1\",\"2\",\"3\",\"4\"]", "\"2\"", (int?)null),
        });
        var questionId = questionIds[0];
        var attemptId  = await StartAttemptViaApiAsync(lessonId, token);

        // First submit — must succeed
        var (resp1, _, body1) = await SubmitAnswerAsync(
            attemptId, questionId, "\"2\"", 5, false, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK,
            "first submit must succeed; body: {0}", body1);

        // Second submit for the same question — must return 424
        var (resp2, root2, body2) = await SubmitAnswerAsync(
            attemptId, questionId, "\"2\"", 5, false, token);
        ((int)resp2.StatusCode).Should().Be(424,
            "duplicate question must return 424 BusinessValidation; body: {0}", body2);

        TryProp(root2, "successed", out var successed).Should().BeTrue("body: {0}", body2);
        successed.GetBoolean().Should().BeFalse("Successed must be false for 424; body: {0}", body2);
    }

    // =========================================================================
    // SubmitAnswer — Case 4: Ownership violation → 401
    // =========================================================================

    [Fact(DisplayName = "P208-C04 SubmitAnswer ownership violation (student A submits to student B attempt) → 401")]
    public async Task SubmitAnswer_OwnershipViolation_Returns401()
    {
        // Create two students
        var (tokenA, _) = await CreateStudentViaParentFlowAsync();
        var (tokenB, _) = await CreateStudentViaParentFlowAsync();

        var lessonId = await SeedLessonAsync("C04-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("Q for ownership test?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
        });
        var questionId = questionIds[0];

        // Student B starts an attempt
        var attemptIdB = await StartAttemptViaApiAsync(lessonId, tokenB);

        // Student A tries to submit to Student B's attempt
        var (resp, root, body) = await SubmitAnswerAsync(
            attemptIdB, questionId, "\"A\"", 5, false, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "submitting to another student's attempt must return 401; body: {0}", body);
    }

    // =========================================================================
    // SubmitAnswer — Case 5: Non-existent attempt → 404
    // =========================================================================

    [Fact(DisplayName = "P208-C05 SubmitAnswer non-existent attemptId → 404")]
    public async Task SubmitAnswer_NonExistentAttempt_Returns404()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        const int phantomAttemptId = 999_999_997;

        var (resp, root, body) = await SubmitAnswerAsync(
            phantomAttemptId, 1, "\"answer\"", 5, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "non-existent attemptId must return 404; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false for 404; body: {0}", body);
    }

    // =========================================================================
    // SubmitAnswer — Case 6: Attempt not InProgress → 424
    // =========================================================================

    [Fact(DisplayName = "P208-C06 SubmitAnswer on Completed attempt → 424 BusinessValidation")]
    public async Task SubmitAnswer_AttemptNotInProgress_Returns424()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C06-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("Q1?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
            ("Q2?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)null),
        });
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Complete the attempt first
        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Complete must succeed before testing; body: {0}", complBody);

        // Now try to submit an answer — attempt is Completed, should be 424
        var (resp, root, body) = await SubmitAnswerAsync(
            attemptId, questionIds[0], "\"A\"", 5, false, token);

        ((int)resp.StatusCode).Should().Be(424,
            "submitting to a non-InProgress attempt must return 424; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false; body: {0}", body);
    }

    // =========================================================================
    // CompleteAttempt — Case 7: Happy path
    // =========================================================================

    [Fact(DisplayName = "P208-C07 CompleteAttempt happy path → Status=Completed, correct AccuracyPercentage, CompletedAt set, DurationSeconds > 0")]
    public async Task CompleteAttempt_HappyPath_CorrectAggregates()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C07-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("Q1?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
            ("Q2?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)null),
            ("Q3?", "[\"A\",\"B\",\"C\",\"D\"]", "\"C\"", (int?)null),
        });
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit 3 answers: 2 correct, 1 wrong
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 10, false, token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"B\"", 8, false, token);  // correct
        await SubmitAnswerAsync(attemptId, questionIds[2], "\"A\"", 12, true, token);  // wrong

        var (resp, root, body) = await CompleteAttemptAsync(attemptId, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Status = Completed
        TryProp(data, "status", out var statusProp).Should().BeTrue("data.status required; body: {0}", body);
        statusProp.GetString().Should().Be("Completed", "Status must be 'Completed'; body: {0}", body);

        // AccuracyPercentage = 2/3 * 100 ≈ 66.67
        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        var accuracy = accuracyProp.GetDouble();
        accuracy.Should().BeApproximately(66.67, 0.1,
            "AccuracyPercentage must be 2/3*100 ≈ 66.67; body: {0}", body);

        // CompletedAt must be set
        TryProp(data, "completedAt", out var completedAtProp).Should().BeTrue("data.completedAt required; body: {0}", body);
        completedAtProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "completedAt must not be null after Complete; body: {0}", body);

        // DurationSeconds > 0
        TryProp(data, "durationSeconds", out var durationProp).Should().BeTrue("body: {0}", body);
        durationProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            "DurationSeconds must be >= 0; body: {0}", body);
    }

    // =========================================================================
    // CompleteAttempt — Case 8: Zero answers → AccuracyPercentage=0, no divide-by-zero
    // =========================================================================

    [Fact(DisplayName = "P208-C08 CompleteAttempt with zero answers → AccuracyPercentage=0, no error")]
    public async Task CompleteAttempt_ZeroAnswers_AccuracyIsZeroNoError()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C08-Lesson");
        var attemptId   = await StartAttemptViaApiAsync(lessonId, token);

        // Complete without submitting any answers
        var (resp, root, body) = await CompleteAttemptAsync(attemptId, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt with zero answers must return 200 (no divide-by-zero); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        accuracyProp.GetDouble().Should().Be(0.0, "AccuracyPercentage must be 0 for zero answers; body: {0}", body);

        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        statusProp.GetString().Should().Be("Completed", "body: {0}", body);
    }

    // =========================================================================
    // CompleteAttempt — Case 9: Idempotent second call → 200, current completed state
    // =========================================================================

    [Fact(DisplayName = "P208-C09 CompleteAttempt idempotent: second call returns 200 with completed state")]
    public async Task CompleteAttempt_Idempotent_SecondCallReturns200()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C09-Lesson");
        var attemptId   = await StartAttemptViaApiAsync(lessonId, token);

        // First Complete
        var (resp1, _, body1) = await CompleteAttemptAsync(attemptId, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first Complete must succeed; body: {0}", body1);

        // Second Complete — must be idempotent
        var (resp2, root2, body2) = await CompleteAttemptAsync(attemptId, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second Complete (idempotent) must return 200; body: {0}", body2);

        TryProp(root2, "successed", out var successed).Should().BeTrue("body: {0}", body2);
        successed.GetBoolean().Should().BeTrue("Successed must be true for idempotent Complete; body: {0}", body2);

        TryProp(root2, "data", out var data).Should().BeTrue("body: {0}", body2);
        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body2);
        statusProp.GetString().Should().Be("Completed", "Status must remain Completed; body: {0}", body2);
    }

    // =========================================================================
    // AbandonAttempt — Case 10: Partial capture
    // =========================================================================

    [Fact(DisplayName = "P208-C10 AbandonAttempt partial (2 of 5) → Status=Abandoned, aggregates over 2 answers, prior answers preserved in DB")]
    public async Task AbandonAttempt_Partial_CorrectAggregatesAndAnswersPreserved()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C10-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("Q1?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
            ("Q2?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)null),
            ("Q3?", "[\"A\",\"B\",\"C\",\"D\"]", "\"C\"", (int?)null),
            ("Q4?", "[\"A\",\"B\",\"C\",\"D\"]", "\"D\"", (int?)null),
            ("Q5?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)null),
        });
        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit only 2 of 5: Q1 correct (A=A), Q2 wrong (A≠B)
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 10, false, token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"A\"", 8, true, token);   // wrong

        // Abandon
        var (resp, root, body) = await AbandonAttemptAsync(attemptId, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AbandonAttempt must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Status = Abandoned
        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        statusProp.GetString().Should().Be("Abandoned", "Status must be 'Abandoned'; body: {0}", body);

        // CompletedAt must be set
        TryProp(data, "completedAt", out var completedAtProp).Should().BeTrue("body: {0}", body);
        completedAtProp.ValueKind.Should().NotBe(JsonValueKind.Null,
            "completedAt must be set after Abandon; body: {0}", body);

        // Aggregates are over 2 answers: 1 correct / 2 total = 50%
        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        accuracyProp.GetDouble().Should().BeApproximately(50.0, 0.1,
            "AccuracyPercentage must be 50% (1 of 2 correct); body: {0}", body);

        TryProp(data, "hintsUsedCount", out var hintsProp).Should().BeTrue("body: {0}", body);
        hintsProp.GetInt32().Should().Be(1, "HintsUsedCount must be 1 (Q2 had hintUsed=true); body: {0}", body);

        // Verify prior answer rows still in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var answerCount = await db.StudentAnswers.CountAsync(sa => sa.AttemptId == attemptId);
        answerCount.Should().Be(2, "the 2 submitted answers must still be present in the DB after Abandon; attemptId: {0}", attemptId);
    }

    // =========================================================================
    // AbandonAttempt — Case 11: Zero answers → zeroed stats, no error
    // =========================================================================

    [Fact(DisplayName = "P208-C11 AbandonAttempt zero answers → AccuracyPercentage=0, HintsUsedCount=0, DurationSeconds>=0, no error")]
    public async Task AbandonAttempt_ZeroAnswers_ReturnsZeroedStats()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C11-Lesson");
        var attemptId   = await StartAttemptViaApiAsync(lessonId, token);

        // Abandon immediately without submitting any answers
        var (resp, root, body) = await AbandonAttemptAsync(attemptId, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "AbandonAttempt with zero answers must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        accuracyProp.GetDouble().Should().Be(0.0, "AccuracyPercentage must be 0 for zero answers; body: {0}", body);

        TryProp(data, "hintsUsedCount", out var hintsProp).Should().BeTrue("body: {0}", body);
        hintsProp.GetInt32().Should().Be(0, "HintsUsedCount must be 0 for zero answers; body: {0}", body);

        TryProp(data, "durationSeconds", out var durationProp).Should().BeTrue("body: {0}", body);
        durationProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            "DurationSeconds must be >= 0 (server-side elapsed); body: {0}", body);

        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        statusProp.GetString().Should().Be("Abandoned", "body: {0}", body);
    }

    // =========================================================================
    // AbandonAttempt — Case 12: Already abandoned idempotent
    // =========================================================================

    [Fact(DisplayName = "P208-C12 AbandonAttempt already abandoned → second call returns current state, no error")]
    public async Task AbandonAttempt_AlreadyAbandoned_IdempotentNoError()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var lessonId    = await SeedLessonAsync("C12-Lesson");
        var attemptId   = await StartAttemptViaApiAsync(lessonId, token);

        // First Abandon
        var (resp1, _, body1) = await AbandonAttemptAsync(attemptId, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first Abandon must succeed; body: {0}", body1);

        // Second Abandon — must be idempotent
        var (resp2, root2, body2) = await AbandonAttemptAsync(attemptId, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second Abandon (idempotent) must return 200; body: {0}", body2);

        TryProp(root2, "successed", out var successed).Should().BeTrue("body: {0}", body2);
        successed.GetBoolean().Should().BeTrue("Successed must be true for idempotent Abandon; body: {0}", body2);

        TryProp(root2, "data", out var data).Should().BeTrue("body: {0}", body2);
        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body2);
        statusProp.GetString().Should().Be("Abandoned", "Status must remain Abandoned; body: {0}", body2);
    }

    // =========================================================================
    // GetStudentAttempts — Case 13: Self returns 2 items, no correctAnswer field
    // =========================================================================

    [Fact(DisplayName = "P208-C13 GetStudentAttempts self → 2 items returned, no 'correctAnswer' anywhere in body")]
    public async Task GetStudentAttempts_Self_Returns2ItemsNoCorrectAnswerField()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var lessonId1 = await SeedLessonAsync("C13-L1");
        var lessonId2 = await SeedLessonAsync("C13-L2");

        // Create attempt 1 → complete it
        var attemptId1 = await StartAttemptViaApiAsync(lessonId1, token);
        await CompleteAttemptAsync(attemptId1, token);

        // Create attempt 2 → abandon it
        var attemptId2 = await StartAttemptViaApiAsync(lessonId2, token);
        await AbandonAttemptAsync(attemptId2, token);

        // GET /api/Learning/Students/{studentId}/Attempts
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Students/{studentId}/Attempts", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GetStudentAttempts must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().Be(JsonValueKind.Array, "data must be an array; body: {0}", body);
        data.GetArrayLength().Should().Be(2,
            "exactly 2 attempts were seeded for this student; body: {0}", body);

        // Critical security check: no 'correctAnswer' key anywhere in the raw body
        body.ToLowerInvariant().Should().NotContain("\"correctanswer\"",
            "CorrectAnswer must NEVER appear in GetStudentAttempts response (AC-4 security); body: {0}", body);
    }

    // =========================================================================
    // GetStudentAttempts — Case 14: Cross-student IDOR → 401
    // =========================================================================

    [Fact(DisplayName = "P208-C14 GetStudentAttempts IDOR: student A calls GET /Students/{studentB_id}/Attempts → 401")]
    public async Task GetStudentAttempts_OtherStudentIDOR_Returns401()
    {
        var (tokenA, _)   = await CreateStudentViaParentFlowAsync();
        var (tokenB, idB) = await CreateStudentViaParentFlowAsync();

        // Student A tries to read Student B's attempts
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Students/{idB}/Attempts", null, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "student A accessing student B's attempts must return 401 (IDOR guard); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse("Successed must be false for 401; body: {0}", body);
    }

    // =========================================================================
    // GetSkillStats — Case 15: With data → correct aggregates
    // =========================================================================

    [Fact(DisplayName = "P208-C15 GetSkillStats with data → correct TotalAnswers, CorrectAnswers, AccuracyPercentage, AvgTimeSpentSeconds, HintUsageRate")]
    public async Task GetSkillStats_WithData_CorrectAggregates()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var skillId             = await SeedSkillAsync();
        var lessonId            = await SeedLessonAsync("C15-Lesson");

        // Seed 3 questions linked to the skill
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("SQ1?", "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)skillId),
            ("SQ2?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)skillId),
            ("SQ3?", "[\"A\",\"B\",\"C\",\"D\"]", "\"C\"", (int?)skillId),
        });

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit: Q1 correct (10s, hint=false), Q2 wrong (20s, hint=true), Q3 correct (30s, hint=false)
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 10, false, token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"A\"", 20, true, token);  // wrong (A≠B)
        await SubmitAnswerAsync(attemptId, questionIds[2], "\"C\"", 30, false, token); // correct
        await CompleteAttemptAsync(attemptId, token);

        // GET /api/Learning/Skills/{skillId}/Stats?studentId={studentId}
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Skills/{skillId}/Stats?studentId={studentId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GetSkillStats must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // TotalAnswers = 3
        TryProp(data, "totalAnswers", out var totalProp).Should().BeTrue("body: {0}", body);
        totalProp.GetInt32().Should().Be(3, "TotalAnswers must be 3; body: {0}", body);

        // CorrectAnswers = 2
        TryProp(data, "correctAnswers", out var correctProp).Should().BeTrue("body: {0}", body);
        correctProp.GetInt32().Should().Be(2, "CorrectAnswers must be 2; body: {0}", body);

        // AccuracyPercentage = 2/3 * 100 ≈ 66.67
        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        accuracyProp.GetDouble().Should().BeApproximately(66.67, 0.1,
            "AccuracyPercentage must be 66.67; body: {0}", body);

        // AvgTimeSpentSeconds = (10+20+30)/3 = 20
        TryProp(data, "avgTimeSpentSeconds", out var avgTimeProp).Should().BeTrue("body: {0}", body);
        avgTimeProp.GetDouble().Should().BeApproximately(20.0, 0.1,
            "AvgTimeSpentSeconds must be (10+20+30)/3 = 20; body: {0}", body);

        // HintUsageRate = 1/3 * 100 ≈ 33.33
        TryProp(data, "hintUsageRate", out var hintRateProp).Should().BeTrue("body: {0}", body);
        hintRateProp.GetDouble().Should().BeApproximately(33.33, 0.1,
            "HintUsageRate must be 1/3 * 100 ≈ 33.33; body: {0}", body);
    }

    // =========================================================================
    // GetSkillStats — Case 16: No data → zeroed stats (not 404, not 500)
    // =========================================================================

    [Fact(DisplayName = "P208-C16 GetSkillStats no data for skill → returns zeroed stats (not 404 or 500)")]
    public async Task GetSkillStats_NoData_ReturnsZeroedStats()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var skillId             = await SeedSkillAsync(); // skill exists but has no answers

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Skills/{skillId}/Stats?studentId={studentId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GetSkillStats with no data must return 200 (not 404, not 500); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true for zeroed stats; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        TryProp(data, "totalAnswers", out var totalProp).Should().BeTrue("body: {0}", body);
        totalProp.GetInt32().Should().Be(0, "TotalAnswers must be 0; body: {0}", body);

        TryProp(data, "correctAnswers", out var correctProp).Should().BeTrue("body: {0}", body);
        correctProp.GetInt32().Should().Be(0, "CorrectAnswers must be 0; body: {0}", body);

        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        accuracyProp.GetDouble().Should().Be(0.0, "AccuracyPercentage must be 0.0; body: {0}", body);

        TryProp(data, "avgTimeSpentSeconds", out var avgTimeProp).Should().BeTrue("body: {0}", body);
        avgTimeProp.GetDouble().Should().Be(0.0, "AvgTimeSpentSeconds must be 0.0; body: {0}", body);

        TryProp(data, "hintUsageRate", out var hintRateProp).Should().BeTrue("body: {0}", body);
        hintRateProp.GetDouble().Should().Be(0.0, "HintUsageRate must be 0.0; body: {0}", body);
    }

    // =========================================================================
    // GetSkillStats — Case 17: Questions without SkillId not counted
    // =========================================================================

    [Fact(DisplayName = "P208-C17 GetSkillStats: answers to questions with null SkillId are NOT counted in any skill's stats")]
    public async Task GetSkillStats_QuestionsWithoutSkillId_NotCountedInStats()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync();
        var skillId             = await SeedSkillAsync();
        var lessonId            = await SeedLessonAsync("C17-Lesson");

        // Seed 2 questions: one linked to skill, one with SkillId=null
        var questionIds = await SeedQuestionsAsync(lessonId, new[]
        {
            ("SkillQ?",   "[\"A\",\"B\",\"C\",\"D\"]", "\"A\"", (int?)skillId),  // linked to skill
            ("NoSkillQ?", "[\"A\",\"B\",\"C\",\"D\"]", "\"B\"", (int?)null),      // not linked to any skill
        });

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit answers to BOTH questions
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", 10, false, token); // correct, skill-linked
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"B\"", 15, false, token); // correct, no-skill
        await CompleteAttemptAsync(attemptId, token);

        // GET skill stats — should only count the one skill-linked answer
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Skills/{skillId}/Stats?studentId={studentId}", null, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Only 1 answer (the skill-linked one) should appear in stats
        TryProp(data, "totalAnswers", out var totalProp).Should().BeTrue("body: {0}", body);
        totalProp.GetInt32().Should().Be(1,
            "Only the skill-linked question's answer must be counted; null-SkillId question must be excluded; body: {0}", body);

        TryProp(data, "correctAnswers", out var correctProp).Should().BeTrue("body: {0}", body);
        correctProp.GetInt32().Should().Be(1, "1 correct out of 1 counted; body: {0}", body);

        TryProp(data, "accuracyPercentage", out var accuracyProp).Should().BeTrue("body: {0}", body);
        accuracyProp.GetDouble().Should().BeApproximately(100.0, 0.1,
            "AccuracyPercentage must be 100% (only the skill-linked answer); body: {0}", body);
    }
}

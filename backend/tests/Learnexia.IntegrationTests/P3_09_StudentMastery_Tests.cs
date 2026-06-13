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
/// P3-09 integration tests — "Track per-skill mastery (Student Modeling Engine)".
///
/// Endpoints under test:
///   GET  /api/Learning/Mastery              [Authorize(Roles="Student")]
///   GET  /api/Learning/Mastery/Skill/{id}   [Authorize(Roles="Student")]
///
/// Write path under test (via attempt completion):
///   POST /api/Learning/Quizzes/{attemptId}/Complete  — triggers MasteryEngine + upsert
///
/// Seeding strategy: inline per-test (bespoke test data via EF direct insert).
/// Student auth: parent-driven onboarding flow — mirrored exactly from P2_08_RecordGranularAnswers_Tests.cs.
///
/// Coverage map (8 plan test cases):
///   Case 1 → NewStudent_NoAnswers_GetMastery_ReturnsEmptyOrAllNotStarted
///   Case 2 → NewStudent_GetSkillMastery_ReturnsNotStarted
///   Case 3 → CompleteAttempt_HighAccuracy_SkillMastered
///   Case 4 → CompleteAttempt_LowAccuracy_SkillNeedsReview
///   Case 5 → CompleteAttempt_MidAccuracy_SkillInProgress
///   Case 6 → Reproducibility_SamePattern_IdenticalMasteryResult
///   Case 7 → IDOR_StudentB_CannotReadStudentA_Mastery
///   Case 8 → UnknownSkillId_ReturnsNotStartedDefaults_Never500
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_09_StudentMastery_Tests : IAsyncLifetime
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
    private readonly HttpClient _client;

    public P3_09_StudentMastery_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Shared HTTP helpers — mirrors P2_08_RecordGranularAnswers_Tests exactly
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p309_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive property lookup — handles camelCase and PascalCase JSON.</summary>
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
            catch { /* non-JSON body — root remains default */ }
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
    /// Registers a parent, adds a child (Student-role account), signs in as the child.
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
                LearningLanguage = "ar",
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

    // -------------------------------------------------------------------------
    // DB-seeding helpers (EF direct — mirrors P2_08 approach exactly)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Seeds Grade → Subject → Unit → Lesson. Returns lessonId.
    /// </summary>
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
            CreatedBy      = 0
        };
        await db.Lessons.AddAsync(lesson);
        await db.SaveChangesAsync();

        return lesson.Id;
    }

    /// <summary>
    /// Seeds a Skill (with a configurable MasteryThreshold). Returns the new Skill.Id.
    /// </summary>
    private async Task<int> SeedSkillAsync(int masteryThreshold = 80)
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
            Name                 = $"Skill_{Guid.NewGuid():N}",
            MasteryThreshold     = masteryThreshold,
            EstimatedTimeMinutes = 15,
            ConceptId            = concept.Id,
            CreatedAt            = now,
            CreatedBy            = 0,
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    /// <summary>
    /// Seeds QuizQuestion rows linked to a given skill. Returns the created question IDs.
    /// </summary>
    private async Task<List<int>> SeedQuestionsAsync(int lessonId, int skillId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var ids = new List<int>();
        for (var i = 1; i <= count; i++)
        {
            var q = new QuizQuestion
            {
                LessonId       = lessonId,
                SkillId        = skillId,
                QuestionType   = QuestionType.MCQ,
                QuestionText   = $"Q{i}?",
                Options        = "[\"A\",\"B\",\"C\",\"D\"]",
                CorrectAnswer  = "\"A\"",  // always "A" — easy to control correct/wrong
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

    // -------------------------------------------------------------------------
    // API action helpers
    // -------------------------------------------------------------------------

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
        SubmitAnswerAsync(int attemptId, int questionId, string answerPayload, string? token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answerPayload, TimeSpentSeconds = 10, HintUsed = false },
            token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        CompleteAttemptAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetStudentMasteryAsync(string? token)
        => SendAsync(_client, HttpMethod.Get, "api/Learning/Mastery", null, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetSkillMasteryAsync(int skillId, string? token)
        => SendAsync(_client, HttpMethod.Get, $"api/Learning/Mastery/Skill/{skillId}", null, token);

    // =========================================================================
    // Case 1 — New student, no answers → GET /Mastery returns empty (200)
    // =========================================================================

    [Fact(DisplayName = "P309-C01 New student with no answers → GET /Mastery returns 200 with empty array or all NotStarted")]
    public async Task NewStudent_NoAnswers_GetMastery_ReturnsEmptyOrAllNotStarted()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();

        var (resp, root, body) = await GetStudentMasteryAsync(token);

        // Assert HTTP 200
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Mastery with no attempts must return 200; body: {0}", body);

        // Assert BaseResponse<T> envelope shape
        TryProp(root, "successed", out var successed).Should().BeTrue(
            "envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "Successed must be true for a successful (empty) mastery response; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "StatusCode must be 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);

        // Data must be an array (empty — student has no attempts)
        data.ValueKind.Should().Be(JsonValueKind.Array,
            "data must be an array for GET /Mastery; body: {0}", body);

        // Every element, if present, must have Status == 0 (NotStarted)
        foreach (var item in data.EnumerateArray())
        {
            TryProp(item, "status", out var itemStatus).Should().BeTrue("body: {0}", body);
            itemStatus.GetInt32().Should().Be(0,
                "all mastery items for a student with no answers must have status=0 (NotStarted); body: {0}", body);
        }
    }

    // =========================================================================
    // Case 2 — New student, single skill → GET /Mastery/Skill/{id} → NotStarted
    // =========================================================================

    [Fact(DisplayName = "P309-C02 New student → GET /Mastery/Skill/{id} returns 200 with NotStarted defaults")]
    public async Task NewStudent_GetSkillMastery_ReturnsNotStarted()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();

        var (resp, root, body) = await GetSkillMasteryAsync(skillId, token);

        // Assert HTTP 200
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Mastery/Skill/{id} with no attempts must return 200 (never 404/500); body: {0}", body);

        // Assert BaseResponse<T> envelope shape
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // status = 0 (NotStarted)
        TryProp(data, "status", out var statusProp).Should().BeTrue("data.status required; body: {0}", body);
        statusProp.GetInt32().Should().Be(0,
            "status must be 0 (NotStarted) for a student with no answers for this skill; body: {0}", body);

        // masteryPercentage = 0
        TryProp(data, "masteryPercentage", out var pctProp).Should().BeTrue("data.masteryPercentage required; body: {0}", body);
        pctProp.GetInt32().Should().Be(0,
            "masteryPercentage must be 0 for NotStarted; body: {0}", body);

        // skillId matches the one queried
        TryProp(data, "skillId", out var skillIdProp).Should().BeTrue("body: {0}", body);
        skillIdProp.GetInt32().Should().Be(skillId, "skillId in response must match the requested skillId; body: {0}", body);
    }

    // =========================================================================
    // Case 3 — Complete attempt ≥80% correct (threshold 80) → Mastered
    // =========================================================================

    [Fact(DisplayName = "P309-C03 Complete attempt with ≥80% correct (threshold 80) → Mastered (status=2, pct≥80)")]
    public async Task CompleteAttempt_HighAccuracy_SkillMastered()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId    = await SeedLessonAsync("C03-Lesson");
        // 5 questions; all correct answer is "A"
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, 5);

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit 5 of 5 correct (5/5 = 100% ≥ 80 → Mastered)
        foreach (var qId in questionIds)
            await SubmitAnswerAsync(attemptId, qId, "\"A\"", token);

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must return 200; body: {0}", complBody);

        // Now read the mastery row
        var (resp, root, body) = await GetSkillMasteryAsync(skillId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "GET /Mastery/Skill/{id} must return 200; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // status = 2 (Mastered)
        TryProp(data, "status", out var statusProp).Should().BeTrue("data.status required; body: {0}", body);
        statusProp.GetInt32().Should().Be(2,
            "status must be 2 (Mastered) for ≥80% accuracy with threshold 80; body: {0}", body);

        // masteryPercentage ≥ 80
        TryProp(data, "masteryPercentage", out var pctProp).Should().BeTrue("body: {0}", body);
        pctProp.GetInt32().Should().BeGreaterThanOrEqualTo(80,
            "masteryPercentage must be ≥80 for Mastered; body: {0}", body);

        // attemptsCount must be at least 1
        TryProp(data, "attemptsCount", out var attProp).Should().BeTrue("body: {0}", body);
        attProp.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "attemptsCount must be ≥1 after completing an attempt; body: {0}", body);
    }

    // =========================================================================
    // Case 4 — Complete attempt <50% correct → NeedsReview
    // =========================================================================

    [Fact(DisplayName = "P309-C04 Complete attempt with <50% correct → NeedsReview (status=3, pct<50)")]
    public async Task CompleteAttempt_LowAccuracy_SkillNeedsReview()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId    = await SeedLessonAsync("C04-Lesson");
        // 5 questions; correct is "A", wrong is "B"
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, 5);

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit: 1 correct + 4 wrong = 20% < 50% → NeedsReview
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[2], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[3], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[4], "\"B\"", token); // wrong

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK, "CompleteAttempt must succeed; body: {0}", complBody);

        var (resp, root, body) = await GetSkillMasteryAsync(skillId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // status = 3 (NeedsReview)
        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        statusProp.GetInt32().Should().Be(3,
            "status must be 3 (NeedsReview) for <50% accuracy; body: {0}", body);

        // masteryPercentage < 50
        TryProp(data, "masteryPercentage", out var pctProp).Should().BeTrue("body: {0}", body);
        pctProp.GetInt32().Should().BeLessThan(50,
            "masteryPercentage must be <50 for NeedsReview; body: {0}", body);
    }

    // =========================================================================
    // Case 5 — Complete attempt ~60% correct (threshold 80) → InProgress
    // =========================================================================

    [Fact(DisplayName = "P309-C05 Complete attempt with ~60% correct (threshold 80) → InProgress (status=1, 50≤pct<80)")]
    public async Task CompleteAttempt_MidAccuracy_SkillInProgress()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId    = await SeedLessonAsync("C05-Lesson");
        // 5 questions; correct = "A"
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, 5);

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit: 3 correct + 2 wrong = 60% — sits in [50, 80) → InProgress
        await SubmitAnswerAsync(attemptId, questionIds[0], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[1], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[2], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId, questionIds[3], "\"B\"", token); // wrong
        await SubmitAnswerAsync(attemptId, questionIds[4], "\"B\"", token); // wrong

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK, "CompleteAttempt must succeed; body: {0}", complBody);

        var (resp, root, body) = await GetSkillMasteryAsync(skillId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // status = 1 (InProgress)
        TryProp(data, "status", out var statusProp).Should().BeTrue("body: {0}", body);
        statusProp.GetInt32().Should().Be(1,
            "status must be 1 (InProgress) for 60% accuracy with threshold 80; body: {0}", body);

        // masteryPercentage in [50, 80)
        TryProp(data, "masteryPercentage", out var pctProp).Should().BeTrue("body: {0}", body);
        var pct = pctProp.GetInt32();
        pct.Should().BeGreaterThanOrEqualTo(50,
            "masteryPercentage must be ≥50 for InProgress; body: {0}", body);
        pct.Should().BeLessThan(80,
            "masteryPercentage must be <80 (threshold) for InProgress; body: {0}", body);
    }

    // =========================================================================
    // Case 6 — Reproducibility: same attempt pattern twice → identical result
    // =========================================================================

    [Fact(DisplayName = "P309-C06 Reproducibility: completing the same answer pattern twice yields identical masteryPercentage and status")]
    public async Task Reproducibility_SamePattern_IdenticalMasteryResult()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);

        // ── First attempt ─────────────────────────────────────────────────────
        var lessonId1    = await SeedLessonAsync("C06-L1");
        var questionIds1 = await SeedQuestionsAsync(lessonId1, skillId, 4);

        var attemptId1 = await StartAttemptViaApiAsync(lessonId1, token);
        // 3 correct + 1 wrong = 75%
        await SubmitAnswerAsync(attemptId1, questionIds1[0], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId1, questionIds1[1], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId1, questionIds1[2], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId1, questionIds1[3], "\"B\"", token); // wrong
        await CompleteAttemptAsync(attemptId1, token);

        var (resp1, root1, body1) = await GetSkillMasteryAsync(skillId, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body1);
        TryProp(root1, "data", out var data1).Should().BeTrue("body: {0}", body1);
        TryProp(data1, "status", out var status1).Should().BeTrue("body: {0}", body1);
        TryProp(data1, "masteryPercentage", out var pct1).Should().BeTrue("body: {0}", body1);
        var firstStatus = status1.GetInt32();
        var firstPct    = pct1.GetInt32();

        // ── Second attempt — same lesson restarted with same questions ─────────
        // Re-start the same lesson (new attempt object, same skill-linked questions)
        var attemptId2 = await StartAttemptViaApiAsync(lessonId1, token);
        await SubmitAnswerAsync(attemptId2, questionIds1[0], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId2, questionIds1[1], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId2, questionIds1[2], "\"A\"", token); // correct
        await SubmitAnswerAsync(attemptId2, questionIds1[3], "\"B\"", token); // wrong
        await CompleteAttemptAsync(attemptId2, token);

        var (resp2, root2, body2) = await GetSkillMasteryAsync(skillId, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body2);
        TryProp(root2, "data", out var data2).Should().BeTrue("body: {0}", body2);
        TryProp(data2, "status", out var status2).Should().BeTrue("body: {0}", body2);
        TryProp(data2, "masteryPercentage", out var pct2).Should().BeTrue("body: {0}", body2);

        // Identical pct AND status
        status2.GetInt32().Should().Be(firstStatus,
            "status must be identical when the same answer pattern is submitted twice (deterministic engine); " +
            $"first={firstStatus}, second={status2.GetInt32()}; body1={body1} body2={body2}");

        pct2.GetInt32().Should().Be(firstPct,
            "masteryPercentage must be identical when the same answer pattern is submitted twice; " +
            $"first={firstPct}, second={pct2.GetInt32()}; body1={body1} body2={body2}");
    }

    // =========================================================================
    // Case 7 — IDOR: Student B cannot read Student A's mastery
    // =========================================================================

    [Fact(DisplayName = "P309-C07 IDOR: Student B calling GET /Mastery sees only their own data (empty), never Student A's rows")]
    public async Task IDOR_StudentB_CannotReadStudentA_Mastery()
    {
        // Student A earns some mastery on a skill
        var (tokenA, _) = await CreateStudentViaParentFlowAsync();
        var skillId      = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId     = await SeedLessonAsync("C07-Lesson");
        var questionIds  = await SeedQuestionsAsync(lessonId, skillId, 5);

        var attemptIdA = await StartAttemptViaApiAsync(lessonId, tokenA);
        foreach (var qId in questionIds)
            await SubmitAnswerAsync(attemptIdA, qId, "\"A\"", tokenA); // all correct → Mastered
        await CompleteAttemptAsync(attemptIdA, tokenA);

        // Confirm Student A has Mastered (sanity check)
        var (respA, rootA, bodyA) = await GetSkillMasteryAsync(skillId, tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "A's mastery must be readable; body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        TryProp(dataA, "status", out var statusA).Should().BeTrue("body: {0}", bodyA);
        statusA.GetInt32().Should().Be(2, "Student A must be Mastered; body: {0}", bodyA);

        // Student B — a different student with no attempts
        var (tokenB, _) = await CreateStudentViaParentFlowAsync();

        // B calls GET /Mastery/Skill/{skillId} — must get NotStarted (their own, empty state)
        var (respB_skill, rootB_skill, bodyB_skill) = await GetSkillMasteryAsync(skillId, tokenB);
        respB_skill.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student B's skill mastery query must return 200; body: {0}", bodyB_skill);
        TryProp(rootB_skill, "data", out var dataB_skill).Should().BeTrue("body: {0}", bodyB_skill);
        TryProp(dataB_skill, "status", out var statusB_skill).Should().BeTrue("body: {0}", bodyB_skill);
        statusB_skill.GetInt32().Should().Be(0,
            "Student B must see NotStarted (their own state), NOT Student A's Mastered row (IDOR guard); body: {0}", bodyB_skill);
        TryProp(dataB_skill, "masteryPercentage", out var pctB_skill).Should().BeTrue("body: {0}", bodyB_skill);
        pctB_skill.GetInt32().Should().Be(0,
            "Student B's masteryPercentage must be 0 (their own, not A's); body: {0}", bodyB_skill);

        // B calls GET /Mastery — must return empty list (not A's rows)
        var (respB_all, rootB_all, bodyB_all) = await GetStudentMasteryAsync(tokenB);
        respB_all.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB_all);
        TryProp(rootB_all, "data", out var dataB_all).Should().BeTrue("body: {0}", bodyB_all);
        dataB_all.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", bodyB_all);
        dataB_all.GetArrayLength().Should().Be(0,
            "Student B must see an empty list, not Student A's mastery rows (IDOR guard); body: {0}", bodyB_all);
    }

    // =========================================================================
    // Case 8 — Unknown skillId → 200 with NotStarted defaults, never 500
    // =========================================================================

    [Fact(DisplayName = "P309-C08 Unknown skillId → GET /Mastery/Skill/{id} returns 200 with NotStarted defaults, never 404 or 500")]
    public async Task UnknownSkillId_ReturnsNotStartedDefaults_Never500()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        const int phantomSkillId = 999_999_998;

        var (resp, root, body) = await GetSkillMasteryAsync(phantomSkillId, token);

        // Must be 200, never 404 or 500
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "unknown skillId must return 200 (NotStarted default), never 404 or 500; body: {0}", body);

        // Envelope shape
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // Defaults: status=0 (NotStarted), masteryPercentage=0
        TryProp(data, "status", out var statusProp).Should().BeTrue("data.status required; body: {0}", body);
        statusProp.GetInt32().Should().Be(0,
            "status must be 0 (NotStarted) for an unknown/untouched skillId; body: {0}", body);

        TryProp(data, "masteryPercentage", out var pctProp).Should().BeTrue("body: {0}", body);
        pctProp.GetInt32().Should().Be(0,
            "masteryPercentage must be 0 for NotStarted; body: {0}", body);
    }

    // =========================================================================
    // Auth guard — both endpoints must return 401 without a JWT
    // =========================================================================

    [Fact(DisplayName = "P309-AUTH-01 GET /Mastery without JWT → 401 Unauthorized")]
    public async Task GetMastery_WithoutJwt_Returns401()
    {
        var (resp, root, body) = await GetStudentMasteryAsync(token: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET /Mastery without JWT must return 401; body: {0}", body);
    }

    [Fact(DisplayName = "P309-AUTH-02 GET /Mastery/Skill/{id} without JWT → 401 Unauthorized")]
    public async Task GetSkillMastery_WithoutJwt_Returns401()
    {
        var (resp, root, body) = await GetSkillMasteryAsync(skillId: 1, token: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET /Mastery/Skill/{id} without JWT must return 401; body: {0}", body);
    }
}

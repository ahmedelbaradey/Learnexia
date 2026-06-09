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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P2-07 Extended integration tests — QC catalog cases not covered by the base P2_07_InstantAnswerFeedback_Tests.
///
/// NEW cases from docs/qc/P2-07/backend-test-cases.md:
///   BE-TC-03  Feedback is synchronous (no polling)
///   BE-TC-04  Answer result persisted with all fields
///   BE-TC-05  MCQ case-insensitive correct (OrdinalIgnoreCase)
///   BE-TC-06  MCQ wrong → DB row IsCorrect=false
///   BE-TC-08  TrueFalse wrong ("false" vs "True")
///   BE-TC-09  TrueFalse malformed payload → isCorrect=false (no 500)
///   BE-TC-11  FillInBlank wrong
///   BE-TC-12  Whitespace-only AnswerPayload → 200 isCorrect=false (or 422)
///   BE-TC-13  Matching string-compare fallback (documentary)
///   BE-TC-14  Correct answer NOT leaked on correct-path multi-type sweep
///   BE-TC-15  No ex.Message on error path
///   BE-TC-16  Forged isCorrect in body is ignored (server grades wrong → false)
///   BE-TC-17  Injected IsCorrect:false is ignored (server grades correct → true)
///   BE-TC-18  IDOR: Student A → Student B attempt → 401
///   BE-TC-19  Cross-lesson question injection → 404
///   BE-TC-20  Anonymous (no JWT) → 401
///   BE-TC-21  Parent JWT (wrong role) → 403
///   BE-TC-22  SuperAdmin JWT → 403
///   BE-TC-29  Event payload data minimization (no PII / no answer text)
///   BE-TC-30  AttemptId=0 in route → 422
///   BE-TC-31  QuestionId=0 → 422
///   BE-TC-32  Empty AnswerPayload → 422
///   BE-TC-33  TimeSpentSeconds boundaries (3600 ok, 3601 → 422, -1 → 422)
///
/// Cases already in base:
///   BE-TC-01(C01), BE-TC-02(C02), BE-TC-07(C03), BE-TC-10(C04), BE-TC-23(C05),
///   BE-TC-24(C06), BE-TC-23-NEG(C07), BE-TC-25(C08), BE-TC-26(C09), BE-TC-27(C10),
///   BE-TC-28(C11), envelope(C12).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_07_InstantAnswerFeedback_Extended_Tests : IAsyncLifetime
{
    private const string RegisterParentUrl   = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl           = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl         = "api/Parent/Add-Child";
    private const string SuperAdminUserName  = "superadmin";
    private const string SuperAdminPassword  = "123Pa$$word!";
    private const string ValidChildPassword  = "Child@Pass1";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P2_07_InstantAnswerFeedback_Extended_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();
        CapturingAnswerSubmittedHandler.Captured.Clear();
        CapturingLessonCompletedHandler.Captured.Clear();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"p207x_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = prop.Value; return true; }
        }
        value = default;
        return false;
    }

    private static async Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr)) try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, root, bodyStr);
    }

    private async Task<string> SignInAsync(string userName, string password)
    {
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, SignInUrl, new { UserName = userName, Password = password });
        r.StatusCode.Should().Be(HttpStatusCode.OK, $"sign-in must succeed; body={b}");
        TryProp(root, "data", out var d); TryProp(d, "accessToken", out var t);
        return t.GetString()!;
    }

    private async Task<(string Token, int UserId)> CreateParentAsync(string? tag = null)
    {
        var email = UniqueEmail(tag ?? "par");
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)r.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"parent reg; body={b}");
        TryProp(root, "data", out var d); TryProp(d, "accessToken", out var tok); TryProp(d, "userId", out var uid);
        return (tok.GetString()!, uid.GetInt32());
    }

    private async Task<(string StudentToken, int StudentId)> CreateStudentAsync(string? tag = null)
    {
        var (parentToken, _) = await CreateParentAsync(tag ?? "p");
        var childEmail = UniqueEmail(tag ?? "ch");
        var (addR, addRoot, addB) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new { FullName = "Test Student", Email = childEmail, Password = ValidChildPassword,
                  Grade = 3, Language = "ar", Country = "EG", LearningLanguage = "ar" }, parentToken);
        ((int)addR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"Add-Child; body={addB}");
        TryProp(addRoot, "data", out var d); TryProp(d, "id", out var id);
        var studentToken = await SignInAsync(childEmail, ValidChildPassword);
        return (studentToken, id.GetInt32());
    }

    private async Task<int> SeedLessonAsync(string name, int? skillId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var grade = new Grade { Number = 91, DisplayName = $"G_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade); await db.SaveChangesAsync();
        var subject = new Subject { Name = $"S_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject); await db.SaveChangesAsync();
        var unit = new Learnexia.Modules.Learning.Domain.Entities.Unit { Name = $"U_{Guid.NewGuid():N}", SequenceOrder = 1, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Units.AddAsync(unit); await db.SaveChangesAsync();
        var lesson = new Lesson { Name = name, Difficulty = DifficultyLevel.Easy, SequenceOrder = 1, IsLocked = false,
            UnitId = unit.Id, SkillId = skillId, IsActive = true, LifecycleState = LifecycleState.Published,
            CreatedAt = now, CreatedBy = 0 };
        await db.Lessons.AddAsync(lesson); await db.SaveChangesAsync();
        return lesson.Id;
    }

    private async Task<int> SeedQuestionAsync(int lessonId, QuestionType type, string correctAnswer, int? skillId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var q = new QuizQuestion { LessonId = lessonId, SkillId = skillId, QuestionType = type,
            QuestionText = "Q?", Options = "[\"A\",\"B\"]", CorrectAnswer = correctAnswer,
            Difficulty = DifficultyLevel.Easy, GeneratedBy = GeneratedBy.Curated,
            IsActive = true, LifecycleState = LifecycleState.Published, CreatedAt = now, CreatedBy = 0 };
        await db.QuizQuestions.AddAsync(q); await db.SaveChangesAsync();
        return q.Id;
    }

    private async Task<int> SeedSkillAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var grade = new Grade { Number = 92, DisplayName = $"SG_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade); await db.SaveChangesAsync();
        var subject = new Subject { Name = $"SS_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject); await db.SaveChangesAsync();
        var concept = new Concept { Name = $"SC_{Guid.NewGuid():N}", DifficultyLevel = DifficultyLevel.Easy, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Concepts.AddAsync(concept); await db.SaveChangesAsync();
        var skill = new Skill { Name = $"Sk_{Guid.NewGuid():N}", MasteryThreshold = 70, EstimatedTimeMinutes = 15, ConceptId = concept.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Skills.AddAsync(skill); await db.SaveChangesAsync();
        return skill.Id;
    }

    private async Task<int> StartAttemptAsync(int lessonId, string studentToken)
    {
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        r.StatusCode.Should().Be(HttpStatusCode.OK, $"StartAttempt; body={b}");
        TryProp(root, "data", out var d); TryProp(d, "attemptId", out var id);
        return id.GetInt32();
    }

    private Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SubmitAsync(int attemptId, int questionId, string answerPayload, int timeSpent, bool hintUsed, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answerPayload, TimeSpentSeconds = timeSpent, HintUsed = hintUsed }, token);

    private Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        CompleteAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-03: Synchronous feedback (no polling)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-03: Feedback is synchronous — data.isCorrect present in the immediate POST response")]
    public async Task BeTc03_FeedbackIsSynchronous_NoPollingRequired()
    {
        var lessonId = await SeedLessonAsync("x03");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s03");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "\"A\"", 5, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        // isCorrect must be in the immediate response body
        TryProp(root, "data", out var data);
        bool hasIsCorrect = TryProp(data, "isCorrect", out _);
        hasIsCorrect.Should().BeTrue($"isCorrect must be in the synchronous POST response; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-04: Answer result persisted with all fields
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-04: Answer result persisted — IsCorrect, TimeSpentSeconds, HintUsed match submitted values")]
    public async Task BeTc04_AnswerPersisted_AllFieldsCorrect()
    {
        var lessonId = await SeedLessonAsync("x04");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, studentId) = await CreateStudentAsync("s04");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, _, body) = await SubmitAsync(attemptId, qId, "\"B\"", 15, true, token); // wrong
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var row = await db.StudentAnswers.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == qId);
        row.Should().NotBeNull($"StudentAnswer row must exist; body={body}");
        row!.IsCorrect.Should().BeFalse($"server grades 'B' vs 'A' as wrong; body={body}");
        row.TimeSpentSeconds.Should().Be(15, $"TimeSpentSeconds must be persisted; body={body}");
        row.HintUsed.Should().BeTrue($"HintUsed=true must be persisted; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-05: MCQ case-insensitive (OrdinalIgnoreCase)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-05: MCQ OrdinalIgnoreCase — lowercase 'a' matches stored '\"A\"'")]
    public async Task BeTc05_McqCaseInsensitive()
    {
        var lessonId = await SeedLessonAsync("x05");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s05");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "\"a\"", 5, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeTrue($"'a' must match 'A' case-insensitively; body={body}");
        if (TryProp(data, "correctAnswer", out var ca))
            ca.ValueKind.Should().Be(JsonValueKind.Null, $"correctAnswer must be null when correct; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-06: MCQ wrong → DB row IsCorrect=false
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-06: MCQ wrong → DB row has IsCorrect=false (server authority)")]
    public async Task BeTc06_McqWrong_DbRowIsCorrectFalse()
    {
        var lessonId = await SeedLessonAsync("x06");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s06");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "\"B\"", 10, false, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeFalse($"'B' vs 'A' must be wrong; body={body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var row = await db.StudentAnswers.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == qId);
        row.Should().NotBeNull();
        row!.IsCorrect.Should().BeFalse($"DB IsCorrect must also be false; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-08: TrueFalse wrong
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-08: TrueFalse 'false' vs stored 'true' → isCorrect=false, correctAnswer populated")]
    public async Task BeTc08_TrueFalse_WrongAnswer()
    {
        var lessonId = await SeedLessonAsync("x08");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.TrueFalse, "true");
        var (token, _) = await CreateStudentAsync("s08");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "false", 5, false, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeFalse($"'false' vs 'true' must be wrong; body={body}");
        TryProp(data, "correctAnswer", out var ca);
        ca.ValueKind.Should().NotBe(JsonValueKind.Null, $"correctAnswer must be populated when wrong; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-09: TrueFalse malformed → isCorrect=false, no 500
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-09: TrueFalse malformed payload ('yes','1') → isCorrect=false, no 500")]
    public async Task BeTc09_TrueFalse_MalformedPayload_IsCorrectFalse()
    {
        var lessonId = await SeedLessonAsync("x09");
        var qId1 = await SeedQuestionAsync(lessonId, QuestionType.TrueFalse, "true");
        var qId2 = await SeedQuestionAsync(lessonId, QuestionType.TrueFalse, "true");
        var (token, _) = await CreateStudentAsync("s09");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // "yes" — bool.TryParse fails → should grade as false
        var (r1, root1, b1) = await SubmitAsync(attemptId, qId1, "yes", 5, false, token);
        ((int)r1.StatusCode).Should().BeOneOf(new[] { 200, 422 }, $"'yes' must not cause 500; body={b1}");
        if ((int)r1.StatusCode == 200)
        {
            TryProp(root1, "data", out var d1); TryProp(d1, "isCorrect", out var ic1);
            ic1.GetBoolean().Should().BeFalse($"'yes' must grade as false; body={b1}");
        }

        // "1" — bool.TryParse fails → should grade as false
        var (r2, root2, b2) = await SubmitAsync(attemptId, qId2, "1", 5, false, token);
        ((int)r2.StatusCode).Should().BeOneOf(new[] { 200, 422 }, $"'1' must not cause 500; body={b2}");
        if ((int)r2.StatusCode == 200)
        {
            TryProp(root2, "data", out var d2); TryProp(d2, "isCorrect", out var ic2);
            ic2.GetBoolean().Should().BeFalse($"'1' must grade as false; body={b2}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-11: FillInBlank wrong
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-11: FillInBlank 'giza' vs stored 'cairo' → isCorrect=false, correctAnswer='cairo'")]
    public async Task BeTc11_FillInBlank_Wrong()
    {
        var lessonId = await SeedLessonAsync("x11");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.FillInBlank, "\"cairo\"");
        var (token, _) = await CreateStudentAsync("s11");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "\"giza\"", 5, false, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeFalse($"'giza' vs 'cairo' must be wrong; body={body}");
        TryProp(data, "correctAnswer", out var ca);
        ca.ValueKind.Should().NotBe(JsonValueKind.Null, $"correctAnswer must be populated when wrong; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-12: Whitespace-only payload → 200 isCorrect=false (or 422)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-12: Whitespace-only AnswerPayload ('   ') → 200 isCorrect=false (or 422 if NotEmpty rejects whitespace)")]
    public async Task BeTc12_WhitespacePayload_IsCorrectFalseOr422()
    {
        var lessonId = await SeedLessonAsync("x12");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s12");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "   ", 5, false, token);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 422 },
            $"whitespace payload must return 200 (comparator false) or 422 (NotEmpty rejects whitespace); body={body}");

        if ((int)resp.StatusCode == 200)
        {
            TryProp(root, "data", out var data); TryProp(data, "isCorrect", out var ic);
            ic.GetBoolean().Should().BeFalse($"whitespace payload must grade as false; body={body}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-13: Matching string-compare fallback (documentary)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-13: Matching type → string OrdinalIgnoreCase compare (Phase-2 fallback, documented behavior)")]
    public async Task BeTc13_Matching_StringCompareFallback()
    {
        var lessonId = await SeedLessonAsync("x13");
        // CorrectAnswer for Matching must be valid JSON (jsonb column). Use JSON string "\"x\"".
        var qId1 = await SeedQuestionAsync(lessonId, QuestionType.Matching, "\"x\"");
        var qId2 = await SeedQuestionAsync(lessonId, QuestionType.Matching, "\"x\"");
        var (token, _) = await CreateStudentAsync("s13");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // Submit "\"x\"" (JSON-quoted to match stored "\"x\"" value) → isCorrect=true
        var (ra, roota, ba) = await SubmitAsync(attemptId, qId1, "\"x\"", 5, false, token);
        ra.StatusCode.Should().Be(HttpStatusCode.OK, $"Matching '\"x\"' vs stored '\"x\"'; body={ba}");
        TryProp(roota, "data", out var da); TryProp(da, "isCorrect", out var ica);
        ica.GetBoolean().Should().BeTrue($"Matching '\"x\"' == stored '\"x\"' → true (Phase-2 fallback string compare); body={ba}");

        // Submit "\"y\"" vs stored "\"x\"" → isCorrect=false
        var (rb, rootb, bb) = await SubmitAsync(attemptId, qId2, "\"y\"", 5, false, token);
        rb.StatusCode.Should().Be(HttpStatusCode.OK, $"Matching '\"y\"' vs stored '\"x\"'; body={bb}");
        TryProp(rootb, "data", out var db); TryProp(db, "isCorrect", out var icb);
        icb.GetBoolean().Should().BeFalse($"Matching '\"y\"' != stored '\"x\"' → false (Phase-2 fallback); body={bb}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-14: Correct answer never leaked on the correct path (multi-type sweep)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-14: Correct answer NOT leaked in response body on correct submissions (MCQ, TrueFalse, FillInBlank)")]
    public async Task BeTc14_CorrectAnswer_NotLeaked_OnCorrectPath()
    {
        var lessonId = await SeedLessonAsync("x14");
        var mcqId  = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"SecretMCQ\"");
        var tfId   = await SeedQuestionAsync(lessonId, QuestionType.TrueFalse, "true");
        var fibId  = await SeedQuestionAsync(lessonId, QuestionType.FillInBlank, "\"SecretFIB\"");
        var (token, _) = await CreateStudentAsync("s14");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // MCQ
        var (r1, root1, b1) = await SubmitAsync(attemptId, mcqId, "\"SecretMCQ\"", 5, false, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, $"MCQ correct; body={b1}");
        TryProp(root1, "data", out var d1);
        if (TryProp(d1, "correctAnswer", out var ca1))
            ca1.ValueKind.Should().Be(JsonValueKind.Null, $"correctAnswer must be null on MCQ correct; body={b1}");
        b1.Should().NotContain("SecretMCQ", $"correct answer string must not appear in MCQ correct response; body={b1}");

        // TrueFalse
        var (r2, root2, b2) = await SubmitAsync(attemptId, tfId, "true", 5, false, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, $"TF correct; body={b2}");
        TryProp(root2, "data", out var d2);
        if (TryProp(d2, "correctAnswer", out var ca2))
            ca2.ValueKind.Should().Be(JsonValueKind.Null, $"correctAnswer must be null on TF correct; body={b2}");

        // FillInBlank
        var (r3, root3, b3) = await SubmitAsync(attemptId, fibId, "\"SecretFIB\"", 5, false, token);
        r3.StatusCode.Should().Be(HttpStatusCode.OK, $"FIB correct; body={b3}");
        TryProp(root3, "data", out var d3);
        if (TryProp(d3, "correctAnswer", out var ca3))
            ca3.ValueKind.Should().Be(JsonValueKind.Null, $"correctAnswer must be null on FIB correct; body={b3}");
        b3.Should().NotContain("SecretFIB", $"correct answer string must not appear in FIB correct response; body={b3}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-15: No ex.Message on error path
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-15: Error responses do not leak ex.Message/stack-trace/answer text")]
    public async Task BeTc15_NoExMessageLeak_OnErrorPath()
    {
        var lessonId = await SeedLessonAsync("x15");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s15");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // Submit once to succeed, then duplicate → 424
        await SubmitAsync(attemptId, qId, "\"A\"", 5, false, token);
        var (resp, root, body) = await SubmitAsync(attemptId, qId, "\"A\"", 5, false, token);

        ((int)resp.StatusCode).Should().Be(424, $"duplicate must return 424; body={body}");
        body.Should().NotContain("Exception", $"body must not contain exception type name; body={body}");
        body.Should().NotContain("StackTrace", $"body must not contain stack trace; body={body}");
        body.Should().NotContain("at System.", $"body must not contain stack frames; body={body}");
        body.Should().NotContain("\"A\"", $"correct answer must not leak in error response; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-16: Forged isCorrect:true in body is ignored (server grades wrong → false)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-16: Injected IsCorrect:true in request body is ignored — server grades wrong answer as false")]
    public async Task BeTc16_ForgedIsCorrect_Ignored_ServerGradesWrong()
    {
        var lessonId = await SeedLessonAsync("x16");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s16");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // POST with forged extra fields
        var req = new HttpRequestMessage(HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers");
        req.Content = new StringContent(
            $"{{\"QuestionId\":{qId},\"AnswerPayload\":\"\\\"B\\\"\",\"TimeSpentSeconds\":5,\"HintUsed\":false,\"IsCorrect\":true,\"CorrectAnswer\":\"\\\"B\\\"\"}}",
            Encoding.UTF8, "application/json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var r = await _client.SendAsync(req);
        var body = await r.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;

        r.StatusCode.Should().Be(HttpStatusCode.OK, $"forged fields body must still return 200; body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeFalse($"server must grade 'B' as false despite injected IsCorrect:true; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-17: Injected IsCorrect:false in body is ignored (server grades correct → true)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-17: Injected IsCorrect:false in body is ignored — server grades correct answer as true")]
    public async Task BeTc17_ForgedIsCorrectFalse_Ignored_ServerGradesCorrect()
    {
        var lessonId = await SeedLessonAsync("x17");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s17");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var req = new HttpRequestMessage(HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers");
        req.Content = new StringContent(
            $"{{\"QuestionId\":{qId},\"AnswerPayload\":\"\\\"A\\\"\",\"TimeSpentSeconds\":5,\"HintUsed\":false,\"IsCorrect\":false}}",
            Encoding.UTF8, "application/json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var r = await _client.SendAsync(req);
        var body = await r.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(body).RootElement;

        r.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeTrue($"server must grade 'A' as true despite injected IsCorrect:false; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-18: IDOR → 401
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-18: Student A submits to Student B's attempt → 401 (IDOR)")]
    public async Task BeTc18_Idor_401()
    {
        var lessonId = await SeedLessonAsync("x18");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (tokenA, _) = await CreateStudentAsync("s18a");
        var (tokenB, _) = await CreateStudentAsync("s18b");
        var attemptB = await StartAttemptAsync(lessonId, tokenB);

        var (resp, root, body) = await SubmitAsync(attemptB, qId, "\"A\"", 5, false, tokenA);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"IDOR must return 401; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-19: Cross-lesson question injection → 404
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-19: Cross-lesson question injection (Q from L2 into L1 attempt) → 404")]
    public async Task BeTc19_CrossLessonInjection_404()
    {
        var lessonId1 = await SeedLessonAsync("x19L1");
        var lessonId2 = await SeedLessonAsync("x19L2");
        var qL2 = await SeedQuestionAsync(lessonId2, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s19");
        var attemptId = await StartAttemptAsync(lessonId1, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qL2, "\"A\"", 5, false, token);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"cross-lesson Q must return 404; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-20: Anonymous → 401
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-20: No JWT → 401 (framework challenge before handler)")]
    public async Task BeTc20_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Answers",
            new { QuestionId = 1, AnswerPayload = "\"A\"", TimeSpentSeconds = 5, HintUsed = false }, null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"anonymous must return 401; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-21: Parent JWT → 403
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-21: Parent JWT on SubmitAnswer → 403 (role gate: Student only)")]
    public async Task BeTc21_ParentJwt_Returns403()
    {
        var (parentToken, _) = await CreateParentAsync("p21");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Answers",
            new { QuestionId = 1, AnswerPayload = "\"A\"", TimeSpentSeconds = 5, HintUsed = false }, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"parent JWT must return 403; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-22: SuperAdmin JWT → 403
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-22: SuperAdmin JWT on SubmitAnswer → 403 (role gate: Student only)")]
    public async Task BeTc22_SuperAdminJwt_Returns403()
    {
        string adminToken;
        try { adminToken = await SignInAsync(SuperAdminUserName, SuperAdminPassword); }
        catch { return; } // superadmin not present in this DB — skip

        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Answers",
            new { QuestionId = 1, AnswerPayload = "\"A\"", TimeSpentSeconds = 5, HintUsed = false }, adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"superadmin JWT must return 403; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-29: Event payload data minimization
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-29: AnswerSubmittedIntegrationEvent payload has no PII/answer text — only opaque IDs")]
    public async Task BeTc29_EventPayload_DataMinimization()
    {
        // Create a fork with the capturing handler
        var fork = _factory.WithWebHostBuilder(b => b.ConfigureServices(svc =>
            svc.AddSingleton<INotificationHandler<AnswerSubmittedIntegrationEvent>, CapturingAnswerSubmittedHandler>()));
        var forkClient = fork.CreateClient();

        var (parentToken, _) = await CreateParentAsync("p29");
        var childEmail = UniqueEmail("s29");
        var (addR, addRoot, addB) = await SendAsync(forkClient, HttpMethod.Post, AddChildUrl,
            new { FullName = "P29 Student", Email = childEmail, Password = ValidChildPassword,
                  Grade = 3, Language = "ar", Country = "EG", LearningLanguage = "ar" }, parentToken);
        ((int)addR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"Add-Child; body={addB}");
        var signInR = await forkClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, SignInUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { UserName = childEmail, Password = ValidChildPassword }), Encoding.UTF8, "application/json")
        });
        var signInBody = await signInR.Content.ReadAsStringAsync();
        var signInRoot = JsonDocument.Parse(signInBody).RootElement;
        TryProp(signInRoot, "data", out var sd); TryProp(sd, "accessToken", out var st);
        var studentToken = st.GetString()!;

        var skillId = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("x29");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"SecretAnswer\"", skillId);

        async Task<(HttpResponseMessage, JsonElement, string)> SendViaFork(HttpMethod m, string url, object? body2 = null, string? tok2 = null)
        {
            var req = new HttpRequestMessage(m, url);
            if (body2 is not null) req.Content = new StringContent(JsonSerializer.Serialize(body2), Encoding.UTF8, "application/json");
            if (tok2 is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok2);
            var r = await forkClient.SendAsync(req);
            var bs = await r.Content.ReadAsStringAsync();
            JsonElement root = default;
            if (!string.IsNullOrWhiteSpace(bs)) try { root = JsonDocument.Parse(bs).RootElement; } catch { }
            return (r, root, bs);
        }

        var (startR, startRoot, startB) = await SendViaFork(HttpMethod.Post, $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        startR.StatusCode.Should().Be(HttpStatusCode.OK, $"StartAttempt; body={startB}");
        TryProp(startRoot, "data", out var startD); TryProp(startD, "attemptId", out var aid);
        var attemptId = aid.GetInt32();

        CapturingAnswerSubmittedHandler.Captured.Clear();

        await SendViaFork(HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = qId, AnswerPayload = "\"SecretAnswer\"", TimeSpentSeconds = 5, HintUsed = false }, studentToken);

        CapturingAnswerSubmittedHandler.Captured.Should().HaveCount(1, "event must be captured");
        var evt = CapturingAnswerSubmittedHandler.Captured.Single();

        // Assert data minimization: opaque IDs only, no answer text
        evt.EventId.Should().NotBe(Guid.Empty, "EventId must be a GUID");
        evt.OccurredOnUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
        evt.StudentId.Should().BeGreaterThan(0, "StudentId must be a positive int");
        evt.LessonId.Should().Be(lessonId, "LessonId must match");
        evt.SkillId.Should().Be(skillId, "SkillId must match");
        evt.CorrectAnswerCount.Should().BeOneOf(new[] { 0, 1 }, "CorrectAnswerCount is 0 or 1");

        // No PII or answer text in the event
        // (AnswerSubmittedIntegrationEvent must NOT have AnswerPayload or CorrectAnswer fields)
        var evtJson = JsonSerializer.Serialize(evt);
        evtJson.Should().NotContain("SecretAnswer", $"event JSON must not contain the answer value; evtJson={evtJson}");
        evtJson.Should().NotContain("AnswerPayload", $"event JSON must not contain AnswerPayload field; evtJson={evtJson}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-30: AttemptId=0 → 422
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-30: AttemptId=0 in route → 422 validation")]
    public async Task BeTc30_AttemptIdZero_Returns422()
    {
        var (token, _) = await CreateStudentAsync("s30");
        var (resp, root, body) = await SubmitAsync(0, 1, "\"A\"", 5, false, token);
        ((int)resp.StatusCode).Should().Be(422, $"AttemptId=0 must return 422; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-31: QuestionId=0 → 422
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-31: QuestionId=0 → 422 validation")]
    public async Task BeTc31_QuestionIdZero_Returns422()
    {
        var lessonId = await SeedLessonAsync("x31");
        var (token, _) = await CreateStudentAsync("s31");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, 0, "\"A\"", 5, false, token);
        ((int)resp.StatusCode).Should().Be(422, $"QuestionId=0 must return 422; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-32: Empty AnswerPayload → 422
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-32: Empty AnswerPayload → 422 (AnswerPayload NotEmpty)")]
    public async Task BeTc32_EmptyAnswerPayload_Returns422()
    {
        var lessonId = await SeedLessonAsync("x32");
        var qId = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s32");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "", 5, false, token);
        ((int)resp.StatusCode).Should().Be(422, $"empty AnswerPayload must return 422; body={body}");
        TryProp(root, "successed", out var suc);
        suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BE-TC-33: TimeSpentSeconds boundaries
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-33: TimeSpentSeconds: -1→422, 0→200, 3600→200, 3601→422")]
    public async Task BeTc33_TimeSpentSeconds_Boundaries()
    {
        var lessonId = await SeedLessonAsync("x33");
        var qId1 = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var qId2 = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var qId3 = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var qId4 = await SeedQuestionAsync(lessonId, QuestionType.MCQ, "\"A\"");
        var (token, _) = await CreateStudentAsync("s33");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // -1 → 422
        var (r1, root1, b1) = await SubmitAsync(attemptId, qId1, "\"A\"", -1, false, token);
        ((int)r1.StatusCode).Should().Be(422, $"TimeSpentSeconds=-1 must return 422; body={b1}");
        TryProp(root1, "successed", out var s1); s1.GetBoolean().Should().BeFalse($"body={b1}");

        // 0 → 200
        var (r2, root2, b2) = await SubmitAsync(attemptId, qId1, "\"A\"", 0, false, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, $"TimeSpentSeconds=0 must return 200; body={b2}");
        TryProp(root2, "successed", out var s2); s2.GetBoolean().Should().BeTrue($"body={b2}");

        // 3600 → 200 (inclusive ceiling)
        var (r3, root3, b3) = await SubmitAsync(attemptId, qId2, "\"A\"", 3600, false, token);
        r3.StatusCode.Should().Be(HttpStatusCode.OK, $"TimeSpentSeconds=3600 must return 200; body={b3}");
        TryProp(root3, "successed", out var s3); s3.GetBoolean().Should().BeTrue($"body={b3}");

        // 3601 → 422
        var (r4, root4, b4) = await SubmitAsync(attemptId, qId3, "\"A\"", 3601, false, token);
        ((int)r4.StatusCode).Should().Be(422, $"TimeSpentSeconds=3601 must return 422; body={b4}");
        TryProp(root4, "successed", out var s4); s4.GetBoolean().Should().BeFalse($"body={b4}");
    }
}

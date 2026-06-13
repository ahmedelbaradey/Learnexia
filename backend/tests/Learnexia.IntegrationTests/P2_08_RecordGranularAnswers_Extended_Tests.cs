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
/// P2-08 Extended integration tests — QC catalog cases not covered by the base P2_08_RecordGranularAnswers_Tests.
///
/// NEW cases from docs/qc/P2-08/backend-test-cases.md:
///   BE-TC-03  HintUsed=true persists
///   BE-TC-04  Server-side grading authority (TrueFalse "TRUE" → isCorrect=true)
///   BE-TC-09  Cross-lesson question injection → 404
///   BE-TC-10  Missing JWT → 401 (framework)
///   BE-TC-11  Parent/Admin JWT → 403 (role gate)
///   BE-TC-12  Empty/null AnswerPayload → 422
///   BE-TC-13  QuestionId ≤ 0 → 422
///   BE-TC-14  TimeSpentSeconds boundaries → 422/-1, 200/0, 200/3600, 422/3601
///   BE-TC-15  AttemptId ≤ 0 in route → 422
///   BE-TC-16  Oversized AnswerPayload (1 MB) → document actual (200 or 422)
///   BE-TC-19  AccuracyPercentage rounding (1 of 3 = 33.33)
///   BE-TC-21  Complete an Abandoned attempt → 424
///   BE-TC-22  Complete IDOR → 401
///   BE-TC-23  Complete non-existent attempt → 404
///   BE-TC-24  Complete: missing JWT → 401; Parent JWT → 403
///   BE-TC-25  Complete: AttemptId ≤ 0 → 422
///   BE-TC-26  Submitted answers survive completion
///   BE-TC-30  Abandon a Completed attempt → 424
///   BE-TC-31  Abandon IDOR → 401
///   BE-TC-32  Abandon non-existent attempt → 404
///   BE-TC-33  Abandon: missing JWT → 401; Parent JWT → 403; AttemptId ≤ 0 → 422
///   BE-TC-34  Abandoned answers are retrievable afterward
///   BE-TC-38  Empty attempts list → 200 empty array (not 404)
///   BE-TC-39  studentId ≤ 0 → 400 (inline, not 422)
///   BE-TC-40  E4 auth gates: missing JWT 401; unlinked parent 403 generic; own id 200
///   BE-TC-40b E4 linked parent reads own child's attempts → 200 (parent↔child seam)
///   BE-TC-44  Skill stats scoped to requesting student
///   BE-TC-45  Skill stats IDOR → 401
///   BE-TC-46  Validation: skillId ≤ 0 → 400; studentId ≤ 0 → 400
///   BE-TC-47  E5 missing JWT → 401
///   BE-TC-48  BaseResponse envelope shape + successed spelling
///   BE-TC-50  DurationSeconds is server-side elapsed (non-negative)
///   BE-TC-51  Mass-assignment guard (forged studentId / isCorrect ignored)
///
/// Cases already in base file: C01-C17.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P2_08_RecordGranularAnswers_Extended_Tests : IAsyncLifetime
{
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string SuperAdminUserName = "superadmin";
    private const string SuperAdminPassword = "123Pa$$word!";
    private const string ValidChildPassword = "Child@Pass1";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P2_08_RecordGranularAnswers_Extended_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"p208x_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        value = default;
        return false;
    }

    private static async Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? token = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (token is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var r = await client.SendAsync(req);
        var bs = await r.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bs)) try { root = JsonDocument.Parse(bs).RootElement; } catch { }
        return (r, root, bs);
    }

    private async Task<string> SignInAsync(string userName, string password)
    {
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, SignInUrl, new { UserName = userName, Password = password });
        r.StatusCode.Should().Be(HttpStatusCode.OK, $"sign-in; body={b}");
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
        var (parentToken, _) = await CreateParentAsync(tag);
        var email = UniqueEmail(tag ?? "ch");
        var (addR, addRoot, addB) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new { FullName = "Student", Email = email, Password = ValidChildPassword,
                  Grade = 3, Language = "ar", Country = "EG", LearningLanguage = "ar" }, parentToken);
        ((int)addR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"Add-Child; body={addB}");
        TryProp(addRoot, "data", out var d); TryProp(d, "id", out var id);
        var token = await SignInAsync(email, ValidChildPassword);
        return (token, id.GetInt32());
    }

    private async Task<int> SeedLessonAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var grade = new Grade { Number = 93, DisplayName = $"G_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade); await db.SaveChangesAsync();
        var subject = new Subject { Name = $"S_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject); await db.SaveChangesAsync();
        var unit = new Unit { Name = $"U_{Guid.NewGuid():N}", SequenceOrder = 1, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Units.AddAsync(unit); await db.SaveChangesAsync();
        var lesson = new Lesson { Name = name, Difficulty = DifficultyLevel.Easy, SequenceOrder = 1, IsLocked = false,
            UnitId = unit.Id, IsActive = true, LifecycleState = LifecycleState.Published, CreatedAt = now, CreatedBy = 0 };
        await db.Lessons.AddAsync(lesson); await db.SaveChangesAsync();
        return lesson.Id;
    }

    private async Task<int> SeedQuestionAsync(int lessonId, int? skillId = null, QuestionType type = QuestionType.MCQ, string correct = "\"A\"")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var q = new QuizQuestion { LessonId = lessonId, SkillId = skillId, QuestionType = type,
            QuestionText = "Q?", Options = "[\"A\",\"B\"]", CorrectAnswer = correct,
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
        var grade = new Grade { Number = 94, DisplayName = $"SG_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade); await db.SaveChangesAsync();
        var subject = new Subject { Name = $"SS_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject); await db.SaveChangesAsync();
        var concept = new Concept { Name = $"SC_{Guid.NewGuid():N}", DifficultyLevel = DifficultyLevel.Easy, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Concepts.AddAsync(concept); await db.SaveChangesAsync();
        var skill = new Skill { Name = $"Sk_{Guid.NewGuid():N}", MasteryThreshold = 70, EstimatedTimeMinutes = 15, ConceptId = concept.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Skills.AddAsync(skill); await db.SaveChangesAsync();
        return skill.Id;
    }

    private async Task<int> StartAttemptAsync(int lessonId, string token)
    {
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{lessonId}/Attempt", null, token);
        r.StatusCode.Should().Be(HttpStatusCode.OK, $"StartAttempt; body={b}");
        TryProp(root, "data", out var d); TryProp(d, "attemptId", out var id);
        return id.GetInt32();
    }

    private Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SubmitAsync(int attemptId, int questionId, string payload, int timeSpent, bool hintUsed, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = payload, TimeSpentSeconds = timeSpent, HintUsed = hintUsed }, token);

    private Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        CompleteAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    private Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        AbandonAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Abandon", null, token);

    // ─────────────────────────────────────────────────────────────────────────
    // Group A — SubmitAnswer NEW cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-03: HintUsed=true persists to DB")]
    public async Task BeTc03_HintUsedTrue_Persists()
    {
        var lessonId = await SeedLessonAsync("x03");
        var qId = await SeedQuestionAsync(lessonId);
        var (token, _) = await CreateStudentAsync("s03");
        var attemptId = await StartAttemptAsync(lessonId, token);
        await SubmitAsync(attemptId, qId, "\"A\"", 5, true, token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var row = await db.StudentAnswers.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == qId);
        row.Should().NotBeNull("StudentAnswer row must exist");
        row!.HintUsed.Should().BeTrue("HintUsed=true must be persisted");
    }

    [Fact(DisplayName = "BE-TC-04: TrueFalse 'TRUE' → isCorrect=true (server-side OrdinalIgnoreCase via bool.TryParse)")]
    public async Task BeTc04_TrueFalse_UppercaseTrue_IsCorrectTrue()
    {
        var lessonId = await SeedLessonAsync("x04");
        var qId = await SeedQuestionAsync(lessonId, type: QuestionType.TrueFalse, correct: "true");
        var (token, _) = await CreateStudentAsync("s04");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (resp, root, body) = await SubmitAsync(attemptId, qId, "TRUE", 5, false, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var d); TryProp(d, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeTrue($"'TRUE' must match 'true' via bool.TryParse; body={body}");
    }

    [Fact(DisplayName = "BE-TC-09: Cross-lesson question injection → 404")]
    public async Task BeTc09_CrossLessonInjection_Returns404()
    {
        var lessonA = await SeedLessonAsync("x09a");
        var lessonB = await SeedLessonAsync("x09b");
        var qB = await SeedQuestionAsync(lessonB);
        var (token, _) = await CreateStudentAsync("s09");
        var attemptA = await StartAttemptAsync(lessonA, token);

        var (resp, root, body) = await SubmitAsync(attemptA, qB, "\"A\"", 5, false, token);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"cross-lesson Q must return 404; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-10: Missing JWT on SubmitAnswer → 401 (framework)")]
    public async Task BeTc10_MissingJwt_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Answers",
            new { QuestionId = 1, AnswerPayload = "\"A\"", TimeSpentSeconds = 5, HintUsed = false }, null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"no JWT must return 401; body={body}");
    }

    [Fact(DisplayName = "BE-TC-11: Parent JWT on SubmitAnswer → 403 (role gate)")]
    public async Task BeTc11_ParentJwt_Returns403()
    {
        var (parentToken, _) = await CreateParentAsync("p11");
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Answers",
            new { QuestionId = 1, AnswerPayload = "\"A\"", TimeSpentSeconds = 5, HintUsed = false }, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"parent JWT must return 403; body={body}");
    }

    [Fact(DisplayName = "BE-TC-12: Empty AnswerPayload → 422 (NotEmpty)")]
    public async Task BeTc12_EmptyPayload_Returns422()
    {
        var lessonId = await SeedLessonAsync("x12");
        var qId = await SeedQuestionAsync(lessonId);
        var (token, _) = await CreateStudentAsync("s12");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (r1, root1, b1) = await SubmitAsync(attemptId, qId, "", 5, false, token);
        ((int)r1.StatusCode).Should().Be(422, $"empty payload must return 422; body={b1}");

        var (r2, root2, b2) = await SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = qId, TimeSpentSeconds = 5, HintUsed = false }, token);
        ((int)r2.StatusCode).Should().Be(422, $"null/missing payload must return 422; body={b2}");
    }

    [Fact(DisplayName = "BE-TC-13: QuestionId=0 and QuestionId=-1 → 422")]
    public async Task BeTc13_QuestionIdZeroOrNeg_Returns422()
    {
        var lessonId = await SeedLessonAsync("x13");
        var (token, _) = await CreateStudentAsync("s13");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (r1, root1, b1) = await SubmitAsync(attemptId, 0, "\"A\"", 5, false, token);
        ((int)r1.StatusCode).Should().Be(422, $"QuestionId=0 must return 422; body={b1}");

        var (r2, root2, b2) = await SubmitAsync(attemptId, -1, "\"A\"", 5, false, token);
        ((int)r2.StatusCode).Should().Be(422, $"QuestionId=-1 must return 422; body={b2}");
    }

    [Fact(DisplayName = "BE-TC-14: TimeSpentSeconds: -1→422, 0→200, 3600→200, 3601→422")]
    public async Task BeTc14_TimeSpentSeconds_Boundaries()
    {
        var lessonId = await SeedLessonAsync("x14");
        var qId1 = await SeedQuestionAsync(lessonId);
        var qId2 = await SeedQuestionAsync(lessonId);
        var qId3 = await SeedQuestionAsync(lessonId);
        var (token, _) = await CreateStudentAsync("s14");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var (r1, _, b1) = await SubmitAsync(attemptId, qId1, "\"A\"", -1, false, token);
        ((int)r1.StatusCode).Should().Be(422, $"TimeSpentSeconds=-1 must return 422; body={b1}");

        var (r2, root2, b2) = await SubmitAsync(attemptId, qId1, "\"A\"", 0, false, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, $"TimeSpentSeconds=0 must return 200; body={b2}");

        var (r3, root3, b3) = await SubmitAsync(attemptId, qId2, "\"A\"", 3600, false, token);
        r3.StatusCode.Should().Be(HttpStatusCode.OK, $"TimeSpentSeconds=3600 must return 200; body={b3}");

        var (r4, _, b4) = await SubmitAsync(attemptId, qId3, "\"A\"", 3601, false, token);
        ((int)r4.StatusCode).Should().Be(422, $"TimeSpentSeconds=3601 must return 422; body={b4}");
    }

    [Fact(DisplayName = "BE-TC-15: AttemptId=0 in route → 422")]
    public async Task BeTc15_AttemptIdZero_Returns422()
    {
        var (token, _) = await CreateStudentAsync("s15");
        var (resp, root, body) = await SubmitAsync(0, 1, "\"A\"", 5, false, token);
        ((int)resp.StatusCode).Should().Be(422, $"AttemptId=0 must return 422; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-16: Oversized AnswerPayload (~1MB) — document current behavior (F-01)")]
    public async Task BeTc16_OversizedPayload_DocumentCurrentBehavior()
    {
        var lessonId = await SeedLessonAsync("x16");
        var qId = await SeedQuestionAsync(lessonId);
        var (token, _) = await CreateStudentAsync("s16");
        var attemptId = await StartAttemptAsync(lessonId, token);

        var bigPayload = "\"" + new string('X', 1_000_000) + "\"";
        var (resp, root, body) = await SubmitAsync(attemptId, qId, bigPayload, 5, false, token);

        // Per QC spec F-01: no max-length validator → today's behavior is 200 accepted.
        // Document actual — do NOT mark RED unless a bound is added.
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 422 },
            $"F-01: oversized payload: no 500 allowed; actual={resp.StatusCode}; body (truncated)={body[..Math.Min(200, body.Length)]}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group B — CompleteAttempt NEW cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-19: AccuracyPercentage rounds to 2dp (1 of 3 correct → 33.33)")]
    public async Task BeTc19_AccuracyRounding_TwoDp()
    {
        var lessonId = await SeedLessonAsync("x19");
        var qId1 = await SeedQuestionAsync(lessonId, correct: "\"A\"");
        var qId2 = await SeedQuestionAsync(lessonId, correct: "\"A\"");
        var qId3 = await SeedQuestionAsync(lessonId, correct: "\"A\"");
        var (token, _) = await CreateStudentAsync("s19");
        var attemptId = await StartAttemptAsync(lessonId, token);

        await SubmitAsync(attemptId, qId1, "\"A\"", 5, false, token); // correct
        await SubmitAsync(attemptId, qId2, "\"B\"", 5, false, token); // wrong
        await SubmitAsync(attemptId, qId3, "\"B\"", 5, false, token); // wrong

        var (resp, root, body) = await CompleteAsync(attemptId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var d); TryProp(d, "accuracyPercentage", out var acc);
        var accuracy = acc.GetDouble();
        // 1/3 * 100 = 33.333... → Math.Round(33.333, 2) = 33.33
        accuracy.Should().BeApproximately(33.33, 0.1, $"1/3 correct → 33.33; body={body}");
    }

    [Fact(DisplayName = "BE-TC-21: Complete an already-Abandoned attempt → 424")]
    public async Task BeTc21_CompleteAbandoned_Returns424()
    {
        var lessonId = await SeedLessonAsync("x21");
        var (token, _) = await CreateStudentAsync("s21");
        var attemptId = await StartAttemptAsync(lessonId, token);
        await AbandonAsync(attemptId, token);

        var (resp, root, body) = await CompleteAsync(attemptId, token);
        ((int)resp.StatusCode).Should().Be(424, $"Complete after Abandon must return 424; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-22: Complete IDOR (B's attempt with A's JWT) → 401")]
    public async Task BeTc22_Complete_Idor_Returns401()
    {
        var lessonId = await SeedLessonAsync("x22");
        var (tokenA, _) = await CreateStudentAsync("s22a");
        var (tokenB, _) = await CreateStudentAsync("s22b");
        var attemptB = await StartAttemptAsync(lessonId, tokenB);

        var (resp, root, body) = await CompleteAsync(attemptB, tokenA);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"Complete IDOR must return 401; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-23: Complete non-existent attempt → 404")]
    public async Task BeTc23_CompleteNonExistent_Returns404()
    {
        var (token, _) = await CreateStudentAsync("s23");
        var (resp, root, body) = await CompleteAsync(2147483647, token);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"Complete non-existent must return 404; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-24: Complete — missing JWT → 401; Parent JWT → 403")]
    public async Task BeTc24_Complete_AuthGates()
    {
        var (noJwtResp, _, noJwtBody) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Complete", null, null);
        noJwtResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"no JWT must return 401; body={noJwtBody}");

        var (parentToken, _) = await CreateParentAsync("p24");
        var (parentResp, _, parentBody) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Complete", null, parentToken);
        parentResp.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"parent JWT must return 403; body={parentBody}");
    }

    [Fact(DisplayName = "BE-TC-25: Complete with AttemptId=0 or -1 → 422")]
    public async Task BeTc25_CompleteAttemptIdZero_Returns422()
    {
        var (token, _) = await CreateStudentAsync("s25");
        var (r1, root1, b1) = await CompleteAsync(0, token);
        ((int)r1.StatusCode).Should().Be(422, $"Complete AttemptId=0 must return 422; body={b1}");

        var (r2, root2, b2) = await CompleteAsync(-1, token);
        ((int)r2.StatusCode).Should().Be(422, $"Complete AttemptId=-1 must return 422; body={b2}");
    }

    [Fact(DisplayName = "BE-TC-26: Submitted answers survive completion (not deleted)")]
    public async Task BeTc26_AnswersSurviveCompletion()
    {
        var lessonId = await SeedLessonAsync("x26");
        var qIds = new[]
        {
            await SeedQuestionAsync(lessonId),
            await SeedQuestionAsync(lessonId),
            await SeedQuestionAsync(lessonId)
        };
        var (token, _) = await CreateStudentAsync("s26");
        var attemptId = await StartAttemptAsync(lessonId, token);

        foreach (var qId in qIds)
            await SubmitAsync(attemptId, qId, "\"A\"", 5, false, token);

        var (resp, _, body) = await CompleteAsync(attemptId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Complete; body={body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var count = await db.StudentAnswers.AsNoTracking().CountAsync(sa => sa.AttemptId == attemptId);
        count.Should().Be(3, $"all 3 submitted answers must survive completion; count={count}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group C — AbandonAttempt NEW cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-30: Abandon an already-Completed attempt → 424")]
    public async Task BeTc30_AbandonCompleted_Returns424()
    {
        var lessonId = await SeedLessonAsync("x30");
        var (token, _) = await CreateStudentAsync("s30");
        var attemptId = await StartAttemptAsync(lessonId, token);
        await CompleteAsync(attemptId, token);

        var (resp, root, body) = await AbandonAsync(attemptId, token);
        ((int)resp.StatusCode).Should().Be(424, $"Abandon after Complete must return 424; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-31: Abandon IDOR (B's attempt with A's JWT) → 401")]
    public async Task BeTc31_Abandon_Idor_Returns401()
    {
        var lessonId = await SeedLessonAsync("x31");
        var (tokenA, _) = await CreateStudentAsync("s31a");
        var (tokenB, _) = await CreateStudentAsync("s31b");
        var attemptB = await StartAttemptAsync(lessonId, tokenB);

        var (resp, root, body) = await AbandonAsync(attemptB, tokenA);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"Abandon IDOR must return 401; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-32: Abandon non-existent attempt → 404")]
    public async Task BeTc32_AbandonNonExistent_Returns404()
    {
        var (token, _) = await CreateStudentAsync("s32");
        var (resp, root, body) = await AbandonAsync(2147483647, token);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"Abandon non-existent must return 404; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-33: Abandon — missing JWT → 401; Parent JWT → 403; AttemptId=0 → 422")]
    public async Task BeTc33_Abandon_AuthAndValidationGates()
    {
        // No JWT
        var (r1, _, b1) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Abandon", null, null);
        r1.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"no JWT → 401; body={b1}");

        // Parent JWT
        var (parentToken, _) = await CreateParentAsync("p33");
        var (r2, _, b2) = await SendAsync(_client, HttpMethod.Post, "api/Learning/Quizzes/1/Abandon", null, parentToken);
        r2.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"parent JWT → 403; body={b2}");

        // AttemptId=0
        var (studentToken, _) = await CreateStudentAsync("s33");
        var (r3, root3, b3) = await AbandonAsync(0, studentToken);
        ((int)r3.StatusCode).Should().Be(422, $"AttemptId=0 → 422; body={b3}");
    }

    [Fact(DisplayName = "BE-TC-34: Abandoned answers are retrievable afterward with correct field values")]
    public async Task BeTc34_AbandonedAnswers_Retrievable()
    {
        var lessonId = await SeedLessonAsync("x34");
        var qId1 = await SeedQuestionAsync(lessonId, correct: "\"A\"");
        var qId2 = await SeedQuestionAsync(lessonId, correct: "\"A\"");
        var (token, _) = await CreateStudentAsync("s34");
        var attemptId = await StartAttemptAsync(lessonId, token);

        await SubmitAsync(attemptId, qId1, "\"A\"", 10, false, token); // correct
        await SubmitAsync(attemptId, qId2, "\"B\"", 20, true, token);  // wrong, hinted

        var (resp, _, body) = await AbandonAsync(attemptId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Abandon; body={body}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var rows = await db.StudentAnswers.AsNoTracking()
            .Where(sa => sa.AttemptId == attemptId).OrderBy(sa => sa.QuestionId).ToListAsync();
        rows.Should().HaveCount(2, "both answers must be retrievable after abandon");
        rows.First(r => r.QuestionId == qId1).IsCorrect.Should().BeTrue("Q1 was correct");
        rows.First(r => r.QuestionId == qId1).TimeSpentSeconds.Should().Be(10);
        rows.First(r => r.QuestionId == qId2).IsCorrect.Should().BeFalse("Q2 was wrong");
        rows.First(r => r.QuestionId == qId2).HintUsed.Should().BeTrue("Q2 had hint");
        rows.First(r => r.QuestionId == qId2).TimeSpentSeconds.Should().Be(20);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group D — GetStudentAttempts (E4) NEW cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-38: New student with no attempts → 200 empty list (not 404)")]
    public async Task BeTc38_NoAttempts_Returns200EmptyList()
    {
        var (token, studentId) = await CreateStudentAsync("s38");
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Students/{studentId}/Attempts", null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"empty attempts must return 200; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeTrue($"body={body}");
        TryProp(root, "data", out var data);
        data.ValueKind.Should().Be(JsonValueKind.Array, $"data must be array; body={body}");
        data.GetArrayLength().Should().Be(0, $"must be empty array; body={body}");
    }

    [Fact(DisplayName = "BE-TC-39: studentId=0 and studentId=-1 → 400 (inline validation, not 422)")]
    public async Task BeTc39_StudentIdZero_Returns400()
    {
        var (token, _) = await CreateStudentAsync("s39");

        var (r1, root1, b1) = await SendAsync(_client, HttpMethod.Get, "api/Learning/Students/0/Attempts", null, token);
        ((int)r1.StatusCode).Should().Be(400, $"studentId=0 must return 400; body={b1}");

        var (r2, root2, b2) = await SendAsync(_client, HttpMethod.Get, "api/Learning/Students/-1/Attempts", null, token);
        ((int)r2.StatusCode).Should().Be(400, $"studentId=-1 must return 400; body={b2}");
    }

    [Fact(DisplayName = "BE-TC-40: Missing JWT → 401; UNLINKED parent JWT → 403 generic; parent's own ID → 200 (F-05)")]
    public async Task BeTc40_E4_AuthGates()
    {
        // No JWT → 401 (framework)
        var (r1, _, b1) = await SendAsync(_client, HttpMethod.Get, "api/Learning/Students/1/Attempts", null, null);
        r1.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"no JWT → 401; body={b1}");

        // UNLINKED parent JWT calling another (not-their-own) student's id → 403 Forbidden
        // (authenticated-but-not-authorized must NOT be 401 — 401 triggers FE token refresh).
        var (parentToken, parentUserId) = await CreateParentAsync("p40");
        var (studentToken, studentId) = await CreateStudentAsync("s40");
        var (r2, root2, b2) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Students/{studentId}/Attempts", null, parentToken);
        r2.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"unlinked parent calling student's id → 403 (IDOR guard); body={b2}");
        TryProp(root2, "successed", out var suc2); suc2.GetBoolean().Should().BeFalse($"body={b2}");

        // Unknown student id with an authenticated caller → SAME generic 403 (anti-enumeration:
        // response must not reveal whether the id exists). 999999 has no user behind it.
        var (r4, root4, b4) = await SendAsync(_client, HttpMethod.Get, "api/Learning/Students/999999/Attempts", null, parentToken);
        r4.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"unknown id → 403 (generic, anti-enumeration); body={b4}");
        TryProp(root4, "message", out var m4); TryProp(root2, "message", out var m2);
        m4.GetString().Should().Be(m2.GetString(), "unknown-id and not-owned-id must return the identical generic message");

        // Parent calling own userId → 200 (route id == JWT id branch; F-05)
        var (r3, root3, b3) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Students/{parentUserId}/Attempts", null, parentToken);
        r3.StatusCode.Should().Be(HttpStatusCode.OK, $"F-05: parent accessing own userId → 200 empty; body={b3}");
    }

    [Fact(DisplayName = "BE-TC-40b: LINKED parent reads own child's attempts → 200 with data, no correctAnswer")]
    public async Task BeTc40b_LinkedParent_Returns200WithChildAttempts()
    {
        // Parent registers and adds their own child (creates the ParentStudent link).
        var (parentToken, _) = await CreateParentAsync("p40b");
        var childEmail = UniqueEmail("c40b");
        var (addR, addRoot, addB) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new { FullName = "Student", Email = childEmail, Password = ValidChildPassword,
                  Grade = 3, Language = "ar", Country = "EG", LearningLanguage = "ar" }, parentToken);
        ((int)addR.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"Add-Child; body={addB}");
        TryProp(addRoot, "data", out var addData); TryProp(addData, "id", out var childIdProp);
        var childId = childIdProp.GetInt32();

        // Child completes one attempt so the parent has real data to read.
        var childToken = await SignInAsync(childEmail, ValidChildPassword);
        var lessonId = await SeedLessonAsync("x40b");
        var attemptId = await StartAttemptAsync(lessonId, childToken);
        await CompleteAsync(attemptId, childToken);

        // Linked parent reads the child's attempts → 200 with the attempt present.
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Students/{childId}/Attempts", null, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"linked parent must read own child's attempts; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeTrue($"body={body}");
        TryProp(root, "data", out var data);
        data.ValueKind.Should().Be(JsonValueKind.Array, $"data must be array; body={body}");
        data.GetArrayLength().Should().Be(1, $"the child's single attempt must be returned; body={body}");

        // Security: CorrectAnswer must never leak to the parent either.
        body.ToLowerInvariant().Should().NotContain("\"correctanswer\"", $"no correctAnswer in parent read; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group E — GetSkillStats (E5) NEW cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-44: Skill stats scoped to requesting student — S2 answers not included")]
    public async Task BeTc44_SkillStats_ScopedToStudent()
    {
        var skillId = await SeedSkillAsync();
        var lessonId = await SeedLessonAsync("x44");
        var qId1 = await SeedQuestionAsync(lessonId, skillId, correct: "\"A\"");
        var qId2 = await SeedQuestionAsync(lessonId, skillId, correct: "\"A\"");
        var (tokenA, studentA) = await CreateStudentAsync("s44a");
        var (tokenB, studentB) = await CreateStudentAsync("s44b");

        var attemptA = await StartAttemptAsync(lessonId, tokenA);
        await SubmitAsync(attemptA, qId1, "\"A\"", 5, false, tokenA);
        await CompleteAsync(attemptA, tokenA);

        var attemptB = await StartAttemptAsync(lessonId, tokenB);
        await SubmitAsync(attemptB, qId2, "\"A\"", 5, false, tokenB);
        await CompleteAsync(attemptB, tokenB);

        // S1 checks own stats — should see 1 answer (only S1's), not 2
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Skills/{skillId}/Stats?studentId={studentA}", null, tokenA);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var data);
        TryProp(data, "totalAnswers", out var ta);
        ta.GetInt32().Should().Be(1, $"stats must be scoped to S1's answers only; body={body}");
    }

    [Fact(DisplayName = "BE-TC-45: S1 calls GET /Skills/{id}/Stats?studentId={S2} → 401 (IDOR)")]
    public async Task BeTc45_SkillStats_Idor_Returns401()
    {
        var skillId = await SeedSkillAsync();
        var (tokenA, studentA) = await CreateStudentAsync("s45a");
        var (tokenB, studentB) = await CreateStudentAsync("s45b");

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Skills/{skillId}/Stats?studentId={studentB}", null, tokenA);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"S1 calling S2 stats → 401; body={body}");
        TryProp(root, "successed", out var suc); suc.GetBoolean().Should().BeFalse($"body={body}");
    }

    [Fact(DisplayName = "BE-TC-46: skillId=0 → 400; studentId=0 → 400 (inline validation, not 422)")]
    public async Task BeTc46_SkillStats_ValidationBoundaries_Returns400()
    {
        var (token, studentId) = await CreateStudentAsync("s46");

        var (r1, _, b1) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Skills/0/Stats?studentId={studentId}", null, token);
        ((int)r1.StatusCode).Should().Be(400, $"skillId=0 → 400; body={b1}");

        var skillId = await SeedSkillAsync();
        var (r2, _, b2) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Skills/{skillId}/Stats?studentId=0", null, token);
        ((int)r2.StatusCode).Should().Be(400, $"studentId=0 → 400; body={b2}");

        // No studentId query param → binds to 0 → 400
        var (r3, _, b3) = await SendAsync(_client, HttpMethod.Get, $"api/Learning/Skills/{skillId}/Stats", null, token);
        ((int)r3.StatusCode).Should().Be(400, $"missing studentId → 400; body={b3}");
    }

    [Fact(DisplayName = "BE-TC-47: E5 missing JWT → 401 (framework)")]
    public async Task BeTc47_SkillStats_NoJwt_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, "api/Learning/Skills/1/Stats?studentId=1", null, null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"no JWT → 401; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Group F — Cross-cutting NEW cases
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-TC-48: BaseResponse envelope — 'successed' key (double-s spelling) present on success and failure")]
    public async Task BeTc48_EnvelopeShape_SuccessedSpelling()
    {
        var lessonId = await SeedLessonAsync("x48");
        var qId = await SeedQuestionAsync(lessonId);
        var (token, _) = await CreateStudentAsync("s48");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // Success response
        var (r1, _, b1) = await SubmitAsync(attemptId, qId, "\"A\"", 5, false, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, $"body={b1}");
        b1.Should().Contain("\"successed\":", $"'successed' key must be present; body={b1}");
        b1.Should().NotContain("\"succeeded\":", $"wrong spelling 'succeeded' must not be present; body={b1}");

        // Failure response (duplicate submit → 424)
        var (r2, _, b2) = await SubmitAsync(attemptId, qId, "\"A\"", 5, false, token);
        ((int)r2.StatusCode).Should().Be(424, $"duplicate must return 424; body={b2}");
        b2.Should().Contain("\"successed\":", $"'successed' key must be present in failure too; body={b2}");
    }

    [Fact(DisplayName = "BE-TC-50: DurationSeconds is server-side elapsed (non-negative) — not sum of client TimeSpentSeconds")]
    public async Task BeTc50_DurationSeconds_ServerSideElapsed()
    {
        var lessonId = await SeedLessonAsync("x50");
        var qId = await SeedQuestionAsync(lessonId);
        var (token, _) = await CreateStudentAsync("s50");
        var attemptId = await StartAttemptAsync(lessonId, token);

        // Submit with large client-reported time (3600) — duration must NOT be 3600
        await SubmitAsync(attemptId, qId, "\"A\"", 3600, false, token);

        var (resp, root, body) = await CompleteAsync(attemptId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        TryProp(root, "data", out var d); TryProp(d, "durationSeconds", out var ds);
        var duration = ds.GetDouble();
        duration.Should().BeGreaterThanOrEqualTo(0, $"durationSeconds must be non-negative; body={body}");
        duration.Should().BeLessThan(3600, $"durationSeconds must be server-elapsed time, not client-reported 3600; body={body}");
    }

    [Fact(DisplayName = "BE-TC-51: Mass-assignment guard — injected studentId/isCorrect are ignored")]
    public async Task BeTc51_MassAssignment_Guard()
    {
        var lessonId = await SeedLessonAsync("x51");
        var qId = await SeedQuestionAsync(lessonId, correct: "\"A\"");
        var (tokenA, studentA) = await CreateStudentAsync("s51a");
        var (tokenB, studentB) = await CreateStudentAsync("s51b");
        var attemptA = await StartAttemptAsync(lessonId, tokenA);

        // POST with forged studentId (=studentB) and isCorrect=true, but wrong answer
        var req = new HttpRequestMessage(HttpMethod.Post, $"api/Learning/Quizzes/{attemptA}/Answers");
        req.Content = new StringContent(
            $"{{\"QuestionId\":{qId},\"AnswerPayload\":\"\\\"B\\\"\",\"TimeSpentSeconds\":5,\"HintUsed\":false,\"studentId\":{studentB},\"isCorrect\":true}}",
            Encoding.UTF8, "application/json");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        var r = await _client.SendAsync(req);
        var body = await r.Content.ReadAsStringAsync();

        r.StatusCode.Should().Be(HttpStatusCode.OK, $"body={body}");
        var root = JsonDocument.Parse(body).RootElement;
        TryProp(root, "data", out var d); TryProp(d, "isCorrect", out var ic);
        ic.GetBoolean().Should().BeFalse($"server must grade 'B' as wrong despite injected isCorrect:true; body={body}");

        // Verify row belongs to studentA, not studentB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var row = await db.StudentAnswers.AsNoTracking()
            .Include(sa => sa.Attempt)
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptA && sa.QuestionId == qId);
        row.Should().NotBeNull("answer row must exist");
        row!.Attempt.StudentId.Should().Be(studentA, $"row must belong to studentA={studentA}, not injected studentB={studentB}");
        row.IsCorrect.Should().BeFalse("wrong answer must not be overridden by injected isCorrect:true");
    }
}

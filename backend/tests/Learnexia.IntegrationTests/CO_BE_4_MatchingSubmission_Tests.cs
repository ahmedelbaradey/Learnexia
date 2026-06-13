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
/// CO-BE-4 integration tests — Matching question submission end-to-end (carryover item).
///
/// Verifies the locked Matching wire contract (CO-BE-1/CO-BE-2) at the HTTP level:
///   Student payload: {"pairs":[{"leftId":"…","rightId":"…"}],"attemptOrder":N,"timeMs":N}
///   Correctness = order-independent pair-set equality; attemptOrder/timeMs are metadata only.
///
/// Matrix:
///   M01  Correct pairing in canonical order → 200, IsCorrect=true, Successed=true, correctAnswer=null
///   M02  Correct pairing in shuffled order  → 200, IsCorrect=true (order-independence)
///   M03  Same pairs, varied/absent attemptOrder+timeMs → correctness unchanged (metadata ignored)
///   M04  Wrong pairing (swapped rightIds)   → 200, IsCorrect=false, correctAnswer populated
///   M05  Partial (missing pair)             → 200, IsCorrect=false
///   M06  Extra pair                         → 200, IsCorrect=false
///   M07  Duplicate leftId                   → 200, IsCorrect=false
///   M08  Valid JSON, wrong shape            → 200, IsCorrect=false (per 422-semantics decision)
///   M09  Structurally malformed JSON        → 422 (AnswerPayloadMalformedJson)
///   M10  Empty payload                      → 422 (NotEmpty)
///   M11  Persisted StudentAnswer stores the verbatim payload incl. metadata
///   M12  Seeded Grade-1 Math lesson (LearningSeeder, CO-BE-3) Matching question e2e
///
/// Complements BE-TC-13 in P2_07_InstantAnswerFeedback_Extended_Tests (shuffled-correct +
/// swapped-wrong smoke) — this file is the full matrix promised there.
/// </summary>
[Collection("IntegrationTests")]
public sealed class CO_BE_4_MatchingSubmission_Tests : IAsyncLifetime
{
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    /// <summary>Stored correct answer used by all locally-seeded Matching questions (l1→r1, l2→r2, l3→r3).</summary>
    private const string MatchingCorrectAnswer =
        """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"}]}""";

    /// <summary>Student-visible Matching content per the CO-BE-1 contract ("right" non-aligned).</summary>
    private const string MatchingOptions =
        """{"left":[{"id":"l1","text":"1"},{"id":"l2","text":"2"},{"id":"l3","text":"3"}],"right":[{"id":"r3","text":"three"},{"id":"r1","text":"one"},{"id":"r2","text":"two"}]}""";

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public CO_BE_4_MatchingSubmission_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Helpers (mirror P2_07 Extended harness) ──────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"cobe4_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    private async Task<(string StudentToken, int StudentId)> CreateStudentAsync(string tag)
    {
        var parentEmail = UniqueEmail($"par{tag}");
        var (pr, pRoot, pb) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)pr.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"parent reg; body={pb}");
        TryProp(pRoot, "data", out var pd); TryProp(pd, "accessToken", out var pTok);
        var parentToken = pTok.GetString()!;

        var childEmail = UniqueEmail($"ch{tag}");
        var (ar, aRoot, ab) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new { FullName = "CO-BE-4 Student", Email = childEmail, Password = ValidChildPassword,
                  Grade = 1, Language = "ar", Country = "EG", LearningLanguage = "ar" }, parentToken);
        ((int)ar.StatusCode).Should().BeOneOf(new[] { 200, 201 }, $"Add-Child; body={ab}");
        TryProp(aRoot, "data", out var ad); TryProp(ad, "id", out var id);

        var studentToken = await SignInAsync(childEmail, ValidChildPassword);
        return (studentToken, id.GetInt32());
    }

    private async Task<int> SeedLessonAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var grade = new Grade { Number = 94, DisplayName = $"G_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade); await db.SaveChangesAsync();
        var subject = new Subject { Name = $"S_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject); await db.SaveChangesAsync();
        var unit = new Learnexia.Modules.Learning.Domain.Entities.Unit { Name = $"U_{Guid.NewGuid():N}", SequenceOrder = 1, SubjectId = subject.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Units.AddAsync(unit); await db.SaveChangesAsync();
        var lesson = new Lesson { Name = name, Difficulty = DifficultyLevel.Easy, SequenceOrder = 1, IsLocked = false,
            UnitId = unit.Id, SkillId = null, IsActive = true, LifecycleState = LifecycleState.Published,
            CreatedAt = now, CreatedBy = 0 };
        await db.Lessons.AddAsync(lesson); await db.SaveChangesAsync();
        return lesson.Id;
    }

    private async Task<int> SeedMatchingQuestionAsync(int lessonId, string correctAnswer = MatchingCorrectAnswer)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var q = new QuizQuestion { LessonId = lessonId, SkillId = null, QuestionType = QuestionType.Matching,
            QuestionText = "Match each number to its word.", Options = MatchingOptions, CorrectAnswer = correctAnswer,
            Difficulty = DifficultyLevel.Easy, GeneratedBy = GeneratedBy.Curated,
            IsActive = true, LifecycleState = LifecycleState.Published, CreatedAt = now, CreatedBy = 0 };
        await db.QuizQuestions.AddAsync(q); await db.SaveChangesAsync();
        return q.Id;
    }

    private async Task<int> StartAttemptAsync(int lessonId, string studentToken)
    {
        var (r, root, b) = await SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        r.StatusCode.Should().Be(HttpStatusCode.OK, $"StartAttempt; body={b}");
        TryProp(root, "data", out var d); TryProp(d, "attemptId", out var id);
        return id.GetInt32();
    }

    private Task<(HttpResponseMessage Resp, JsonElement Root, string Body)>
        SubmitAsync(int attemptId, int questionId, string answerPayload, string token)
        => SendAsync(_client, HttpMethod.Post, $"api/Learning/Quizzes/{attemptId}/Answers",
            new { QuestionId = questionId, AnswerPayload = answerPayload, TimeSpentSeconds = 10, HintUsed = false }, token);

    /// <summary>Asserts a 200 envelope and returns the data.isCorrect boolean.</summary>
    private static bool AssertOkAndGetIsCorrect(HttpResponseMessage resp, JsonElement root, string body)
    {
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Matching submit must return 200, never 500; body={body}");
        TryProp(root, "successed", out var suc).Should().BeTrue($"envelope must contain 'successed'; body={body}");
        suc.GetBoolean().Should().BeTrue($"200 envelope must have successed=true; body={body}");
        TryProp(root, "data", out var data).Should().BeTrue($"body={body}");
        TryProp(data, "isCorrect", out var ic).Should().BeTrue($"body={body}");
        return ic.GetBoolean();
    }

    private static void Assert422(HttpResponseMessage resp, JsonElement root, string body, string because)
    {
        ((int)resp.StatusCode).Should().Be(422, $"{because}; body={body}");
        if (TryProp(root, "successed", out var suc))
            suc.GetBoolean().Should().BeFalse($"422 envelope must have successed=false; body={body}");
    }

    /// <summary>Common setup: lesson + N matching questions + Grade-1 student + in-progress attempt.</summary>
    private async Task<(int AttemptId, List<int> QuestionIds, string Token)> SetupAsync(string tag, int questionCount)
    {
        var lessonId = await SeedLessonAsync($"cobe4-{tag}");
        var qIds = new List<int>();
        for (var i = 0; i < questionCount; i++)
            qIds.Add(await SeedMatchingQuestionAsync(lessonId));
        var (token, _) = await CreateStudentAsync(tag);
        var attemptId = await StartAttemptAsync(lessonId, token);
        return (attemptId, qIds, token);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M01: Correct pairing, canonical order → 200, IsCorrect=true
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M01: Matching correct pairing in canonical order → 200, isCorrect=true, successed=true, correctAnswer=null")]
    public async Task M01_CorrectPairing_CanonicalOrder_IsCorrectTrue()
    {
        var (attemptId, qIds, token) = await SetupAsync("m01", 1);

        const string payload =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":1,"timeMs":4200}""";
        var (resp, root, body) = await SubmitAsync(attemptId, qIds[0], payload, token);

        AssertOkAndGetIsCorrect(resp, root, body).Should().BeTrue(
            $"correct pairing in canonical order must grade correct; body={body}");

        // Correct path must not leak the stored correct answer.
        TryProp(root, "data", out var data);
        if (TryProp(data, "correctAnswer", out var ca))
            ca.ValueKind.Should().Be(JsonValueKind.Null, $"correctAnswer must be null when isCorrect=true; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M02: Correct pairing, shuffled order → IsCorrect=true (key property)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M02: Matching correct pairing in SHUFFLED order → isCorrect=true (order-independent pair-set equality)")]
    public async Task M02_CorrectPairing_ShuffledOrder_IsCorrectTrue()
    {
        var (attemptId, qIds, token) = await SetupAsync("m02", 2);

        // Shuffle 1: l3, l1, l2
        const string shuffled1 =
            """{"pairs":[{"leftId":"l3","rightId":"r3"},{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"}],"attemptOrder":1,"timeMs":3100}""";
        var (r1, root1, b1) = await SubmitAsync(attemptId, qIds[0], shuffled1, token);
        AssertOkAndGetIsCorrect(r1, root1, b1).Should().BeTrue(
            $"shuffled order (l3,l1,l2) must still grade correct; body={b1}");

        // Shuffle 2: l2, l3, l1
        const string shuffled2 =
            """{"pairs":[{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"},{"leftId":"l1","rightId":"r1"}],"attemptOrder":2,"timeMs":2750}""";
        var (r2, root2, b2) = await SubmitAsync(attemptId, qIds[1], shuffled2, token);
        AssertOkAndGetIsCorrect(r2, root2, b2).Should().BeTrue(
            $"shuffled order (l2,l3,l1) must still grade correct; body={b2}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M03: attemptOrder/timeMs varied or absent → correctness unchanged
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M03: attemptOrder/timeMs varied or absent → correctness unchanged (metadata ignored)")]
    public async Task M03_MetadataVariations_DoNotAffectCorrectness()
    {
        var (attemptId, qIds, token) = await SetupAsync("m03", 4);

        // a) Extreme metadata values, correct pairs → true
        const string extremeMeta =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":999,"timeMs":2147483647}""";
        var (ra, roota, ba) = await SubmitAsync(attemptId, qIds[0], extremeMeta, token);
        AssertOkAndGetIsCorrect(ra, roota, ba).Should().BeTrue(
            $"extreme attemptOrder/timeMs must not affect a correct answer; body={ba}");

        // b) Metadata absent entirely, correct pairs → true
        const string noMeta =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"}]}""";
        var (rb, rootb, bb) = await SubmitAsync(attemptId, qIds[1], noMeta, token);
        AssertOkAndGetIsCorrect(rb, rootb, bb).Should().BeTrue(
            $"absent attemptOrder/timeMs must not affect a correct answer; body={bb}");

        // c) attemptOrder = 0, timeMs = 0, correct pairs → true
        const string zeroMeta =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":0,"timeMs":0}""";
        var (rc, rootc, bc) = await SubmitAsync(attemptId, qIds[2], zeroMeta, token);
        AssertOkAndGetIsCorrect(rc, rootc, bc).Should().BeTrue(
            $"zero metadata must not affect a correct answer; body={bc}");

        // d) Metadata present but pairing WRONG → still false (metadata can never make a wrong answer right)
        const string wrongWithMeta =
            """{"pairs":[{"leftId":"l1","rightId":"r2"},{"leftId":"l2","rightId":"r1"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":1,"timeMs":100}""";
        var (rd, rootd, bd) = await SubmitAsync(attemptId, qIds[3], wrongWithMeta, token);
        AssertOkAndGetIsCorrect(rd, rootd, bd).Should().BeFalse(
            $"metadata must never make a wrong pairing correct; body={bd}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M04: Wrong pairing → 200, IsCorrect=false, correctAnswer populated
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M04: Matching wrong pairing (swapped rightIds) → 200, isCorrect=false, correctAnswer populated")]
    public async Task M04_WrongPairing_IsCorrectFalse()
    {
        var (attemptId, qIds, token) = await SetupAsync("m04", 1);

        const string swapped =
            """{"pairs":[{"leftId":"l1","rightId":"r2"},{"leftId":"l2","rightId":"r3"},{"leftId":"l3","rightId":"r1"}],"attemptOrder":1,"timeMs":5000}""";
        var (resp, root, body) = await SubmitAsync(attemptId, qIds[0], swapped, token);

        AssertOkAndGetIsCorrect(resp, root, body).Should().BeFalse(
            $"fully swapped pairing must grade wrong; body={body}");

        TryProp(root, "data", out var data);
        TryProp(data, "correctAnswer", out var ca).Should().BeTrue(
            $"correctAnswer must be present when wrong; body={body}");
        ca.ValueKind.Should().NotBe(JsonValueKind.Null,
            $"correctAnswer must be populated when isCorrect=false; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M05: Partial pairing (missing pair) → 200, IsCorrect=false
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M05: Matching partial pairing (2 of 3 pairs) → 200, isCorrect=false")]
    public async Task M05_PartialPairing_IsCorrectFalse()
    {
        var (attemptId, qIds, token) = await SetupAsync("m05", 1);

        const string partial =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"}],"attemptOrder":1,"timeMs":1500}""";
        var (resp, root, body) = await SubmitAsync(attemptId, qIds[0], partial, token);

        AssertOkAndGetIsCorrect(resp, root, body).Should().BeFalse(
            $"a missing pair must grade wrong (set sizes differ); body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M06: Extra pair → 200, IsCorrect=false
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M06: Matching extra pair (4 pairs vs 3 stored) → 200, isCorrect=false")]
    public async Task M06_ExtraPair_IsCorrectFalse()
    {
        var (attemptId, qIds, token) = await SetupAsync("m06", 1);

        const string extra =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"},{"leftId":"l4","rightId":"r4"}],"attemptOrder":1,"timeMs":2000}""";
        var (resp, root, body) = await SubmitAsync(attemptId, qIds[0], extra, token);

        AssertOkAndGetIsCorrect(resp, root, body).Should().BeFalse(
            $"an extra pair must grade wrong (set sizes differ); body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M07: Duplicate leftId → 200, IsCorrect=false
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M07: Matching duplicate leftId → 200, isCorrect=false (never 500)")]
    public async Task M07_DuplicateLeftId_IsCorrectFalse()
    {
        var (attemptId, qIds, token) = await SetupAsync("m07", 1);

        // l1 paired twice — the whole document is invalid per the contract → graded wrong.
        const string duplicate =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l1","rightId":"r2"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":1,"timeMs":1800}""";
        var (resp, root, body) = await SubmitAsync(attemptId, qIds[0], duplicate, token);

        AssertOkAndGetIsCorrect(resp, root, body).Should().BeFalse(
            $"duplicate leftId invalidates the submission → graded wrong; body={body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M08: Valid JSON but wrong shape → 200, IsCorrect=false (decision: not 422)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M08: Valid JSON but wrong shape → 200, isCorrect=false (per 422-semantics decision)")]
    public async Task M08_ValidJsonWrongShape_200IsCorrectFalse()
    {
        var (attemptId, qIds, token) = await SetupAsync("m08", 3);

        // a) Object without "pairs"
        var (ra, roota, ba) = await SubmitAsync(attemptId, qIds[0], """{"answers":[{"leftId":"l1","rightId":"r1"}]}""", token);
        AssertOkAndGetIsCorrect(ra, roota, ba).Should().BeFalse(
            $"object without 'pairs' must grade false, not 422/500; body={ba}");

        // b) Array root
        var (rb, rootb, bb) = await SubmitAsync(attemptId, qIds[1], """[{"leftId":"l1","rightId":"r1"}]""", token);
        AssertOkAndGetIsCorrect(rb, rootb, bb).Should().BeFalse(
            $"array root must grade false, not 422/500; body={bb}");

        // c) "pairs" not an array
        var (rc, rootc, bc) = await SubmitAsync(attemptId, qIds[2], """{"pairs":"l1r1l2r2l3r3"}""", token);
        AssertOkAndGetIsCorrect(rc, rootc, bc).Should().BeFalse(
            $"'pairs' as a string must grade false, not 422/500; body={bc}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M09: Structurally malformed JSON → 422 (AnswerPayloadMalformedJson)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M09: Structurally malformed JSON payload → 422 (AnswerPayloadMalformedJson)")]
    public async Task M09_MalformedJson_Returns422()
    {
        var (attemptId, qIds, token) = await SetupAsync("m09", 1);

        // a) Truncated object
        var (ra, roota, ba) = await SubmitAsync(attemptId, qIds[0], """{"pairs":[{"leftId":"l1","rightId":""", token);
        Assert422(ra, roota, ba, "truncated JSON object must be rejected with 422");

        // b) Unbalanced bracket
        var (rb, rootb, bb) = await SubmitAsync(attemptId, qIds[0], """{"pairs":[}""", token);
        Assert422(rb, rootb, bb, "unbalanced-bracket JSON must be rejected with 422");

        // c) Bare '{'
        var (rc, rootc, bc) = await SubmitAsync(attemptId, qIds[0], "{", token);
        Assert422(rc, rootc, bc, "bare '{' must be rejected with 422");

        // The 422 rejections must NOT have consumed the question — a valid submit still works.
        const string correct =
            """{"pairs":[{"leftId":"l1","rightId":"r1"},{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":1,"timeMs":900}""";
        var (rd, rootd, bd) = await SubmitAsync(attemptId, qIds[0], correct, token);
        AssertOkAndGetIsCorrect(rd, rootd, bd).Should().BeTrue(
            $"after 422 rejections the question must still be answerable; body={bd}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M10: Empty payload → 422
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M10: Empty AnswerPayload on a Matching question → 422 (NotEmpty)")]
    public async Task M10_EmptyPayload_Returns422()
    {
        var (attemptId, qIds, token) = await SetupAsync("m10", 1);

        var (resp, root, body) = await SubmitAsync(attemptId, qIds[0], "", token);
        Assert422(resp, root, body, "empty AnswerPayload must be rejected with 422");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M11: Persisted StudentAnswer row stores the verbatim payload incl. metadata
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M11: Persisted StudentAnswer stores the verbatim payload including attemptOrder/timeMs")]
    public async Task M11_PersistedRow_StoresVerbatimPayloadWithMetadata()
    {
        var (attemptId, qIds, token) = await SetupAsync("m11", 2);

        // a) Correct submission — verbatim payload + IsCorrect=true persisted
        const string correctPayload =
            """{"pairs":[{"leftId":"l2","rightId":"r2"},{"leftId":"l1","rightId":"r1"},{"leftId":"l3","rightId":"r3"}],"attemptOrder":7,"timeMs":12345}""";
        var (r1, _, b1) = await SubmitAsync(attemptId, qIds[0], correctPayload, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, $"body={b1}");

        // b) Wrong submission — verbatim payload + IsCorrect=false persisted
        const string wrongPayload =
            """{"pairs":[{"leftId":"l1","rightId":"r3"},{"leftId":"l2","rightId":"r1"},{"leftId":"l3","rightId":"r2"}],"attemptOrder":8,"timeMs":67890}""";
        var (r2, _, b2) = await SubmitAsync(attemptId, qIds[1], wrongPayload, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, $"body={b2}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var row1 = await db.StudentAnswers.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == qIds[0]);
        row1.Should().NotBeNull("the correct submission must be persisted");
        row1!.AnswerPayload.Should().Be(correctPayload,
            "the StudentAnswer row must store the verbatim payload including attemptOrder/timeMs metadata");
        row1.IsCorrect.Should().BeTrue("server graded the shuffled-correct pairing as correct");
        row1.TimeSpentSeconds.Should().Be(10, "TimeSpentSeconds from the command must be persisted");

        var row2 = await db.StudentAnswers.AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.AttemptId == attemptId && sa.QuestionId == qIds[1]);
        row2.Should().NotBeNull("the wrong submission must be persisted");
        row2!.AnswerPayload.Should().Be(wrongPayload,
            "the wrong submission's verbatim payload (incl. metadata) must be persisted too");
        row2.IsCorrect.Should().BeFalse("server graded the swapped pairing as wrong");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // M12: Seeded Grade-1 Math lesson (CO-BE-3) Matching question e2e
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CO-BE-4 M12: Seeded 'مقدمة في العد (ص1)' Matching question (SequenceOrder 3) grades shuffled-correct=true, swapped=false")]
    public async Task M12_SeededGrade1MathLesson_MatchingQuestion_EndToEnd()
    {
        // LearningSeeder is idempotent — safe to run on the shared collection DB.
        using (var scope = _factory.Services.CreateScope())
            await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Test students are created with LearningLanguage="ar", so target the Arabic tree's
        // root lesson — the P8-04 learning-language gate 403s the English tree for "ar" students.
        int lessonId;
        int questionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

            var lesson = await db.Lessons.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Name == "مقدمة في العد (ص1)");
            lesson.Should().NotBeNull("LearningSeeder must seed the Arabic Grade-1 Math root lesson");
            lessonId = lesson!.Id;

            var question = await db.QuizQuestions.AsNoTracking()
                .FirstOrDefaultAsync(q => q.LessonId == lessonId && q.QuestionType == QuestionType.Matching);
            question.Should().NotBeNull("CO-BE-3 must seed a Matching question on the Grade-1 Math root lesson");
            question!.SequenceOrder.Should().Be(3, "the seeded Matching question is SequenceOrder 3");
            questionId = question.Id;
        }

        // Student A: shuffled correct pairing (l1→r1, l2→r2, l3→r3 per the seeder) → true
        var (tokenA, _) = await CreateStudentAsync("m12a");
        var attemptA = await StartAttemptAsync(lessonId, tokenA);
        const string shuffledCorrect =
            """{"pairs":[{"leftId":"l2","rightId":"r2"},{"leftId":"l3","rightId":"r3"},{"leftId":"l1","rightId":"r1"}],"attemptOrder":1,"timeMs":6400}""";
        var (ra, roota, ba) = await SubmitAsync(attemptA, questionId, shuffledCorrect, tokenA);
        AssertOkAndGetIsCorrect(ra, roota, ba).Should().BeTrue(
            $"the seeded Matching question must grade shuffled l1→r1,l2→r2,l3→r3 as correct; body={ba}");

        // Student B: swapped pairing on the same seeded question → false
        var (tokenB, _) = await CreateStudentAsync("m12b");
        var attemptB = await StartAttemptAsync(lessonId, tokenB);
        const string swapped =
            """{"pairs":[{"leftId":"l1","rightId":"r3"},{"leftId":"l2","rightId":"r1"},{"leftId":"l3","rightId":"r2"}],"attemptOrder":1,"timeMs":7100}""";
        var (rb, rootb, bb) = await SubmitAsync(attemptB, questionId, swapped, tokenB);
        AssertOkAndGetIsCorrect(rb, rootb, bb).Should().BeFalse(
            $"the seeded Matching question must grade a swapped pairing as wrong; body={bb}");
    }
}

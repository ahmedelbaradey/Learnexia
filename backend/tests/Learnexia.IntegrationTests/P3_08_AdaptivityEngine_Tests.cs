using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P3-08 integration tests — "Adjust difficulty adaptively (Adaptivity Engine)".
///
/// Endpoint under test:
///   GET /api/Learning/Adaptivity/Skill/{skillId}   [Authorize(Roles="Student")]
///
/// Write path used to seed signal history (via attempt-completion flow):
///   POST /api/Learning/Quizzes/{lessonId}/Attempt        — StartAttempt
///   POST /api/Learning/Quizzes/{attemptId}/Answers       — SubmitAnswer (controls accuracy/hints)
///   POST /api/Learning/Quizzes/{attemptId}/Complete      — CompleteAttempt (triggers mastery upsert)
///
/// Default engine weights (AdaptivityOptions defaults):
///   WeightAccuracy=0.5, WeightTime=0.2, WeightHint=0.2, WeightRetry=0.1
///   HighBand=0.8 (→ Hard), LowBand=0.5 (→ Easy), ExpectedAttempts=2
///   time baseline = 30s
///
/// Coverage map (6 plan test cases):
///   Case 1 → ColdStart_NoHistory_ReturnsMediumIsDefault
///   Case 2 → HighPerformanceHistory_ReturnsHard
///   Case 3 → StruggleHistory_ReturnsEasy
///   Case 4 → Reproducibility_SameHistory_IdenticalDecision
///   Case 5 → NullSkillPath_ServiceReturnsDefault (service-level, via phantom skillId with no answers)
///   Case 6 → IDOR_StudentB_ReflectsOwnHistoryOnly
///   Auth   → NoJwt_Returns401
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_08_AdaptivityEngine_Tests : IAsyncLifetime
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

    public P3_08_AdaptivityEngine_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Shared HTTP helpers — mirrors P3_09_StudentMastery_Tests exactly
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p308_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
    /// Mirrors CreateStudentViaParentFlowAsync from P3_09_StudentMastery_Tests.cs exactly.
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
    // DB-seeding helpers (EF direct — mirrors P3_09_StudentMastery_Tests exactly)
    // -------------------------------------------------------------------------

    /// <summary>Seeds Grade → Subject → Unit → Lesson. Returns lessonId.</summary>
    private async Task<int> SeedLessonAsync(string lessonName = "P308 Lesson")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade { Number = 55, DisplayName = $"G_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
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
    /// Seeds a Skill (under a fresh Grade → Subject → Concept hierarchy).
    /// Returns the new Skill.Id.
    /// </summary>
    private async Task<int> SeedSkillAsync(int masteryThreshold = 80)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade { Number = 66, DisplayName = $"SkG_{Guid.NewGuid():N}", CreatedAt = now, CreatedBy = 0 };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject { Name = $"SkS_{Guid.NewGuid():N}", GradeId = grade.Id, CreatedAt = now, CreatedBy = 0 };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"SkC_{Guid.NewGuid():N}",
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
    /// Seeds QuizQuestion rows linked to a lesson and skill.
    /// All correct answers are "A" so tests can control correctness via the submitted answer payload.
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
                QuestionText   = $"P308-Q{i}?",
                Options        = "[\"A\",\"B\",\"C\",\"D\"]",
                CorrectAnswer  = "\"A\"",   // "A" is always correct
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
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var idProp).Should().BeTrue("body: {0}", body);
        return idProp.GetInt32();
    }

    /// <summary>
    /// Submits an answer with configurable correctness, time, and hint usage.
    /// correctAnswer = "A" (always the CorrectAnswer seeded above).
    /// wrongAnswer   = "B"
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SubmitAnswerAsync(
            int     attemptId,
            int     questionId,
            string  answerPayload,
            bool    hintUsed,
            int     timeSpentSeconds,
            string? token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new
            {
                QuestionId       = questionId,
                AnswerPayload    = answerPayload,
                TimeSpentSeconds = timeSpentSeconds,
                HintUsed         = hintUsed,
            },
            token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        CompleteAttemptAsync(int attemptId, string? token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetAdaptivityDecisionAsync(int skillId, string? token)
        => SendAsync(_client, HttpMethod.Get,
            $"api/Learning/Adaptivity/Skill/{skillId}", null, token);

    // -------------------------------------------------------------------------
    // Envelope assertion helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asserts the BaseResponse&lt;T&gt; envelope shape and returns the data element.
    /// Checks: successed=true, statusCode=200, data property present.
    /// </summary>
    private static JsonElement AssertEnvelopeAndGetData(JsonElement root, string body)
    {
        TryProp(root, "successed", out var successed).Should().BeTrue(
            "envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "Successed must be true for a successful adaptivity response; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        statusCode.GetInt32().Should().Be(200,
            "StatusCode must be 200; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null for a successful adaptivity response; body: {0}", body);
        return data;
    }

    /// <summary>
    /// Asserts the AdaptivityDecisionDto shape from the data element.
    /// Returns (difficulty int value, isDefault, score).
    /// </summary>
    private static (int Difficulty, bool IsDefault, double Score)
        AssertDecisionDtoShape(JsonElement data, string body)
    {
        // difficulty field
        TryProp(data, "difficulty", out var diffProp).Should().BeTrue(
            "data.difficulty must be present; body: {0}", body);

        // isDefault field
        TryProp(data, "isDefault", out var isDefaultProp).Should().BeTrue(
            "data.isDefault must be present; body: {0}", body);

        // score field
        TryProp(data, "score", out var scoreProp).Should().BeTrue(
            "data.score must be present; body: {0}", body);

        var difficulty = diffProp.GetInt32();
        var isDefault  = isDefaultProp.GetBoolean();
        var score      = scoreProp.GetDouble();

        score.Should().BeGreaterThanOrEqualTo(0.0,
            "score must be >= 0; body: {0}", body);
        score.Should().BeLessThanOrEqualTo(1.0,
            "score must be <= 1.0; body: {0}", body);

        return (difficulty, isDefault, score);
    }

    // =========================================================================
    // Case 1 — Cold-start: new student (no history) → Medium + isDefault=true
    // =========================================================================
    // AC: AC3 (cold-start returns default Medium), AC2 (deterministic).
    // Engine path: TotalAnswers==0 → signals.IsDefault=true → Medium, IsDefault=true, Score=0.0.
    //
    // Note on null-skillId path (plan Case 5 "null-skillId"):
    //   The endpoint signature requires an int route parameter (skillId:int) so a null value is
    //   not expressible via the HTTP route — it would match no route and return 404 at routing level.
    //   IAdaptivityService.GetTargetDifficulty(studentId, null) is the null-skill cold-start code path;
    //   it is exercised directly by the endpoint when the query handler calls GetTargetDifficulty with
    //   a non-null skillId that has NO answers — which is the same IsDefault=true code path.
    //   We cover the null path via the service-level mechanism: a skill that exists but has no answers
    //   for the student (TotalAnswers==0 → IsDefault=true → Medium). A dedicated Case 5 below
    //   uses a phantom skillId (never seeded) to confirm the graceful fallback.
    // =========================================================================

    [Fact(DisplayName = "P308-C01 New student (no history) → GET /Adaptivity/Skill/{id} → Medium + isDefault=true (cold-start)")]
    public async Task ColdStart_NoHistory_ReturnsMediumIsDefault()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();

        // The student has NO attempts/answers for this skill → cold-start path.
        var (resp, root, body) = await GetAdaptivityDecisionAsync(skillId, token);

        // Assert HTTP 200
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "cold-start must return 200; body: {0}", body);

        // Assert envelope
        var data = AssertEnvelopeAndGetData(root, body);
        var (difficulty, isDefault, score) = AssertDecisionDtoShape(data, body);

        // Difficulty must be Medium (enum value 2)
        difficulty.Should().Be((int)DifficultyLevel.Medium,
            "cold-start must return Medium (2); body: {0}", body);

        // isDefault must be true — signals no real computation was performed
        isDefault.Should().BeTrue(
            "isDefault must be true for a student with no answer history; body: {0}", body);

        // Score must be 0.0 when cold-start
        score.Should().Be(0.0,
            "score must be 0.0 for the cold-start default; body: {0}", body);
    }

    // =========================================================================
    // Case 2 — High-performance history → Hard
    // =========================================================================
    // AC: AC1 (four signals), AC3 (sustained high performance raises difficulty).
    //
    // To guarantee Hard (score >= HighBand=0.8):
    //   - 10 of 10 answers correct  → accuracy=1.0 → acc component = 0.5*1.0 = 0.5
    //   - TimeSpentSeconds=15s < 30s baseline → timeNorm = 30/15 = 2.0 → clamped to 1.0
    //                                         → time component = 0.2*1.0 = 0.2
    //   - hintUsed=false for all   → hintRate=0.0 → hint component = 0.2*(1-0) = 0.2
    //   - 1 attempt (only attempt) → retryCount=TotalAnswers-1=9 (proxy), ExpectedAttempts=2
    //                             → retryNorm=9/2=4.5→clamped to 1.0 → retryComponent=1-1=0.0
    //   Total = 0.5+0.2+0.2+0.0 = 0.9 >= 0.8 → Hard
    //
    // Note: retry proxy (TotalAnswers-1) means more answers inflate retryCount.
    // We use a large number (10) of questions to produce a definitive accuracy signal;
    // the retry component degrades (all correct with no hints still forces score=0.9 ≥ 0.8).
    // =========================================================================

    [Fact(DisplayName = "P308-C02 High-performance history (≥80% accuracy, fast, no hints) → Hard")]
    public async Task HighPerformanceHistory_ReturnsHard()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId    = await SeedLessonAsync("C02-HighPerf-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, count: 10);

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit all 10 correct, fast (15s < 30s baseline), no hints
        foreach (var qId in questionIds)
        {
            var (aResp, _, aBody) = await SubmitAnswerAsync(
                attemptId, qId, answerPayload: "\"A\"",
                hintUsed: false, timeSpentSeconds: 15, token);
            aResp.IsSuccessStatusCode.Should().BeTrue(
                "SubmitAnswer must succeed; body: {0}", aBody);
        }

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", complBody);

        // Now fetch the adaptivity decision
        var (resp, root, body) = await GetAdaptivityDecisionAsync(skillId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Adaptivity/Skill/{id} must return 200; body: {0}", body);

        var data = AssertEnvelopeAndGetData(root, body);
        var (difficulty, isDefault, score) = AssertDecisionDtoShape(data, body);

        // Must NOT be cold-start
        isDefault.Should().BeFalse(
            "isDefault must be false when real history exists; body: {0}", body);

        // Difficulty must be Hard (enum value 3)
        // Score verification: acc=1.0(w=0.5)+timeNorm=1.0(w=0.2)+hint=0(w=0.2)+retry≥0(w=0.1) ≥ 0.8
        difficulty.Should().Be((int)DifficultyLevel.Hard,
            $"10/10 correct, fast, no-hint history must produce Hard (3); score={score}; body: {body}");
    }

    // =========================================================================
    // Case 3 — Struggle history → Easy
    // =========================================================================
    // AC: AC1 (four signals), AC3 (repeated struggle lowers difficulty).
    //
    // To guarantee Easy (score <= LowBand=0.5):
    //   - 2 of 10 correct → accuracy=0.2 → acc component = 0.5*0.2 = 0.1
    //   - TimeSpentSeconds=120s >> 30s baseline → timeNorm=30/120=0.25 → time=0.2*0.25=0.05
    //   - hintUsed=true for all 10 → hintRate=1.0 → hint component = 0.2*(1-1.0) = 0.0
    //   - retryCount=TotalAnswers-1=9 → retryNorm=9/2=4.5→clamped to 1.0 → retryComp=0.0
    //   Total = 0.1+0.05+0.0+0.0 = 0.15 <= 0.5 → Easy
    // =========================================================================

    [Fact(DisplayName = "P308-C03 Struggle history (<40% accuracy, high hints, slow) → Easy")]
    public async Task StruggleHistory_ReturnsEasy()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId    = await SeedLessonAsync("C03-Struggle-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, count: 10);

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // Submit: 2 correct + 8 wrong, all with hints, all slow (120s)
        for (var i = 0; i < questionIds.Count; i++)
        {
            var isCorrect    = i < 2; // first 2 correct, rest wrong
            var answerPayload = isCorrect ? "\"A\"" : "\"B\"";
            var (aResp, _, aBody) = await SubmitAnswerAsync(
                attemptId, questionIds[i], answerPayload,
                hintUsed: true, timeSpentSeconds: 120, token);
            aResp.IsSuccessStatusCode.Should().BeTrue(
                "SubmitAnswer must succeed; body: {0}", aBody);
        }

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", complBody);

        var (resp, root, body) = await GetAdaptivityDecisionAsync(skillId, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Adaptivity/Skill/{id} must return 200; body: {0}", body);

        var data = AssertEnvelopeAndGetData(root, body);
        var (difficulty, isDefault, score) = AssertDecisionDtoShape(data, body);

        isDefault.Should().BeFalse(
            "isDefault must be false when real history exists; body: {0}", body);

        // Difficulty must be Easy (enum value 1)
        difficulty.Should().Be((int)DifficultyLevel.Easy,
            $"2/10 correct, all hints, slow history must produce Easy (1); score={score}; body: {body}");
    }

    // =========================================================================
    // Case 4 — Reproducibility: same seeded history → identical decision twice
    // =========================================================================
    // AC: AC2 (deterministic and reproducible — same inputs always produce same output).
    //
    // Calls the endpoint twice on the same student+skill (same history = same signal aggregate).
    // The engine is pure static; no randomness, no wall-clock. Both calls must return identical
    // difficulty and score.
    // =========================================================================

    [Fact(DisplayName = "P308-C04 Reproducibility: calling GET /Adaptivity/Skill/{id} twice with the same history returns identical difficulty and score")]
    public async Task Reproducibility_SameHistory_IdenticalDecision()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId     = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId    = await SeedLessonAsync("C04-Repro-Lesson");
        var questionIds = await SeedQuestionsAsync(lessonId, skillId, count: 6);

        var attemptId = await StartAttemptViaApiAsync(lessonId, token);

        // 4 of 6 correct, no hints, moderate time — result should land in Medium band
        // score ≈ 0.5*(4/6) + 0.2*(30/45) + 0.2*1.0 + 0.1*(1 - min(5/2,1))
        //       = 0.5*0.667 + 0.2*0.667 + 0.2 + 0.1*(1-1) = 0.333+0.133+0.2+0 = 0.666 (Medium)
        for (var i = 0; i < questionIds.Count; i++)
        {
            var isCorrect     = i < 4; // 4 correct, 2 wrong
            var answerPayload = isCorrect ? "\"A\"" : "\"B\"";
            await SubmitAnswerAsync(attemptId, questionIds[i], answerPayload,
                hintUsed: false, timeSpentSeconds: 45, token);
        }

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", complBody);

        // First call
        var (resp1, root1, body1) = await GetAdaptivityDecisionAsync(skillId, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "First call must return 200; body: {0}", body1);
        var data1 = AssertEnvelopeAndGetData(root1, body1);
        var (diff1, isDef1, score1) = AssertDecisionDtoShape(data1, body1);

        // Second call — history unchanged, engine is deterministic
        var (resp2, root2, body2) = await GetAdaptivityDecisionAsync(skillId, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "Second call must return 200; body: {0}", body2);
        var data2 = AssertEnvelopeAndGetData(root2, body2);
        var (diff2, isDef2, score2) = AssertDecisionDtoShape(data2, body2);

        // Difficulty must be identical
        diff2.Should().Be(diff1,
            $"difficulty must be reproducible (first={diff1}, second={diff2}); body1={body1} body2={body2}");

        // IsDefault must be identical
        isDef2.Should().Be(isDef1,
            $"isDefault must be reproducible (first={isDef1}, second={isDef2}); body1={body1} body2={body2}");

        // Score must be identical (pure static computation — no floating-point variation between calls)
        score2.Should().Be(score1,
            $"score must be reproducible (first={score1}, second={score2}); body1={body1} body2={body2}");
    }

    // =========================================================================
    // Case 5 — Null/absent skill cold-start path
    // =========================================================================
    // Plan note: "Null SkillId (via a lesson with no skill) — call service directly or via a test
    // endpoint → Medium+isDefault" or document if the endpoint requires a skillId.
    //
    // The inspection endpoint route is /Adaptivity/Skill/{skillId:int}, so a null/zero/missing
    // skillId is not expressible as a valid route — a missing segment yields routing-level 404/400.
    // The null-skillId code path (IAdaptivityService.GetTargetDifficulty with skillId=null) is an
    // in-process path for P3-11 callers; the endpoint itself does inline validation rejecting <=0.
    //
    // This test covers two meaningful cold-start HTTP cases:
    //   5a — skillId=0 (fails inline handler validation → 400 BadRequest, not 500)
    //   5b — phantom skillId with no answer history → the service finds TotalAnswers=0 →
    //        engine returns IsDefault=true → endpoint returns 200 Medium+isDefault.
    //
    // Both confirm the safe/graceful cold-start behavior visible at HTTP level.
    // =========================================================================

    [Fact(DisplayName = "P308-C05a Skill with no answer history (phantom skillId) → 200 Medium + isDefault=true (cold-start fallback)")]
    public async Task PhantomSkill_NoAnswerHistory_ReturnsMediumIsDefault()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();

        // Use a large phantom skillId that is never seeded.
        // The service will find TotalAnswers=0 → IsDefault=true → Medium.
        const int phantomSkillId = 999_000_308;

        var (resp, root, body) = await GetAdaptivityDecisionAsync(phantomSkillId, token);

        // Must be 200 (graceful cold-start), never 404 or 500
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "phantom skill with no history must return 200 (cold-start default), not 404/500; body: {0}", body);

        var data = AssertEnvelopeAndGetData(root, body);
        var (difficulty, isDefault, score) = AssertDecisionDtoShape(data, body);

        difficulty.Should().Be((int)DifficultyLevel.Medium,
            "phantom/no-history skill must return Medium (2); body: {0}", body);
        isDefault.Should().BeTrue(
            "isDefault must be true when no answers exist for this skill; body: {0}", body);
        score.Should().Be(0.0,
            "score must be 0.0 for the cold-start default; body: {0}", body);
    }

    [Fact(DisplayName = "P308-C05b skillId=0 (invalid) → inline handler validation returns 400 (not 500)")]
    public async Task InvalidSkillId_Zero_Returns400()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();

        // skillId=0 fails the inline validation check (SkillId <= 0 → BadRequest)
        // in GetAdaptivityDecisionQueryHandler. This maps to HTTP 400 via BaseResponseHandler.
        var (resp, root, body) = await GetAdaptivityDecisionAsync(skillId: 0, token);

        // HTTP 400 from the BadRequest response path
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "skillId=0 must return 400 (BadRequest) per inline validation; body: {0}", body);

        // Envelope check: successed=false, statusCode=400
        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeFalse(
            "Successed must be false for a BadRequest response; body: {0}", body);

        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("body: {0}", body);
        statusCode.GetInt32().Should().Be(400, "statusCode must be 400; body: {0}", body);
    }

    // =========================================================================
    // Case 6 — IDOR: Student B's decision reflects only their own history
    // =========================================================================
    // AC: IDOR guard — studentId is always taken from JWT, never from the request.
    //
    // Student A has a high-performance history on a skill (→ Hard).
    // Student B has no history on the same skill (→ Medium cold-start).
    // Both call the endpoint with the same skillId.
    // Student B must receive Medium+isDefault=true, not Hard (Student A's result).
    // =========================================================================

    [Fact(DisplayName = "P308-C06 IDOR: Student B's adaptivity decision is based only on their own history, not Student A's")]
    public async Task IDOR_StudentB_ReflectsOwnHistoryOnly()
    {
        // ── Student A: seed high-performance history ──────────────────────────
        var (tokenA, _) = await CreateStudentViaParentFlowAsync();
        var skillId      = await SeedSkillAsync(masteryThreshold: 80);
        var lessonId     = await SeedLessonAsync("C06-IDOR-Lesson");
        var questionIds  = await SeedQuestionsAsync(lessonId, skillId, count: 10);

        var attemptIdA = await StartAttemptViaApiAsync(lessonId, tokenA);
        foreach (var qId in questionIds)
        {
            await SubmitAnswerAsync(attemptIdA, qId, "\"A\"",
                hintUsed: false, timeSpentSeconds: 15, tokenA);
        }
        var (complRespA, _, complBodyA) = await CompleteAttemptAsync(attemptIdA, tokenA);
        complRespA.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student A's CompleteAttempt must succeed; body: {0}", complBodyA);

        // Verify Student A sees Hard
        var (respA, rootA, bodyA) = await GetAdaptivityDecisionAsync(skillId, tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "Student A must get 200; body: {0}", bodyA);
        var dataA = AssertEnvelopeAndGetData(rootA, bodyA);
        var (diffA, _, _) = AssertDecisionDtoShape(dataA, bodyA);
        diffA.Should().Be((int)DifficultyLevel.Hard,
            $"Student A must see Hard after high-performance seeding; body: {bodyA}");

        // ── Student B: no history on the same skillId ─────────────────────────
        var (tokenB, _) = await CreateStudentViaParentFlowAsync();

        var (respB, rootB, bodyB) = await GetAdaptivityDecisionAsync(skillId, tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK,
            "Student B must get 200 (their own cold-start); body: {0}", bodyB);

        var dataB = AssertEnvelopeAndGetData(rootB, bodyB);
        var (diffB, isDefaultB, scoreB) = AssertDecisionDtoShape(dataB, bodyB);

        // Student B must NOT see Student A's Hard decision
        diffB.Should().Be((int)DifficultyLevel.Medium,
            $"Student B must see Medium (their own cold-start), not Student A's Hard " +
            $"(IDOR guard — studentId from JWT); diffB={diffB}; body: {bodyB}");

        isDefaultB.Should().BeTrue(
            "Student B's isDefault must be true (no history); body: {0}", bodyB);

        scoreB.Should().Be(0.0,
            "Student B's score must be 0.0 (cold-start); body: {0}", bodyB);
    }

    // =========================================================================
    // Auth guard — endpoint must return 401 without a JWT
    // =========================================================================

    [Fact(DisplayName = "P308-AUTH-01 GET /Adaptivity/Skill/{id} without JWT → 401 Unauthorized")]
    public async Task GetAdaptivityDecision_WithoutJwt_Returns401()
    {
        var (resp, _, body) = await GetAdaptivityDecisionAsync(skillId: 1, token: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET /Adaptivity/Skill/{id} without JWT must return 401; body: {0}", body);
    }
}

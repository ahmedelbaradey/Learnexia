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
/// P3-11 integration tests — "Serve adaptive quizzes (adaptive question selection)".
///
/// Endpoint under test:
///   POST /api/Learning/Quizzes/{lessonId}/Attempt   [Authorize(Roles="Student")]
///
/// Feature under test (P3-11-BE-4 wiring):
///   StartAttemptCommandHandler now calls IAdaptivityService.GetTargetDifficulty before building
///   the question list, then QuizSelectionEngine.Select to weight the served set, and finally
///   persists Attempt.ServedDifficultyMix (jsonb) + Attempt.TargetDifficulty (int) on the entity.
///
/// PREREQUISITE — multi-difficulty data:
///   The standard seed likely has no questions at multiple DifficultyLevels. Every test that needs
///   observable selection seeds its own lesson with >= 3 Easy + >= 3 Medium + >= 3 Hard questions
///   via the test DbContext (EF direct insert — bypasses HTTP to keep tests self-contained).
///   Correct answer is always "\"A\"" so tests can control correctness in SubmitAnswer payloads.
///
/// How "strong" and "struggle" history is produced (Cases 2/3):
///   The adaptivity engine (P3-08) is score-based:
///     score = 0.5*accuracy + 0.2*timeNorm + 0.2*(1-hintRate) + 0.1*(1-retryNorm)
///     Hard if score >= 0.8; Easy if score <= 0.5; Medium otherwise.
///   For "strong" (Hard target): 10/10 correct, 15 s each, no hints → score ≥ 0.9.
///   For "struggle" (Easy target): 2/10 correct, 120 s each, all hints  → score ≈ 0.15.
///   One completed attempt on the skill is enough to move out of cold-start.
///
/// Coverage map (8 plan cases):
///   Case 1  → ColdStart_NewStudent_MixIsDefaultAndBalanced
///   Case 2  → StrongHistory_MixSkewsHard
///   Case 3  → StruggleHistory_MixSkewsEasy
///   Case 4  → ResumeAttempt_IdenticalQuestionList
///   Case 5  → NullSkillLesson_StartsWithoutError_MixIsDefault
///   Case 6  → ThinPool_OnlyEasyQuestions_StartsSuccessfully
///   Case 7  → UnpublishedQuestions_NotServed_ExistingGuardPreserved
///   Case 8  → ServedDifficultyMix_NonNullParsableJson_AfterStart
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_11_AdaptiveQuizSelection_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P3_11_AdaptiveQuizSelection_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // HTTP helpers — mirror P3_08_AdaptivityEngine_Tests patterns exactly
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p311_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
            catch { /* non-JSON body — root stays default */ }
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
                FullName         = "Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 3,
                Language         = "ar",
                Country          = "EG",
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

    // =========================================================================
    // DB seeding helpers
    // =========================================================================

    /// <summary>
    /// Seeds Grade → Subject → Unit → Lesson with an optional skillId.
    /// Subject defaults: SubjectCode=MATH (0) + Language=Ar (0). The P8-03 language guard resolves
    /// SubjectLanguageResolver.Resolve(MATH, Ar) = Ar, which matches Subject.Language=Ar — guard passes.
    /// The student's LearningLanguage="ar" (set in Add-Child) maps to ContentLanguage.Ar via the JWT claim.
    /// </summary>
    private async Task<int> SeedLessonAsync(string lessonName = "P311 Lesson", int? skillId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 99,
            DisplayName = $"G311_{Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        // SubjectCode defaults to MATH (0); Language defaults to Ar (0).
        // SubjectLanguageResolver.Resolve(MATH, Ar) = Ar = Subject.Language → guard passes.
        var subject = new Subject
        {
            Name      = $"S311_{Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var unit = new Unit
        {
            Name          = $"U311_{Guid.NewGuid():N}",
            SequenceOrder = 1,
            SubjectId     = subject.Id,
            CreatedAt     = now,
            CreatedBy     = 0
        };
        await db.Units.AddAsync(unit);
        await db.SaveChangesAsync();

        var lesson = new Lesson
        {
            Name           = lessonName,
            Difficulty     = DifficultyLevel.Medium,
            SequenceOrder  = 1,
            IsLocked       = false,
            UnitId         = unit.Id,
            SkillId        = skillId,
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
    /// Seeds a Skill entity (requires Concept → Subject → Grade hierarchy). Returns the new Skill.Id.
    /// </summary>
    private async Task<int> SeedSkillAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        var grade = new Grade
        {
            Number      = 88,
            DisplayName = $"SkG311_{Guid.NewGuid():N}",
            CreatedAt   = now,
            CreatedBy   = 0
        };
        await db.Grades.AddAsync(grade);
        await db.SaveChangesAsync();

        var subject = new Subject
        {
            Name      = $"SkS311_{Guid.NewGuid():N}",
            GradeId   = grade.Id,
            CreatedAt = now,
            CreatedBy = 0
        };
        await db.Subjects.AddAsync(subject);
        await db.SaveChangesAsync();

        var concept = new Concept
        {
            Name            = $"SkC311_{Guid.NewGuid():N}",
            DifficultyLevel = DifficultyLevel.Medium,
            SubjectId       = subject.Id,
            CreatedAt       = now,
            CreatedBy       = 0
        };
        await db.Concepts.AddAsync(concept);
        await db.SaveChangesAsync();

        var skill = new Skill
        {
            Name                 = $"Skill311_{Guid.NewGuid():N}",
            MasteryThreshold     = 80,
            EstimatedTimeMinutes = 15,
            ConceptId            = concept.Id,
            CreatedAt            = now,
            CreatedBy            = 0
        };
        await db.Skills.AddAsync(skill);
        await db.SaveChangesAsync();

        return skill.Id;
    }

    /// <summary>
    /// Seeds questions for a lesson at specific difficulty levels.
    /// All have CorrectAnswer="\"A\"" so tests control correctness by submitting "\"A\"" or "\"B\"".
    /// </summary>
    private async Task<List<int>> SeedQuestionsAtDifficultyAsync(
        int lessonId,
        int? skillId,
        DifficultyLevel difficulty,
        int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;
        var ids = new List<int>();

        for (var i = 0; i < count; i++)
        {
            var q = new QuizQuestion
            {
                LessonId       = lessonId,
                SkillId        = skillId,
                QuestionType   = QuestionType.MCQ,
                QuestionText   = $"P311-{difficulty}-Q{i + 1}-{Guid.NewGuid():N}?",
                Options        = "[\"A\",\"B\",\"C\",\"D\"]",
                CorrectAnswer  = "\"A\"",
                Difficulty     = difficulty,
                GeneratedBy    = GeneratedBy.Curated,
                IsActive       = true,
                LifecycleState = LifecycleState.Published,
                CreatedAt      = now,
                CreatedBy      = 0
            };
            await db.QuizQuestions.AddAsync(q);
            await db.SaveChangesAsync();
            ids.Add(q.Id);
        }

        return ids;
    }

    /// <summary>
    /// Seeds the full multi-difficulty pool required for observable adaptive selection:
    /// >= 3 Easy + >= 3 Medium + >= 3 Hard questions for a lesson on a skill.
    /// Returns the combined list of all question IDs.
    /// </summary>
    private async Task<(List<int> EasyIds, List<int> MediumIds, List<int> HardIds)>
        SeedMultiDifficultyPoolAsync(int lessonId, int skillId, int perLevel = 3)
    {
        var easy   = await SeedQuestionsAtDifficultyAsync(lessonId, skillId, DifficultyLevel.Easy,   perLevel);
        var medium = await SeedQuestionsAtDifficultyAsync(lessonId, skillId, DifficultyLevel.Medium, perLevel);
        var hard   = await SeedQuestionsAtDifficultyAsync(lessonId, skillId, DifficultyLevel.Hard,   perLevel);
        return (easy, medium, hard);
    }

    // =========================================================================
    // API action helpers
    // =========================================================================

    private async Task<(int AttemptId, JsonElement Data, string Body)>
        StartAttemptAndGetDataAsync(int lessonId, string studentToken)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; body: {0}", body);

        // Assert envelope
        TryProp(root, "successed", out var successed).Should().BeTrue("envelope must have 'successed'; body: {0}", body);
        successed.GetBoolean().Should().BeTrue("Successed must be true; body: {0}", body);
        TryProp(root, "statusCode", out var statusCode).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        statusCode.GetInt32().Should().Be(200, "statusCode must be 200; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body: {0}", body);

        TryProp(data, "attemptId", out var idProp).Should().BeTrue("data.attemptId required; body: {0}", body);
        return (idProp.GetInt32(), data, body);
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SubmitAnswerAsync(int attemptId, int questionId, string answerPayload,
            bool hintUsed, int timeSpentSeconds, string token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new
            {
                QuestionId       = questionId,
                AnswerPayload    = answerPayload,
                TimeSpentSeconds = timeSpentSeconds,
                HintUsed         = hintUsed
            },
            token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        CompleteAttemptAsync(int attemptId, string token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    /// <summary>
    /// Drives a full attempt: start → submit all questions → complete.
    /// correctFraction controls how many questions are answered correctly.
    /// hintUsed and timeSpentSeconds apply to every answer.
    /// </summary>
    private async Task DriveCompletedAttemptAsync(
        int lessonId,
        List<int> questionIds,
        string token,
        double correctFraction,
        bool hintUsed,
        int timeSpentSeconds)
    {
        var (attemptId, _, _) = await StartAttemptAndGetDataAsync(lessonId, token);

        int correctCount = (int)Math.Round(questionIds.Count * correctFraction);
        for (var i = 0; i < questionIds.Count; i++)
        {
            var answer = i < correctCount ? "\"A\"" : "\"B\"";
            var (aResp, _, aBody) = await SubmitAnswerAsync(
                attemptId, questionIds[i], answer, hintUsed, timeSpentSeconds, token);
            aResp.IsSuccessStatusCode.Should().BeTrue(
                "SubmitAnswer must succeed; body: {0}", aBody);
        }

        var (cResp, _, cBody) = await CompleteAttemptAsync(attemptId, token);
        cResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", cBody);
    }

    /// <summary>
    /// Reads the Attempt row from the DB and returns the raw ServedDifficultyMix string and TargetDifficulty.
    /// </summary>
    private async Task<(string? ServedDifficultyMix, int? TargetDifficulty)>
        ReadAttemptMixFromDbAsync(int attemptId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var attempt = await db.Attempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attemptId);
        attempt.Should().NotBeNull($"Attempt {attemptId} must exist in the DB");
        return (attempt!.ServedDifficultyMix, attempt.TargetDifficulty);
    }

    // =========================================================================
    // Case 1 — Cold-start: new student → WasDefault=true + balanced/Medium mix (AC3)
    // =========================================================================
    // A brand-new student has no answer history for any skill, so the adaptivity engine
    // (P3-08) returns IsDefault=true + Difficulty=Medium. QuizSelectionEngine therefore
    // selects a Medium-weighted mix. We verify:
    //   - StartAttempt succeeds (200)
    //   - BaseResponse<T> envelope is correct
    //   - Attempt.ServedDifficultyMix.WasDefault == true (cold-start flag persisted)
    //   - Questions array is non-empty (pool has all three difficulty levels)
    //   - CorrectAnswer is NOT present in the response body (security guard preserved)
    // =========================================================================

    [Fact(DisplayName = "P311-C01 Cold-start new student → StartAttempt 200; ServedDifficultyMix.WasDefault=true; CorrectAnswer absent (AC3)")]
    public async Task ColdStart_NewStudent_MixIsDefaultAndBalanced()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C01-ColdStart-Lesson", skillId);
        await SeedMultiDifficultyPoolAsync(lessonId, skillId);

        // New student has no history → cold-start path (IsDefault=true, target=Medium)
        var (attemptId, data, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        // Questions list must be present and non-empty
        TryProp(data, "questions", out var questions).Should().BeTrue(
            "data.questions must be present; body: {0}", body);
        questions.ValueKind.Should().Be(JsonValueKind.Array,
            "questions must be an array; body: {0}", body);
        questions.GetArrayLength().Should().BeGreaterThan(0,
            "questions list must be non-empty (multi-difficulty pool seeded); body: {0}", body);

        // CorrectAnswer must NOT appear anywhere in the raw response (security guard AC-P2-06)
        body.ToLowerInvariant().Should().NotContain("\"correctanswer\"",
            "CorrectAnswer must never be exposed in StartAttempt response; body: {0}", body);

        // Read the mix from the DB and confirm WasDefault=true
        var (mixJson, targetDifficulty) = await ReadAttemptMixFromDbAsync(attemptId);
        mixJson.Should().NotBeNullOrWhiteSpace(
            "ServedDifficultyMix must be persisted (not null) after StartAttempt (AC4)");

        var mix = JsonDocument.Parse(mixJson!).RootElement;
        TryProp(mix, "WasDefault", out var wasDefault).Should().BeTrue(
            "ServedDifficultyMix must contain 'WasDefault'; json: {0}", mixJson);
        wasDefault.GetBoolean().Should().BeTrue(
            "WasDefault must be true for a cold-start (new student, no history); json: {0}", mixJson);

        // TargetDifficulty persisted as Medium (2)
        targetDifficulty.Should().Be((int)DifficultyLevel.Medium,
            "TargetDifficulty must be Medium (2) for cold-start; json: {0}", mixJson);

        // Target field in the JSON must be "Medium"
        TryProp(mix, "Target", out var target).Should().BeTrue(
            "ServedDifficultyMix must contain 'Target'; json: {0}", mixJson);
        target.GetString().Should().Be("Medium",
            "Target field must be 'Medium' for cold-start; json: {0}", mixJson);
    }

    // =========================================================================
    // Case 2 — Strong history → mix skews Hard (hardCount > easyCount) (AC2 + AC4)
    // =========================================================================
    // We seed a full multi-difficulty pool, drive a high-performance attempt on the skill
    // (10/10 correct, 15s each, no hints → score ≥ 0.9 → Hard target), then start a SECOND
    // attempt and verify the recorded mix has more Hard questions than Easy questions.
    //
    // AdaptivityEngine weights (defaults): WeightAccuracy=0.5, WeightTime=0.2, WeightHint=0.2,
    // WeightRetry=0.1; HighBand=0.8; baseline=30s.
    // Score path: accuracy=1.0→0.5 + timeNorm=clamp(30/15,1)=1.0→0.2 + hint=0→0.2 = 0.9 ≥ 0.8 → Hard.
    // =========================================================================

    [Fact(DisplayName = "P311-C02 Strong history (10/10 correct, fast, no hint) → second StartAttempt mix skews Hard (hardCount > easyCount) (AC2 + AC4)")]
    public async Task StrongHistory_MixSkewsHard()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C02-StrongHistory-Lesson", skillId);
        var (easyIds, mediumIds, hardIds) = await SeedMultiDifficultyPoolAsync(lessonId, skillId, perLevel: 3);
        var allIds = easyIds.Concat(mediumIds).Concat(hardIds).ToList();

        // Drive a high-performance completed attempt so the adaptivity model records Hard signal
        await DriveCompletedAttemptAsync(
            lessonId, allIds, token,
            correctFraction: 1.0,   // all correct
            hintUsed: false,
            timeSpentSeconds: 15);  // fast (< 30s baseline) → Hard target

        // Start a new attempt (now out of cold-start, history says Hard)
        var (attemptId, data, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        // Read the persisted mix from the DB
        var (mixJson, targetDifficulty) = await ReadAttemptMixFromDbAsync(attemptId);
        mixJson.Should().NotBeNullOrWhiteSpace(
            "ServedDifficultyMix must be persisted; body: {0}", body);

        var mix = JsonDocument.Parse(mixJson!).RootElement;

        // Target must be Hard
        TryProp(mix, "Target", out var target).Should().BeTrue("'Target' must be in mix json; {0}", mixJson);
        target.GetString().Should().Be("Hard",
            "Target must be 'Hard' for a high-performance student; json: {0}", mixJson);

        targetDifficulty.Should().Be((int)DifficultyLevel.Hard,
            "TargetDifficulty column must be Hard (3); json: {0}", mixJson);

        // WasDefault must be false (real history exists)
        TryProp(mix, "WasDefault", out var wasDefault).Should().BeTrue("'WasDefault' must be in mix; {0}", mixJson);
        wasDefault.GetBoolean().Should().BeFalse(
            "WasDefault must be false — student has real history; json: {0}", mixJson);

        // The mix must skew Hard: hardCount > easyCount (AC2 direction)
        TryProp(mix, "Hard", out var hardCount).Should().BeTrue("'Hard' must be in mix; {0}", mixJson);
        TryProp(mix, "Easy", out var easyCount).Should().BeTrue("'Easy' must be in mix; {0}", mixJson);
        hardCount.GetInt32().Should().BeGreaterThan(easyCount.GetInt32(),
            "Hard-target mix must have more Hard questions than Easy questions (AC2 direction skews Hard); json: {0}", mixJson);
    }

    // =========================================================================
    // Case 3 — Struggle history → mix skews Easy (easyCount > hardCount) (AC2)
    // =========================================================================
    // Score path: 2/10 correct→0.1 + slow(120s)→0.05 + all hints→0.0 = 0.15 ≤ 0.5 → Easy target.
    // =========================================================================

    [Fact(DisplayName = "P311-C03 Struggle history (2/10 correct, slow, all hints) → second StartAttempt mix skews Easy (easyCount > hardCount) (AC2)")]
    public async Task StruggleHistory_MixSkewsEasy()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C03-StruggleHistory-Lesson", skillId);
        var (easyIds, mediumIds, hardIds) = await SeedMultiDifficultyPoolAsync(lessonId, skillId, perLevel: 3);
        var allIds = easyIds.Concat(mediumIds).Concat(hardIds).ToList();

        // Drive a struggling completed attempt: only 2 of 9 correct, slow, all with hints
        // correctFraction ≈ 2/9 ≈ 0.22 → score ≤ 0.5 → Easy target
        await DriveCompletedAttemptAsync(
            lessonId, allIds, token,
            correctFraction: 2.0 / 9.0,  // ~2 correct of 9 total
            hintUsed: true,
            timeSpentSeconds: 120);       // very slow (>> 30s baseline)

        // Start a new attempt — now history says Easy
        var (attemptId, data, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        var (mixJson, targetDifficulty) = await ReadAttemptMixFromDbAsync(attemptId);
        mixJson.Should().NotBeNullOrWhiteSpace(
            "ServedDifficultyMix must be persisted; body: {0}", body);

        var mix = JsonDocument.Parse(mixJson!).RootElement;

        // Target must be Easy
        TryProp(mix, "Target", out var target).Should().BeTrue("'Target' must be in mix; {0}", mixJson);
        target.GetString().Should().Be("Easy",
            "Target must be 'Easy' for a struggling student; json: {0}", mixJson);

        targetDifficulty.Should().Be((int)DifficultyLevel.Easy,
            "TargetDifficulty column must be Easy (1); json: {0}", mixJson);

        TryProp(mix, "WasDefault", out var wasDefault).Should().BeTrue("'WasDefault' must be in mix; {0}", mixJson);
        wasDefault.GetBoolean().Should().BeFalse(
            "WasDefault must be false — real history exists; json: {0}", mixJson);

        // Mix must skew Easy: easyCount > hardCount (AC2 direction)
        TryProp(mix, "Easy", out var easyCount).Should().BeTrue("'Easy' must be in mix; {0}", mixJson);
        TryProp(mix, "Hard", out var hardCount).Should().BeTrue("'Hard' must be in mix; {0}", mixJson);
        easyCount.GetInt32().Should().BeGreaterThan(hardCount.GetInt32(),
            "Easy-target mix must have more Easy questions than Hard questions (AC2 direction skews Easy); json: {0}", mixJson);
    }

    // =========================================================================
    // Case 4 — Resume same attempt → identical question list (determinism, AC4)
    // =========================================================================
    // QuizSelectionEngine sorts candidates by Id before selection. Re-running Select with the
    // same candidate pool + same target always produces the same ordered set. We verify that
    // the second StartAttempt call (which resumes the existing in-progress attempt) returns
    // exactly the same list of question IDs in the same order.
    // =========================================================================

    [Fact(DisplayName = "P311-C04 Resume in-progress attempt → identical question list returned (determinism, AC4)")]
    public async Task ResumeAttempt_IdenticalQuestionList()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C04-Resume-Lesson", skillId);
        await SeedMultiDifficultyPoolAsync(lessonId, skillId);

        // First StartAttempt — creates the attempt and serves the selected set
        var (resp1, root1, body1) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first StartAttempt must succeed; body: {0}", body1);
        TryProp(root1, "data", out var data1).Should().BeTrue("body: {0}", body1);
        TryProp(data1, "questions", out var questions1).Should().BeTrue("body: {0}", body1);
        var ids1 = questions1.EnumerateArray()
            .Select(q => { TryProp(q, "id", out var idEl); return idEl.GetInt32(); })
            .ToList();

        // Second StartAttempt — resumes (in-progress attempt still open)
        var (resp2, root2, body2) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "second StartAttempt (resume) must succeed; body: {0}", body2);
        TryProp(root2, "data", out var data2).Should().BeTrue("body: {0}", body2);
        TryProp(data2, "questions", out var questions2).Should().BeTrue("body: {0}", body2);
        var ids2 = questions2.EnumerateArray()
            .Select(q => { TryProp(q, "id", out var idEl); return idEl.GetInt32(); })
            .ToList();

        // The question lists must be identical (same IDs in the same order — determinism AC4)
        ids1.Should().HaveSameCount(ids2,
            "resume must return the same number of questions; body2: {0}", body2);
        ids2.Should().Equal(ids1,
            "resume must return identical question IDs in the same order (determinism AC4); " +
            $"first={string.Join(",", ids1)} second={string.Join(",", ids2)}; body2={body2}");
    }

    // =========================================================================
    // Case 5 — Lesson with null SkillId → starts without error; mix is default/balanced (AC3)
    // =========================================================================
    // When a lesson has no SkillId, IAdaptivityService.GetTargetDifficulty(studentId, null, ct)
    // returns a cold-start default (Medium, IsDefault=true) without touching the database.
    // The quiz start must succeed and the persisted mix must have WasDefault=true.
    // =========================================================================

    [Fact(DisplayName = "P311-C05 Lesson with null SkillId → StartAttempt 200; mix is default/balanced; WasDefault=true (AC3 null-skill)")]
    public async Task NullSkillLesson_StartsWithoutError_MixIsDefault()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();

        // Seed lesson WITHOUT a skill (SkillId = null)
        var lessonId = await SeedLessonAsync("C05-NullSkill-Lesson", skillId: null);

        // Seed some questions (only Easy — we just need a non-empty pool; difficulty doesn't matter here)
        await SeedQuestionsAtDifficultyAsync(lessonId, null, DifficultyLevel.Easy,   3);
        await SeedQuestionsAtDifficultyAsync(lessonId, null, DifficultyLevel.Medium, 3);
        await SeedQuestionsAtDifficultyAsync(lessonId, null, DifficultyLevel.Hard,   3);

        var (attemptId, data, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        // Questions must be returned (non-empty pool)
        TryProp(data, "questions", out var questions).Should().BeTrue("body: {0}", body);
        questions.GetArrayLength().Should().BeGreaterThan(0,
            "questions must be served even for null-SkillId lessons; body: {0}", body);

        // Read persisted mix from the DB
        var (mixJson, targetDifficulty) = await ReadAttemptMixFromDbAsync(attemptId);
        mixJson.Should().NotBeNullOrWhiteSpace(
            "ServedDifficultyMix must be persisted even for null-SkillId lessons (AC4)");

        var mix = JsonDocument.Parse(mixJson!).RootElement;

        TryProp(mix, "WasDefault", out var wasDefault).Should().BeTrue("'WasDefault' must be in mix; {0}", mixJson);
        wasDefault.GetBoolean().Should().BeTrue(
            "WasDefault must be true (null SkillId → cold-start default path); json: {0}", mixJson);

        // Target must be Medium (cold-start default)
        TryProp(mix, "Target", out var target).Should().BeTrue("'Target' must be in mix; {0}", mixJson);
        target.GetString().Should().Be("Medium",
            "Target must be 'Medium' (cold-start default for null-skill lesson); json: {0}", mixJson);

        targetDifficulty.Should().Be((int)DifficultyLevel.Medium,
            "TargetDifficulty column must be Medium (2) for null-skill cold-start; json: {0}", mixJson);
    }

    // =========================================================================
    // Case 6 — Thin pool (only Easy questions) → quiz starts successfully; mix contains only Easy
    // =========================================================================
    // The graceful degradation path: when the target bucket (Medium or Hard) is empty,
    // QuizSelectionEngine falls back to serving the full sorted pool (all Easy).
    // The quiz must still start (no 500), and the persisted mix must reflect only Easy questions.
    // =========================================================================

    [Fact(DisplayName = "P311-C06 Thin pool (only Easy questions) → StartAttempt succeeds; mix contains only Easy questions (AC resilience)")]
    public async Task ThinPool_OnlyEasyQuestions_StartsSuccessfully()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C06-ThinPool-Lesson", skillId);

        // Seed ONLY Easy questions — Medium and Hard buckets are empty
        await SeedQuestionsAtDifficultyAsync(lessonId, skillId, DifficultyLevel.Easy, 4);

        // New student (cold-start → Medium target) but only Easy questions exist
        // QuizSelectionEngine: target=Medium, mediumBucket is empty → falls back to full sorted pool (all Easy)
        var (attemptId, data, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        // Questions must be returned (graceful degradation: returns all Easy)
        TryProp(data, "questions", out var questions).Should().BeTrue("body: {0}", body);
        questions.GetArrayLength().Should().BeGreaterThan(0,
            "questions must be served even when target bucket is empty (graceful degradation); body: {0}", body);

        // Read the DB mix
        var (mixJson, _) = await ReadAttemptMixFromDbAsync(attemptId);
        mixJson.Should().NotBeNullOrWhiteSpace(
            "ServedDifficultyMix must be persisted even for thin pool; body: {0}", body);

        var mix = JsonDocument.Parse(mixJson!).RootElement;

        // Must contain only Easy questions (Medium and Hard counts must be 0)
        TryProp(mix, "Easy",   out var easyCount).Should().BeTrue("'Easy' must be in mix; {0}", mixJson);
        TryProp(mix, "Medium", out var medCount).Should().BeTrue("'Medium' must be in mix; {0}", mixJson);
        TryProp(mix, "Hard",   out var hardCount).Should().BeTrue("'Hard' must be in mix; {0}", mixJson);

        easyCount.GetInt32().Should().BeGreaterThan(0,
            "Easy count must be > 0 (all questions are Easy); json: {0}", mixJson);
        medCount.GetInt32().Should().Be(0,
            "Medium count must be 0 (no Medium questions seeded); json: {0}", mixJson);
        hardCount.GetInt32().Should().Be(0,
            "Hard count must be 0 (no Hard questions seeded); json: {0}", mixJson);

        // No 500 — the endpoint returned 200 (already asserted in StartAttemptAndGetDataAsync)
    }

    // =========================================================================
    // Case 7 — Regression: lifecycle/language guard still rejects unpublished/inactive questions
    // =========================================================================
    // The selection step is additive — it must not bypass the existing P7-04/P7-05 guards.
    // We seed a lesson with both Published+Active questions AND Archived or Inactive questions.
    // Only the Published+Active questions must appear in the StartAttempt response.
    // We also seed a Draft question. The question count in the response must match only the valid ones.
    // =========================================================================

    [Fact(DisplayName = "P311-C07 Regression: Archived/Draft/Inactive questions excluded from StartAttempt even after P3-11 wiring (lifecycle guard preserved)")]
    public async Task UnpublishedQuestions_NotServed_ExistingGuardPreserved()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C07-LifecycleGuard-Lesson", skillId);

        using var scope = _factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var now = DateTime.UtcNow;

        // Valid questions (Published + Active) — these should be served
        var validQ1 = new QuizQuestion
        {
            LessonId       = lessonId,
            SkillId        = skillId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = "ValidQ1?",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = DifficultyLevel.Easy,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0
        };
        var validQ2 = new QuizQuestion
        {
            LessonId       = lessonId,
            SkillId        = skillId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = "ValidQ2?",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = DifficultyLevel.Medium,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = true,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0
        };

        // Archived question — must NOT be served
        var archivedQ = new QuizQuestion
        {
            LessonId       = lessonId,
            SkillId        = skillId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = "ArchivedQ?",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = DifficultyLevel.Hard,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = true,
            LifecycleState = LifecycleState.Archived,
            CreatedAt      = now,
            CreatedBy      = 0
        };

        // Draft question — must NOT be served
        var draftQ = new QuizQuestion
        {
            LessonId       = lessonId,
            SkillId        = skillId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = "DraftQ?",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = DifficultyLevel.Easy,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = true,
            LifecycleState = LifecycleState.Draft,
            CreatedAt      = now,
            CreatedBy      = 0
        };

        // Inactive (IsActive=false) question — must NOT be served
        var inactiveQ = new QuizQuestion
        {
            LessonId       = lessonId,
            SkillId        = skillId,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = "InactiveQ?",
            Options        = "[\"A\",\"B\",\"C\",\"D\"]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = DifficultyLevel.Medium,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = false,
            LifecycleState = LifecycleState.Published,
            CreatedAt      = now,
            CreatedBy      = 0
        };

        await db.QuizQuestions.AddRangeAsync(validQ1, validQ2, archivedQ, draftQ, inactiveQ);
        await db.SaveChangesAsync();

        var (attemptId, data, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        TryProp(data, "questions", out var questions).Should().BeTrue("body: {0}", body);
        var returnedIds = questions.EnumerateArray()
            .Select(q => { TryProp(q, "id", out var idEl); return idEl.GetInt32(); })
            .ToList();

        // Only validQ1 and validQ2 must appear — no archived, draft, or inactive question
        returnedIds.Should().NotContain(archivedQ.Id,
            "Archived question must not appear in StartAttempt response (P7-05 lifecycle guard)");
        returnedIds.Should().NotContain(draftQ.Id,
            "Draft question must not appear in StartAttempt response (P7-05 lifecycle guard)");
        returnedIds.Should().NotContain(inactiveQ.Id,
            "Inactive question must not appear in StartAttempt response (P7-04 IsActive guard)");

        returnedIds.Should().Contain(validQ1.Id,
            "Published+Active question validQ1 must appear; ids: {0}", string.Join(",", returnedIds));
        returnedIds.Should().Contain(validQ2.Id,
            "Published+Active question validQ2 must appear; ids: {0}", string.Join(",", returnedIds));
    }

    // =========================================================================
    // Case 8 — Attempt.ServedDifficultyMix is non-null and parseable JSON after start (AC4 persistence)
    // =========================================================================
    // Verifies the end-to-end persistence of AC4: after StartAttempt, the Attempt row in the DB
    // must have a non-null ServedDifficultyMix column, and that value must parse as valid JSON
    // with all required keys: Easy, Medium, Hard, Target, WasDefault.
    // =========================================================================

    [Fact(DisplayName = "P311-C08 Attempt.ServedDifficultyMix is non-null and parseable JSON with all required keys after StartAttempt (AC4 persistence)")]
    public async Task ServedDifficultyMix_NonNullParsableJson_AfterStart()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync();
        var skillId    = await SeedSkillAsync();
        var lessonId   = await SeedLessonAsync("C08-MixPersistence-Lesson", skillId);
        await SeedMultiDifficultyPoolAsync(lessonId, skillId);

        var (attemptId, _, body) = await StartAttemptAndGetDataAsync(lessonId, token);

        // Read mix directly from the DB
        var (mixJson, targetDifficulty) = await ReadAttemptMixFromDbAsync(attemptId);

        // AC4: must be non-null and non-empty
        mixJson.Should().NotBeNullOrWhiteSpace(
            "Attempt.ServedDifficultyMix must be persisted (non-null) after StartAttempt (AC4); body: {0}", body);

        // Must be parseable JSON
        JsonDocument parsedMix;
        Action parse = () => parsedMix = JsonDocument.Parse(mixJson!);
        parse.Should().NotThrow(
            "ServedDifficultyMix must be valid JSON; value: {0}", mixJson);

        var mix = JsonDocument.Parse(mixJson!).RootElement;

        // Required keys: Easy, Medium, Hard, Target, WasDefault
        TryProp(mix, "Easy",       out var easy).Should().BeTrue("'Easy' key must be present; json: {0}",       mixJson);
        TryProp(mix, "Medium",     out var medium).Should().BeTrue("'Medium' key must be present; json: {0}",   mixJson);
        TryProp(mix, "Hard",       out var hard).Should().BeTrue("'Hard' key must be present; json: {0}",       mixJson);
        TryProp(mix, "Target",     out var target).Should().BeTrue("'Target' key must be present; json: {0}",   mixJson);
        TryProp(mix, "WasDefault", out var wasDefault).Should().BeTrue("'WasDefault' must be present; json: {0}", mixJson);

        // Type assertions
        easy.ValueKind.Should().Be(JsonValueKind.Number,   "'Easy' must be a number; json: {0}", mixJson);
        medium.ValueKind.Should().Be(JsonValueKind.Number, "'Medium' must be a number; json: {0}", mixJson);
        hard.ValueKind.Should().Be(JsonValueKind.Number,   "'Hard' must be a number; json: {0}", mixJson);
        target.ValueKind.Should().Be(JsonValueKind.String, "'Target' must be a string; json: {0}", mixJson);
        wasDefault.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.True, JsonValueKind.False },
            "'WasDefault' must be a boolean; json: {0}", mixJson);

        // Non-negative counts
        easy.GetInt32().Should().BeGreaterThanOrEqualTo(0,   "'Easy' count must be >= 0; json: {0}",   mixJson);
        medium.GetInt32().Should().BeGreaterThanOrEqualTo(0, "'Medium' count must be >= 0; json: {0}", mixJson);
        hard.GetInt32().Should().BeGreaterThanOrEqualTo(0,   "'Hard' count must be >= 0; json: {0}",   mixJson);

        // Target is one of the valid difficulty strings
        target.GetString().Should().BeOneOf("Easy", "Medium", "Hard",
            "'Target' must be a valid DifficultyLevel name; json: {0}", mixJson);

        // TargetDifficulty column must also be populated
        targetDifficulty.Should().NotBeNull(
            "Attempt.TargetDifficulty column must be non-null after StartAttempt (AC4)");
        var validDifficultyInts = new[] { (int)DifficultyLevel.Easy, (int)DifficultyLevel.Medium, (int)DifficultyLevel.Hard };
        validDifficultyInts.Should().Contain(targetDifficulty!.Value,
            "Attempt.TargetDifficulty must be a valid DifficultyLevel int value; json: {0}", mixJson);

        // Total count in the mix must match the number of questions returned
        var totalInMix = easy.GetInt32() + medium.GetInt32() + hard.GetInt32();
        totalInMix.Should().BeGreaterThan(0,
            "Total question count in ServedDifficultyMix must be > 0 (pool was seeded); json: {0}", mixJson);

        // Security: CorrectAnswer must not be in the StartAttempt response (pre-existing guard)
        body.ToLowerInvariant().Should().NotContain("\"correctanswer\"",
            "CorrectAnswer must never be exposed in StartAttempt response");
    }
}

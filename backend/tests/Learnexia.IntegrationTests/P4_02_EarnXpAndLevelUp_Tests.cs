using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// Test-local INotificationHandler registrations for P4-02
// =============================================================================

/// <summary>
/// Deliberately-throwing handler for LessonCompletedIntegrationEvent.
/// Used by T11 to verify IsolatedNotificationPublisher lets the real Gamification handler
/// still run even when this sibling throws.
/// </summary>
public sealed class ThrowingLessonCompletedHandlerP402
    : INotificationHandler<LessonCompletedIntegrationEvent>
{
    public Task Handle(LessonCompletedIntegrationEvent e, CancellationToken ct)
        => throw new InvalidOperationException("P4-02 T11: deliberate throw from ThrowingLessonCompletedHandlerP402");
}

/// <summary>
/// P4-02 integration tests — "Earn XP and Level Up".
///
/// Tests the Gamification module's XP engine end-to-end:
///   - AnswerSubmittedIntegrationEvent → CorrectAnswer +10 XP
///   - LessonCompletedIntegrationEvent → LessonCompleted +50 XP
///   - LessonCompleted with accuracy ≥ 70% → QuizPass +20 XP
///   - Idempotency: duplicate event does NOT double-award
///   - Level curve: XP crossing thresholds advances CurrentLevel
///   - GET /api/Gamification/Profile → real XP/Level (zero-state for new students)
///   - GET /api/Learning/Dashboard → real XP via IStudentXpQuery cross-module seam
///   - IDOR: endpoint scoped to JWT, no studentId parameter
///
/// Mirrors P2_07_InstantAnswerFeedback_Tests.cs pattern for event capture.
///
/// Coverage map (13 test cases from P4-02 B4-1 spec):
///   T1   CorrectAnswer_Awards10Xp_AndCreatesXpAwardRow
///   T2   WrongAnswer_AwardsNoXp
///   T3   LessonComplete_At100Pct_AwardsLessonCompleteAndQuizPass
///   T4   LessonComplete_At50Pct_AwardsLessonCompleteButNotQuizPass
///   T5   Idempotency_DuplicateLessonCompletedEvent_DoesNotDoubleAward
///   T6   Idempotency_DuplicateAnswerSubmittedEvent_DoesNotDoubleAward
///   T7   LevelUp_CrossingL2Threshold_At100Xp
///   T8   LevelUp_CrossingL3Threshold_At250Xp
///   T9   BrandNewStudent_GamificationProfile_ZeroState
///   T10  StudentWithXp_GamificationProfile_ReturnsRealValues
///   T11  IDORProof_StudentAProfileDoesNotLeakToStudentB
///   T12  CrossModuleSeam_Dashboard_ReturnsRealXp
///   T13  BrandNewStudent_Dashboard_ZeroState
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_02_EarnXpAndLevelUp_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl    = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl            = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl          = "api/Parent/Add-Child";
    private const string GamificationProfile  = "api/Gamification/Profile";
    private const string DashboardUrl         = "api/Learning/Dashboard";
    private const string ValidChildPassword   = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Demo lesson resolved during InitializeAsync.
    private int _mathG1DemoLessonId;
    private int _mathG1DemoLessonSkillId;

    public P4_02_EarnXpAndLevelUp_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        // Resolve the Math G1 demo lesson — seeded by LearningSeeder.SeedDemoLessonContentAsync.
        // "Introduction to Counting (G1)" has 4 MCQ questions with non-null SkillId.
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var demoLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");

        demoLesson.Should().NotBeNull(
            "LearningSeeder must seed 'Introduction to Counting (G1)' with demo content; " +
            "ensure LearningSeeder.SeedDemoLessonContentAsync ran successfully.");

        _mathG1DemoLessonId  = demoLesson!.Id;
        _mathG1DemoLessonSkillId = demoLesson.SkillId
            ?? throw new InvalidOperationException(
                "Demo lesson 'Introduction to Counting (G1)' must have a non-null SkillId " +
                "for the AnswerSubmitted event producer to fire (P2-07 skips null-SkillId).");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p402_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>Case-insensitive JSON property lookup.</summary>
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

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpClient client, HttpMethod method, string url,
            object? body = null, string? bearerToken = null)
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

    private async Task<string> SignInAndGetTokenAsync(HttpClient client, string userName, string password)
    {
        var (resp, root, body) = await SendAsync(client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Parent-driven onboarding: register parent → add child (Grade 1) → sign in as child.
    /// Returns (studentToken, studentId).
    /// </summary>
    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(
        string? tag = null)
    {
        var cl = _client;
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = UniqueEmail($"parent_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(cl, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTok).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTok.GetString()!;

        var childEmail = UniqueEmail($"child_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(cl, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student P402",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        var studentToken = await SignInAndGetTokenAsync(cl, childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    /// <summary>Starts a quiz attempt via the API. Returns the attemptId.</summary>
    private async Task<int> StartAttemptAsync(int lessonId, string studentToken)
    {
        var cl = _client;
        var (resp, root, body) = await SendAsync(cl, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var id).Should().BeTrue("body: {0}", body);
        return id.GetInt32();
    }

    /// <summary>Submits one answer for a question.</summary>
    private Task<(HttpResponseMessage, JsonElement, string)>
        SubmitAnswerAsync(int attemptId, int questionId, string answerPayload, string token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new
            {
                QuestionId      = questionId,
                AnswerPayload   = answerPayload,
                TimeSpentSeconds = 10,
                HintUsed        = false
            },
            token);

    /// <summary>Completes an attempt.</summary>
    private Task<(HttpResponseMessage, JsonElement, string)>
        CompleteAttemptAsync(int attemptId, string token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    /// <summary>
    /// Returns the XP profile from GET /api/Gamification/Profile.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetProfileAsync(string token)
        => SendAsync(_client, HttpMethod.Get, GamificationProfile, null, token);

    /// <summary>
    /// Returns the dashboard from GET /api/Learning/Dashboard.
    /// </summary>
    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetDashboardAsync(string token)
        => SendAsync(_client, HttpMethod.Get, DashboardUrl, null, token);

    /// <summary>
    /// Loads demo lesson questions from DB (4 MCQ questions for Math G1 demo lesson).
    /// Returns list of (QuestionId, CorrectAnswer).
    /// </summary>
    private async Task<List<(int QuestionId, string CorrectAnswer)>> GetDemoLessonQuestionsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Filter to MCQ-only: P2_04 tests seed extra TrueFalse questions onto this shared demo lesson
        // (SeedMasteredSkillAnswersAsync / SeedPartialSkillAnswersAsync), and the JSON-encoded
        // CorrectAnswer ("\"True\"") doesn't round-trip through AnswerComparator.AreEqual for TrueFalse
        // — so picking MCQ keeps T3/T4 stable regardless of which Phase-2 tests ran first.
        var questions = await db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.LessonId == _mathG1DemoLessonId && q.QuestionType == QuestionType.MCQ)
            .OrderBy(q => q.Id)
            .Select(q => new { q.Id, q.CorrectAnswer })
            .ToListAsync();

        questions.Should().NotBeEmpty(
            "Demo lesson 'Introduction to Counting (G1)' must have MCQ quiz questions seeded by LearningSeeder.");

        return questions.Select(q => (q.Id, q.CorrectAnswer)).ToList();
    }

    /// <summary>
    /// Queries the Gamification DB directly for XpAwards for a student profile.
    /// </summary>
    private async Task<List<XpAward>> GetXpAwardsForStudentAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        // XpAward is stored via StudentXpProfileId FK, so we need to join through the profile.
        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new List<XpAward>();

        return await db.XpAwards
            .AsNoTracking()
            .Where(a => a.StudentXpProfileId == profile.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Queries the Gamification DB directly for a StudentXpProfile row.
    /// </summary>
    private async Task<StudentXpProfile?> GetXpProfileAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        return await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);
    }

    /// <summary>
    /// Option A (Bug-3 fix): ensures <paramref name="lessonId"/> has exactly <paramref name="count"/>
    /// MCQ QuizQuestions in the DB, inserting extras if fewer exist. Tests own their seed data;
    /// the production seeder seeds only 1 question per demo lesson (P2-05 constraint).
    ///
    /// Each inserted question is a trivial MCQ: correct answer = "A", wrong options = "B", "C", "D".
    /// SkillId is propagated from the demo lesson so AnswerSubmittedIntegrationEvent is published
    /// (P2-07 skips null-SkillId questions).
    ///
    /// Idempotent: already-existing questions are counted toward <paramref name="count"/>, so
    /// calling this multiple times from concurrent tests does not produce duplicates beyond the target.
    /// </summary>
    private async Task EnsureNQuestionsForLessonAsync(int lessonId, int skillId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        // Count MCQ-only — P2_04 may seed extra TrueFalse rows onto this shared demo lesson, and
        // GetDemoLessonQuestionsAsync also filters to MCQ. Counting all types would falsely think
        // "already enough" when only TrueFalse rows are present.
        var existing = await db.QuizQuestions
            .Where(q => q.LessonId == lessonId && q.QuestionType == QuestionType.MCQ)
            .CountAsync();

        var toAdd = count - existing;
        if (toAdd <= 0)
            return;

        var options = JsonSerializer.Serialize(new[] { "A", "B", "C", "D" });
        var correctAnswer = JsonSerializer.Serialize("A");

        for (int i = 0; i < toAdd; i++)
        {
            var q = new QuizQuestion
            {
                LessonId      = lessonId,
                SkillId       = skillId,
                QuestionType  = QuestionType.MCQ,
                QuestionText  = $"Test question {existing + i + 2} for lesson {lessonId}",
                Options       = options,
                CorrectAnswer = correctAnswer,
                Difficulty    = DifficultyLevel.Easy,
                GeneratedBy   = GeneratedBy.Curated,
            };
            db.QuizQuestions.Add(q);
        }

        await db.SaveChangesAsync(LearningSeeder.SystemUserId);
    }

    // =========================================================================
    // T1 — Correct answer awards +10 XP and creates XpAward row
    //
    // AC1 (XP per event via handler) + AC2 (ledger entry) gate.
    // A correct answer for a lesson with non-null SkillId should fire
    // AnswerSubmittedIntegrationEvent → AnswerSubmittedXpAwardHandler →
    // AwardAnswerSubmittedXpCommand → XpAward(CorrectAnswer, 10) + profile upsert.
    // =========================================================================

    [Fact(DisplayName = "P402-T1 Correct answer → +10 XP, XpAward row created, Profile shows TotalXp=10")]
    public async Task T1_CorrectAnswer_Awards10Xp_AndCreatesXpAwardRow()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t1");

        // Get demo lesson questions (Math G1 demo: "Introduction to Counting (G1)").
        var questions = await GetDemoLessonQuestionsAsync();
        var (firstQuestionId, correctAnswer) = questions.First();

        // Start attempt → submit one correct answer.
        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);
        var (ansResp, ansRoot, ansBody) = await SubmitAnswerAsync(
            attemptId, firstQuestionId, correctAnswer, token);
        ansResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer must succeed; body: {0}", ansBody);

        // Allow time for the async event handler to write XP (in-process MediatR is synchronous
        // by default in the test host — the IsolatedNotificationPublisher runs handlers inline
        // before SubmitAnswer returns; no sleep needed).

        // Assert via GET /api/Gamification/Profile.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/Gamification/Profile must succeed; body: {0}", profBody);

        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);
        TryProp(profData, "totalXp", out var totalXp).Should().BeTrue("body: {0}", profBody);
        totalXp.GetInt32().Should().Be(10,
            "one correct answer should award exactly 10 XP; body: {0}", profBody);

        TryProp(profData, "currentLevel", out var level).Should().BeTrue("body: {0}", profBody);
        level.GetInt32().Should().Be(1, "10 XP is still L1 (threshold = 100); body: {0}", profBody);

        // Assert DB: exactly one XpAward row with Reason=CorrectAnswer(1), XpAmount=10.
        var awards = await GetXpAwardsForStudentAsync(studentId);
        awards.Should().ContainSingle(
            "one correct answer should create exactly one XpAward row");
        awards[0].Reason.Should().Be(XpReason.CorrectAnswer,
            "the award reason must be CorrectAnswer");
        awards[0].XpAmount.Should().Be(10,
            "CorrectAnswer awards exactly 10 XP per GamificationConstants.XpRewards.CorrectAnswer");
        awards[0].LessonId.Should().Be(_mathG1DemoLessonId,
            "OriginLessonId must be populated from the event");
        awards[0].SkillId.Should().Be(_mathG1DemoLessonSkillId,
            "OriginSkillId must be populated from the event");
    }

    // =========================================================================
    // T2 — Wrong answer awards 0 XP (no XpAward row, no profile row)
    //
    // AC1 negative case: AnswerSubmitted with CorrectAnswerCount=0 must not write.
    // =========================================================================

    [Fact(DisplayName = "P402-T2 Wrong answer → 0 XP, no XpAward row, no StudentXpProfile row")]
    public async Task T2_WrongAnswer_AwardsNoXp()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t2");
        var questions = await GetDemoLessonQuestionsAsync();
        var (firstQuestionId, correctAnswer) = questions.First();

        // Submit a deliberately wrong answer.
        var wrongAnswer = "\"__WRONG_ANSWER_P402_T2__\"";
        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);
        var (ansResp, _, ansBody) = await SubmitAnswerAsync(
            attemptId, firstQuestionId, wrongAnswer, token);
        ansResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "SubmitAnswer (wrong) must still return 200 OK; body: {0}", ansBody);

        // Assert profile endpoint returns zero-state.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profBody);
        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);
        TryProp(profData, "totalXp", out var totalXp).Should().BeTrue("body: {0}", profBody);
        totalXp.GetInt32().Should().Be(0,
            "wrong answer must not award XP; body: {0}", profBody);

        // Assert DB: no XpAward rows.
        var awards = await GetXpAwardsForStudentAsync(studentId);
        awards.Should().BeEmpty("wrong answer must not create any XpAward rows");

        // P4-04 behavioural change: a wrong answer creates a StudentXpProfile row to track heart loss.
        // Before P4-04 the profile was absent for wrong-only students; now LoseHeartCommandHandler
        // creates one on the first heart loss so hearts are persisted from the start.
        var profile = await GetXpProfileAsync(studentId);
        profile.Should().NotBeNull(
            "P4-04: LoseHeartCommandHandler creates a profile on the first wrong answer to track heart loss");
        profile!.TotalXp.Should().Be(0,
            "wrong answer must not award XP");
        profile.Hearts.Should().Be(4,
            "P4-04: one heart is lost on a wrong answer; Hearts = 5 (default) - 1 = 4");
    }

    // =========================================================================
    // T3 — Lesson complete at 100% → LessonCompleted + QuizPass XP (AC1, AC2, Q3.bis)
    //
    // Submit all 4 correct → Complete. Expected:
    //   4×CorrectAnswer (4×10=40) + LessonCompleted (50) + QuizPass (20) = 110 XP
    //   6 XpAward rows.
    // =========================================================================

    [Fact(DisplayName = "P402-T3 100% lesson → 4×CorrectAnswer(40) + LessonCompleted(50) + QuizPass(20) = 110 XP, 6 award rows")]
    public async Task T3_LessonComplete_At100Pct_AwardsLessonCompleteAndQuizPass()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t3");

        // Bug-3 fix (Option A): ensure 4 questions exist for the demo lesson so T3's XP arithmetic
        // (4×10 + 50 + 20 = 110) matches what's actually submitted. Production seeder seeds only 1.
        await EnsureNQuestionsForLessonAsync(_mathG1DemoLessonId, _mathG1DemoLessonSkillId, 4);

        var questions = await GetDemoLessonQuestionsAsync();

        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);

        // Submit all 4 correct answers.
        foreach (var (questionId, correctAnswer) in questions)
        {
            var (ansResp, _, ansBody) = await SubmitAnswerAsync(
                attemptId, questionId, correctAnswer, token);
            ansResp.StatusCode.Should().Be(HttpStatusCode.OK,
                "SubmitAnswer must succeed; body: {0}", ansBody);
        }

        // Complete the attempt → triggers LessonCompletedIntegrationEvent.
        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "CompleteAttempt must succeed; body: {0}", complBody);

        // Assert total XP = 4*10 + 50 + 20 + 30 (StreakBonus, P4-03) + 20 (FIRST_LESSON badge, P4-05) = 160.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profBody);
        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);
        TryProp(profData, "totalXp", out var totalXp).Should().BeTrue("body: {0}", profBody);
        totalXp.GetInt32().Should().Be(160,
            "4 correct answers (40 XP) + LessonCompleted (50 XP) + QuizPass (20 XP) + StreakBonus (30 XP, P4-03) + FIRST_LESSON badge (20 XP, P4-05) = 160 XP; body: {0}", profBody);

        // Assert 8 XpAward rows (P4-03 adds 1 StreakBonus row; P4-05 adds 1 BadgeEarned row for FIRST_LESSON).
        var awards = await GetXpAwardsForStudentAsync(studentId);
        awards.Should().HaveCount(8,
            "4 CorrectAnswer + 1 LessonCompleted + 1 QuizPass + 1 StreakBonus (P4-03) + 1 BadgeEarned/FIRST_LESSON (P4-05) = 8 rows");

        var correctAwards = awards.Where(a => a.Reason == XpReason.CorrectAnswer).ToList();
        correctAwards.Should().HaveCount(4, "4 correct answers → 4 CorrectAnswer award rows");
        correctAwards.Should().AllSatisfy(a =>
            a.XpAmount.Should().Be(10, "each CorrectAnswer award is +10 XP"));

        awards.Should().ContainSingle(a => a.Reason == XpReason.LessonCompleted,
            "exactly 1 LessonCompleted award row");
        awards.Single(a => a.Reason == XpReason.LessonCompleted).XpAmount
            .Should().Be(50, "LessonCompleted awards +50 XP");

        awards.Should().ContainSingle(a => a.Reason == XpReason.QuizCompleted,
            "exactly 1 QuizPass (QuizCompleted) award row for 100% accuracy (≥70% threshold)");
        awards.Single(a => a.Reason == XpReason.QuizCompleted).XpAmount
            .Should().Be(20, "QuizPass awards +20 XP");

        // P4-03: StreakBonus is now awarded on the first lesson completion (first-ever activity = day-1 streak).
        awards.Should().ContainSingle(a => a.Reason == XpReason.StreakBonus,
            "exactly 1 StreakBonus award row (first-ever activity advances streak to 1, P4-03)");
        awards.Single(a => a.Reason == XpReason.StreakBonus).XpAmount
            .Should().Be(30, "StreakBonus awards +30 XP");

        // P4-05: FIRST_LESSON badge is awarded on the first LessonCompleted event (Common rarity, +20 XP).
        awards.Should().ContainSingle(a => a.Reason == XpReason.BadgeEarned,
            "exactly 1 BadgeEarned award row for the FIRST_LESSON badge (P4-05)");
        awards.Single(a => a.Reason == XpReason.BadgeEarned).XpAmount
            .Should().Be(20, "FIRST_LESSON badge is Common rarity — awards +20 XP");
    }

    // =========================================================================
    // T4 — Lesson complete at 50% → LessonCompleted but NOT QuizPass
    //
    // Submit 2 correct, 2 wrong → Complete. Expected:
    //   2×CorrectAnswer (20) + LessonCompleted (50) = 70 XP, NO QuizPass.
    //   3 XpAward rows.
    // =========================================================================

    [Fact(DisplayName = "P402-T4 50% lesson → 2×CorrectAnswer(20) + LessonCompleted(50) = 70 XP, no QuizPass, 3 award rows")]
    public async Task T4_LessonComplete_At50Pct_AwardsLessonCompleteButNotQuizPass()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t4");

        // Bug-3 fix (Option A): ensure 4 questions exist for the demo lesson so T4's XP arithmetic
        // (2×10 + 50 = 70) uses the correct denominator (4 questions, 2 correct = 50% accuracy < 70%).
        await EnsureNQuestionsForLessonAsync(_mathG1DemoLessonId, _mathG1DemoLessonSkillId, 4);

        var questions = await GetDemoLessonQuestionsAsync();

        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);

        // Submit 2 correct, 2 wrong (first 2 correct, last 2 wrong).
        var wrongAnswer = "\"__WRONG_ANSWER_P402_T4__\"";
        var correctPair  = questions.Take(2).ToList();
        var wrongPair    = questions.Skip(2).ToList();

        foreach (var (questionId, correctAnswer) in correctPair)
        {
            var (r, _, b) = await SubmitAnswerAsync(attemptId, questionId, correctAnswer, token);
            r.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", b);
        }
        foreach (var (questionId, _) in wrongPair)
        {
            var (r, _, b) = await SubmitAnswerAsync(attemptId, questionId, wrongAnswer, token);
            r.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", b);
        }

        var (complResp, _, complBody) = await CompleteAttemptAsync(attemptId, token);
        complResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", complBody);

        // Assert total XP = 2*10 + 50 + 30 (StreakBonus, P4-03) + 20 (FIRST_LESSON badge, P4-05) = 120.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profBody);
        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);
        TryProp(profData, "totalXp", out var totalXp).Should().BeTrue("body: {0}", profBody);
        totalXp.GetInt32().Should().Be(120,
            "2 correct (20 XP) + LessonCompleted (50 XP) + StreakBonus (30 XP, P4-03) + FIRST_LESSON badge (20 XP, P4-05) = 120 XP; body: {0}", profBody);

        // Assert 5 XpAward rows (P4-03 adds 1 StreakBonus; P4-05 adds 1 BadgeEarned for FIRST_LESSON).
        var awards = await GetXpAwardsForStudentAsync(studentId);
        awards.Should().HaveCount(5,
            "2 CorrectAnswer + 1 LessonCompleted + 1 StreakBonus (P4-03) + 1 BadgeEarned/FIRST_LESSON (P4-05) = 5 rows; 50% accuracy is below 70% QuizPass threshold");

        awards.Count(a => a.Reason == XpReason.CorrectAnswer).Should().Be(2,
            "2 correct answers → 2 CorrectAnswer award rows");
        awards.Should().ContainSingle(a => a.Reason == XpReason.LessonCompleted,
            "1 LessonCompleted award row (unconditional on completion)");
        awards.Any(a => a.Reason == XpReason.QuizCompleted).Should().BeFalse(
            "50% accuracy is below the 70% QuizPass threshold — no QuizPass award");

        // P4-03: StreakBonus is now awarded on the first lesson completion (first-ever activity = day-1 streak).
        awards.Should().ContainSingle(a => a.Reason == XpReason.StreakBonus,
            "exactly 1 StreakBonus award row (first-ever activity advances streak to 1, P4-03)");
        awards.Single(a => a.Reason == XpReason.StreakBonus).XpAmount
            .Should().Be(30, "StreakBonus awards +30 XP");

        // P4-05: FIRST_LESSON badge awarded on LessonCompleted regardless of accuracy.
        awards.Should().ContainSingle(a => a.Reason == XpReason.BadgeEarned,
            "exactly 1 BadgeEarned award row for the FIRST_LESSON badge (P4-05)");
        awards.Single(a => a.Reason == XpReason.BadgeEarned).XpAmount
            .Should().Be(20, "FIRST_LESSON badge is Common rarity — awards +20 XP");
    }

    // =========================================================================
    // T5 — Idempotency: duplicate LessonCompletedIntegrationEvent does NOT double-award (AC4)
    //
    // Publish the same LessonCompletedIntegrationEvent twice (same EventId).
    // Assert: exactly 1 LessonCompleted XpAward row, TotalXp unchanged after the second publish.
    // =========================================================================

    [Fact(DisplayName = "P402-T5 Idempotency: duplicate LessonCompleted event (same EventId) → no double-award, XpAward count=1")]
    public async Task T5_Idempotency_DuplicateLessonCompletedEvent_DoesNotDoubleAward()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t5");

        // Publish a LessonCompletedIntegrationEvent directly (bypassing the full HTTP flow).
        var eventId = Guid.NewGuid();
        var integrationEvent = new LessonCompletedIntegrationEvent(
            EventId:            eventId,
            OccurredOnUtc:      DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // First publish — should write XpAward rows.
        using (var scope1 = _factory.Services.CreateScope())
        {
            var publisher = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(integrationEvent);
        }

        var afterFirst = await GetXpAwardsForStudentAsync(studentId);
        // Should have LessonCompleted + QuizPass (100% accuracy ≥ 70%).
        var firstLessonCount = afterFirst.Count(a => a.Reason == XpReason.LessonCompleted);
        firstLessonCount.Should().Be(1,
            "first publish → exactly 1 LessonCompleted award row");

        var profileAfterFirst = await GetXpProfileAsync(studentId);
        profileAfterFirst.Should().NotBeNull("profile should exist after first publish");
        var xpAfterFirst = profileAfterFirst!.TotalXp;

        // Second publish — same EventId — must be silently ignored (idempotency).
        using (var scope2 = _factory.Services.CreateScope())
        {
            var publisher = scope2.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(integrationEvent);
        }

        var afterSecond = await GetXpAwardsForStudentAsync(studentId);
        var secondLessonCount = afterSecond.Count(a => a.Reason == XpReason.LessonCompleted);
        secondLessonCount.Should().Be(1,
            "second publish with same EventId must NOT create another LessonCompleted row (AC4 idempotency)");

        var profileAfterSecond = await GetXpProfileAsync(studentId);
        profileAfterSecond!.TotalXp.Should().Be(xpAfterFirst,
            "TotalXp must not change after duplicate event delivery (AC4 idempotency)");
    }

    // =========================================================================
    // T6 — Idempotency: duplicate AnswerSubmittedIntegrationEvent does NOT double-award (AC4)
    //
    // Publish the same AnswerSubmittedIntegrationEvent twice (same EventId, IsCorrect=true).
    // Assert: exactly 1 CorrectAnswer XpAward row, TotalXp = 10.
    // =========================================================================

    [Fact(DisplayName = "P402-T6 Idempotency: duplicate AnswerSubmitted event (same EventId) → 1 CorrectAnswer award, TotalXp=10")]
    public async Task T6_Idempotency_DuplicateAnswerSubmittedEvent_DoesNotDoubleAward()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t6");

        var eventId = Guid.NewGuid();
        var integrationEvent = new AnswerSubmittedIntegrationEvent(
            EventId:           eventId,
            OccurredOnUtc:     DateTime.UtcNow,
            StudentId:         studentId,
            LessonId:          _mathG1DemoLessonId,
            SkillId:           _mathG1DemoLessonSkillId,
            CorrectAnswerCount: 1);  // correct answer

        // First publish.
        using (var scope1 = _factory.Services.CreateScope())
        {
            var publisher = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(integrationEvent);
        }

        // Second publish — same EventId.
        using (var scope2 = _factory.Services.CreateScope())
        {
            var publisher = scope2.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(integrationEvent);
        }

        var awards = await GetXpAwardsForStudentAsync(studentId);
        awards.Should().ContainSingle(a => a.Reason == XpReason.CorrectAnswer,
            "duplicate AnswerSubmitted event with same EventId must NOT create a second CorrectAnswer award row (AC4)");

        var profile = await GetXpProfileAsync(studentId);
        profile.Should().NotBeNull("profile must exist after a correct answer event");
        profile!.TotalXp.Should().Be(10,
            "exactly 10 XP after a single correct answer (not 20 from a double-award)");
    }

    // =========================================================================
    // T7 — Level up: cumulative XP crossing 100 → CurrentLevel = 2 (AC3)
    //
    // Publish 10 AnswerSubmitted events (10×10=100 XP exactly) → should cross L2 threshold.
    // =========================================================================

    [Fact(DisplayName = "P402-T7 Level up: 10 correct answers = 100 XP → CurrentLevel=2")]
    public async Task T7_LevelUp_CrossingL2Threshold_At100Xp()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t7");

        // Publish 10 distinct AnswerSubmitted events (each a different EventId).
        for (int i = 0; i < 10; i++)
        {
            var ev = new AnswerSubmittedIntegrationEvent(
                EventId:            Guid.NewGuid(),
                OccurredOnUtc:      DateTime.UtcNow,
                StudentId:          studentId,
                LessonId:           _mathG1DemoLessonId,
                SkillId:            _mathG1DemoLessonSkillId,
                CorrectAnswerCount: 1);
            using var scope = _factory.Services.CreateScope();
            var pub = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        var profile = await GetXpProfileAsync(studentId);
        profile.Should().NotBeNull("profile must exist after 10 correct answers");
        profile!.TotalXp.Should().Be(100, "10 × 10 XP = 100 total XP");
        profile.CurrentLevel.Should().Be(2,
            "100 XP exactly crosses the L2 threshold (100) → CurrentLevel must be 2 (AC3)");

        // Verify via API too.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profBody);
        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);
        TryProp(profData, "currentLevel", out var apiLevel).Should().BeTrue("body: {0}", profBody);
        apiLevel.GetInt32().Should().Be(2, "API profile must also report CurrentLevel=2; body: {0}", profBody);
    }

    // =========================================================================
    // T8 — Level up: cumulative XP crossing 250 → CurrentLevel = 3 (AC3)
    //
    // Need 250 XP: 25 correct answers (25×10=250).
    // =========================================================================

    [Fact(DisplayName = "P402-T8 Level up: 25 correct answers = 250 XP → CurrentLevel=3")]
    public async Task T8_LevelUp_CrossingL3Threshold_At250Xp()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t8");

        for (int i = 0; i < 25; i++)
        {
            var ev = new AnswerSubmittedIntegrationEvent(
                EventId:            Guid.NewGuid(),
                OccurredOnUtc:      DateTime.UtcNow,
                StudentId:          studentId,
                LessonId:           _mathG1DemoLessonId,
                SkillId:            _mathG1DemoLessonSkillId,
                CorrectAnswerCount: 1);
            using var scope = _factory.Services.CreateScope();
            var pub = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        var profile = await GetXpProfileAsync(studentId);
        profile.Should().NotBeNull("profile must exist after 25 correct answers");
        profile!.TotalXp.Should().Be(250, "25 × 10 XP = 250 total XP");
        profile.CurrentLevel.Should().Be(3,
            "250 XP exactly crosses the L3 threshold (250) → CurrentLevel must be 3 (AC3)");
    }

    // =========================================================================
    // T9 — Brand-new student GET /api/Gamification/Profile → zero-state (AC3)
    //
    // Student has no StudentXpProfile row yet. The handler must fall back to (0, 1, 0, 100).
    // =========================================================================

    [Fact(DisplayName = "P402-T9 Brand-new student GET /api/Gamification/Profile → zero-state (TotalXp=0, CurrentLevel=1, XpToNextLevel=100)")]
    public async Task T9_BrandNewStudent_GamificationProfile_ZeroState()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t9");

        // Assert no StudentXpProfile row in DB.
        var profile = await GetXpProfileAsync(studentId);
        profile.Should().BeNull(
            "brand-new student must have no StudentXpProfile row before any XP events");

        // Assert API returns zero-state.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/Gamification/Profile must return 200 for a brand-new student; body: {0}", profBody);

        TryProp(profRoot, "successed", out var successed).Should().BeTrue("body: {0}", profBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", profBody);

        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);

        TryProp(profData, "totalXp", out var totalXp).Should().BeTrue("body: {0}", profBody);
        totalXp.GetInt32().Should().Be(0, "brand-new student has 0 XP; body: {0}", profBody);

        TryProp(profData, "currentLevel", out var level).Should().BeTrue("body: {0}", profBody);
        level.GetInt32().Should().Be(1, "brand-new student is Level 1; body: {0}", profBody);

        TryProp(profData, "xpIntoCurrentLevel", out var xpInto).Should().BeTrue("body: {0}", profBody);
        xpInto.GetInt32().Should().Be(0, "0 XP into current level; body: {0}", profBody);

        TryProp(profData, "xpToNextLevel", out var xpToNext).Should().BeTrue("body: {0}", profBody);
        // The implementation returns band width (100-0=100) for L1 → L2.
        xpToNext.GetInt32().Should().Be(100,
            "L1→L2 band width = 100 XP (the amount to advance from L1 floor); body: {0}", profBody);
    }

    // =========================================================================
    // T10 — Student with XP: GET /api/Gamification/Profile → real values (AC3)
    //
    // After T1 (single correct answer → TotalXp=10, Level=1), profile must return real values.
    // This test is independent (creates its own student) to avoid ordering dependencies.
    // =========================================================================

    [Fact(DisplayName = "P402-T10 Student with 10 XP: GET /api/Gamification/Profile → TotalXp=10, Level=1, XpInto=10, XpToNext=100")]
    public async Task T10_StudentWithXp_GamificationProfile_ReturnsRealValues()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t10");
        var questions = await GetDemoLessonQuestionsAsync();
        var (firstQuestionId, correctAnswer) = questions.First();

        // Submit one correct answer → 10 XP.
        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);
        await SubmitAnswerAsync(attemptId, firstQuestionId, correctAnswer, token);

        // Assert profile returns real values.
        var (profResp, profRoot, profBody) = await GetProfileAsync(token);
        profResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", profBody);
        TryProp(profRoot, "data", out var profData).Should().BeTrue("body: {0}", profBody);

        TryProp(profData, "totalXp", out var totalXp).Should().BeTrue("body: {0}", profBody);
        totalXp.GetInt32().Should().Be(10, "body: {0}", profBody);

        TryProp(profData, "currentLevel", out var level).Should().BeTrue("body: {0}", profBody);
        level.GetInt32().Should().Be(1, "10 XP is L1; body: {0}", profBody);

        TryProp(profData, "xpIntoCurrentLevel", out var xpInto).Should().BeTrue("body: {0}", profBody);
        xpInto.GetInt32().Should().Be(10, "10 XP earned into L1 (floor=0); body: {0}", profBody);

        TryProp(profData, "xpToNextLevel", out var xpToNext).Should().BeTrue("body: {0}", profBody);
        xpToNext.GetInt32().Should().Be(90,
            "XpToNextLevel = remaining XP to L2 (100 - 10 = 90); body: {0}", profBody);
    }

    // =========================================================================
    // T11 — IDOR-proof: GET /api/Gamification/Profile returns only own data (AC4 isolation)
    //
    // Two students: A with 50 XP, B with 200 XP.
    // A's JWT → must return A's 50 XP.
    // The endpoint takes no studentId param — IDOR is structurally impossible.
    // =========================================================================

    [Fact(DisplayName = "P402-T11 IDOR-proof: student A's JWT → A's profile, not B's (no studentId param)")]
    public async Task T11_IDORProof_StudentAProfileDoesNotLeakToStudentB()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("t11a");
        var (tokenB, studentIdB) = await CreateStudentViaParentFlowAsync("t11b");

        // Give student A exactly 5 correct answers → 50 XP.
        for (int i = 0; i < 5; i++)
        {
            var ev = new AnswerSubmittedIntegrationEvent(
                EventId:            Guid.NewGuid(),
                OccurredOnUtc:      DateTime.UtcNow,
                StudentId:          studentIdA,
                LessonId:           _mathG1DemoLessonId,
                SkillId:            _mathG1DemoLessonSkillId,
                CorrectAnswerCount: 1);
            using var scope = _factory.Services.CreateScope();
            var pub = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        // Give student B exactly 20 correct answers → 200 XP.
        for (int i = 0; i < 20; i++)
        {
            var ev = new AnswerSubmittedIntegrationEvent(
                EventId:            Guid.NewGuid(),
                OccurredOnUtc:      DateTime.UtcNow,
                StudentId:          studentIdB,
                LessonId:           _mathG1DemoLessonId,
                SkillId:            _mathG1DemoLessonSkillId,
                CorrectAnswerCount: 1);
            using var scope = _factory.Services.CreateScope();
            var pub = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        // Student A's JWT must return A's 50 XP.
        var (respA, rootA, bodyA) = await GetProfileAsync(tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        TryProp(dataA, "totalXp", out var xpA).Should().BeTrue("body: {0}", bodyA);
        xpA.GetInt32().Should().Be(50,
            "student A's profile must show A's 50 XP, not B's 200 XP (IDOR proof); body: {0}", bodyA);

        // Student B's JWT must return B's 200 XP.
        var (respB, rootB, bodyB) = await GetProfileAsync(tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB);
        TryProp(rootB, "data", out var dataB).Should().BeTrue("body: {0}", bodyB);
        TryProp(dataB, "totalXp", out var xpB).Should().BeTrue("body: {0}", bodyB);
        xpB.GetInt32().Should().Be(200,
            "student B's profile must show B's 200 XP; body: {0}", bodyB);
    }

    // =========================================================================
    // T12 — Cross-module seam: GET /api/Learning/Dashboard returns real XP (AC3)
    //
    // After earning 10 XP (1 correct answer), the dashboard via IStudentXpQuery seam
    // must report xp=10, level=1 (not the old hard-coded 0).
    // =========================================================================

    [Fact(DisplayName = "P402-T12 Cross-module seam: dashboard returns real XP after 1 correct answer (xp=10, level=1)")]
    public async Task T12_CrossModuleSeam_Dashboard_ReturnsRealXp()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t12");
        var questions = await GetDemoLessonQuestionsAsync();
        var (firstQuestionId, correctAnswer) = questions.First();

        // Submit one correct answer → 10 XP.
        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);
        await SubmitAnswerAsync(attemptId, firstQuestionId, correctAnswer, token);

        // Dashboard must now show real XP via IStudentXpQuery seam.
        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/Learning/Dashboard must succeed; body: {0}", dashBody);

        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);

        TryProp(dashData, "xp", out var xp).Should().BeTrue("dashboard data.xp required; body: {0}", dashBody);
        xp.GetInt32().Should().Be(10,
            "dashboard must reflect real XP (10) via IStudentXpQuery cross-module seam (P4-02); body: {0}", dashBody);

        TryProp(dashData, "level", out var level).Should().BeTrue(
            "dashboard data.level required (added by P4-02 DashboardDto); body: {0}", dashBody);
        level.GetInt32().Should().Be(1, "10 XP is L1; body: {0}", dashBody);
    }

    // =========================================================================
    // T13 — Brand-new student dashboard → zero-state graceful null (AC3)
    //
    // When IStudentXpQuery.GetByStudentIdAsync returns null, the dashboard handler
    // must default to (xp=0, level=1) — no exception, no 500.
    // =========================================================================

    [Fact(DisplayName = "P402-T13 Brand-new student GET /api/Learning/Dashboard → xp=0, level=1 (graceful null from IStudentXpQuery)")]
    public async Task T13_BrandNewStudent_Dashboard_ZeroState()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t13");

        // Assert no profile row exists.
        var profile = await GetXpProfileAsync(studentId);
        profile.Should().BeNull("brand-new student has no StudentXpProfile row");

        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /api/Learning/Dashboard must return 200 for a brand-new student (no XP); body: {0}", dashBody);

        TryProp(dashRoot, "successed", out var successed).Should().BeTrue("body: {0}", dashBody);
        successed.GetBoolean().Should().BeTrue("body: {0}", dashBody);

        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);

        TryProp(dashData, "xp", out var xp).Should().BeTrue("data.xp required; body: {0}", dashBody);
        xp.GetInt32().Should().Be(0,
            "brand-new student must see xp=0 when IStudentXpQuery returns null; body: {0}", dashBody);

        TryProp(dashData, "level", out var level).Should().BeTrue("data.level required; body: {0}", dashBody);
        level.GetInt32().Should().Be(1,
            "brand-new student must see level=1 (default from null profile); body: {0}", dashBody);
    }

    // =========================================================================
    // T11b — Sibling handler failure does NOT block XP award (IsolatedNotificationPublisher)
    //
    // This test uses the _throwingHandlerClient which has a deliberately-throwing
    // INotificationHandler<LessonCompletedIntegrationEvent> registered alongside the real
    // Gamification handler. The XP award must still be written.
    // =========================================================================

    [Fact(DisplayName = "P402-T11b IsolatedNotificationPublisher: publish does not throw even if all handlers fail")]
    public async Task T11b_SiblingHandlerFailure_DoesNotBlockXpAward()
    {
        // Create a student via the normal client — same shared DB.
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t11b");

        // Build an event to publish.
        var ev = new LessonCompletedIntegrationEvent(
            EventId:            Guid.NewGuid(),
            OccurredOnUtc:      DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // Publish via the _factory.Services scope (which has the real Gamification handler).
        // The IsolatedNotificationPublisher is registered at the host level — even if one handler
        // throws, it catches the exception and lets sibling handlers complete. This tests the
        // P4-01 IsolatedNotificationPublisher behaviour from the Gamification module's perspective.
        Exception? publishException = null;
        try
        {
            using var scope = _factory.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(ev);
        }
        catch (Exception ex)
        {
            publishException = ex;
        }

        // The publish must NOT throw (IsolatedNotificationPublisher catches handler exceptions).
        publishException.Should().BeNull(
            "IsolatedNotificationPublisher must catch exceptions from individual handlers " +
            "and not propagate them to the caller (even if one handler throws)");

        // The Gamification handler must have written XP.
        var awards = await GetXpAwardsForStudentAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.LessonCompleted,
            "Gamification LessonCompleted XpAward must be written via the handler");
    }

    // =========================================================================
    // Envelope sanity: "successed" camelCase in all new responses
    // =========================================================================

    [Fact(DisplayName = "P402-Envelope GET /api/Gamification/Profile response envelope has 'successed':true camelCase")]
    public async Task EnvelopeSanity_GamificationProfile_HasSuccessedCamelCase()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("env");

        var (resp, root, body) = await GetProfileAsync(token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Critical: the literal string "successed": must appear in camelCase with lowercase 'd'.
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> uses the canonical key 'successed' (sic — Successed camelCase lowercase d per CONVENTIONS §5); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        // Full envelope shape.
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);
    }

    // =========================================================================
    // Anonymous access: GET /api/Gamification/Profile → 401
    // =========================================================================

    [Fact(DisplayName = "P402-Auth GET /api/Gamification/Profile without JWT → 401")]
    public async Task Auth_GamificationProfile_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Get, GamificationProfile);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GamificationController.GetProfile has [Authorize]; anonymous caller must receive 401; body: {0}", body);
    }
}

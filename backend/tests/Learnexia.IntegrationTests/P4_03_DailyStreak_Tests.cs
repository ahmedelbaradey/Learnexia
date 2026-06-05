using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
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

/// <summary>
/// P4-03 integration tests — "Maintain a Daily Streak".
///
/// Tests the streak engine end-to-end:
///   - LessonCompletedIntegrationEvent → AdvanceStreakCommand → streak advance + StreakBonus XP
///   - Same-day second lesson → no double-advance, no double StreakBonus
///   - Consecutive-day lesson → streak increments by 1
///   - Missed-day lesson → streak resets to 1, LongestStreak preserved
///   - Duplicate event (same EventId) → idempotent, no double-advance
///   - Out-of-order event (OccurredOnUtc in the past) → ignored
///   - StreakSweepJob.RunAsync → resets CurrentStreak=0 for stale profiles, leaves LongestStreak
///   - GET /api/Learning/Dashboard → returns real Streak from IStudentStreakQuery
///   - AnswerSubmittedIntegrationEvent does NOT advance streak (D2 locked decision)
///   - Envelope sanity: "successed" camelCase
///
/// Coverage map (15 test cases per P4-03 plan B4-1):
///   T1   FirstEverLesson_SetsStreakTo1_Awards30StreakBonus
///   T2   ConsecutiveDay_AdvancesStreakBy1
///   T3   SameDaySecondLesson_NoDoubleAdvance_NoDoubleBonus
///   T4   MissedDay_ResetsTo1_PreservesLongestStreak
///   T5   LongestStreakRecordPreservedAcrossResets
///   T6   Idempotency_DuplicateEventId_NoDoubleAdvance
///   T7   OutOfOrderEvent_Ignored
///   T8   BrandNewStudent_ZeroState_DashboardStreak0
///   T9   CrossStudentIsolation_StreaksSeparate
///   T10  SweepJob_BrokenStreak_ResetsTo0
///   T11  SweepJob_ActiveStreak_LeftAlone
///   T12  SweepJob_Idempotent_RunTwice
///   T13  StreakBonus_TriggersLevelUp
///   T14  AnswerSubmitted_DoesNotAdvanceStreak
///   T15  EnvelopeSanity_SuccessedLowercaseD
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_03_DailyStreak_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string DashboardUrl      = "api/Learning/Dashboard";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    // Demo lesson resolved during InitializeAsync.
    private int _mathG1DemoLessonId;
    private int _mathG1DemoLessonSkillId;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    public P4_03_DailyStreak_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var demoLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");

        demoLesson.Should().NotBeNull(
            "LearningSeeder must seed 'Introduction to Counting (G1)' with demo content.");

        _mathG1DemoLessonId      = demoLesson!.Id;
        _mathG1DemoLessonSkillId = demoLesson.SkillId
            ?? throw new InvalidOperationException(
                "Demo lesson must have a non-null SkillId for integration events to fire.");
    }

    public Task DisposeAsync()
    {
        // Reset the test clock after every test suite run so other suites are unaffected.
        _factory.TestClock.Reset();
        return Task.CompletedTask;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p403_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(
        string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = UniqueEmail($"parent_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTok).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTok.GetString()!;

        var childEmail = UniqueEmail($"child_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student P403",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "en", // P8-03: "en" so language guard passes for MATH/En demo lesson
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

    private async Task<int> StartAttemptAsync(int lessonId, string studentToken)
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{lessonId}/Attempt", null, studentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "StartAttempt must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "attemptId", out var id).Should().BeTrue("body: {0}", body);
        return id.GetInt32();
    }

    private Task<(HttpResponseMessage, JsonElement, string)>
        SubmitAnswerAsync(int attemptId, int questionId, string answerPayload, string token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Answers",
            new
            {
                QuestionId       = questionId,
                AnswerPayload    = answerPayload,
                TimeSpentSeconds = 10,
                HintUsed         = false
            },
            token);

    private Task<(HttpResponseMessage, JsonElement, string)>
        CompleteAttemptAsync(int attemptId, string token)
        => SendAsync(_client, HttpMethod.Post,
            $"api/Learning/Quizzes/{attemptId}/Complete", null, token);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetDashboardAsync(string token)
        => SendAsync(_client, HttpMethod.Get, DashboardUrl, null, token);

    private async Task<List<(int QuestionId, string CorrectAnswer)>> GetDemoLessonQuestionsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var questions = await db.QuizQuestions
            .AsNoTracking()
            .Where(q => q.LessonId == _mathG1DemoLessonId && q.QuestionType == QuestionType.MCQ)
            .OrderBy(q => q.Id)
            .Select(q => new { q.Id, q.CorrectAnswer })
            .ToListAsync();

        questions.Should().NotBeEmpty(
            "Demo lesson must have MCQ questions.");

        return questions.Select(q => (q.Id, q.CorrectAnswer)).ToList();
    }

    private async Task EnsureNQuestionsForLessonAsync(int lessonId, int skillId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var existing = await db.QuizQuestions
            .Where(q => q.LessonId == lessonId && q.QuestionType == QuestionType.MCQ)
            .CountAsync();

        var toAdd = count - existing;
        if (toAdd <= 0) return;

        var options = JsonSerializer.Serialize(new[] { "A", "B", "C", "D" });
        var correctAnswer = JsonSerializer.Serialize("A");

        for (int i = 0; i < toAdd; i++)
        {
            db.QuizQuestions.Add(new QuizQuestion
            {
                LessonId      = lessonId,
                SkillId       = skillId,
                QuestionType  = QuestionType.MCQ,
                QuestionText  = $"P403 Test Q {existing + i + 2}",
                Options       = options,
                CorrectAnswer = correctAnswer,
                Difficulty    = DifficultyLevel.Easy,
                GeneratedBy   = GeneratedBy.Curated,
            });
        }
        await db.SaveChangesAsync(LearningSeeder.SystemUserId);
    }

    /// <summary>
    /// Directly seeds / updates streak fields on a StudentXpProfile row via raw SQL.
    /// Required because CurrentStreak / LongestStreak / LastActivityDateUtc have <c>internal set</c>
    /// (the mutation invariant is enforced by domain methods; tests bypass this for setup only).
    /// </summary>
    private async Task SeedStreakProfileAsync(
        int studentId,
        int currentStreak,
        int longestStreak,
        DateOnly? lastActivityDateUtc,
        int totalXp = 0,
        int currentLevel = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null)
        {
            // Insert via raw SQL to avoid the internal-setter restriction
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO gamification.""StudentXpProfiles""
                   (""StudentId"", ""TotalXp"", ""CurrentLevel"", ""LastAwardAtUtc"",
                    ""CurrentStreak"", ""LongestStreak"", ""LastActivityDateUtc"",
                    ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {totalXp}, {currentLevel}, {DateTime.UtcNow},
                           {currentStreak}, {longestStreak}, {lastActivityDateUtc},
                           {DateTime.UtcNow}, {0})");
        }
        else
        {
            // Update via raw SQL
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE gamification.""StudentXpProfiles""
                   SET ""TotalXp"" = {totalXp},
                       ""CurrentLevel"" = {currentLevel},
                       ""CurrentStreak"" = {currentStreak},
                       ""LongestStreak"" = {longestStreak},
                       ""LastActivityDateUtc"" = {lastActivityDateUtc}
                   WHERE ""StudentId"" = {studentId}");
        }
    }

    private async Task<StudentXpProfile?> GetProfileAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        return await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);
    }

    private async Task<List<XpAward>> GetAwardsAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new();

        return await db.XpAwards
            .AsNoTracking()
            .Where(a => a.StudentXpProfileId == profile.Id)
            .ToListAsync();
    }

    private async Task PublishLessonCompletedEventAsync(
        int studentId,
        Guid eventId,
        DateTime occurredOnUtc,
        int lessonId,
        int skillId,
        int accuracyPct = 100,
        int correctCount = 4)
    {
        var ev = new LessonCompletedIntegrationEvent(
            EventId:            eventId,
            OccurredOnUtc:      occurredOnUtc,
            StudentId:          studentId,
            LessonId:           lessonId,
            SkillId:            skillId,
            AccuracyPercentage: accuracyPct,
            CorrectAnswerCount: correctCount);

        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(ev);
    }

    private async Task RunSweepJobAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StreakSweepJob>();
        await job.RunAsync(CancellationToken.None);
    }

    // =========================================================================
    // T1 — First-ever lesson sets streak=1, awards +30 StreakBonus XP (AC1+AC3)
    //
    // Brand-new student completes one lesson.
    // Assert: CurrentStreak=1, LongestStreak=1, LastActivityDateUtc=today,
    //         one StreakBonus XpAward row, Dashboard.Streak=1.
    // =========================================================================

    [Fact(DisplayName = "P403-T1 First-ever lesson → CurrentStreak=1, LongestStreak=1, StreakBonus XpAward +30")]
    public async Task T1_FirstEverLesson_SetsStreakTo1_Awards30StreakBonus()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t1");

        await EnsureNQuestionsForLessonAsync(_mathG1DemoLessonId, _mathG1DemoLessonSkillId, 4);
        var questions = await GetDemoLessonQuestionsAsync();

        var attemptId = await StartAttemptAsync(_mathG1DemoLessonId, token);
        foreach (var (qId, answer) in questions)
        {
            var (r, _, b) = await SubmitAnswerAsync(attemptId, qId, answer, token);
            r.StatusCode.Should().Be(HttpStatusCode.OK, "SubmitAnswer; body: {0}", b);
        }
        var (compResp, _, compBody) = await CompleteAttemptAsync(attemptId, token);
        compResp.StatusCode.Should().Be(HttpStatusCode.OK, "CompleteAttempt; body: {0}", compBody);

        // Assert streak profile
        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull("profile must be created on first lesson completion");
        profile!.CurrentStreak.Should().Be(1,
            "first-ever lesson must start the streak at 1");
        profile.LongestStreak.Should().Be(1,
            "LongestStreak must equal CurrentStreak on day-1");
        profile.LastActivityDateUtc.Should().Be(today,
            "LastActivityDateUtc must equal today's UTC date");

        // Assert StreakBonus XpAward
        var awards = await GetAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.StreakBonus,
            "StreakBonus XpAward must be written on first-ever lesson");
        awards.Single(a => a.Reason == XpReason.StreakBonus).XpAmount
            .Should().Be(30, "StreakBonus awards exactly +30 XP");

        // Assert Dashboard shows streak=1
        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);
        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);
        TryProp(dashData, "streak", out var streakVal).Should().BeTrue(
            "dashboard data.streak required; body: {0}", dashBody);
        streakVal.GetInt32().Should().Be(1,
            "dashboard must reflect the real streak from IStudentStreakQuery; body: {0}", dashBody);

        // XP total: 4×10 + 50 (LessonCompleted) + 20 (QuizPass at 100%) + 30 (StreakBonus) + 20 (FIRST_LESSON badge, P4-05) = 160
        profile.TotalXp.Should().Be(160,
            "4×CorrectAnswer(40) + LessonCompleted(50) + QuizPass(20) + StreakBonus(30) + FIRST_LESSON badge(20, P4-05) = 160 XP");
    }

    // =========================================================================
    // T2 — Consecutive day: streak advances from 1 to 2 (AC1)
    //
    // Seed: CurrentStreak=1, LastActivityDateUtc=yesterday.
    // Action: publish LessonCompleted event with OccurredOnUtc=today.
    // Assert: CurrentStreak=2, one new StreakBonus row.
    // =========================================================================

    [Fact(DisplayName = "P403-T2 Consecutive-day lesson → streak advances from 1 to 2, new StreakBonus row")]
    public async Task T2_ConsecutiveDay_AdvancesStreakBy1()
    {
        var today     = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t2");

        // Seed yesterday's streak state
        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      1,
            longestStreak:      1,
            lastActivityDateUtc: yesterday,
            totalXp:            80);

        // Publish today's lesson completion
        var eventId = Guid.NewGuid();
        await PublishLessonCompletedEventAsync(
            studentId:   studentId,
            eventId:     eventId,
            occurredOnUtc: DateTime.UtcNow,
            lessonId:    _mathG1DemoLessonId,
            skillId:     _mathG1DemoLessonSkillId);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(2,
            "consecutive-day activity must advance streak from 1 to 2");
        profile.LongestStreak.Should().Be(2,
            "LongestStreak must update when CurrentStreak exceeds the previous record");
        profile.LastActivityDateUtc.Should().Be(today,
            "LastActivityDateUtc must be updated to today");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.StreakBonus && a.OriginEventId == eventId,
            "StreakBonus must be awarded for today's activity with the correct OriginEventId");
    }

    // =========================================================================
    // T3 — Same-day second lesson: no double-advance, no double StreakBonus (AC1 negative)
    //
    // Seed: CurrentStreak=1, LastActivityDateUtc=today.
    // Action: publish a fresh LessonCompleted event with a NEW EventId, same day.
    // Assert: CurrentStreak still 1, no new StreakBonus row.
    // =========================================================================

    [Fact(DisplayName = "P403-T3 Same-day second lesson → streak unchanged (1), no second StreakBonus row")]
    public async Task T3_SameDaySecondLesson_NoDoubleAdvance_NoDoubleBonus()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t3");

        // Seed same-day streak state (already did one lesson today)
        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      1,
            longestStreak:      1,
            lastActivityDateUtc: today,
            totalXp:            130);

        // Publish a SECOND lesson event on the same day (different EventId)
        var secondEventId = Guid.NewGuid();
        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       secondEventId,
            occurredOnUtc: DateTime.UtcNow,   // still today
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(1,
            "same-day second lesson must NOT advance the streak (only first lesson of the day earns the bonus)");
        profile.LastActivityDateUtc.Should().Be(today,
            "LastActivityDateUtc must remain today");

        // No StreakBonus row for the second event (the date-based same-day check blocks it)
        var awards = await GetAwardsAsync(studentId);
        awards.Should().NotContain(a => a.Reason == XpReason.StreakBonus && a.OriginEventId == secondEventId,
            "same-day second event must NOT create a new StreakBonus XpAward");
    }

    // =========================================================================
    // T4 — Missed day: streak resets to 1, LongestStreak preserved (AC2)
    //
    // Seed: CurrentStreak=5, LongestStreak=5, LastActivityDateUtc=3 days ago.
    // Action: publish LessonCompleted event with OccurredOnUtc=today.
    // Assert: CurrentStreak=1, LongestStreak=5 (preserved), new StreakBonus row.
    // =========================================================================

    [Fact(DisplayName = "P403-T4 Missed-day lesson → streak resets to 1, LongestStreak=5 preserved")]
    public async Task T4_MissedDay_ResetsTo1_PreservesLongestStreak()
    {
        var today          = DateOnly.FromDateTime(DateTime.UtcNow);
        var threeDaysAgo   = today.AddDays(-3);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t4");

        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      5,
            longestStreak:      5,
            lastActivityDateUtc: threeDaysAgo,
            totalXp:            200);

        var eventId = Guid.NewGuid();
        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       eventId,
            occurredOnUtc: DateTime.UtcNow,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(1,
            "after a gap >1 day, streak must reset to 1 (new activity is day-1 of a fresh streak)");
        profile.LongestStreak.Should().Be(5,
            "LongestStreak must be preserved when streak resets — it never decreases");
        profile.LastActivityDateUtc.Should().Be(today,
            "LastActivityDateUtc must be updated to today");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.StreakBonus && a.OriginEventId == eventId,
            "StreakBonus is awarded even on a reset day (day-1 of a new streak still earns the bonus)");
    }

    // =========================================================================
    // T5 — Longest streak record preserved across resets (AC2 + AC3)
    //
    // Pre: CurrentStreak=0, LongestStreak=7, LastActivityDateUtc=5 days ago.
    // Action: publish LessonCompleted today.
    // Assert: CurrentStreak=1, LongestStreak=7 (unchanged).
    // =========================================================================

    [Fact(DisplayName = "P403-T5 Comeback after long break → CurrentStreak=1, LongestStreak=7 preserved")]
    public async Task T5_LongestStreakRecordPreservedAcrossResets()
    {
        var today       = DateOnly.FromDateTime(DateTime.UtcNow);
        var fiveDaysAgo = today.AddDays(-5);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t5");

        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      0,
            longestStreak:      7,
            lastActivityDateUtc: fiveDaysAgo,
            totalXp:            350);

        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       Guid.NewGuid(),
            occurredOnUtc: DateTime.UtcNow,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(1,
            "current streak starts fresh at 1 after a long break");
        profile.LongestStreak.Should().Be(7,
            "all-time LongestStreak=7 must be preserved — it never decreases");
    }

    // =========================================================================
    // T6 — Idempotency: duplicate EventId does NOT double-advance (AC1 idempotency)
    //
    // Publish the same LessonCompletedIntegrationEvent twice (same EventId).
    // Assert: StreakBonus XpAward count = 1, CurrentStreak unchanged after second publish.
    // =========================================================================

    [Fact(DisplayName = "P403-T6 Duplicate LessonCompleted event (same EventId) → no double streak advance, no double StreakBonus")]
    public async Task T6_Idempotency_DuplicateEventId_NoDoubleAdvance()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t6");

        var eventId = Guid.NewGuid();
        var ev = new LessonCompletedIntegrationEvent(
            EventId:            eventId,
            OccurredOnUtc:      DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // First publish
        using (var scope1 = _factory.Services.CreateScope())
        {
            var pub = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        var profileAfterFirst = await GetProfileAsync(studentId);
        profileAfterFirst.Should().NotBeNull();
        var streakAfterFirst = profileAfterFirst!.CurrentStreak;
        streakAfterFirst.Should().Be(1, "first publish → streak=1");

        var awardsAfterFirst = await GetAwardsAsync(studentId);
        awardsAfterFirst.Count(a => a.Reason == XpReason.StreakBonus)
            .Should().Be(1, "exactly one StreakBonus row after first publish");

        // Second publish — same EventId
        using (var scope2 = _factory.Services.CreateScope())
        {
            var pub = scope2.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        var profileAfterSecond = await GetProfileAsync(studentId);
        profileAfterSecond!.CurrentStreak.Should().Be(streakAfterFirst,
            "duplicate event must not change CurrentStreak");

        var awardsAfterSecond = await GetAwardsAsync(studentId);
        awardsAfterSecond.Count(a => a.Reason == XpReason.StreakBonus)
            .Should().Be(1, "second publish with same EventId must NOT create a second StreakBonus row (idempotency)");
    }

    // =========================================================================
    // T7 — Out-of-order event: OccurredOnUtc in the past is ignored (AC1 out-of-order)
    //
    // Seed: CurrentStreak=1, LastActivityDateUtc=today.
    // Action: publish event with OccurredOnUtc=yesterday.
    // Assert: CurrentStreak still 1, no new StreakBonus row for that eventId.
    // =========================================================================

    [Fact(DisplayName = "P403-T7 Out-of-order event (OccurredOnUtc=yesterday, profile.LastActivityDate=today) → ignored, no streak change")]
    public async Task T7_OutOfOrderEvent_Ignored()
    {
        var today     = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t7");

        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      1,
            longestStreak:      1,
            lastActivityDateUtc: today,
            totalXp:            130);

        // Publish a stale event with OccurredOnUtc=yesterday
        var staleEventId = Guid.NewGuid();
        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       staleEventId,
            occurredOnUtc: new DateTime(yesterday.Year, yesterday.Month, yesterday.Day, 12, 0, 0, DateTimeKind.Utc),
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(1,
            "out-of-order event (before LastActivityDateUtc) must be ignored — streak unchanged");
        profile.LastActivityDateUtc.Should().Be(today,
            "LastActivityDateUtc must NOT be clobbered by a stale event");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().NotContain(a => a.Reason == XpReason.StreakBonus && a.OriginEventId == staleEventId,
            "out-of-order event must NOT produce a StreakBonus XpAward");
    }

    // =========================================================================
    // T8 — Brand-new student: Dashboard shows streak=0 (AC3 zero-state)
    //
    // No profile exists → IStudentStreakQuery returns null → dashboard falls back to 0.
    // =========================================================================

    [Fact(DisplayName = "P403-T8 Brand-new student: Dashboard streak=0 (no profile → IStudentStreakQuery returns null)")]
    public async Task T8_BrandNewStudent_ZeroState_DashboardStreak0()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t8");

        // No events published — no profile should exist
        var profile = await GetProfileAsync(studentId);
        profile.Should().BeNull("brand-new student must have no StudentXpProfile row");

        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);
        TryProp(dashData, "streak", out var streakVal).Should().BeTrue(
            "data.streak must be present; body: {0}", dashBody);
        streakVal.GetInt32().Should().Be(0,
            "brand-new student with no profile must see streak=0 (graceful null from IStudentStreakQuery); body: {0}", dashBody);
    }

    // =========================================================================
    // T9 — Cross-student isolation: two students, independent streaks (AC3)
    //
    // Student A completes a lesson (streak=1); student B does nothing (streak=0).
    // Each dashboard reflects their own streak.
    // =========================================================================

    [Fact(DisplayName = "P403-T9 Cross-student isolation: A's streak=1, B's streak=0 (separate profiles)")]
    public async Task T9_CrossStudentIsolation_StreaksSeparate()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("t9a");
        var (tokenB, studentIdB) = await CreateStudentViaParentFlowAsync("t9b");

        // Student A completes a lesson
        await PublishLessonCompletedEventAsync(
            studentId:     studentIdA,
            eventId:       Guid.NewGuid(),
            occurredOnUtc: DateTime.UtcNow,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId);

        // B does nothing

        // Assert DB directly
        var profileA = await GetProfileAsync(studentIdA);
        profileA.Should().NotBeNull();
        profileA!.CurrentStreak.Should().Be(1, "student A completed a lesson → streak=1");

        var profileB = await GetProfileAsync(studentIdB);
        profileB.Should().BeNull("student B has no activity → no profile row");

        // Assert via Dashboard
        var (dashRespA, dashRootA, dashBodyA) = await GetDashboardAsync(tokenA);
        dashRespA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyA);
        TryProp(dashRootA, "data", out var dataA).Should().BeTrue("body: {0}", dashBodyA);
        TryProp(dataA, "streak", out var streakA).Should().BeTrue("body: {0}", dashBodyA);
        streakA.GetInt32().Should().Be(1, "student A's dashboard must show streak=1; body: {0}", dashBodyA);

        var (dashRespB, dashRootB, dashBodyB) = await GetDashboardAsync(tokenB);
        dashRespB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyB);
        TryProp(dashRootB, "data", out var dataB).Should().BeTrue("body: {0}", dashBodyB);
        TryProp(dataB, "streak", out var streakB).Should().BeTrue("body: {0}", dashBodyB);
        streakB.GetInt32().Should().Be(0, "student B's dashboard must show streak=0; body: {0}", dashBodyB);

        // IDOR check: profile A's XpAward rows must not appear in B's profile
        var awardsA = await GetAwardsAsync(studentIdA);
        var awardsB = await GetAwardsAsync(studentIdB);
        awardsA.Should().NotBeEmpty("student A has XpAward rows");
        awardsB.Should().BeEmpty("student B has no XpAward rows");
    }

    // =========================================================================
    // T10 — Sweep job: stale profile gets CurrentStreak=0 (AC4)
    //
    // Seed: CurrentStreak=3, LongestStreak=3, LastActivityDateUtc=today-3 days.
    // Set TestClock to "today" UTC.
    // Action: RunSweepJobAsync().
    // Assert: CurrentStreak=0, LongestStreak=3 (preserved), LastActivityDateUtc unchanged.
    // Follow-up: publish LessonCompleted today → CurrentStreak=1 (NOT 3).
    // =========================================================================

    [Fact(DisplayName = "P403-T10 Sweep job resets stale CurrentStreak=0; LongestStreak preserved; follow-up activity starts at 1")]
    public async Task T10_SweepJob_BrokenStreak_ResetsTo0()
    {
        var now          = DateTime.UtcNow;
        var today        = DateOnly.FromDateTime(now);
        var threeDaysAgo = today.AddDays(-3);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t10");

        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      3,
            longestStreak:      3,
            lastActivityDateUtc: threeDaysAgo,
            totalXp:            150);

        // Fix the test clock so the sweep's "today" is deterministic
        _factory.TestClock.SetUtcNow(now);

        await RunSweepJobAsync();

        var profileAfterSweep = await GetProfileAsync(studentId);
        profileAfterSweep.Should().NotBeNull();
        profileAfterSweep!.CurrentStreak.Should().Be(0,
            "sweep must reset CurrentStreak to 0 for profiles where LastActivityDateUtc < today.AddDays(-1)");
        profileAfterSweep.LongestStreak.Should().Be(3,
            "sweep must NOT touch LongestStreak — it is preserved across resets");
        profileAfterSweep.LastActivityDateUtc.Should().Be(threeDaysAgo,
            "sweep must NOT change LastActivityDateUtc");

        // Follow-up: now the student comes back today → streak starts at 1 (NOT 3)
        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId);

        var profileAfterReturn = await GetProfileAsync(studentId);
        profileAfterReturn!.CurrentStreak.Should().Be(1,
            "after the sweep zeroed CurrentStreak, the next activity must start fresh at 1");
    }

    // =========================================================================
    // T11 — Sweep job: active streak (LastActivityDateUtc=yesterday) is left alone (AC4)
    //
    // Seed: CurrentStreak=3, LastActivityDateUtc=yesterday.
    // Run sweep (today's clock).
    // Assert: CurrentStreak=3 unchanged.
    // =========================================================================

    [Fact(DisplayName = "P403-T11 Sweep job leaves active streak (LastActivityDateUtc=yesterday) unchanged")]
    public async Task T11_SweepJob_ActiveStreak_LeftAlone()
    {
        var now       = DateTime.UtcNow;
        var today     = DateOnly.FromDateTime(now);
        var yesterday = today.AddDays(-1);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t11");

        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      3,
            longestStreak:      3,
            lastActivityDateUtc: yesterday,
            totalXp:            150);

        _factory.TestClock.SetUtcNow(now);
        await RunSweepJobAsync();

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(3,
            "yesterday's activity is within the grace boundary — sweep must NOT reset this streak");
    }

    // =========================================================================
    // T12 — Sweep job idempotent: running twice resets only once (AC4)
    //
    // Seed: stale streak. Run sweep twice. Assert: second run is a no-op.
    // =========================================================================

    [Fact(DisplayName = "P403-T12 Sweep job idempotent: running twice resets stale streak only once")]
    public async Task T12_SweepJob_Idempotent_RunTwice()
    {
        var now          = DateTime.UtcNow;
        var today        = DateOnly.FromDateTime(now);
        var threeDaysAgo = today.AddDays(-3);

        var (_, studentId) = await CreateStudentViaParentFlowAsync("t12");

        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      5,
            longestStreak:      5,
            lastActivityDateUtc: threeDaysAgo,
            totalXp:            250);

        _factory.TestClock.SetUtcNow(now);

        // First run — resets streak
        await RunSweepJobAsync();

        var afterFirst = await GetProfileAsync(studentId);
        afterFirst!.CurrentStreak.Should().Be(0, "first sweep run → CurrentStreak=0");
        afterFirst.LongestStreak.Should().Be(5, "LongestStreak preserved after first sweep");

        // Second run — must be a no-op (BreakStreak() is idempotent: early-return when already 0)
        await RunSweepJobAsync();

        var afterSecond = await GetProfileAsync(studentId);
        afterSecond!.CurrentStreak.Should().Be(0,
            "second sweep run must be a no-op — CurrentStreak was already 0");
        afterSecond.LongestStreak.Should().Be(5,
            "LongestStreak must still be 5 after second sweep run");
    }

    // =========================================================================
    // T13 — StreakBonus triggers level-up (AC1 + level-up)
    //
    // Seed: TotalXp=95, CurrentStreak=0, LastActivityDateUtc=yesterday.
    // Action: publish LessonCompleted today. LessonCompleted(50) + StreakBonus(30) ≥ 100 → Level 2.
    // =========================================================================

    [Fact(DisplayName = "P403-T13 StreakBonus triggers level-up: TotalXp=95+50+30=175 → Level 2")]
    public async Task T13_StreakBonus_TriggersLevelUp()
    {
        var today     = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var (token, studentId) = await CreateStudentViaParentFlowAsync("t13");

        // Seed profile with 95 XP, streak at 0 (just reset), active yesterday
        await SeedStreakProfileAsync(
            studentId:          studentId,
            currentStreak:      0,
            longestStreak:      3,
            lastActivityDateUtc: yesterday,
            totalXp:            95,
            currentLevel:       1);

        // Publish LessonCompleted today (100% accuracy = also QuizPass +20)
        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       Guid.NewGuid(),
            occurredOnUtc: DateTime.UtcNow,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId,
            accuracyPct:   100,
            correctCount:  4);

        // Expected XP: 95 (seed) + 50 (LessonCompleted) + 20 (QuizPass) + 30 (StreakBonus) + 20 (FIRST_LESSON badge, P4-05) = 215.
        // Level 2 threshold = 100 XP cumulative.
        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.TotalXp.Should().Be(215,
            "95 (seed) + LessonCompleted(50) + QuizPass(20) + StreakBonus(30) + FIRST_LESSON badge(20, P4-05) = 215 XP");
        profile.CurrentLevel.Should().Be(2,
            "215 XP exceeds the L2 threshold of 100 XP");
        profile.CurrentStreak.Should().Be(1,
            "consecutive-day activity (yesterday → today with CurrentStreak=0) starts a fresh streak at 1");
    }

    // =========================================================================
    // T14 — AnswerSubmittedIntegrationEvent does NOT advance streak (D2 locked decision)
    //
    // Publish an AnswerSubmitted event for a brand-new student.
    // Assert: 10 XP awarded (CorrectAnswer), but no streak advance (streak=0, no profile streak fields set).
    // =========================================================================

    [Fact(DisplayName = "P403-T14 AnswerSubmitted event does NOT advance streak (D2: only LessonCompleted qualifies)")]
    public async Task T14_AnswerSubmitted_DoesNotAdvanceStreak()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t14");

        var ev = new AnswerSubmittedIntegrationEvent(
            EventId:            Guid.NewGuid(),
            OccurredOnUtc:      DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            CorrectAnswerCount: 1);   // one correct answer → +10 XP

        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(ev);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull("AnswerSubmitted creates a StudentXpProfile via the XP handler");
        profile!.TotalXp.Should().Be(10,
            "one correct answer awards exactly 10 XP via AnswerSubmittedIntegrationEventHandler");
        profile.CurrentStreak.Should().Be(0,
            "AnswerSubmittedIntegrationEventHandler must NOT advance the streak (D2 locked decision)");
        profile.LastActivityDateUtc.Should().BeNull(
            "LastActivityDateUtc must remain null — only LessonCompleted sets it");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.CorrectAnswer,
            "CorrectAnswer XpAward must exist");
        awards.Should().NotContain(a => a.Reason == XpReason.StreakBonus,
            "AnswerSubmitted must NOT produce a StreakBonus XpAward row");
    }

    // =========================================================================
    // T15 — Envelope sanity: "successed" camelCase with lowercase 'd' (non-functional)
    //
    // Verifies that the Dashboard response envelope preserves the canonical key spelling
    // defined in CONVENTIONS.md §5 — "successed" (sic, lowercase-d camelCase).
    // =========================================================================

    [Fact(DisplayName = "P403-T15 Dashboard response envelope has 'successed':true (camelCase lowercase-d)")]
    public async Task T15_EnvelopeSanity_SuccessedLowercaseD()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("t15");

        var (resp, root, body) = await GetDashboardAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // The literal string "successed": must appear in camelCase with lowercase 'd'.
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> uses the canonical key 'successed' (sic — CONVENTIONS §5); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue(
            "envelope must have 'successed' key; body: {0}", body);
        successed.GetBoolean().Should().BeTrue(
            "successed must be true for a successful dashboard call; body: {0}", body);

        // Full envelope shape check
        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue(
            "envelope must have 'message'; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue(
            "envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue(
            "envelope must have 'errors'; body: {0}", body);

        // Dashboard-specific payload check
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "streak", out _).Should().BeTrue(
            "dashboard data must contain 'streak'; body: {0}", body);
        TryProp(data, "xp", out _).Should().BeTrue(
            "dashboard data must contain 'xp'; body: {0}", body);
        TryProp(data, "level", out _).Should().BeTrue(
            "dashboard data must contain 'level'; body: {0}", body);
    }
}

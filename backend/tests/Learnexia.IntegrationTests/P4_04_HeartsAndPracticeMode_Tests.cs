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

/// <summary>
/// P4-04 integration tests — "Lose hearts and enter Practice Mode".
///
/// Tests the hearts engine end-to-end:
///   - AnswerSubmittedIntegrationEvent (wrong) → LoseHeartCommand → Hearts decrements
///   - AnswerSubmittedIntegrationEvent (correct, Hearts=0) → RegainHeartFromPractice → Hearts +1
///   - AnswerSubmittedIntegrationEvent (correct, Hearts>0) → AwardAnswerSubmittedXp → XP +10
///   - LessonCompleted at Hearts=0 → no XP, no streak (Practice Mode gates)
///   - Idempotency: duplicate AnswerSubmittedEvent → exactly one HeartLoss row
///   - Out-of-order event (OccurredAtUtc too old) → ignored, Hearts unchanged
///   - Time-based lazy refill: hearts tick back when time elapses
///   - Dashboard: Hearts + InPracticeMode via IStudentHeartsQuery cross-module seam
///   - IDOR: dashboard Hearts scoped to JWT
///
/// Coverage map (14 test cases from P4-04 plan B5-1):
///   H1   BrandNewStudent_DashboardShowsFullHearts
///   H2   WrongAnswer_CreatesProfileHearts4_OneHeartLossRow
///   H3   FiveWrongAnswers_HeartsZero_PracticeMode
///   H4   SixthWrongAtZero_AuditRowWritten_HeartsStillZero
///   H5   Idempotency_SameEventId_ExactlyOneHeartLoss
///   H7   CorrectAnswerAtZero_PracticeMode_Regens1Heart_NoXp
///   H8   CorrectAnswerAboveZero_AwardsXp_HeartsUnchanged
///   H10  LessonCompleteAtPracticeMode_NoXpNoStreak
///   H11  LazyRefill_TimeElapsed_HeartsTickBack
///   H12  OutOfOrderEvent_Ignored_HeartsUnchanged
///   H13  Dashboard_ReflectsRealHeartsAndPracticeMode
///   H14  IDOR_DashboardScopedToJwt
///   H15  PracticeModeGate_AdvanceStreak_Suppressed
///   HEnv Envelope_HeartsAndInPracticeModeFields_Present
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_04_HeartsAndPracticeMode_Tests : IAsyncLifetime
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

    public P4_04_HeartsAndPracticeMode_Tests(LearnexiaWebAppFactory factory)
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
        _factory.TestClock.Reset();
        return Task.CompletedTask;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p404_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
                FullName = "Test Student P404",
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

        var studentToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetDashboardAsync(string token)
        => SendAsync(_client, HttpMethod.Get, DashboardUrl, null, token);

    /// <summary>
    /// Publishes an AnswerSubmittedIntegrationEvent directly via IPublisher (MediatR fan-out).
    /// correctAnswerCount = 0 → wrong answer; 1 → correct answer.
    /// </summary>
    private async Task PublishAnswerSubmittedAsync(
        int studentId,
        Guid eventId,
        DateTime occurredOnUtc,
        int lessonId,
        int skillId,
        int correctAnswerCount)
    {
        var ev = new AnswerSubmittedIntegrationEvent(
            EventId:            eventId,
            OccurredOnUtc:      occurredOnUtc,
            StudentId:          studentId,
            LessonId:           lessonId,
            SkillId:            skillId,
            CorrectAnswerCount: correctAnswerCount);

        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(ev);
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

    private async Task<List<HeartLoss>> GetHeartLossesAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new();

        return await db.HeartLosses
            .AsNoTracking()
            .Where(h => h.StudentXpProfileId == profile.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Seeds a StudentXpProfile with specific Hearts and LastHeartRefillAtUtc values via raw SQL.
    /// Required because Hearts/LastHeartRefillAtUtc have private setters — raw SQL bypasses entity restrictions.
    /// </summary>
    private async Task SeedProfileWithHeartsAsync(
        int studentId,
        int hearts,
        DateTime? lastHeartRefillAtUtc,
        int totalXp = 0,
        int currentLevel = 1,
        int currentStreak = 0,
        int longestStreak = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var existing = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (existing is null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO gamification.""StudentXpProfiles""
                   (""StudentId"", ""TotalXp"", ""CurrentLevel"", ""LastAwardAtUtc"",
                    ""CurrentStreak"", ""LongestStreak"",
                    ""Hearts"", ""LastHeartRefillAtUtc"",
                    ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {totalXp}, {currentLevel}, {DateTime.UtcNow},
                           {currentStreak}, {longestStreak},
                           {hearts}, {lastHeartRefillAtUtc},
                           {DateTime.UtcNow}, {0})");
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE gamification.""StudentXpProfiles""
                   SET ""TotalXp"" = {totalXp},
                       ""CurrentLevel"" = {currentLevel},
                       ""CurrentStreak"" = {currentStreak},
                       ""LongestStreak"" = {longestStreak},
                       ""Hearts"" = {hearts},
                       ""LastHeartRefillAtUtc"" = {lastHeartRefillAtUtc}
                   WHERE ""StudentId"" = {studentId}");
        }
    }

    // =========================================================================
    // H1 — Brand-new student starts with full hearts on first read (AC1, AC3)
    //
    // No profile row exists for a brand-new student.
    // IStudentHeartsQuery returns a sentinel snapshot (hearts=cap=5, inPracticeMode=false).
    // GET /api/Learning/Dashboard → hearts=5, inPracticeMode=false.
    // =========================================================================

    [Fact(DisplayName = "P404-H1 Brand-new student: GET /Dashboard → hearts=5, inPracticeMode=false (sentinel from IStudentHeartsQuery)")]
    public async Task H1_BrandNewStudent_DashboardShowsFullHearts()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("h1");

        // No events published — no profile should exist
        var profile = await GetProfileAsync(studentId);
        profile.Should().BeNull("brand-new student must have no StudentXpProfile row");

        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);
        TryProp(dashData, "hearts", out var heartsVal).Should().BeTrue(
            "data.hearts must be present in P4-04 dashboard DTO; body: {0}", dashBody);
        heartsVal.GetInt32().Should().Be(5,
            "brand-new student has no profile → sentinel returns cap=5; body: {0}", dashBody);

        TryProp(dashData, "inPracticeMode", out var pmVal).Should().BeTrue(
            "data.inPracticeMode must be present; body: {0}", dashBody);
        pmVal.GetBoolean().Should().BeFalse(
            "brand-new student is not in Practice Mode; body: {0}", dashBody);
    }

    // =========================================================================
    // H2 — Wrong answer creates profile with Hearts=4, one HeartLoss row (AC1, AC4)
    //
    // Publish a wrong AnswerSubmittedIntegrationEvent for a brand-new student.
    // Assert: StudentXpProfile.Hearts=4, LastHeartRefillAtUtc != null.
    //         1 HeartLoss row with HeartsAfter=4, OriginEventId = event.EventId.
    //         Dashboard shows hearts=4, inPracticeMode=false.
    // =========================================================================

    [Fact(DisplayName = "P404-H2 One wrong answer → profile Hearts=4, LastHeartRefillAtUtc set, 1 HeartLoss row")]
    public async Task H2_WrongAnswer_CreatesProfileHearts4_OneHeartLossRow()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("h2");

        var eventId = Guid.NewGuid();
        var now = _factory.TestClock.UtcNow;
        await PublishAnswerSubmittedAsync(
            studentId:          studentId,
            eventId:            eventId,
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 0); // wrong answer

        // Assert profile state
        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull("LoseHeartCommand creates profile on first loss");
        profile!.Hearts.Should().Be(4, "brand-new student starts at 5; -1 = 4");
        profile.LastHeartRefillAtUtc.Should().NotBeNull(
            "refill clock is set to OccurredAtUtc on loss");

        // Assert HeartLoss audit row
        var losses = await GetHeartLossesAsync(studentId);
        losses.Should().HaveCount(1, "exactly one HeartLoss row for one wrong answer");
        losses[0].OriginEventId.Should().Be(eventId, "OriginEventId must match the event");
        losses[0].HeartsAfter.Should().Be(4, "HeartsAfter snapshot must equal remaining hearts");

        // Assert Dashboard
        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);
        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);
        TryProp(dashData, "hearts", out var heartsVal).Should().BeTrue("body: {0}", dashBody);
        heartsVal.GetInt32().Should().Be(4, "dashboard must show updated hearts=4; body: {0}", dashBody);
        TryProp(dashData, "inPracticeMode", out var pmVal).Should().BeTrue("body: {0}", dashBody);
        pmVal.GetBoolean().Should().BeFalse("hearts=4 is not Practice Mode; body: {0}", dashBody);
    }

    // =========================================================================
    // H3 — Five wrong answers drop hearts to 0 → Practice Mode (AC1, AC2)
    //
    // Publish 5 wrong AnswerSubmittedIntegrationEvents (5 distinct EventIds, each "fresh" in time).
    // Assert: Hearts=0, InPracticeMode=true.
    //         5 HeartLoss rows.
    //         Dashboard shows hearts=0, inPracticeMode=true.
    // =========================================================================

    [Fact(DisplayName = "P404-H3 Five wrong answers → Hearts=0, inPracticeMode=true, 5 HeartLoss rows")]
    public async Task H3_FiveWrongAnswers_HeartsZero_PracticeMode()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("h3");
        var now = _factory.TestClock.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            // Each event is stamped to "now" so each is within the out-of-order window.
            await PublishAnswerSubmittedAsync(
                studentId:          studentId,
                eventId:            Guid.NewGuid(),
                occurredOnUtc:      now,
                lessonId:           _mathG1DemoLessonId,
                skillId:            _mathG1DemoLessonSkillId,
                correctAnswerCount: 0);
        }

        // Assert profile
        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.Hearts.Should().Be(0, "5 wrong answers starting from 5 → 0 hearts");

        // Assert HeartLoss rows
        var losses = await GetHeartLossesAsync(studentId);
        losses.Should().HaveCount(5, "5 wrong answers → 5 HeartLoss rows");

        // Assert dashboard
        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);
        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);
        TryProp(dashData, "hearts", out var heartsVal).Should().BeTrue("body: {0}", dashBody);
        heartsVal.GetInt32().Should().Be(0, "dashboard must show hearts=0; body: {0}", dashBody);
        TryProp(dashData, "inPracticeMode", out var pmVal).Should().BeTrue("body: {0}", dashBody);
        pmVal.GetBoolean().Should().BeTrue("hearts=0 → inPracticeMode=true; body: {0}", dashBody);
    }

    // =========================================================================
    // H4 — Sixth wrong answer at Hearts=0 → HeartLoss audit row written, Hearts stays 0 (AC2)
    //
    // Pre: profile seeded at Hearts=0.
    // Action: publish wrong answer.
    // Assert: 1 new HeartLoss row with HeartsAfter=0. Profile Hearts still 0.
    // Note: Hearts stays at Math.Max(0, 0-1) = 0 (clamped). Audit row still written (D7).
    // =========================================================================

    [Fact(DisplayName = "P404-H4 Sixth wrong answer at Hearts=0 → HeartLoss audit row written, Hearts remains 0")]
    public async Task H4_SixthWrongAtZero_AuditRowWritten_HeartsStillZero()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h4");
        var now = _factory.TestClock.UtcNow;

        // Seed profile at Hearts=0 (already in Practice Mode)
        await SeedProfileWithHeartsAsync(
            studentId:            studentId,
            hearts:               0,
            lastHeartRefillAtUtc: now.AddMinutes(-5)); // recent loss so out-of-order guard passes

        var eventId = Guid.NewGuid();
        await PublishAnswerSubmittedAsync(
            studentId:          studentId,
            eventId:            eventId,
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 0); // another wrong answer at 0

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.Hearts.Should().Be(0, "Math.Max(0, 0-1) = 0 — hearts stays at 0 (clamped)");

        var losses = await GetHeartLossesAsync(studentId);
        losses.Should().HaveCount(1,
            "audit row is still written even at Hearts=0 (D7 — wrong answer happened; audit reflects it)");
        losses[0].OriginEventId.Should().Be(eventId);
        losses[0].HeartsAfter.Should().Be(0, "HeartsAfter snapshot = 0 (at floor)");
    }

    // =========================================================================
    // H5 — Idempotency: redelivered AnswerSubmittedIntegrationEvent (same EventId) → exactly one HeartLoss (AC1 idempotency)
    //
    // Publish the same wrong-answer event twice with the same EventId.
    // Assert: exactly 1 HeartLoss row, Hearts decremented exactly once (=4).
    // =========================================================================

    [Fact(DisplayName = "P404-H5 Duplicate wrong-answer event (same EventId) → exactly one HeartLoss row (idempotency)")]
    public async Task H5_Idempotency_SameEventId_ExactlyOneHeartLoss()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h5");
        var now = _factory.TestClock.UtcNow;

        var eventId = Guid.NewGuid();
        var ev = new AnswerSubmittedIntegrationEvent(
            EventId:            eventId,
            OccurredOnUtc:      now,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            CorrectAnswerCount: 0);

        // First publish
        using (var scope1 = _factory.Services.CreateScope())
        {
            var pub = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        var profileAfterFirst = await GetProfileAsync(studentId);
        profileAfterFirst.Should().NotBeNull();
        profileAfterFirst!.Hearts.Should().Be(4, "first wrong answer → 5-1=4");

        var lossesAfterFirst = await GetHeartLossesAsync(studentId);
        lossesAfterFirst.Should().HaveCount(1, "one HeartLoss row after first delivery");

        // Second publish — same EventId (duplicate)
        using (var scope2 = _factory.Services.CreateScope())
        {
            var pub = scope2.ServiceProvider.GetRequiredService<IPublisher>();
            await pub.Publish(ev);
        }

        var profileAfterSecond = await GetProfileAsync(studentId);
        profileAfterSecond!.Hearts.Should().Be(4,
            "duplicate event must NOT decrement hearts a second time (idempotency)");

        var lossesAfterSecond = await GetHeartLossesAsync(studentId);
        lossesAfterSecond.Should().HaveCount(1,
            "second delivery with same EventId must NOT create a second HeartLoss row");
    }

    // =========================================================================
    // H7 — Correct answer at Hearts=0 (Practice Mode) → +1 heart, NO XP awarded (AC2, Q3=yes)
    //
    // Pre: profile seeded at Hearts=0, TotalXp=0.
    // Action: publish correct answer.
    // Assert: Profile Hearts=1, TotalXp=0. No XpAward(CorrectAnswer). No HeartLoss row.
    //         Hearts 0→1 exits Practice Mode (HeartsRefilledDomainEvent raised internally).
    // =========================================================================

    [Fact(DisplayName = "P404-H7 Correct answer at Hearts=0 (Practice Mode) → Hearts becomes 1, TotalXp=0 (no XP in PracticeMode; regen fires)")]
    public async Task H7_CorrectAnswerAtZero_PracticeMode_Regens1Heart_NoXp()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h7");
        var now = _factory.TestClock.UtcNow;

        // Seed Hearts=0 (Practice Mode)
        await SeedProfileWithHeartsAsync(
            studentId:            studentId,
            hearts:               0,
            lastHeartRefillAtUtc: now.AddMinutes(-5), // recent — out-of-order guard passes
            totalXp:              0);

        var correctEventId = Guid.NewGuid();
        await PublishAnswerSubmittedAsync(
            studentId:          studentId,
            eventId:            correctEventId,
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 1); // correct answer in Practice Mode

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.Hearts.Should().Be(1,
            "correct answer in Practice Mode (Q3=yes) → RegainHeartFromPractice → Hearts 0→1");
        profile.TotalXp.Should().Be(0,
            "XP is suspended while in Practice Mode — AwardAnswerSubmittedXp gate fires before award");

        // No CorrectAnswer XpAward row
        var awards = await GetAwardsAsync(studentId);
        awards.Should().NotContain(a => a.Reason == XpReason.CorrectAnswer,
            "no CorrectAnswer XpAward must be written while in Practice Mode");

        // No HeartLoss row (this was a correct answer, not wrong)
        var losses = await GetHeartLossesAsync(studentId);
        losses.Should().BeEmpty("correct answer does not produce a HeartLoss row");
    }

    // =========================================================================
    // H8 — Correct answer at Hearts>0 (NOT Practice Mode) → XP awarded, hearts unchanged (AC1, AC2)
    //
    // Pre: profile seeded at Hearts=3, TotalXp=20.
    // Action: publish correct answer.
    // Assert: Profile Hearts=3 (unchanged), TotalXp=30. 1 XpAward(CorrectAnswer, +10).
    // =========================================================================

    [Fact(DisplayName = "P404-H8 Correct answer at Hearts=3 (not Practice Mode) → +10 XP, hearts unchanged at 3")]
    public async Task H8_CorrectAnswerAboveZero_AwardsXp_HeartsUnchanged()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h8");
        var now = _factory.TestClock.UtcNow;

        // Seed Hearts=3 (NOT Practice Mode)
        await SeedProfileWithHeartsAsync(
            studentId:            studentId,
            hearts:               3,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            totalXp:              20);

        var eventId = Guid.NewGuid();
        await PublishAnswerSubmittedAsync(
            studentId:          studentId,
            eventId:            eventId,
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 1); // correct answer, not Practice Mode

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.Hearts.Should().Be(3, "correct answer does NOT change hearts when not in Practice Mode");
        profile.TotalXp.Should().Be(30, "20 (seed) + 10 (CorrectAnswer) = 30 XP");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.CorrectAnswer && a.OriginEventId == eventId,
            "CorrectAnswer XpAward must be written when not in Practice Mode");

        var losses = await GetHeartLossesAsync(studentId);
        losses.Should().BeEmpty("correct answer produces no HeartLoss row");
    }

    // =========================================================================
    // H10 — Lesson complete at Hearts=0 (Practice Mode) → NO LessonComplete XP, NO StreakBonus (AC2)
    //
    // Pre: profile seeded at Hearts=0.
    // Action: publish LessonCompletedIntegrationEvent directly via IPublisher.
    // Assert: no XpAward(LessonCompleted), no XpAward(StreakBonus), no XpAward(QuizPass).
    //         Profile CurrentStreak unchanged (gate fires in AdvanceStreakCommandHandler too).
    //
    // Note: This test uses direct event publish because HTTP-level lesson completion would
    // require going through the full quiz flow, and setting up Hearts=0 beforehand via
    // the quiz API (5 wrong answers) would interfere with the attempt state. Raw SQL seeding
    // + direct IPublisher.Publish gives a clean, readable test.
    // =========================================================================

    [Fact(DisplayName = "P404-H10 LessonCompleted at Hearts=0 (Practice Mode) → no LessonCompleted XP, no StreakBonus, streak unchanged")]
    public async Task H10_LessonCompleteAtPracticeMode_NoXpNoStreak()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h10");
        var now = _factory.TestClock.UtcNow;

        // Seed Hearts=0 (Practice Mode), TotalXp=0, CurrentStreak=0
        await SeedProfileWithHeartsAsync(
            studentId:            studentId,
            hearts:               0,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            totalXp:              0,
            currentStreak:        0);

        // Publish LessonCompleted directly (bypasses quiz HTTP flow)
        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId,
            accuracyPct:   100,
            correctCount:  4);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        // P4-05 D11: FIRST_LESSON badge CAN fire in Practice Mode (lesson completion is not blocked).
        // The badge awards +20 XP (Common rarity). LessonCompleted / QuizPass / StreakBonus are still gated.
        profile!.TotalXp.Should().Be(20,
            "AwardLessonCompletedXpCommandHandler gate fires — no LessonCompleted XP in Practice Mode; " +
            "but P4-05 FIRST_LESSON badge awards +20 XP (D11: badge fires regardless of Practice Mode)");
        profile.CurrentStreak.Should().Be(0,
            "AdvanceStreakCommandHandler gate fires — no streak advance in Practice Mode");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().NotContain(a => a.Reason == XpReason.LessonCompleted,
            "LessonCompleted XpAward must NOT be written in Practice Mode");
        awards.Should().NotContain(a => a.Reason == XpReason.StreakBonus,
            "StreakBonus XpAward must NOT be written in Practice Mode");
        awards.Should().NotContain(a => a.Reason == XpReason.QuizCompleted,
            "QuizPass XpAward must NOT be written in Practice Mode");
        // P4-05: FIRST_LESSON badge fires even in Practice Mode (D11).
        awards.Should().ContainSingle(a => a.Reason == XpReason.BadgeEarned,
            "FIRST_LESSON badge XpAward must be written even in Practice Mode (P4-05 D11)");
        awards.Single(a => a.Reason == XpReason.BadgeEarned).XpAmount
            .Should().Be(20, "FIRST_LESSON badge is Common rarity — awards +20 XP");
    }

    // =========================================================================
    // H11 — Time-based lazy refill: hearts tick back after interval elapses (AC3, D5)
    //
    // Pre: profile seeded at Hearts=2, LastHeartRefillAtUtc=now-61min.
    // Use TestClock set to "now".
    // Action: publish wrong answer (triggers LoseHeart → lazy-refill first inside LoseHeart()).
    //         LazyHeartRefiller.Compute sees 61min elapsed / 30min interval = 2 ticks → Hearts goes 2+2=4.
    //         Then loses 1 heart → Hearts = 4-1 = 3.
    // Assert: Profile Hearts=3.
    //
    // Note: Dashboard IStudentHeartsQuery also applies lazy refill on read (D5/D8), but verifying
    // the DB-persisted state after a write-path trigger is the most deterministic approach.
    // =========================================================================

    [Fact(DisplayName = "P404-H11 Lazy refill: 61 min elapsed (2 ticks), then wrong answer → Hearts = (2+2)-1 = 3")]
    public async Task H11_LazyRefill_TimeElapsed_HeartsTickBack()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h11");
        var now = DateTime.UtcNow;

        // Lock clock to "now" so out-of-order guard computes predictably
        _factory.TestClock.SetUtcNow(now);

        // Seed Hearts=2, last refill 61 minutes ago
        // LazyHeartRefiller: floor(61/30) = 2 ticks → newHearts = 2+2 = 4 (still below cap=5)
        var lastRefillAt = now.AddMinutes(-61);
        await SeedProfileWithHeartsAsync(
            studentId:            studentId,
            hearts:               2,
            lastHeartRefillAtUtc: lastRefillAt,
            totalXp:              0);

        // Publish a wrong answer at "now" (within the 30-min out-of-order window)
        await PublishAnswerSubmittedAsync(
            studentId:          studentId,
            eventId:            Guid.NewGuid(),
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 0);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.Hearts.Should().Be(3,
            "2 hearts + 2 regen ticks (61/30 = 2) = 4, then -1 wrong answer = 3");
    }

    // =========================================================================
    // H12 — Out-of-order event guard: stale event ignored (D10, AC1 guard)
    //
    // The LoseHeartCommandHandler guard: if `OccurredAtUtc + RefillIntervalMinutes < now`,
    // the event is treated as stale and is short-circuited without writing.
    //
    // Pre: profile at Hearts=5 (fresh).
    // Action: publish wrong answer with OccurredAtUtc = now - 60min (more than 30min ago).
    // Assert: Hearts still 5, no HeartLoss row.
    // =========================================================================

    [Fact(DisplayName = "P404-H12 Stale event (OccurredAtUtc > 30min ago) is ignored by out-of-order guard — Hearts unchanged")]
    public async Task H12_OutOfOrderEvent_Ignored_HeartsUnchanged()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h12");
        var now = DateTime.UtcNow;
        _factory.TestClock.SetUtcNow(now);

        // Seed a brand-new-style profile with full hearts
        // (no profile yet → LoseHeartCommandHandler would create one, but the guard fires first)

        // Publish event with OccurredAtUtc = now - 60min (older than RefillIntervalMinutes=30)
        // Guard condition: OccurredAtUtc + 30min < now → (now-60) + 30 = now-30 < now → stale
        var staleEventId = Guid.NewGuid();
        await PublishAnswerSubmittedAsync(
            studentId:          studentId,
            eventId:            staleEventId,
            occurredOnUtc:      now.AddMinutes(-60), // 60 min ago — older than RefillIntervalMinutes
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 0);

        // No profile row should be created (guard fires before profile load/create)
        var profile = await GetProfileAsync(studentId);
        // The guard fires before UpsertXpProfile — no profile created for a stale event
        // on a brand-new student (profile is null)
        if (profile is not null)
        {
            // Profile was created (possible if guard position differs); hearts must still be 5
            profile.Hearts.Should().Be(5,
                "stale event must NOT decrement hearts — guard short-circuits before LoseHeart() call");
        }

        var losses = await GetHeartLossesAsync(studentId);
        losses.Should().BeEmpty("stale out-of-order event must NOT produce a HeartLoss row");
    }

    // =========================================================================
    // H13 — Cross-module dashboard exposure: Hearts and InPracticeMode reflect real state (AC3)
    //
    // Part A: profile at Hearts=3 → dashboard shows hearts=3, inPracticeMode=false.
    // Part B: profile at Hearts=0 → dashboard shows hearts=0, inPracticeMode=true.
    // =========================================================================

    [Fact(DisplayName = "P404-H13 Dashboard Hearts and InPracticeMode reflect real DB state (cross-module IStudentHeartsQuery seam)")]
    public async Task H13_Dashboard_ReflectsRealHeartsAndPracticeMode()
    {
        var now = _factory.TestClock.UtcNow;

        // Part A — Hearts=3
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("h13a");
        await SeedProfileWithHeartsAsync(
            studentId:            studentIdA,
            hearts:               3,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            totalXp:              0);

        var (dashRespA, dashRootA, dashBodyA) = await GetDashboardAsync(tokenA);
        dashRespA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyA);
        TryProp(dashRootA, "data", out var dataA).Should().BeTrue("body: {0}", dashBodyA);
        TryProp(dataA, "hearts", out var heartsA).Should().BeTrue("body: {0}", dashBodyA);
        heartsA.GetInt32().Should().Be(3,
            "dashboard must read Hearts=3 from IStudentHeartsQuery; body: {0}", dashBodyA);
        TryProp(dataA, "inPracticeMode", out var pmA).Should().BeTrue("body: {0}", dashBodyA);
        pmA.GetBoolean().Should().BeFalse("Hearts=3 → inPracticeMode=false; body: {0}", dashBodyA);

        // Part B — Hearts=0
        var (tokenB, studentIdB) = await CreateStudentViaParentFlowAsync("h13b");
        await SeedProfileWithHeartsAsync(
            studentId:            studentIdB,
            hearts:               0,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            totalXp:              0);

        var (dashRespB, dashRootB, dashBodyB) = await GetDashboardAsync(tokenB);
        dashRespB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyB);
        TryProp(dashRootB, "data", out var dataB).Should().BeTrue("body: {0}", dashBodyB);
        TryProp(dataB, "hearts", out var heartsB).Should().BeTrue("body: {0}", dashBodyB);
        heartsB.GetInt32().Should().Be(0,
            "dashboard must read Hearts=0 from IStudentHeartsQuery; body: {0}", dashBodyB);
        TryProp(dataB, "inPracticeMode", out var pmB).Should().BeTrue("body: {0}", dashBodyB);
        pmB.GetBoolean().Should().BeTrue("Hearts=0 → inPracticeMode=true; body: {0}", dashBodyB);
    }

    // =========================================================================
    // H14 — IDOR: dashboard scoped to JWT (AC3, security)
    //
    // Student A has Hearts=2, Student B has Hearts=5 (no profile).
    // A's JWT → dashboard returns A's hearts=2.
    // B's JWT → dashboard returns B's hearts=5 (sentinel — no profile).
    // =========================================================================

    [Fact(DisplayName = "P404-H14 IDOR: Student A's dashboard shows A's hearts; Student B's dashboard shows B's hearts independently")]
    public async Task H14_IDOR_DashboardScopedToJwt()
    {
        var now = _factory.TestClock.UtcNow;

        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("h14a");
        var (tokenB, studentIdB) = await CreateStudentViaParentFlowAsync("h14b");

        // Seed A at Hearts=2, B has no profile (starts at sentinel=5)
        await SeedProfileWithHeartsAsync(
            studentId:            studentIdA,
            hearts:               2,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            totalXp:              0);

        // A's dashboard
        var (dashRespA, dashRootA, dashBodyA) = await GetDashboardAsync(tokenA);
        dashRespA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyA);
        TryProp(dashRootA, "data", out var dataA).Should().BeTrue("body: {0}", dashBodyA);
        TryProp(dataA, "hearts", out var heartsA).Should().BeTrue("body: {0}", dashBodyA);
        heartsA.GetInt32().Should().Be(2, "Student A's dashboard must show A's 2 hearts; body: {0}", dashBodyA);

        // B's dashboard (no profile → sentinel=5)
        var (dashRespB, dashRootB, dashBodyB) = await GetDashboardAsync(tokenB);
        dashRespB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyB);
        TryProp(dashRootB, "data", out var dataB).Should().BeTrue("body: {0}", dashBodyB);
        TryProp(dataB, "hearts", out var heartsB).Should().BeTrue("body: {0}", dashBodyB);
        heartsB.GetInt32().Should().Be(5,
            "Student B has no profile → sentinel=5; and must NOT see A's hearts; body: {0}", dashBodyB);

        // Explicit IDOR check: B's hearts (5) must not equal A's hearts (2)
        heartsA.GetInt32().Should().NotBe(heartsB.GetInt32(),
            "A and B must have independent heart counts (IDOR isolation)");
    }

    // =========================================================================
    // H15 — Practice-Mode gate on AdvanceStreak (AC2)
    //
    // Pre: profile at Hearts=0, CurrentStreak=0.
    // Action: publish LessonCompletedIntegrationEvent.
    // Assert: AdvanceStreakCommandHandler gate fires → CurrentStreak remains 0.
    //         No StreakBonus XpAward row.
    // =========================================================================

    [Fact(DisplayName = "P404-H15 Practice Mode gates AdvanceStreak: CurrentStreak stays 0, no StreakBonus XpAward")]
    public async Task H15_PracticeModeGate_AdvanceStreak_Suppressed()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("h15");
        var now = _factory.TestClock.UtcNow;

        // Seed Hearts=0, no streak
        await SeedProfileWithHeartsAsync(
            studentId:            studentId,
            hearts:               0,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            totalXp:              0,
            currentStreak:        0);

        await PublishLessonCompletedEventAsync(
            studentId:     studentId,
            eventId:       Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:      _mathG1DemoLessonId,
            skillId:       _mathG1DemoLessonSkillId,
            accuracyPct:   100,
            correctCount:  4);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(0,
            "AdvanceStreakCommandHandler gate fires in Practice Mode — streak stays at 0");

        var awards = await GetAwardsAsync(studentId);
        awards.Should().NotContain(a => a.Reason == XpReason.StreakBonus,
            "StreakBonus XpAward must NOT be written in Practice Mode");
    }

    // =========================================================================
    // HEnv — Envelope sanity: dashboard returns hearts and inPracticeMode fields (AC3)
    //
    // Verifies the P4-04 DashboardDto additive ctor fields are present in the JSON response
    // and that the BaseResponse envelope shape is correct (successed camelCase-d).
    // =========================================================================

    [Fact(DisplayName = "P404-HEnv Dashboard envelope contains 'hearts' and 'inPracticeMode' fields; 'successed' key preserved")]
    public async Task HEnv_Envelope_HeartsAndInPracticeModeFields_Present()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("henv");

        var (resp, root, body) = await GetDashboardAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Envelope shape
        body.Should().Contain("\"successed\":",
            "'successed' (lowercase-d camelCase) must appear in the response envelope per CONVENTIONS §5; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);

        // P4-04 additive DTO fields
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "hearts", out _).Should().BeTrue(
            "dashboard data must contain 'hearts' (P4-04 additive field); body: {0}", body);
        TryProp(data, "inPracticeMode", out _).Should().BeTrue(
            "dashboard data must contain 'inPracticeMode' (P4-04 additive field); body: {0}", body);

        // Legacy fields still present (non-regression)
        TryProp(data, "xp",     out _).Should().BeTrue("dashboard data must contain 'xp'; body: {0}", body);
        TryProp(data, "streak", out _).Should().BeTrue("dashboard data must contain 'streak'; body: {0}", body);
        TryProp(data, "level",  out _).Should().BeTrue("dashboard data must contain 'level'; body: {0}", body);
    }
}

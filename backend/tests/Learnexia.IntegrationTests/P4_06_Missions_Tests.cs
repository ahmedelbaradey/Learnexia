using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P4-06 integration tests — "Complete daily/weekly missions".
///
/// Tests the mission engine end-to-end:
///   - MissionSeeder runs at startup → 8 catalog rows always present (5 daily + 3 weekly).
///   - Lazy instantiation: IStudentMissionsQuery creates per-period rows on dashboard read.
///   - LessonCompleted / AnswerSubmitted / StreakAdvanced events → mission progress increments.
///   - Completion transition: Status=Completed, XpAward(MissionCompleted), XP increases.
///   - Idempotency: same OriginEventId → no double-increment (MissionProgressLog unique index).
///   - MissionRolloverJob: bulk-expires past-period Active/NotStarted rows.
///   - GET /api/Gamification/Missions/Me → JWT-scoped, IDOR-proof, full metadata.
///   - Dashboard: dailyMissions list + weeklyMission summary populated after profile creation.
///
/// Coverage map (21 test cases from P4-06 plan B5-1):
///   T1   Catalog seeded: 8 rows with correct Code/Cadence/Target/RewardXp values
///   T2   Brand-new student GET /Dashboard → dailyMissions null/empty, weeklyMission null
///   T3   First dashboard read (student with profile) → lazily instantiates 5 daily + 3 weekly rows
///   T4   Second dashboard read same day → idempotent (no duplicate rows)
///   T5   Anonymous GET /Missions/Me → 401
///   T6   GET /Missions/Me → daily (5 items) + weekly (3 items) with full metadata
///   T7   LessonCompleted event → DAILY_3_LESSONS progress = 1
///   T8   3 LessonCompleted events → DAILY_3_LESSONS completes, +30 XP awarded
///   T9   Idempotency: redelivered event (same EventId) → progress = 1, not 2
///   T10  AnswerSubmitted correct → DAILY_10_CORRECT progress = 1
///   T11  AnswerSubmitted wrong (CorrectAnswerCount=0) → no mission progress
///   T12  Streak advance → DAILY_KEEP_STREAK completes (target=1), +50 XP
///   T13  Practice Mode: lesson completion counts toward DAILY_3_LESSONS
///   T14  Daily rollover job → past-period Active row → Expired
///   T15  Rollover idempotent: run twice → same status, no exception
///   T16  Rollover does NOT affect active current-period rows
///   T17  IDOR: dashboard missions scoped to JWT
///   T18  IDOR: /Missions/Me scoped to JWT
///   T19  Mission completion chain: bonus XP + potential level-up
///   T20  Weekly mission accumulates across days
///   T21  Enum serialization smoke test: status/targetType int values in response
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_06_Missions_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string DashboardUrl      = "api/Learning/Dashboard";
    private const string MissionsMeUrl     = "api/Gamification/Missions/Me";
    private const string ValidChildPassword = "Child@Pass1";

    // Mission codes from the locked catalog (D1)
    private const string CODE_DAILY_3_LESSONS      = "DAILY_3_LESSONS";
    private const string CODE_DAILY_10_CORRECT     = "DAILY_10_CORRECT";
    private const string CODE_DAILY_KEEP_STREAK    = "DAILY_KEEP_STREAK";
    private const string CODE_DAILY_1_LESSON_EARLY = "DAILY_1_LESSON_EARLY";
    private const string CODE_DAILY_20_CORRECT     = "DAILY_20_CORRECT";
    private const string CODE_WEEKLY_15_LESSONS    = "WEEKLY_15_LESSONS";
    private const string CODE_WEEKLY_75_CORRECT    = "WEEKLY_75_CORRECT";
    private const string CODE_WEEKLY_5_DAY_STREAK  = "WEEKLY_5_DAY_STREAK";

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

    public P4_06_Missions_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Seed badge catalog (idempotent — required for level-up badge chain tests).
        using (var scope = _factory.Services.CreateScope())
        {
            var badgeSeeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
            await badgeSeeder.SeedAsync();
        }

        // Seed mission catalog (idempotent — MissionSeeder is called at module init but we ensure it here).
        using (var scope = _factory.Services.CreateScope())
        {
            var missionSeeder = scope.ServiceProvider.GetRequiredService<MissionSeeder>();
            await missionSeeder.SeedAsync();
        }

        // Seed demo lesson data.
        using (var scope = _factory.Services.CreateScope())
        {
            await LearningSeeder.SeedAsync(scope.ServiceProvider);
        }

        using var db = _factory.Services.CreateScope()
            .ServiceProvider.GetRequiredService<LearningDbContext>();
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
        => $"p406_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
            catch { /* non-JSON */ }
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
                FullName = "Test Student P406",
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

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMissionsMeAsync(string? token = null)
        => SendAsync(_client, HttpMethod.Get, MissionsMeUrl, null, token);

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

    private async Task PublishAnswerSubmittedEventAsync(
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

    private async Task<StudentXpProfile?> GetProfileAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        return await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);
    }

    private async Task<List<StudentMission>> GetStudentMissionsAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new();

        return await db.StudentMissions
            .AsNoTracking()
            .Include(m => m.MissionDefinition)
            .Where(m => m.StudentXpProfileId == profile.Id)
            .ToListAsync();
    }

    private async Task<List<StudentMission>> GetCurrentPeriodMissionsAsync(
        int studentId, MissionType cadence)
    {
        var now = _factory.TestClock.UtcNow;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new();

        // Compute period key using the same logic as MissionPeriodCalculator
        var (periodKey, _, _) = Learnexia.Modules.Gamification.Domain.Services.MissionPeriodCalculator
            .GetCurrentPeriod(cadence, now);

        return await db.StudentMissions
            .AsNoTracking()
            .Include(m => m.MissionDefinition)
            .Where(m => m.StudentXpProfileId == profile.Id && m.PeriodKey == periodKey)
            .ToListAsync();
    }

    private async Task<List<XpAward>> GetXpAwardsAsync(int studentId)
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

    private async Task<List<MissionProgressLog>> GetMissionProgressLogsAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new();

        var missions = await db.StudentMissions
            .AsNoTracking()
            .Where(m => m.StudentXpProfileId == profile.Id)
            .Select(m => m.Id)
            .ToListAsync();

        return await db.MissionProgressLogs
            .AsNoTracking()
            .Where(l => missions.Contains(l.StudentMissionId))
            .ToListAsync();
    }

    /// <summary>
    /// Seeds a StudentXpProfile with specific values via raw SQL.
    /// Required because many profile fields have private/internal setters.
    /// </summary>
    private async Task SeedProfileAsync(
        int studentId,
        int totalXp = 0,
        int currentLevel = 1,
        int currentStreak = 0,
        int longestStreak = 0,
        DateOnly? lastActivityDateUtc = null,
        int hearts = 5,
        DateTime? lastHeartRefillAtUtc = null)
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
                    ""CurrentStreak"", ""LongestStreak"", ""LastActivityDateUtc"",
                    ""Hearts"", ""LastHeartRefillAtUtc"",
                    ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {totalXp}, {currentLevel}, {DateTime.UtcNow},
                           {currentStreak}, {longestStreak}, {lastActivityDateUtc},
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
                       ""LastActivityDateUtc"" = {lastActivityDateUtc},
                       ""Hearts"" = {hearts},
                       ""LastHeartRefillAtUtc"" = {lastHeartRefillAtUtc}
                   WHERE ""StudentId"" = {studentId}");
        }
    }

    /// <summary>
    /// Seeds a past-period StudentMission row directly via SQL.
    /// Used to test the rollover job without waiting for clock advancement.
    /// </summary>
    private async Task<int> SeedPastStudentMissionAsync(
        int studentId,
        string missionCode,
        MissionStatus status = MissionStatus.NotStarted)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        profile.Should().NotBeNull("profile must exist before seeding a past mission");

        var def = await db.MissionDefinitions
            .AsNoTracking()
            .FirstAsync(d => d.Code == missionCode);

        var yesterday = DateTime.UtcNow.AddDays(-1);
        var periodKey = $"D:{yesterday.AddDays(-1):yyyy-MM-dd}"; // two-days-ago key (clearly past)
        var periodStart = yesterday.AddDays(-1).Date;
        var periodEnd   = yesterday.Date; // ended yesterday

        int insertedId = 0;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO gamification.""StudentMissions""
               (""StudentXpProfileId"", ""MissionDefinitionId"", ""MissionType"",
                ""PeriodKey"", ""PeriodStartUtc"", ""PeriodEndUtc"",
                ""Target"", ""Progress"", ""Status"", ""CompletedAtUtc"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({profile!.Id}, {def.Id}, {(int)def.Cadence},
                       {periodKey}, {periodStart}, {periodEnd},
                       {def.Target}, {0}, {(int)status}, {(DateTime?)null},
                       {DateTime.UtcNow}, {0})");

        // Return the inserted row id
        insertedId = await db.StudentMissions
            .AsNoTracking()
            .Where(m => m.StudentXpProfileId == profile.Id
                     && m.MissionDefinitionId == def.Id
                     && m.PeriodKey == periodKey)
            .Select(m => m.Id)
            .FirstAsync();

        return insertedId;
    }

    // =========================================================================
    // T1 — Catalog seeded: 8 rows with correct Code/Cadence/Target/RewardXp
    // =========================================================================

    [Fact(DisplayName = "P406-T1 MissionSeeder seeds exactly 8 rows with correct Code/Cadence/Target/RewardXp")]
    public async Task T1_CatalogSeeded_Exactly8Rows_CorrectMetadata()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var defs = await db.MissionDefinitions.AsNoTracking().ToListAsync();

        defs.Should().HaveCount(8,
            "MissionSeeder seeds exactly 8 mission definitions (D1 locked catalog)");

        // Verify each expected code exists
        var expectedCodes = new[]
        {
            CODE_DAILY_3_LESSONS, CODE_DAILY_10_CORRECT, CODE_DAILY_KEEP_STREAK,
            CODE_DAILY_1_LESSON_EARLY, CODE_DAILY_20_CORRECT,
            CODE_WEEKLY_15_LESSONS, CODE_WEEKLY_75_CORRECT, CODE_WEEKLY_5_DAY_STREAK
        };
        foreach (var code in expectedCodes)
        {
            defs.Should().Contain(d => d.Code == code,
                $"catalog must contain mission with code '{code}'");
        }

        // Verify specific catalog pairs per D1
        AssertMission(defs, CODE_DAILY_3_LESSONS,      MissionType.Daily,  MissionTargetType.CompleteLessons,  3, 30);
        AssertMission(defs, CODE_DAILY_10_CORRECT,     MissionType.Daily,  MissionTargetType.CorrectAnswers,  10, 30);
        AssertMission(defs, CODE_DAILY_KEEP_STREAK,    MissionType.Daily,  MissionTargetType.MaintainStreak,   1, 50);
        AssertMission(defs, CODE_DAILY_1_LESSON_EARLY, MissionType.Daily,  MissionTargetType.CompleteLessons,  1, 20);
        AssertMission(defs, CODE_DAILY_20_CORRECT,     MissionType.Daily,  MissionTargetType.CorrectAnswers,  20, 50);
        AssertMission(defs, CODE_WEEKLY_15_LESSONS,    MissionType.Weekly, MissionTargetType.CompleteLessons, 15, 150);
        AssertMission(defs, CODE_WEEKLY_75_CORRECT,    MissionType.Weekly, MissionTargetType.CorrectAnswers,  75, 150);
        AssertMission(defs, CODE_WEEKLY_5_DAY_STREAK,  MissionType.Weekly, MissionTargetType.MaintainStreak,   5, 200);

        static void AssertMission(List<MissionDefinition> defs, string code,
            MissionType cadence, MissionTargetType targetType, int target, int rewardXp)
        {
            var def = defs.FirstOrDefault(d => d.Code == code);
            def.Should().NotBeNull($"definition '{code}' must exist in catalog");
            def!.Cadence.Should().Be(cadence, $"{code} cadence");
            def.TargetType.Should().Be(targetType, $"{code} targetType");
            def.Target.Should().Be(target, $"{code} target");
            def.RewardXp.Should().Be(rewardXp, $"{code} rewardXp");
        }
    }

    // =========================================================================
    // T2 — Brand-new student GET /Dashboard → dailyMissions null/empty, weeklyMission null
    // =========================================================================

    [Fact(DisplayName = "P406-T2 Brand-new student (no profile) GET /Dashboard → dailyMissions null/empty, weeklyMission null")]
    public async Task T2_BrandNewStudent_NoProfile_DashboardMissionsEmpty()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t2");

        // Brand-new student has no StudentXpProfile row.
        var profile = await GetProfileAsync(studentId);
        profile.Should().BeNull("brand-new student must have no StudentXpProfile row");

        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        TryProp(dashRoot, "data", out var data).Should().BeTrue("body: {0}", dashBody);

        // dailyMissions should be null or empty for brand-new student
        TryProp(data, "dailyMissions", out var dailyMissions).Should().BeTrue(
            "data.dailyMissions must be present (P4-06 additive field); body: {0}", dashBody);
        if (dailyMissions.ValueKind == JsonValueKind.Array)
        {
            dailyMissions.GetArrayLength().Should().Be(0,
                "brand-new student must have no daily missions; body: {0}", dashBody);
        }
        else
        {
            dailyMissions.ValueKind.Should().Be(JsonValueKind.Null,
                "brand-new student dailyMissions must be null or empty array; body: {0}", dashBody);
        }

        // weeklyMission should be null for brand-new student
        TryProp(data, "weeklyMission", out var weeklyMission).Should().BeTrue(
            "data.weeklyMission must be present (P4-06 additive field); body: {0}", dashBody);
        weeklyMission.ValueKind.Should().Be(JsonValueKind.Null,
            "brand-new student weeklyMission must be null; body: {0}", dashBody);
    }

    // =========================================================================
    // T3 — First dashboard read (student with profile) → lazily instantiates missions
    // =========================================================================

    [Fact(DisplayName = "P406-T3 First dashboard read for student with profile → lazily instantiates 5 daily + 3 weekly rows")]
    public async Task T3_FirstDashboardRead_LazilyInstantiatesMissions()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t3");
        var now = _factory.TestClock.UtcNow;

        // Create a profile by seeding it directly
        await SeedProfileAsync(studentId: studentId);

        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull("profile must exist after seeding");

        // First dashboard read triggers lazy instantiation
        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        // Assert dashboard response: dailyMissions = 5 items
        TryProp(dashRoot, "data", out var data).Should().BeTrue("body: {0}", dashBody);
        TryProp(data, "dailyMissions", out var dailyMissions).Should().BeTrue("body: {0}", dashBody);
        dailyMissions.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", dashBody);
        dailyMissions.GetArrayLength().Should().Be(5,
            "dashboard must show 5 daily missions after first read; body: {0}", dashBody);

        // weeklyMission should be non-null (first weekly mission by SortOrder)
        TryProp(data, "weeklyMission", out var weeklyMission).Should().BeTrue("body: {0}", dashBody);
        weeklyMission.ValueKind.Should().NotBe(JsonValueKind.Null,
            "weeklyMission must be non-null after lazy instantiation; body: {0}", dashBody);

        // Assert DB: 5 daily + 3 weekly StudentMission rows created
        var dailyRows = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Daily);
        dailyRows.Should().HaveCount(5,
            "exactly 5 daily StudentMission rows must be created on first dashboard read");

        var weeklyRows = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Weekly);
        weeklyRows.Should().HaveCount(3,
            "exactly 3 weekly StudentMission rows must be created on first dashboard read");

        // All missions start at Progress=0, Status=NotStarted
        dailyRows.Should().AllSatisfy(m =>
        {
            m.Progress.Should().Be(0, $"mission {m.MissionDefinition.Code} must start at Progress=0");
            m.Status.Should().Be(MissionStatus.NotStarted,
                $"mission {m.MissionDefinition.Code} must start at Status=NotStarted");
        });
    }

    // =========================================================================
    // T4 — Second dashboard read same day → idempotent (no duplicate rows)
    // =========================================================================

    [Fact(DisplayName = "P406-T4 Second dashboard read same day → idempotent (still 5 daily + 3 weekly, no duplicates)")]
    public async Task T4_SecondDashboardRead_Idempotent_NoDuplicateRows()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t4");

        await SeedProfileAsync(studentId: studentId);

        // First dashboard read
        await GetDashboardAsync(token);

        var dailyAfterFirst = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Daily);
        var weeklyAfterFirst = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Weekly);
        dailyAfterFirst.Should().HaveCount(5, "5 daily missions after first read");
        weeklyAfterFirst.Should().HaveCount(3, "3 weekly missions after first read");

        // Second dashboard read — lazy instantiation is a no-op when rows already exist
        await GetDashboardAsync(token);

        var dailyAfterSecond = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Daily);
        var weeklyAfterSecond = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Weekly);
        dailyAfterSecond.Should().HaveCount(5,
            "second dashboard read must NOT create duplicate daily missions (idempotency)");
        weeklyAfterSecond.Should().HaveCount(3,
            "second dashboard read must NOT create duplicate weekly missions (idempotency)");
    }

    // =========================================================================
    // T5 — Anonymous GET /Missions/Me → 401
    // =========================================================================

    [Fact(DisplayName = "P406-T5 Anonymous GET /Missions/Me → 401 Unauthorized")]
    public async Task T5_Anonymous_MissionsMe_Returns401()
    {
        var (resp, _, body) = await GetMissionsMeAsync(token: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "MissionsController has [Authorize] — no JWT must return 401; body: {0}", body);
    }

    // =========================================================================
    // T6 — GET /Missions/Me returns daily (5 items) + weekly (3 items) with full metadata
    // =========================================================================

    [Fact(DisplayName = "P406-T6 GET /Missions/Me returns 5 daily + 3 weekly missions with required metadata fields")]
    public async Task T6_MissionsMeReturnsFullMetadata()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t6");

        // Create profile + trigger lazy instantiation
        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token); // triggers lazy instantiation

        var (resp, root, body) = await GetMissionsMeAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);

        // daily array: 5 items
        TryProp(data, "daily", out var dailyArr).Should().BeTrue("body: {0}", body);
        dailyArr.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);
        dailyArr.GetArrayLength().Should().Be(5,
            "GET /Missions/Me must return 5 daily missions; body: {0}", body);

        // weekly array: 3 items
        TryProp(data, "weekly", out var weeklyArr).Should().BeTrue("body: {0}", body);
        weeklyArr.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);
        weeklyArr.GetArrayLength().Should().Be(3,
            "GET /Missions/Me must return 3 weekly missions; body: {0}", body);

        // Each daily item must have required metadata fields
        foreach (var mission in dailyArr.EnumerateArray())
        {
            TryProp(mission, "code",           out _).Should().BeTrue("each mission must have 'code'; body: {0}", body);
            TryProp(mission, "iconKey",        out _).Should().BeTrue("each mission must have 'iconKey'; body: {0}", body);
            TryProp(mission, "titleKey",       out _).Should().BeTrue("each mission must have 'titleKey'; body: {0}", body);
            TryProp(mission, "cadence",        out _).Should().BeTrue("each mission must have 'cadence'; body: {0}", body);
            TryProp(mission, "targetType",     out _).Should().BeTrue("each mission must have 'targetType'; body: {0}", body);
            TryProp(mission, "target",         out _).Should().BeTrue("each mission must have 'target'; body: {0}", body);
            TryProp(mission, "rewardXp",       out _).Should().BeTrue("each mission must have 'rewardXp'; body: {0}", body);
            TryProp(mission, "progress",       out _).Should().BeTrue("each mission must have 'progress'; body: {0}", body);
            TryProp(mission, "status",         out _).Should().BeTrue("each mission must have 'status'; body: {0}", body);
            TryProp(mission, "periodStartUtc", out _).Should().BeTrue("each mission must have 'periodStartUtc'; body: {0}", body);
            TryProp(mission, "periodEndUtc",   out _).Should().BeTrue("each mission must have 'periodEndUtc'; body: {0}", body);
            TryProp(mission, "completedAtUtc", out _).Should().BeTrue("each mission must have 'completedAtUtc'; body: {0}", body);
        }
    }

    // =========================================================================
    // T7 — LessonCompleted event → DAILY_3_LESSONS progress = 1
    // =========================================================================

    [Fact(DisplayName = "P406-T7 LessonCompleted event → DAILY_3_LESSONS mission progress = 1")]
    public async Task T7_LessonCompleted_IncrementsDailyLessonMission()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t7");
        var now = _factory.TestClock.UtcNow;

        // Trigger lazy instantiation via dashboard read
        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token);

        var beforeMissions = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Daily);
        beforeMissions.Should().HaveCount(5, "daily missions must be instantiated before event");

        // Publish one LessonCompleted event
        await PublishLessonCompletedEventAsync(
            studentId:    studentId,
            eventId:      Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:     _mathG1DemoLessonId,
            skillId:      _mathG1DemoLessonSkillId);

        // Verify DAILY_3_LESSONS progress = 1
        var afterMissions = await GetStudentMissionsAsync(studentId);
        var daily3Lessons = afterMissions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_3_LESSONS);
        daily3Lessons.Should().NotBeNull($"{CODE_DAILY_3_LESSONS} must exist");
        daily3Lessons!.Progress.Should().Be(1,
            $"{CODE_DAILY_3_LESSONS} progress must be 1 after one LessonCompleted event");
        daily3Lessons.Status.Should().Be(MissionStatus.InProgress,
            $"{CODE_DAILY_3_LESSONS} must be InProgress (1/3 — not yet complete)");
    }

    // =========================================================================
    // T8 — 3 LessonCompleted events → DAILY_3_LESSONS completes + DAILY_1_LESSON_EARLY too
    // =========================================================================

    [Fact(DisplayName = "P406-T8 3 LessonCompleted events → DAILY_3_LESSONS completes (+30 XP) and DAILY_1_LESSON_EARLY completes (+20 XP)")]
    public async Task T8_ThreeLessons_DailyMissionsComplete_XpAwarded()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t8");
        var now = _factory.TestClock.UtcNow;

        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token); // lazy instantiation

        var profileBefore = await GetProfileAsync(studentId);
        int xpBefore = profileBefore?.TotalXp ?? 0;

        // Complete 3 lessons with distinct EventIds
        for (int i = 0; i < 3; i++)
        {
            await PublishLessonCompletedEventAsync(
                studentId:    studentId,
                eventId:      Guid.NewGuid(),
                occurredOnUtc: now.AddMinutes(i),
                lessonId:     _mathG1DemoLessonId,
                skillId:      _mathG1DemoLessonSkillId);
        }

        var missions = await GetStudentMissionsAsync(studentId);

        // DAILY_3_LESSONS: Progress=3, Status=Completed
        var daily3 = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_3_LESSONS);
        daily3.Should().NotBeNull();
        daily3!.Progress.Should().Be(3, $"{CODE_DAILY_3_LESSONS} must be at Progress=3");
        daily3.Status.Should().Be(MissionStatus.Completed,
            $"{CODE_DAILY_3_LESSONS} must be Completed after 3 lessons");
        daily3.CompletedAtUtc.Should().NotBeNull(
            $"{CODE_DAILY_3_LESSONS}.CompletedAtUtc must be set on completion");

        // XpAward for MissionCompleted (+30 XP) must exist
        var awards = await GetXpAwardsAsync(studentId);
        var missionAwards = awards.Where(a => a.Reason == XpReason.MissionCompleted).ToList();
        missionAwards.Should().Contain(a => a.XpAmount == 30,
            $"{CODE_DAILY_3_LESSONS} completion must award +30 XP (MissionCompleted)");

        // DAILY_1_LESSON_EARLY (target=1): also completes on the first lesson
        var daily1Early = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_1_LESSON_EARLY);
        daily1Early.Should().NotBeNull();
        daily1Early!.Status.Should().Be(MissionStatus.Completed,
            $"{CODE_DAILY_1_LESSON_EARLY} (target=1) must complete after first lesson");
        missionAwards.Should().Contain(a => a.XpAmount == 20,
            $"{CODE_DAILY_1_LESSON_EARLY} completion must award +20 XP");

        // TotalXp must have increased by at least the mission rewards
        var profileAfter = await GetProfileAsync(studentId);
        profileAfter.Should().NotBeNull();
        profileAfter!.TotalXp.Should().BeGreaterThan(xpBefore,
            "TotalXp must increase after mission completions");
        var missionXpTotal = missionAwards.Sum(a => a.XpAmount);
        (profileAfter.TotalXp - xpBefore).Should().BeGreaterThanOrEqualTo(missionXpTotal,
            "TotalXp increase must include all MissionCompleted XP awards");
    }

    // =========================================================================
    // T9 — Idempotency: redelivered event → no double-increment
    // =========================================================================

    [Fact(DisplayName = "P406-T9 Redelivered LessonCompletedIntegrationEvent (same EventId) → DAILY_3_LESSONS progress=1, not 2")]
    public async Task T9_Idempotency_RedeliveredEvent_NoDoubleIncrement()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t9");
        var now = _factory.TestClock.UtcNow;

        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token);

        var sharedEventId = Guid.NewGuid();
        var ev = new LessonCompletedIntegrationEvent(
            EventId:            sharedEventId,
            OccurredOnUtc:      now,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // Publish the same event twice (simulating redelivery)
        using (var scope1 = _factory.Services.CreateScope())
        {
            await scope1.ServiceProvider.GetRequiredService<IPublisher>().Publish(ev);
        }
        using (var scope2 = _factory.Services.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<IPublisher>().Publish(ev);
        }

        var missions = await GetStudentMissionsAsync(studentId);
        var daily3 = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_3_LESSONS);
        daily3.Should().NotBeNull();
        daily3!.Progress.Should().Be(1,
            "redelivered event with same EventId must not double-increment mission progress");

        // Also verify MissionProgressLog has exactly 1 row per (missionId, eventId)
        var logs = await GetMissionProgressLogsAsync(studentId);
        var logsForEvent = logs.Where(l => l.OriginEventId == sharedEventId).ToList();
        // Each active mission of CompleteLessons type will have exactly one log entry per eventId
        logsForEvent.Should().HaveCountLessOrEqualTo(
            5, // at most one per daily CompleteLessons mission (DAILY_3_LESSONS + DAILY_1_LESSON_EARLY)
            "idempotency: at most one progress log per (missionId, eventId)");
        logsForEvent.Select(l => l.StudentMissionId).Should().OnlyHaveUniqueItems(
            "each mission can have at most one progress log per originEventId");
    }

    // =========================================================================
    // T10 — AnswerSubmitted correct → DAILY_10_CORRECT progress = 1
    // =========================================================================

    [Fact(DisplayName = "P406-T10 AnswerSubmitted with CorrectAnswerCount=1 → DAILY_10_CORRECT progress = 1")]
    public async Task T10_CorrectAnswer_IncrementsCorrectAnswerMission()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t10");
        var now = _factory.TestClock.UtcNow;

        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token);

        await PublishAnswerSubmittedEventAsync(
            studentId:          studentId,
            eventId:            Guid.NewGuid(),
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 1);

        var missions = await GetStudentMissionsAsync(studentId);
        var daily10 = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_10_CORRECT);
        daily10.Should().NotBeNull();
        daily10!.Progress.Should().Be(1,
            $"{CODE_DAILY_10_CORRECT} progress must be 1 after one correct answer");
    }

    // =========================================================================
    // T11 — AnswerSubmitted wrong (CorrectAnswerCount=0) → no mission progress
    // =========================================================================

    [Fact(DisplayName = "P406-T11 AnswerSubmitted with CorrectAnswerCount=0 (wrong) → DAILY_10_CORRECT progress stays at 0")]
    public async Task T11_WrongAnswer_NoMissionProgress()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t11");
        var now = _factory.TestClock.UtcNow;

        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token);

        // Publish a wrong answer (CorrectAnswerCount=0)
        await PublishAnswerSubmittedEventAsync(
            studentId:          studentId,
            eventId:            Guid.NewGuid(),
            occurredOnUtc:      now,
            lessonId:           _mathG1DemoLessonId,
            skillId:            _mathG1DemoLessonSkillId,
            correctAnswerCount: 0);

        var missions = await GetStudentMissionsAsync(studentId);
        var daily10 = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_10_CORRECT);
        daily10.Should().NotBeNull();
        daily10!.Progress.Should().Be(0,
            $"wrong answer (CorrectAnswerCount=0) must NOT increment {CODE_DAILY_10_CORRECT}");

        // All missions must remain at Progress=0
        missions.Should().AllSatisfy(m =>
            m.Progress.Should().Be(0,
                $"wrong answer must not increment any mission; mission {m.MissionDefinition.Code}"));
    }

    // =========================================================================
    // T12 — Streak advance → DAILY_KEEP_STREAK completes (target=1), +50 XP
    // =========================================================================

    [Fact(DisplayName = "P406-T12 Streak advance fires StreakAdvancedMissionHandler → DAILY_KEEP_STREAK completes (+50 XP)")]
    public async Task T12_StreakAdvance_KeepStreakMissionCompletes()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t12");
        var now = _factory.TestClock.UtcNow;

        // Seed profile with streak=2, last activity = yesterday so today's lesson advances streak
        var yesterday = DateOnly.FromDateTime(now.AddDays(-1));
        await SeedProfileAsync(
            studentId:          studentId,
            currentStreak:      2,
            longestStreak:      2,
            lastActivityDateUtc: yesterday);
        _factory.TestClock.SetUtcNow(now);

        // Trigger lazy instantiation
        await GetDashboardAsync(token);

        // Publish lesson completed → triggers LessonCompletedIntegrationEventHandler → AdvanceStreakCommand
        // → streak becomes 3 → StreakAdvancedDomainEvent → StreakAdvancedMissionHandler
        await PublishLessonCompletedEventAsync(
            studentId:    studentId,
            eventId:      Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:     _mathG1DemoLessonId,
            skillId:      _mathG1DemoLessonSkillId);

        var missions = await GetStudentMissionsAsync(studentId);
        var keepStreak = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_KEEP_STREAK);
        keepStreak.Should().NotBeNull();
        keepStreak!.Progress.Should().Be(1,
            $"{CODE_DAILY_KEEP_STREAK} (target=1) must reach Progress=1 when streak is advanced");
        keepStreak.Status.Should().Be(MissionStatus.Completed,
            $"{CODE_DAILY_KEEP_STREAK} must be Completed (target=1 reached)");

        // +50 XP for DAILY_KEEP_STREAK completion
        var awards = await GetXpAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.MissionCompleted && a.XpAmount == 50,
            $"{CODE_DAILY_KEEP_STREAK} completion must award +50 XP");
    }

    // =========================================================================
    // T13 — Practice Mode: lesson completion still counts toward DAILY_3_LESSONS
    // =========================================================================

    [Fact(DisplayName = "P406-T13 Practice Mode (Hearts=0): lesson completion still advances DAILY_3_LESSONS (D3 decision)")]
    public async Task T13_PracticeMode_LessonCompletionCountsMissionProgress()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t13");
        var now = _factory.TestClock.UtcNow;

        // Seed Hearts=0 (Practice Mode)
        await SeedProfileAsync(
            studentId: studentId,
            hearts:    0,
            lastHeartRefillAtUtc: now.AddMinutes(-5));

        await GetDashboardAsync(token); // lazy instantiation

        await PublishLessonCompletedEventAsync(
            studentId:    studentId,
            eventId:      Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:     _mathG1DemoLessonId,
            skillId:      _mathG1DemoLessonSkillId);

        // DAILY_3_LESSONS must progress even in Practice Mode (D3 decision)
        var missions = await GetStudentMissionsAsync(studentId);
        var daily3 = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_3_LESSONS);
        daily3.Should().NotBeNull();
        daily3!.Progress.Should().Be(1,
            "lesson completion in Practice Mode must still advance CompleteLessons missions (D3)");

        // DAILY_KEEP_STREAK should NOT progress because StreakAdvancedDomainEvent is not raised in PM
        var keepStreak = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_KEEP_STREAK);
        keepStreak.Should().NotBeNull();
        keepStreak!.Progress.Should().Be(0,
            "MaintainStreak mission must NOT progress in Practice Mode (StreakAdvanced by-construction absent)");
    }

    // =========================================================================
    // T14 — Daily rollover job → past-period Active row → Expired
    // =========================================================================

    [Fact(DisplayName = "P406-T14 MissionRolloverJob.RunAsync → past-period Active StudentMission row flipped to Expired")]
    public async Task T14_RolloverJob_PastActiveRow_FlipsToExpired()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t14");

        await SeedProfileAsync(studentId: studentId);

        // Seed a past-period active mission row directly
        var pastMissionId = await SeedPastStudentMissionAsync(
            studentId:    studentId,
            missionCode:  CODE_DAILY_3_LESSONS,
            status:       MissionStatus.NotStarted);

        // Verify it exists and is NotStarted before the job runs
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
            var pastMission = await db.StudentMissions.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == pastMissionId);
            pastMission.Should().NotBeNull("past mission must be seeded");
            pastMission!.Status.Should().Be(MissionStatus.NotStarted,
                "past mission must be NotStarted before rollover job");
        }

        // Run the rollover job
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<MissionRolloverJob>();
            await job.RunAsync(MissionType.Daily, CancellationToken.None);
        }

        // Assert the past-period row is now Expired
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
            var afterMission = await db.StudentMissions.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == pastMissionId);
            afterMission.Should().NotBeNull();
            afterMission!.Status.Should().Be(MissionStatus.Expired,
                "MissionRolloverJob must flip past-period Active row to Expired");
        }
    }

    // =========================================================================
    // T15 — Rollover idempotent: run twice → same status, no exception
    // =========================================================================

    [Fact(DisplayName = "P406-T15 MissionRolloverJob is idempotent: running twice leaves the row Expired with no exception")]
    public async Task T15_RolloverJob_Idempotent_RunTwice()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t15");
        await SeedProfileAsync(studentId: studentId);

        var pastMissionId = await SeedPastStudentMissionAsync(
            studentId:   studentId,
            missionCode: CODE_DAILY_3_LESSONS,
            status:      MissionStatus.NotStarted);

        // Run twice
        for (int i = 0; i < 2; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<MissionRolloverJob>();
            var act = () => job.RunAsync(MissionType.Daily, CancellationToken.None);
            await act.Should().NotThrowAsync($"run {i + 1} must not throw");
        }

        // Still Expired
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
            var afterMission = await db.StudentMissions.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == pastMissionId);
            afterMission!.Status.Should().Be(MissionStatus.Expired,
                "row must remain Expired after second run (idempotent)");
        }
    }

    // =========================================================================
    // T16 — Rollover does NOT affect active current-period rows
    // =========================================================================

    [Fact(DisplayName = "P406-T16 MissionRolloverJob does NOT expire current-period (active/future) rows")]
    public async Task T16_RolloverJob_DoesNotAffectCurrentPeriodRows()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t16");
        await SeedProfileAsync(studentId: studentId);

        // Trigger lazy instantiation of current-period missions
        await GetDashboardAsync(token);

        var currentDailyBefore = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Daily);
        currentDailyBefore.Should().HaveCount(5, "5 current daily missions must exist before rollover");
        currentDailyBefore.Should().AllSatisfy(m =>
            m.Status.Should().NotBe(MissionStatus.Expired,
                $"current-period mission {m.MissionDefinition.Code} must be Active before rollover"));

        // Run rollover job
        using (var scope = _factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<MissionRolloverJob>();
            await job.RunAsync(MissionType.Daily, CancellationToken.None);
        }

        // Current-period rows must remain Active/NotStarted
        var currentDailyAfter = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Daily);
        currentDailyAfter.Should().HaveCount(5, "5 current daily missions must still exist after rollover");
        currentDailyAfter.Should().AllSatisfy(m =>
            m.Status.Should().NotBe(MissionStatus.Expired,
                $"current-period mission {m.MissionDefinition.Code} must NOT be expired by rollover"));
    }

    // =========================================================================
    // T17 — IDOR: dashboard missions scoped to JWT
    // =========================================================================

    [Fact(DisplayName = "P406-T17 IDOR: Student A and B see independent daily mission lists on dashboard")]
    public async Task T17_IDOR_DashboardMissionsIsolation()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("t17a");
        var (tokenB, _) = await CreateStudentViaParentFlowAsync("t17b");

        // Seed profiles for both students
        await SeedProfileAsync(studentId: studentIdA);

        // Student A reads dashboard → A's missions instantiated
        var (dashA, rootA, bodyA) = await GetDashboardAsync(tokenA);
        dashA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        TryProp(dataA, "dailyMissions", out var dailyA).Should().BeTrue("body: {0}", bodyA);
        // A should have missions (profile was seeded)
        dailyA.ValueKind.Should().Be(JsonValueKind.Array,
            "A's dailyMissions must be an array; body: {0}", bodyA);
        dailyA.GetArrayLength().Should().Be(5,
            "A must have 5 daily missions; body: {0}", bodyA);

        // Student B reads dashboard → B's response (no profile seeded for B)
        var (dashB, rootB, bodyB) = await GetDashboardAsync(tokenB);
        dashB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB);
        TryProp(rootB, "data", out var dataB).Should().BeTrue("body: {0}", bodyB);
        TryProp(dataB, "dailyMissions", out var dailyB).Should().BeTrue("body: {0}", bodyB);
        // B should have no missions (no profile)
        if (dailyB.ValueKind == JsonValueKind.Array)
        {
            dailyB.GetArrayLength().Should().Be(0,
                "B must NOT see A's missions — IDOR isolation; body: {0}", bodyB);
        }
        else
        {
            dailyB.ValueKind.Should().Be(JsonValueKind.Null,
                "B must NOT see A's missions (IDOR); body: {0}", bodyB);
        }
    }

    // =========================================================================
    // T18 — IDOR: /Missions/Me scoped to JWT
    // =========================================================================

    [Fact(DisplayName = "P406-T18 IDOR: A's JWT → A's missions; B's JWT → empty lists (isolation)")]
    public async Task T18_IDOR_MissionsMeIsolation()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("t18a");
        var (tokenB, _) = await CreateStudentViaParentFlowAsync("t18b");

        // Seed profile for A and trigger instantiation
        await SeedProfileAsync(studentId: studentIdA);
        await GetDashboardAsync(tokenA);

        // A's /Missions/Me → 5 daily + 3 weekly
        var (respA, rootA, bodyA) = await GetMissionsMeAsync(tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        TryProp(dataA, "daily", out var dailyA).Should().BeTrue("body: {0}", bodyA);
        dailyA.GetArrayLength().Should().Be(5, "A must see 5 daily missions; body: {0}", bodyA);

        // B's /Missions/Me → empty (no profile)
        var (respB, rootB, bodyB) = await GetMissionsMeAsync(tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB);
        TryProp(rootB, "data", out var dataB).Should().BeTrue("body: {0}", bodyB);
        TryProp(dataB, "daily", out var dailyB).Should().BeTrue("body: {0}", bodyB);
        dailyB.GetArrayLength().Should().Be(0,
            "B must NOT see A's daily missions — IDOR isolation; body: {0}", bodyB);
    }

    // =========================================================================
    // T19 — Mission completion chain: bonus XP triggers level-up
    // =========================================================================

    [Fact(DisplayName = "P406-T19 DAILY_3_LESSONS completion (+30 XP) at TotalXp near L2 threshold → TotalXp increases by at least 30")]
    public async Task T19_MissionCompletion_XpIncrease()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t19");
        var now = _factory.TestClock.UtcNow;

        // Seed profile close to level 2 threshold (100 XP by LevelCurve).
        // With totalXp=90, after lesson XP (≈50) + mission reward (30), total ≈ 170 → well past level 2.
        await SeedProfileAsync(
            studentId:    studentId,
            totalXp:      90,
            currentLevel: 1);

        await GetDashboardAsync(token); // lazy instantiation

        var profileBefore = await GetProfileAsync(studentId);
        int xpBefore = profileBefore!.TotalXp;

        // Complete 3 lessons to trigger DAILY_3_LESSONS completion (+30 XP)
        for (int i = 0; i < 3; i++)
        {
            await PublishLessonCompletedEventAsync(
                studentId:    studentId,
                eventId:      Guid.NewGuid(),
                occurredOnUtc: now.AddSeconds(i),
                lessonId:     _mathG1DemoLessonId,
                skillId:      _mathG1DemoLessonSkillId,
                accuracyPct:  100,
                correctCount: 4);
        }

        var profileAfter = await GetProfileAsync(studentId);
        profileAfter.Should().NotBeNull();

        // TotalXp must have increased by at least the mission reward (30 XP)
        (profileAfter!.TotalXp - xpBefore).Should().BeGreaterThanOrEqualTo(30,
            "TotalXp must increase by at least the 30 XP mission reward");

        // DAILY_3_LESSONS must be completed
        var missions = await GetStudentMissionsAsync(studentId);
        var daily3 = missions.FirstOrDefault(m => m.MissionDefinition.Code == CODE_DAILY_3_LESSONS);
        daily3!.Status.Should().Be(MissionStatus.Completed);

        // XpAward with MissionCompleted reason must exist
        var awards = await GetXpAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.MissionCompleted && a.XpAmount == 30,
            "DAILY_3_LESSONS completion must persist XpAward(MissionCompleted, 30)");
    }

    // =========================================================================
    // T20 — Weekly mission accumulates across days
    // =========================================================================

    [Fact(DisplayName = "P406-T20 WEEKLY_15_LESSONS accumulates progress across multiple lessons; weekly period persists across day boundary")]
    public async Task T20_WeeklyMission_AccumulatesAcrossDays()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t20");
        var now = _factory.TestClock.UtcNow;

        await SeedProfileAsync(studentId: studentId);
        _factory.TestClock.SetUtcNow(now);
        await GetDashboardAsync(token); // lazy instantiation

        // Complete 5 lessons today
        for (int i = 0; i < 5; i++)
        {
            await PublishLessonCompletedEventAsync(
                studentId:    studentId,
                eventId:      Guid.NewGuid(),
                occurredOnUtc: now.AddMinutes(i),
                lessonId:     _mathG1DemoLessonId,
                skillId:      _mathG1DemoLessonSkillId);
        }

        var weeklyRowsDay1 = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Weekly);
        var weekly15Day1 = weeklyRowsDay1.FirstOrDefault(m => m.MissionDefinition.Code == CODE_WEEKLY_15_LESSONS);
        weekly15Day1.Should().NotBeNull();
        weekly15Day1!.Progress.Should().Be(5,
            $"{CODE_WEEKLY_15_LESSONS} must be at Progress=5 after 5 lessons on day 1");
        weekly15Day1.Status.Should().Be(MissionStatus.InProgress,
            $"{CODE_WEEKLY_15_LESSONS} must be InProgress (5/15 — not complete)");

        // Advance time to tomorrow (within same ISO week)
        _factory.TestClock.SetUtcNow(now.AddDays(1));

        // Read dashboard tomorrow — new daily missions instantiated; weekly continues
        await GetDashboardAsync(token);

        // Complete 5 more lessons tomorrow
        for (int i = 0; i < 5; i++)
        {
            await PublishLessonCompletedEventAsync(
                studentId:    studentId,
                eventId:      Guid.NewGuid(),
                occurredOnUtc: _factory.TestClock.UtcNow.AddMinutes(i),
                lessonId:     _mathG1DemoLessonId,
                skillId:      _mathG1DemoLessonSkillId);
        }

        // Weekly mission must now have Progress=10 (5 + 5)
        var weeklyRowsDay2 = await GetCurrentPeriodMissionsAsync(studentId, MissionType.Weekly);
        var weekly15Day2 = weeklyRowsDay2.FirstOrDefault(m => m.MissionDefinition.Code == CODE_WEEKLY_15_LESSONS);
        weekly15Day2.Should().NotBeNull();
        weekly15Day2!.Progress.Should().Be(10,
            $"{CODE_WEEKLY_15_LESSONS} must accumulate to Progress=10 across two days (5+5); " +
            "weekly period persists across day boundaries");
        weekly15Day2.Status.Should().Be(MissionStatus.InProgress,
            $"{CODE_WEEKLY_15_LESSONS} must still be InProgress (10/15)");
    }

    // =========================================================================
    // T21 — Enum serialization smoke test (end-to-end int values)
    // =========================================================================

    [Fact(DisplayName = "P406-T21 GET /Missions/Me response has correct int values for status (0=NotStarted) and targetType (1=CompleteLessons)")]
    public async Task T21_EnumSerializationSmoke_CorrectIntValues()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t21");

        await SeedProfileAsync(studentId: studentId);
        await GetDashboardAsync(token); // lazy instantiation

        var (resp, root, body) = await GetMissionsMeAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "daily", out var dailyArr).Should().BeTrue("body: {0}", body);
        dailyArr.GetArrayLength().Should().BeGreaterThan(0,
            "must have daily missions to test enum values; body: {0}", body);

        // Find DAILY_3_LESSONS (CompleteLessons=1, not-started=0)
        var daily3 = dailyArr.EnumerateArray()
            .FirstOrDefault(m =>
            {
                TryProp(m, "code", out var c);
                return c.GetString() == CODE_DAILY_3_LESSONS;
            });

        TryProp(daily3, "status", out var statusProp).Should().BeTrue(
            "DAILY_3_LESSONS must have a status field; body: {0}", body);
        statusProp.GetInt32().Should().Be(0,
            "NotStarted (0) must be serialized as integer 0 — enum drift check; body: {0}", body);

        TryProp(daily3, "targetType", out var targetTypeProp).Should().BeTrue(
            "DAILY_3_LESSONS must have a targetType field; body: {0}", body);
        targetTypeProp.GetInt32().Should().Be(1,
            "CompleteLessons (1) must be serialized as integer 1 — enum drift check; body: {0}", body);
    }

    // =========================================================================
    // Envelope sanity: BaseResponse<T> shape + P4-06 fields present in responses
    // =========================================================================

    [Fact(DisplayName = "P406-Env Dashboard envelope has 'successed', 'statusCode', 'message', 'data', 'errors' + mission fields")]
    public async Task Env_DashboardEnvelope_MissionFieldsPresent()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("tenv");

        // Seed profile to ensure mission fields are populated
        await SeedProfileAsync(studentId: studentId);

        var (resp, root, body) = await GetDashboardAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // BaseResponse<T> envelope shape
        body.Should().Contain("\"successed\":",
            "'successed' (lowercase-d camelCase) must appear in the response; body: {0}", body);

        TryProp(root, "successed",  out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "statusCode", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("body: {0}", body);

        // P4-06 additive fields
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "dailyMissions",  out _).Should().BeTrue(
            "data.dailyMissions must be present (P4-06 additive field); body: {0}", body);
        TryProp(data, "weeklyMission",  out _).Should().BeTrue(
            "data.weeklyMission must be present (P4-06 additive field); body: {0}", body);

        // Legacy P4-02/03/04/05 fields still present (non-regression)
        TryProp(data, "xp",            out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "streak",        out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "level",         out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "hearts",        out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "inPracticeMode",out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "badgesCount",   out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "recentBadges",  out _).Should().BeTrue("body: {0}", body);
    }

    // =========================================================================
    // Seeder idempotency: running MissionSeeder twice → still exactly 8 rows
    // =========================================================================

    [Fact(DisplayName = "P406-Seed MissionSeeder is idempotent — running twice does not duplicate rows")]
    public async Task Seed_IdempotencyRunTwice_Still8Rows()
    {
        // Run the seeder a second time (first was in InitializeAsync)
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<MissionSeeder>();
        await seeder.SeedAsync(); // second run

        using var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var count = await db.MissionDefinitions.AsNoTracking().CountAsync();
        count.Should().Be(8,
            "MissionSeeder upsert by Code must not duplicate rows on second run");
    }
}

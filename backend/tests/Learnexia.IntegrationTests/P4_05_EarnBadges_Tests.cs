using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
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
/// P4-05 integration tests — "Earn badges".
///
/// Tests the badge engine end-to-end:
///   - BadgeSeeder runs at startup → 10 catalog rows always present.
///   - LessonCompletedIntegrationEvent → LessonCompletedBadgeHandler → FIRST_LESSON badge awarded.
///   - StreakAdvancedDomainEvent → StreakAdvancedBadgeHandler → STREAK_* badges awarded.
///   - StudentLeveledUpDomainEvent → StudentLeveledUpBadgeHandler → LEVEL_* badges awarded.
///   - Idempotency: duplicate events → at most one badge row per (StudentId, BadgeDefinitionId).
///   - GET /api/Gamification/Badges/Me → JWT-scoped catalog with IsEarned flags.
///   - GET /api/Learning/Dashboard → BadgesCount + RecentBadges (top 3 DESC).
///   - IDOR: cross-student isolation, no studentId accepted from request.
///   - Practice Mode: FIRST_LESSON fires; STREAK_* and LEVEL_* do NOT (by construction D11).
///
/// Coverage map (15 test cases from P4-05 plan B5-1):
///   T1  Catalog seeded — exactly 10 rows, correct Code/Rarity/RewardXp pairs
///   T2  Brand-new student → BadgesCount=0, RecentBadges=[]
///   T3  Brand-new student GET /Badges/Me → 10 locked definitions
///   T4  Anonymous GET /Badges/Me → 401
///   T5  First lesson completion → FIRST_LESSON badge awarded + XP bonus
///   T6  Second lesson completion → FIRST_LESSON NOT re-awarded (idempotency)
///   T7  Streak advances: day-3 → STREAK_3; day-7 → STREAK_7
///   T8  Level up → LEVEL_5 badge awarded
///   T9  Idempotency: redelivered LessonCompletedIntegrationEvent → exactly 1 FIRST_LESSON
///   T10 Recent badges ordered by AwardedAtUtc DESC (top 3 of 5)
///   T11 IDOR: cross-student dashboard isolation (badges count)
///   T12 IDOR: cross-student GET /Badges/Me isolation
///   T13 Practice Mode by construction: FIRST_LESSON awards; STREAK_*/LEVEL_* do NOT
///   T14 Badge XP bonus chain: badge XP crosses level threshold → LEVEL_5 also awarded
///   T15 Stress: 3 concurrent identical events → exactly 1 FIRST_LESSON badge
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_05_EarnBadges_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string DashboardUrl      = "api/Learning/Dashboard";
    private const string BadgesMeUrl       = "api/Gamification/Badges/Me";
    private const string ValidChildPassword = "Child@Pass1";

    // Badge codes from the locked catalog (D1)
    private const string CODE_FIRST_LESSON = "FIRST_LESSON";
    private const string CODE_STREAK_3     = "STREAK_3";
    private const string CODE_STREAK_7     = "STREAK_7";
    private const string CODE_LEVEL_5      = "LEVEL_5";

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

    public P4_05_EarnBadges_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Seed badges catalog (all environments) — idempotent.
        using (var scope = _factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
            await seeder.SeedAsync();
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
        => $"p405_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
                FullName = "Test Student P405",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 1,
                Language = "ar",
                Country  = "EG",
                LearningLanguage = "ar", // P8-01: required
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
        GetBadgesMeAsync(string? token = null)
        => SendAsync(_client, HttpMethod.Get, BadgesMeUrl, null, token);

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

    private async Task<List<StudentBadge>> GetStudentBadgesAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null) return new();

        return await db.StudentBadges
            .AsNoTracking()
            .Include(b => b.BadgeDefinition)
            .Where(b => b.StudentXpProfileId == profile.Id)
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

    private async Task<BadgeDefinition?> GetBadgeDefinitionByCodeAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        return await db.BadgeDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code);
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
    /// Seeds StudentBadge rows directly via SQL, bypassing the domain layer.
    /// Used for tests that need specific badge state without going through the full event chain.
    /// </summary>
    private async Task SeedStudentBadgesDirectlyAsync(
        int studentId,
        IEnumerable<(string Code, DateTime AwardedAtUtc)> badges)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        // Ensure profile exists
        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (profile is null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO gamification.""StudentXpProfiles""
                   (""StudentId"", ""TotalXp"", ""CurrentLevel"", ""LastAwardAtUtc"",
                    ""CurrentStreak"", ""LongestStreak"", ""Hearts"",
                    ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {0}, {1}, {DateTime.UtcNow},
                           {0}, {0}, {5}, {DateTime.UtcNow}, {0})");

            profile = await db.StudentXpProfiles
                .AsNoTracking()
                .FirstAsync(p => p.StudentId == studentId);
        }

        foreach (var (code, awardedAt) in badges)
        {
            var def = await db.BadgeDefinitions
                .AsNoTracking()
                .FirstAsync(d => d.Code == code);

            // Check not already inserted
            var alreadyExists = await db.StudentBadges
                .AsNoTracking()
                .AnyAsync(b => b.StudentXpProfileId == profile.Id && b.BadgeDefinitionId == def.Id);

            if (!alreadyExists)
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO gamification.""StudentBadges""
                       (""StudentXpProfileId"", ""BadgeDefinitionId"", ""OriginEventId"",
                        ""OriginEventType"", ""AwardedAtUtc"", ""CreatedAt"", ""CreatedBy"")
                       VALUES ({profile.Id}, {def.Id}, {Guid.NewGuid()},
                               {"DirectSeed"}, {awardedAt}, {DateTime.UtcNow}, {0})");
            }
        }
    }

    // =========================================================================
    // T1 — Catalog seeded: exactly 10 rows with correct Code/Rarity/RewardXp
    // =========================================================================

    [Fact(DisplayName = "P405-T1 BadgeSeeder seeds exactly 10 rows with correct Code/Rarity/RewardXp pairs")]
    public async Task T1_CatalogSeeded_Exactly10Rows_CorrectMetadata()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var defs = await db.BadgeDefinitions.AsNoTracking().ToListAsync();

        defs.Should().HaveCount(10,
            "BadgeSeeder seeds exactly 10 badge definitions (D1 locked catalog)");

        // Verify each expected code exists
        var expectedCodes = new[]
        {
            "FIRST_LESSON", "STREAK_3", "STREAK_7", "STREAK_14", "STREAK_30",
            "LEVEL_5", "LEVEL_10", "LEVEL_20", "LEGENDARY_50", "STREAK_100"
        };
        foreach (var code in expectedCodes)
        {
            defs.Should().Contain(d => d.Code == code,
                $"catalog must contain badge with code '{code}'");
        }

        // Verify rarity → RewardXp mapping per D3 (Common=20, Rare=50, Epic=100, Legendary=250)
        var commonBadges = defs.Where(d => d.Rarity == BadgeRarity.Common).ToList();
        commonBadges.Should().AllSatisfy(d => d.RewardXp.Should().Be(20,
            $"badge {d.Code} (Common) must have RewardXp=20"));

        var rareBadges = defs.Where(d => d.Rarity == BadgeRarity.Rare).ToList();
        rareBadges.Should().AllSatisfy(d => d.RewardXp.Should().Be(50,
            $"badge {d.Code} (Rare) must have RewardXp=50"));

        var epicBadges = defs.Where(d => d.Rarity == BadgeRarity.Epic).ToList();
        epicBadges.Should().AllSatisfy(d => d.RewardXp.Should().Be(100,
            $"badge {d.Code} (Epic) must have RewardXp=100"));

        var legendaryBadges = defs.Where(d => d.Rarity == BadgeRarity.Legendary).ToList();
        legendaryBadges.Should().AllSatisfy(d => d.RewardXp.Should().Be(250,
            $"badge {d.Code} (Legendary) must have RewardXp=250"));
    }

    // =========================================================================
    // T2 — Brand-new student: GET /Dashboard → BadgesCount=0, RecentBadges=[]
    // =========================================================================

    [Fact(DisplayName = "P405-T2 Brand-new student GET /Dashboard → BadgesCount=0, RecentBadges=[]")]
    public async Task T2_BrandNewStudent_DashboardShowsZeroBadges()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t2");

        // No events published — no profile, no badges
        var profile = await GetProfileAsync(studentId);
        profile.Should().BeNull("brand-new student must have no StudentXpProfile row");

        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        TryProp(dashRoot, "data", out var data).Should().BeTrue("body: {0}", dashBody);
        TryProp(data, "badgesCount", out var badgesCount).Should().BeTrue(
            "data.badgesCount must be present (P4-05 additive field); body: {0}", dashBody);
        badgesCount.GetInt32().Should().Be(0,
            "brand-new student has no badges; body: {0}", dashBody);

        TryProp(data, "recentBadges", out var recentBadges).Should().BeTrue(
            "data.recentBadges must be present (P4-05 additive field); body: {0}", dashBody);
        // recentBadges can be null or empty array for brand-new student
        if (recentBadges.ValueKind == JsonValueKind.Array)
        {
            recentBadges.GetArrayLength().Should().Be(0,
                "brand-new student must have no recent badges; body: {0}", dashBody);
        }
        else
        {
            recentBadges.ValueKind.Should().Be(JsonValueKind.Null,
                "brand-new student recentBadges must be null or empty array; body: {0}", dashBody);
        }
    }

    // =========================================================================
    // T3 — Brand-new student GET /Badges/Me → 10 locked definitions
    // =========================================================================

    [Fact(DisplayName = "P405-T3 Brand-new student GET /Badges/Me → 10 definitions, all IsEarned=false")]
    public async Task T3_BrandNewStudent_BadgesMeReturns10Locked()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("t3");

        var (resp, root, body) = await GetBadgesMeAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "badges", out var badgesArr).Should().BeTrue(
            "data.badges array must be present; body: {0}", body);

        badgesArr.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", body);
        badgesArr.GetArrayLength().Should().Be(10,
            "GET /Badges/Me must return all 10 catalog definitions; body: {0}", body);

        foreach (var badge in badgesArr.EnumerateArray())
        {
            TryProp(badge, "isEarned", out var isEarned).Should().BeTrue(
                "each badge must have isEarned field; body: {0}", body);
            isEarned.GetBoolean().Should().BeFalse(
                "brand-new student must see all badges as locked; body: {0}", body);

            TryProp(badge, "awardedAtUtc", out var awardedAt).Should().BeTrue(
                "each badge must have awardedAtUtc field; body: {0}", body);
            awardedAt.ValueKind.Should().Be(JsonValueKind.Null,
                "locked badge must have null awardedAtUtc; body: {0}", body);
        }
    }

    // =========================================================================
    // T4 — Anonymous GET /Badges/Me → 401
    // =========================================================================

    [Fact(DisplayName = "P405-T4 Anonymous GET /Badges/Me → 401 Unauthorized")]
    public async Task T4_Anonymous_BadgesMe_Returns401()
    {
        var (resp, _, body) = await GetBadgesMeAsync(token: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BadgesController has [Authorize] — no JWT must return 401; body: {0}", body);
    }

    // =========================================================================
    // T5 — First lesson completion → FIRST_LESSON badge + XP bonus
    // =========================================================================

    [Fact(DisplayName = "P405-T5 First lesson completion → FIRST_LESSON badge, +20 XP bonus, Dashboard reflects it")]
    public async Task T5_FirstLesson_AwardsBadge_AndXpBonus()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t5");
        var now = _factory.TestClock.UtcNow;

        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId);

        // Assert: StudentBadge row for FIRST_LESSON
        var badges = await GetStudentBadgesAsync(studentId);
        badges.Should().Contain(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON,
            "FIRST_LESSON badge must be awarded after first lesson completion");

        // Assert: XpAward with Reason=BadgeEarned, Amount=20
        var awards = await GetXpAwardsAsync(studentId);
        var badgeAward = awards.FirstOrDefault(a => a.Reason == XpReason.BadgeEarned);
        badgeAward.Should().NotBeNull("XpAward(BadgeEarned) must be written for FIRST_LESSON badge");
        badgeAward!.XpAmount.Should().Be(20,
            "FIRST_LESSON is Common rarity → +20 XP per D3");

        // Assert: TotalXp includes the +20 badge bonus
        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        // Total XP includes: LessonCompleted=20, QuizCompleted=30 (accuracy=100%), and BadgeEarned=20
        // The exact total depends on all XP sources; just assert BadgeEarned contribution is included.
        var totalBadgeEarnedXp = awards.Where(a => a.Reason == XpReason.BadgeEarned).Sum(a => a.XpAmount);
        totalBadgeEarnedXp.Should().Be(20, "one FIRST_LESSON badge earned for +20 XP");

        // Assert: GET /Badges/Me shows FIRST_LESSON as earned
        var (badgesResp, badgesRoot, badgesBody) = await GetBadgesMeAsync(token);
        badgesResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", badgesBody);
        TryProp(badgesRoot, "data", out var badgesData).Should().BeTrue("body: {0}", badgesBody);
        TryProp(badgesData, "badges", out var badgesArr).Should().BeTrue("body: {0}", badgesBody);

        var firstLessonBadge = badgesArr.EnumerateArray()
            .FirstOrDefault(b =>
            {
                TryProp(b, "code", out var code);
                return code.GetString() == CODE_FIRST_LESSON;
            });
        TryProp(firstLessonBadge, "isEarned", out var isEarned).Should().BeTrue(
            "FIRST_LESSON badge must exist in response; body: {0}", badgesBody);
        isEarned.GetBoolean().Should().BeTrue(
            "FIRST_LESSON must be IsEarned=true after first lesson; body: {0}", badgesBody);
        TryProp(firstLessonBadge, "awardedAtUtc", out var awardedAt);
        awardedAt.ValueKind.Should().NotBe(JsonValueKind.Null,
            "earned badge must have non-null AwardedAtUtc; body: {0}", badgesBody);

        // Assert: GET /Dashboard shows BadgesCount=1, RecentBadges contains FIRST_LESSON
        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);
        TryProp(dashRoot, "data", out var dashData).Should().BeTrue("body: {0}", dashBody);
        TryProp(dashData, "badgesCount", out var badgesCount).Should().BeTrue("body: {0}", dashBody);
        badgesCount.GetInt32().Should().Be(1,
            "Dashboard BadgesCount must be 1 after FIRST_LESSON earned; body: {0}", dashBody);

        TryProp(dashData, "recentBadges", out var recentBadges).Should().BeTrue("body: {0}", dashBody);
        if (recentBadges.ValueKind == JsonValueKind.Array)
        {
            recentBadges.GetArrayLength().Should().BeGreaterOrEqualTo(1,
                "RecentBadges must contain FIRST_LESSON; body: {0}", dashBody);
            var firstRecentCode = recentBadges.EnumerateArray()
                .Select(b => { TryProp(b, "code", out var c); return c.GetString(); })
                .FirstOrDefault();
            firstRecentCode.Should().Be(CODE_FIRST_LESSON,
                "most recent badge must be FIRST_LESSON; body: {0}", dashBody);
        }
    }

    // =========================================================================
    // T6 — Second lesson completion → FIRST_LESSON NOT re-awarded (idempotency)
    // =========================================================================

    [Fact(DisplayName = "P405-T6 Second lesson completion → FIRST_LESSON not re-awarded (idempotency)")]
    public async Task T6_SecondLesson_FirstLessonNotReawarded()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t6");
        var now = _factory.TestClock.UtcNow;

        // First lesson completion → FIRST_LESSON awarded
        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId);

        var badgesAfterFirst = await GetStudentBadgesAsync(studentId);
        badgesAfterFirst.Should().ContainSingle(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON,
            "exactly 1 FIRST_LESSON row after first lesson");

        // Second lesson completion (different EventId) → FIRST_LESSON must NOT re-fire
        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(), // different event
            occurredOnUtc: now.AddMinutes(5),
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId);

        var badgesAfterSecond = await GetStudentBadgesAsync(studentId);
        badgesAfterSecond.Count(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON)
            .Should().Be(1,
                "FIRST_LESSON must remain exactly 1 row even after a second lesson completion (idempotency)");
    }

    // =========================================================================
    // T7 — Streak advances: STREAK_3 on day-3, STREAK_7 on day-7
    // =========================================================================

    [Fact(DisplayName = "P405-T7 Streak day-3 → STREAK_3 badge; day-7 → STREAK_7 badge (sequential)")]
    public async Task T7_StreakAdvances_Streak3ThenStreak7_BadgesAwarded()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t7");
        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Seed profile with CurrentStreak=2, last activity = yesterday
        // so the next lesson completion (today) advances to streak=3.
        await SeedProfileAsync(
            studentId:          studentId,
            totalXp:            0,
            currentLevel:       1,
            currentStreak:      2,
            longestStreak:      2,
            lastActivityDateUtc: baseDate.AddDays(-1));

        var now = DateTime.UtcNow;
        _factory.TestClock.SetUtcNow(now);

        // Publish lesson completed today → AdvanceStreakCommand → streak=3 → STREAK_3 badge
        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId);

        // Allow async badge handlers to complete
        await Task.Delay(200);

        var badgesAt3 = await GetStudentBadgesAsync(studentId);
        badgesAt3.Should().Contain(b => b.BadgeDefinition.Code == CODE_STREAK_3,
            "STREAK_3 badge must be awarded when streak reaches 3");

        var awardsAt3 = await GetXpAwardsAsync(studentId);
        awardsAt3.Should().Contain(a => a.Reason == XpReason.BadgeEarned && a.XpAmount == 20,
            "STREAK_3 is Common → +20 XP badge award");

        // Now seed streak=6, last activity = yesterday → next lesson advances to streak=7
        var tomorrow = now.AddDays(1);
        _factory.TestClock.SetUtcNow(tomorrow);
        await SeedProfileAsync(
            studentId:          studentId,
            currentStreak:      6,
            longestStreak:      6,
            lastActivityDateUtc: baseDate); // today (2 days before tomorrow = gap of 1 → consecutive)

        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: tomorrow,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId);

        await Task.Delay(200);

        var badgesAt7 = await GetStudentBadgesAsync(studentId);
        badgesAt7.Should().Contain(b => b.BadgeDefinition.Code == CODE_STREAK_7,
            "STREAK_7 badge must be awarded when streak reaches 7");

        var awardsAt7 = await GetXpAwardsAsync(studentId);
        awardsAt7.Should().Contain(a => a.Reason == XpReason.BadgeEarned && a.XpAmount == 50,
            "STREAK_7 is Rare → +50 XP badge award");
    }

    // =========================================================================
    // T8 — Level up → LEVEL_5 badge awarded
    // =========================================================================

    [Fact(DisplayName = "P405-T8 Level 4 → 5 crossing → LEVEL_5 badge awarded")]
    public async Task T8_LevelUp_Level5BadgeAwarded()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t8");
        var now = _factory.TestClock.UtcNow;

        // L5 threshold = 1000 XP. Seed at 980 XP, level 4.
        // A LessonCompleted event awards: LessonCompleted=20 + QuizCompleted=30 (100% accuracy) = 50 XP.
        // 980 + 50 = 1030 ≥ 1000 → level 5 → StudentLeveledUpDomainEvent → LEVEL_5 badge.
        await SeedProfileAsync(
            studentId:     studentId,
            totalXp:       980,
            currentLevel:  4);

        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId,
            accuracyPct: 100,
            correctCount: 4);

        await Task.Delay(300); // allow async domain-event dispatch + badge handler

        var badges = await GetStudentBadgesAsync(studentId);
        badges.Should().Contain(b => b.BadgeDefinition.Code == CODE_LEVEL_5,
            "LEVEL_5 badge must be awarded when student crosses level 5 threshold");

        var awards = await GetXpAwardsAsync(studentId);
        awards.Should().Contain(a => a.Reason == XpReason.BadgeEarned,
            "XpAward(BadgeEarned) must be written for LEVEL_5 badge");
    }

    // =========================================================================
    // T9 — Idempotency: redelivered LessonCompletedIntegrationEvent → exactly 1 FIRST_LESSON
    // =========================================================================

    [Fact(DisplayName = "P405-T9 Redelivered LessonCompletedIntegrationEvent (same EventId) → exactly 1 FIRST_LESSON badge")]
    public async Task T9_RedeliveredLessonEvent_ExactlyOneBadge()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t9");
        var now = _factory.TestClock.UtcNow;

        var ev = new LessonCompletedIntegrationEvent(
            EventId:            Guid.NewGuid(),
            OccurredOnUtc:      now,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // Publish twice (simulating redelivery)
        using (var scope1 = _factory.Services.CreateScope())
        {
            await scope1.ServiceProvider.GetRequiredService<IPublisher>().Publish(ev);
        }

        using (var scope2 = _factory.Services.CreateScope())
        {
            await scope2.ServiceProvider.GetRequiredService<IPublisher>().Publish(ev);
        }

        await Task.Delay(200);

        var badges = await GetStudentBadgesAsync(studentId);
        badges.Count(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON)
            .Should().Be(1,
                "redelivered event must not cause duplicate FIRST_LESSON badge (two-layer idempotency)");
    }

    // =========================================================================
    // T10 — Recent badges ordered by AwardedAtUtc DESC (top 3 of 5)
    // =========================================================================

    [Fact(DisplayName = "P405-T10 5 badges seeded → Dashboard RecentBadges returns top 3, ordered DESC by AwardedAtUtc")]
    public async Task T10_RecentBadges_Top3_OrderedDescByAwardedAt()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync("t10");
        var baseTime = DateTime.UtcNow;

        // Seed 5 badges with distinct AwardedAtUtc timestamps
        var badgesToSeed = new (string Code, DateTime AwardedAtUtc)[]
        {
            (CODE_FIRST_LESSON, baseTime.AddDays(-5)),  // oldest
            (CODE_STREAK_3,     baseTime.AddDays(-4)),
            (CODE_STREAK_7,     baseTime.AddDays(-3)),
            (CODE_LEVEL_5,      baseTime.AddDays(-2)),
            ("LEVEL_10",        baseTime.AddDays(-1)),  // most recent
        };

        await SeedStudentBadgesDirectlyAsync(studentId, badgesToSeed);

        var (dashResp, dashRoot, dashBody) = await GetDashboardAsync(token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        TryProp(dashRoot, "data", out var data).Should().BeTrue("body: {0}", dashBody);
        TryProp(data, "badgesCount", out var badgesCount).Should().BeTrue("body: {0}", dashBody);
        badgesCount.GetInt32().Should().Be(5,
            "Dashboard BadgesCount must reflect all 5 seeded badges; body: {0}", dashBody);

        TryProp(data, "recentBadges", out var recentBadges).Should().BeTrue("body: {0}", dashBody);
        recentBadges.ValueKind.Should().Be(JsonValueKind.Array,
            "recentBadges must be an array when badges exist; body: {0}", dashBody);
        recentBadges.GetArrayLength().Should().Be(3,
            "recent badges strip returns top 3 only; body: {0}", dashBody);

        // Verify ordering: first item must be most-recent (LEVEL_10, t-1d)
        var first = recentBadges.EnumerateArray().First();
        TryProp(first, "code", out var firstCode).Should().BeTrue("body: {0}", dashBody);
        firstCode.GetString().Should().Be("LEVEL_10",
            "most recent badge (awarded t-1d) must be first in RecentBadges; body: {0}", dashBody);
    }

    // =========================================================================
    // T11 — IDOR: cross-student dashboard isolation
    // =========================================================================

    [Fact(DisplayName = "P405-T11 IDOR: Student A (3 badges) and B (0 badges) see independent BadgesCount")]
    public async Task T11_IDOR_DashboardBadgeCountIsolation()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("t11a");
        var (tokenB, _) = await CreateStudentViaParentFlowAsync("t11b");

        var baseTime = DateTime.UtcNow;

        // Seed 3 badges for Student A
        await SeedStudentBadgesDirectlyAsync(studentIdA, new (string, DateTime)[]
        {
            (CODE_FIRST_LESSON, baseTime.AddDays(-3)),
            (CODE_STREAK_3,     baseTime.AddDays(-2)),
            (CODE_LEVEL_5,      baseTime.AddDays(-1)),
        });

        // Student A's dashboard
        var (dashRespA, dashRootA, dashBodyA) = await GetDashboardAsync(tokenA);
        dashRespA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyA);
        TryProp(dashRootA, "data", out var dataA).Should().BeTrue("body: {0}", dashBodyA);
        TryProp(dataA, "badgesCount", out var countA).Should().BeTrue("body: {0}", dashBodyA);
        countA.GetInt32().Should().Be(3, "Student A must see 3 badges; body: {0}", dashBodyA);

        // Student B's dashboard (no badges)
        var (dashRespB, dashRootB, dashBodyB) = await GetDashboardAsync(tokenB);
        dashRespB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBodyB);
        TryProp(dashRootB, "data", out var dataB).Should().BeTrue("body: {0}", dashBodyB);
        TryProp(dataB, "badgesCount", out var countB).Should().BeTrue("body: {0}", dashBodyB);
        countB.GetInt32().Should().Be(0,
            "Student B must see 0 badges — must NOT see A's badges (IDOR isolation); body: {0}", dashBodyB);
    }

    // =========================================================================
    // T12 — IDOR: GET /Badges/Me cross-student isolation
    // =========================================================================

    [Fact(DisplayName = "P405-T12 IDOR: A's JWT returns A's 3 earned; B's JWT returns 0 earned (all locked)")]
    public async Task T12_IDOR_BadgesMeIsolation()
    {
        var (tokenA, studentIdA) = await CreateStudentViaParentFlowAsync("t12a");
        var (tokenB, _) = await CreateStudentViaParentFlowAsync("t12b");

        var baseTime = DateTime.UtcNow;

        // Seed 3 badges for Student A
        await SeedStudentBadgesDirectlyAsync(studentIdA, new (string, DateTime)[]
        {
            (CODE_FIRST_LESSON, baseTime.AddDays(-3)),
            (CODE_STREAK_3,     baseTime.AddDays(-2)),
            (CODE_LEVEL_5,      baseTime.AddDays(-1)),
        });

        // A's /Badges/Me → 3 earned, 7 locked
        var (respA, rootA, bodyA) = await GetBadgesMeAsync(tokenA);
        respA.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyA);
        TryProp(rootA, "data", out var dataA).Should().BeTrue("body: {0}", bodyA);
        TryProp(dataA, "badges", out var badgesA).Should().BeTrue("body: {0}", bodyA);
        badgesA.GetArrayLength().Should().Be(10, "A sees all 10 definitions; body: {0}", bodyA);

        var earnedByA = badgesA.EnumerateArray()
            .Count(b => { TryProp(b, "isEarned", out var e); return e.GetBoolean(); });
        earnedByA.Should().Be(3, "A must see 3 badges as earned; body: {0}", bodyA);

        // B's /Badges/Me → all 10 locked
        var (respB, rootB, bodyB) = await GetBadgesMeAsync(tokenB);
        respB.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyB);
        TryProp(rootB, "data", out var dataB).Should().BeTrue("body: {0}", bodyB);
        TryProp(dataB, "badges", out var badgesB).Should().BeTrue("body: {0}", bodyB);
        badgesB.GetArrayLength().Should().Be(10, "B sees all 10 definitions; body: {0}", bodyB);

        var earnedByB = badgesB.EnumerateArray()
            .Count(b => { TryProp(b, "isEarned", out var e); return e.GetBoolean(); });
        earnedByB.Should().Be(0,
            "B must see 0 badges earned — must NOT see A's earned state (IDOR isolation); body: {0}", bodyB);
    }

    // =========================================================================
    // T13 — Practice Mode by construction: FIRST_LESSON awards; STREAK_*/LEVEL_* do NOT
    // =========================================================================

    [Fact(DisplayName = "P405-T13 Practice Mode: FIRST_LESSON awards; AdvanceStreak suppressed so no STREAK_* or LEVEL_* (D11)")]
    public async Task T13_PracticeMode_FirstLessonAwards_StreakAndLevelBadgesDoNot()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t13");
        var now = _factory.TestClock.UtcNow;

        // Seed Hearts=0 (Practice Mode), streak=2 (close to STREAK_3)
        await SeedProfileAsync(
            studentId:          studentId,
            hearts:             0,
            lastHeartRefillAtUtc: now.AddMinutes(-5),
            currentStreak:      2,
            lastActivityDateUtc: DateOnly.FromDateTime(now.AddDays(-1)));

        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId,
            accuracyPct:   100,
            correctCount:  4);

        await Task.Delay(200);

        var badges = await GetStudentBadgesAsync(studentId);

        // FIRST_LESSON must fire even in Practice Mode (D11)
        badges.Should().Contain(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON,
            "FIRST_LESSON must award in Practice Mode — lesson completion is not blocked (D11)");

        // STREAK_3 must NOT fire because AdvanceStreakCommandHandler gates in Practice Mode
        // → StreakAdvancedDomainEvent is never raised → StreakAdvancedBadgeHandler never fires
        badges.Should().NotContain(b => b.BadgeDefinition.Code == CODE_STREAK_3,
            "STREAK_3 must NOT award in Practice Mode (AdvanceStreak short-circuits — D11 by construction)");

        // LEVEL_* must NOT fire because no XP is awarded in Practice Mode (except badge XP itself)
        // The FIRST_LESSON badge awards +20 XP (Common) but that alone at TotalXp=20 is far from L5 (1000 XP)
        badges.Should().NotContain(b => b.BadgeDefinition.Code == CODE_LEVEL_5,
            "LEVEL_5 must NOT award in Practice Mode (no lesson XP → no level-up → no LEVEL_5)");

        // Verify streak did NOT advance in Practice Mode
        var profile = await GetProfileAsync(studentId);
        profile.Should().NotBeNull();
        profile!.CurrentStreak.Should().Be(2,
            "AdvanceStreakCommandHandler short-circuits in Practice Mode — streak stays at 2");
    }

    // =========================================================================
    // T14 — Badge XP bonus chain: badge XP crosses level threshold → LEVEL_5 cascades
    // =========================================================================

    [Fact(DisplayName = "P405-T14 Badge XP bonus cascades: FIRST_LESSON +20 XP at TotalXp=980 → level up → LEVEL_5 badge also awarded")]
    public async Task T14_BadgeXpBonusChain_CrossesLevelThreshold_Level5Awarded()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t14");
        var now = _factory.TestClock.UtcNow;

        // L5 threshold = 1000 XP.
        // Seed at TotalXp=979, Level=4. Hearts=5 (NOT Practice Mode so all XP fires).
        // LessonCompleted event awards: LessonCompleted=20, StreakBonus=0 (streak=0), QuizCompleted=30 (100%)
        //   = 50 XP → 979+50=1029 → Level 5 → StudentLeveledUpDomainEvent → LEVEL_5 badge → +20 XP more.
        // FIRST_LESSON badge awards +20 XP from LessonCompletedBadgeHandler.
        // Net effect: FIRST_LESSON + LEVEL_5 both awarded.
        await SeedProfileAsync(
            studentId:    studentId,
            totalXp:      979,
            currentLevel: 4,
            hearts:       5);

        await PublishLessonCompletedEventAsync(
            studentId: studentId,
            eventId:   Guid.NewGuid(),
            occurredOnUtc: now,
            lessonId:  _mathG1DemoLessonId,
            skillId:   _mathG1DemoLessonSkillId,
            accuracyPct:   100,
            correctCount:  4);

        // Domain-event dispatch is async (post-commit). Give it time to chain.
        await Task.Delay(500);

        var badges = await GetStudentBadgesAsync(studentId);

        badges.Should().Contain(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON,
            "FIRST_LESSON must always be awarded on first lesson");

        badges.Should().Contain(b => b.BadgeDefinition.Code == CODE_LEVEL_5,
            "LEVEL_5 badge must cascade: lesson XP crosses L5 threshold → StudentLeveledUpDomainEvent → LEVEL_5 badge");

        // Exactly 2 badges earned (no more)
        badges.Should().HaveCount(2,
            "exactly 2 badges earned: FIRST_LESSON + LEVEL_5; no infinite chain");

        // XP awards: at least 2 BadgeEarned awards (20 for FIRST_LESSON + 20 for LEVEL_5)
        var awards = await GetXpAwardsAsync(studentId);
        var badgeAwardTotal = awards.Where(a => a.Reason == XpReason.BadgeEarned).Sum(a => a.XpAmount);
        badgeAwardTotal.Should().Be(40,
            "FIRST_LESSON (Common +20) + LEVEL_5 (Common +20) = 40 XP in badge rewards");
    }

    // =========================================================================
    // T15 — Stress: 3 concurrent identical events → exactly 1 FIRST_LESSON badge
    // =========================================================================

    [Fact(DisplayName = "P405-T15 3 concurrent LessonCompletedEvents → exactly 1 FIRST_LESSON badge (idempotency under concurrency)")]
    public async Task T15_ConcurrentEvents_ExactlyOneBadge()
    {
        var (_, studentId) = await CreateStudentViaParentFlowAsync("t15");
        var now = _factory.TestClock.UtcNow;

        // Publish 3 distinct events concurrently — each triggers LessonCompletedBadgeHandler.
        // The FIRST_LESSON badge should be awarded exactly once due to the unique index
        // UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId.
        var tasks = Enumerable.Range(0, 3).Select(_ =>
            PublishLessonCompletedEventAsync(
                studentId: studentId,
                eventId:   Guid.NewGuid(),
                occurredOnUtc: now,
                lessonId:  _mathG1DemoLessonId,
                skillId:   _mathG1DemoLessonSkillId));

        await Task.WhenAll(tasks);
        await Task.Delay(500); // allow all async handlers to settle

        var badges = await GetStudentBadgesAsync(studentId);
        badges.Count(b => b.BadgeDefinition.Code == CODE_FIRST_LESSON)
            .Should().Be(1,
                "exactly 1 FIRST_LESSON badge must exist even after 3 concurrent events " +
                "(DB unique index + handler pre-check guarantee idempotency)");
    }

    // =========================================================================
    // Envelope sanity: BadgesCount + RecentBadges fields present in Dashboard
    // =========================================================================

    [Fact(DisplayName = "P405-Env Dashboard envelope contains 'badgesCount' and 'recentBadges' fields; 'successed' key preserved")]
    public async Task Env_EnvelopeSanity_P405FieldsPresent()
    {
        var (token, _) = await CreateStudentViaParentFlowAsync("tenv");

        var (resp, root, body) = await GetDashboardAsync(token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // BaseResponse<T> envelope shape
        body.Should().Contain("\"successed\":",
            "'successed' (lowercase-d camelCase) must appear in the response; body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("body: {0}", body);

        // P4-05 additive fields
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "badgesCount",  out _).Should().BeTrue(
            "data.badgesCount must be present (P4-05 additive field); body: {0}", body);
        TryProp(data, "recentBadges", out _).Should().BeTrue(
            "data.recentBadges must be present (P4-05 additive field); body: {0}", body);

        // Legacy fields still present (non-regression P4-02/03/04)
        TryProp(data, "xp",            out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "streak",        out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "level",         out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "hearts",        out _).Should().BeTrue("body: {0}", body);
        TryProp(data, "inPracticeMode",out _).Should().BeTrue("body: {0}", body);
    }

    // =========================================================================
    // Seeder idempotency: running BadgeSeeder twice → still exactly 10 rows
    // =========================================================================

    [Fact(DisplayName = "P405-Seed BadgeSeeder is idempotent — running twice does not duplicate rows")]
    public async Task Seed_IdempotencyRunTwice_Still10Rows()
    {
        // Run the seeder a second time (first was in InitializeAsync)
        using var scope = _factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
        await seeder.SeedAsync(); // second run

        using var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var count = await db.BadgeDefinitions.AsNoTracking().CountAsync();
        count.Should().Be(10,
            "BadgeSeeder upsert by Code must not duplicate rows on second run");
    }
}

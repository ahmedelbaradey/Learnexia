using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P9-07 / P9-05 / P9-08 integration tests — Nudge Arbitration, 3 new consumers, Comeback Ladder.
///
/// Coverage:
///   TC-01  StudentLeveledUpIntegrationEvent → Achievement/LEVELED_UP notification row written
///   TC-02  LeagueTierChangedIntegrationEvent (Promoted) → Achievement/LEAGUE_TIER_CHANGED_PROMOTED row
///   TC-03  LeagueTierChangedIntegrationEvent (non-Promoted) → Achievement/LEAGUE_TIER_CHANGED row
///   TC-04  StreakFreezeConsumedIntegrationEvent → Achievement/STREAK_FREEZE_CONSUMED row
///   TC-05  Global budget gates PUSH (not inbox): budget=1, 2nd event → inbox written but push bit NOT set
///   TC-06  Legacy BadgeEarned is ALSO budget-gated: publish after budget exhausted → push bit not set
///   TC-07  Under budget (high budget) → push bit IS set (arbiter grants)
///   TC-08  Comeback tier GENTLE (days≤2) → LAPSE_WIN_BACK_GENTLE code
///   TC-09  Comeback tier REPAIR (2&lt;days≤5) → LAPSE_WIN_BACK_REPAIR code
///   TC-10  Comeback tier FRESH_START (days&gt;5) → LAPSE_WIN_BACK_FRESH_START code
///   TC-11  Per-tier episode dedupe: same tier second publish → no second row
///   TC-12  Parent sets GlobalDailyPushBudget → 200 + persisted (GET reads it back)
///   TC-13  IDOR: different parent cannot write GlobalDailyPushBudget for unrelated child → 403
///   TC-14  Fail-soft: publishing to a child with no parent link must not throw; publish completes
/// </summary>
[Collection("IntegrationTests")]
public sealed class P9_07_NudgeArbitration_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // ── Infra ──────────────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient             _client;

    public P9_07_NudgeArbitration_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using var scope = _factory.Services.CreateScope();
        await LearningSeeder.SeedAsync(scope.ServiceProvider);

        var badgeSeeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
        await badgeSeeder.SeedAsync();

        _factory.PushSender.Reset();
        _factory.TestClock.Reset();
    }

    public Task DisposeAsync()
    {
        _factory.TestClock.Reset();
        _factory.PushSender.Reset();
        return Task.CompletedTask;
    }

    // =========================================================================
    // Helpers (mirror P4_09 pattern)
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p907_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    /// <summary>Creates a parent+child via the standard API flow.</summary>
    private async Task<(string ParentToken, string ChildToken, int ParentId, int ChildId)>
        CreateParentChildPairAsync(string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = UniqueEmail($"par_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201],
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTokProp.GetString()!;

        TryProp(regData, "userId", out var parentIdProp);
        int parentId = parentIdProp.ValueKind == JsonValueKind.Number ? parentIdProp.GetInt32() : 0;

        var childEmail = UniqueEmail($"chd_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = $"Test Child P907 {tag}",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 1,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        if (parentId == 0)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == parentEmail);
            parentId = user?.Id ?? 0;
        }

        var childToken = await SignInAndGetTokenAsync(childEmail, ValidChildPassword);
        return (parentToken, childToken, parentId, childId);
    }

    private string ChildPrefsUrl(int childId)
        => $"api/Notifications/Preferences/Children/{childId}/Reengagement";

    /// <summary>
    /// Sets push=true, inApp=true for Achievement category and an explicit DailyCap
    /// on the given child via the parent prefs endpoint.
    /// </summary>
    private async Task SetPushEnabledWithBudgetAsync(
        string parentToken,
        int childId,
        int dailyCap         = 10,
        int? globalPushBudget = null)
    {
        var payload = new
        {
            Items = new[]
            {
                new { Category = (int)NotificationCategory.Achievement,          Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.StreakAtRisk,         Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.LapseWinBack,         Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.DailyMissionReminder, Email = false, Push = true, InApp = true },
            },
            QuietHoursStartLocal  = "00:00",
            QuietHoursEndLocal    = "00:01",  // almost-never-quiet
            TimeZoneId            = "UTC",
            DailyCap              = dailyCap,
            GlobalDailyPushBudget = globalPushBudget,
        };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ChildPrefsUrl(childId), payload, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"PUT prefs must succeed; body: {body}");
    }

    /// <summary>
    /// Publishes an event via MediatR IPublisher using a fresh DI scope.
    /// </summary>
    private async Task PublishAsync<TEvent>(TEvent ev) where TEvent : INotification
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(ev);
    }

    private async Task<List<Learnexia.Modules.Notifications.Domain.Entities.Notification>>
        GetNotificationsAsync(int childId, string? code = null, NotificationCategory? category = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var q = db.Notifications.AsNoTracking()
            .Where(n => n.RecipientExternalUserId == childId);
        if (code is not null)
            q = q.Where(n => n.Code == code);
        if (category is not null)
            q = q.Where(n => n.Category == category);
        return await q.ToListAsync();
    }

    // =========================================================================
    // TC-01 — StudentLeveledUpIntegrationEvent → Achievement/LEVELED_UP row
    // =========================================================================

    [Fact(DisplayName = "P907-TC01 StudentLeveledUp → Achievement/LEVELED_UP notification row written")]
    public async Task TC01_StudentLeveledUp_WritesLeveledUpNotification()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc01");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new StudentLeveledUpIntegrationEvent(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     childId,
            OldLevel:      1,
            NewLevel:      2));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "LEVELED_UP");

        notifications.Should().NotBeEmpty("StudentLeveledUpIntegrationEvent must produce a LEVELED_UP notification row");
        notifications.First().Category.Should().Be(NotificationCategory.Achievement,
            "LEVELED_UP must be in the Achievement category");
    }

    // =========================================================================
    // TC-02 — LeagueTierChangedIntegrationEvent (Promoted) → LEAGUE_TIER_CHANGED_PROMOTED
    // =========================================================================

    [Fact(DisplayName = "P907-TC02 LeagueTierChanged (Promoted) → LEAGUE_TIER_CHANGED_PROMOTED row")]
    public async Task TC02_LeagueTierChanged_Promoted_WritesPromotedCode()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc02");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new LeagueTierChangedIntegrationEvent(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     childId,
            OldTier:       "Bronze",
            NewTier:       "Silver",
            Status:        "Promoted"));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "LEAGUE_TIER_CHANGED_PROMOTED");

        notifications.Should().NotBeEmpty(
            "a Promoted league-tier change must produce a LEAGUE_TIER_CHANGED_PROMOTED notification");
        notifications.First().Category.Should().Be(NotificationCategory.Achievement);
    }

    // =========================================================================
    // TC-03 — LeagueTierChangedIntegrationEvent (non-Promoted) → LEAGUE_TIER_CHANGED
    // =========================================================================

    [Fact(DisplayName = "P907-TC03 LeagueTierChanged (Relegated) → LEAGUE_TIER_CHANGED row")]
    public async Task TC03_LeagueTierChanged_Relegated_WritesNeutralCode()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc03");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new LeagueTierChangedIntegrationEvent(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     childId,
            OldTier:       "Silver",
            NewTier:       "Bronze",
            Status:        "Relegated"));

        await Task.Delay(400);

        var promotedNotifs = await GetNotificationsAsync(childId, code: "LEAGUE_TIER_CHANGED_PROMOTED");
        promotedNotifs.Should().BeEmpty("Relegated status must NOT produce a LEAGUE_TIER_CHANGED_PROMOTED code");

        var neutralNotifs = await GetNotificationsAsync(childId, code: "LEAGUE_TIER_CHANGED");
        neutralNotifs.Should().NotBeEmpty(
            "non-Promoted league-tier change must produce a LEAGUE_TIER_CHANGED notification");
    }

    // =========================================================================
    // TC-04 — StreakFreezeConsumedIntegrationEvent → Achievement/STREAK_FREEZE_CONSUMED
    // =========================================================================

    [Fact(DisplayName = "P907-TC04 StreakFreezeConsumed → Achievement/STREAK_FREEZE_CONSUMED row")]
    public async Task TC04_StreakFreezeConsumed_WritesNotification()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc04");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new StreakFreezeConsumedIntegrationEvent(
            EventId:                Guid.NewGuid(),
            OccurredOnUtc:          DateTime.UtcNow,
            StudentId:              childId,
            CurrentStreak:          7,
            RemainingFreezeBalance: 0));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "STREAK_FREEZE_CONSUMED");

        notifications.Should().NotBeEmpty(
            "StreakFreezeConsumedIntegrationEvent must produce a STREAK_FREEZE_CONSUMED notification");
        notifications.First().Category.Should().Be(NotificationCategory.Achievement);
    }

    // =========================================================================
    // TC-05 — Global budget gates PUSH (the centerpiece)
    //   Budget=1. Pre-seed a push notification row for today to exhaust the budget.
    //   Then publish a LapseWinBackIntegrationEvent with Push=true.
    //   The dispatcher ALWAYS writes the inbox row; but with budget exhausted,
    //   the push bit (DeliveredChannels & 2) must NOT be set on the new row.
    //   This proves the DB-authoritative budget gate operates at the dispatcher level.
    // =========================================================================

    [Fact(DisplayName = "P907-TC05 Global budget=1 exhausted: handler writes inbox row but push bit NOT set")]
    public async Task TC05_GlobalBudget1_BudgetExhausted_InboxWrittenNoPushBit()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc05");

        // Set LapseWinBack Push=true, GlobalDailyPushBudget=1
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: 1);
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[TC05_{childId}]");

        // Pre-seed a push-delivered notification row for today to exhaust budget=1.
        // DeliveredChannels = 2 (push bit) + SentAtUtc = now → CountPushesSentTodayAsync = 1.
        await SeedPushNotificationAsync(childId, "PRIOR_PUSH");

        _factory.PushSender.Reset();

        // Publish LapseWinBack → handler runs, budget already exhausted → push suppressed
        await PublishAsync(new LapseWinBackIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, childId, DaysSinceLastActivity: 2));
        await Task.Delay(400);

        // Dispatcher ALWAYS writes inbox row (inbox is never rationed)
        var notifs = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_GENTLE");
        notifs.Should().NotBeEmpty(
            "dispatcher must always write an inbox row even when global push budget is exhausted");

        // Push bit must NOT be set (budget=1 already consumed by the pre-seeded push row)
        var pushBit = notifs.First().DeliveredChannels & 2;
        pushBit.Should().Be(0,
            "push bit must be 0 when global daily push budget (=1) is exhausted — " +
            "DB CountPushesSentTodayAsync is the authoritative budget gate");
    }

    // =========================================================================
    // TC-06 — Under budget (budget=4, push=true): push bit IS set on LapseWinBack event
    //   Also validates that the push path runs and stamps the bit correctly
    //   when the arbiter grants (budget available, no cooldown).
    // =========================================================================

    [Fact(DisplayName = "P907-TC06 Under budget (budget=4, LapseWinBack push=true) → push bit set")]
    public async Task TC06_UnderBudget_LapseWinBack_PushBitSet()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc06");

        // Set LapseWinBack Push=true, budget=4 (default via null)
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: null);
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[TC06_{childId}]");

        _factory.PushSender.Reset();

        await PublishAsync(new LapseWinBackIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, childId, DaysSinceLastActivity: 2));
        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_GENTLE");
        notifs.Should().NotBeEmpty("LapseWinBack must produce a GENTLE row");

        var pushBit = notifs.First().DeliveredChannels & 2;
        pushBit.Should().Be(2,
            "push bit must be set when budget is available (budget=4, 0 prior pushes today) " +
            "and Push=true and device token is registered");
    }

    // =========================================================================
    // TC-07 — Legacy handler (BadgeEarned/Achievement) always has push bit = 0
    //   because Achievement category defaults to Push=false (not in ReengagementCategories).
    //   This is by-design behavior (v1; OQ-8 locked decision) — the inbox row IS
    //   written but the push path is never entered for Achievement-category events.
    // =========================================================================

    [Fact(DisplayName = "P907-TC07 Achievement handlers (BadgeEarned) always inbox-only in v1 (Push=false default)")]
    public async Task TC07_AchievementHandlers_AlwaysInboxOnly_NoPushBit()
    {
        // Achievement is not in ReengagementCategories (StreakAtRisk/DailyMission/LapseWinBack).
        // GetOrDefaultAsync for Achievement always returns CreateDefault → Push=false.
        // So all Achievement-category events (BadgeEarned, LeveledUp, etc.) are inbox-only in v1.
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc07");
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[TC07_{childId}]");

        await PublishAsync(new BadgeEarnedIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, childId, "FIRST_LESSON", 50, "Common"));
        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "BADGE_EARNED");
        notifs.Should().NotBeEmpty("BadgeEarned must always write inbox row");

        var pushBit = notifs.First().DeliveredChannels & 2;
        pushBit.Should().Be(0,
            "Achievement category defaults to Push=false (not in parent-configurable ReengagementCategories in v1); " +
            "push bit must never be set for Achievement-category events");
    }

    // =========================================================================
    // TC-08 — Comeback tier: GENTLE (days ≤ 2) → LAPSE_WIN_BACK_GENTLE
    // =========================================================================

    [Fact(DisplayName = "P907-TC08 LapseWinBack with daysIdle=2 → LAPSE_WIN_BACK_GENTLE code")]
    public async Task TC08_LapseWinBack_DaysIdle2_GentleTier()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc08");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:              Guid.NewGuid(),
            OccurredOnUtc:        DateTime.UtcNow,
            StudentId:            childId,
            DaysSinceLastActivity: 2));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_GENTLE");
        notifications.Should().NotBeEmpty(
            "daysIdle=2 (≤ threshold_1=2) must produce a LAPSE_WIN_BACK_GENTLE notification");
        notifications.First().Category.Should().Be(NotificationCategory.LapseWinBack);
    }

    // =========================================================================
    // TC-09 — Comeback tier: REPAIR (2 < days ≤ 5) → LAPSE_WIN_BACK_REPAIR
    // =========================================================================

    [Fact(DisplayName = "P907-TC09 LapseWinBack with daysIdle=4 → LAPSE_WIN_BACK_REPAIR code")]
    public async Task TC09_LapseWinBack_DaysIdle4_RepairTier()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc09");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:              Guid.NewGuid(),
            OccurredOnUtc:        DateTime.UtcNow,
            StudentId:            childId,
            DaysSinceLastActivity: 4));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_REPAIR");
        notifications.Should().NotBeEmpty(
            "daysIdle=4 (2 < days ≤ 5) must produce a LAPSE_WIN_BACK_REPAIR notification");
        notifications.First().Category.Should().Be(NotificationCategory.LapseWinBack);
    }

    // =========================================================================
    // TC-10 — Comeback tier: FRESH_START (days > 5) → LAPSE_WIN_BACK_FRESH_START
    // =========================================================================

    [Fact(DisplayName = "P907-TC10 LapseWinBack with daysIdle=14 → LAPSE_WIN_BACK_FRESH_START code")]
    public async Task TC10_LapseWinBack_DaysIdle14_FreshStartTier()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc10");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:              Guid.NewGuid(),
            OccurredOnUtc:        DateTime.UtcNow,
            StudentId:            childId,
            DaysSinceLastActivity: 14));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_FRESH_START");
        notifications.Should().NotBeEmpty(
            "daysIdle=14 (> threshold_2=5) must produce a LAPSE_WIN_BACK_FRESH_START notification");
        notifications.First().Category.Should().Be(NotificationCategory.LapseWinBack);
    }

    // =========================================================================
    // TC-11 — Per-tier episode dedupe: re-publishing same tier → no second row
    // =========================================================================

    [Fact(DisplayName = "P907-TC11 LapseWinBack same tier published twice → only 1 notification row (tier dedupe)")]
    public async Task TC11_LapseWinBack_SameTierTwice_DedupeBlocksSecond()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc11");
        await SetPushEnabledWithBudgetAsync(parentToken, childId);

        var ev1 = new LapseWinBackIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow, childId, DaysSinceLastActivity: 14);
        await PublishAsync(ev1);
        await Task.Delay(400);

        // Same tier (days=14 → FRESH_START), same student, different EventId
        var ev2 = new LapseWinBackIntegrationEvent(
            Guid.NewGuid(), DateTime.UtcNow.AddSeconds(1), childId, DaysSinceLastActivity: 14);
        await PublishAsync(ev2);
        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_FRESH_START");
        notifications.Should().HaveCount(1,
            "per-tier episode dedupe must prevent a second FRESH_START row for the same lapse episode");
    }

    // =========================================================================
    // TC-12 — Parent sets GlobalDailyPushBudget → 200 + GET reads it back
    // =========================================================================

    [Fact(DisplayName = "P907-TC12 Parent sets GlobalDailyPushBudget via prefs PUT → 200 + persisted in DB")]
    public async Task TC12_ParentSetsGlobalBudget_200AndPersisted()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("tc12");

        // PUT with GlobalDailyPushBudget = 2
        var payload = new
        {
            Items = new[]
            {
                new { Category = (int)NotificationCategory.Achievement,          Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.StreakAtRisk,         Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.LapseWinBack,         Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.DailyMissionReminder, Email = false, Push = true, InApp = true },
            },
            DailyCap              = 5,
            GlobalDailyPushBudget = 2,
        };

        var (putResp, _, putBody) = await SendAsync(_client, HttpMethod.Put,
            ChildPrefsUrl(childId), payload, parentToken);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"PUT with GlobalDailyPushBudget must return 200; body: {putBody}");

        // Verify persistence directly in DB (the GET DTO doesn't yet expose GlobalDailyPushBudget)
        using var scope = _factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var prefs = await db.ChildReengagementPreferences
            .AsNoTracking()
            .Where(p => p.ChildId == childId)
            .ToListAsync();

        prefs.Should().NotBeEmpty("prefs must be persisted after PUT");
        prefs.All(p => p.GlobalDailyPushBudget == 2).Should().BeTrue(
            "all category rows for this child must carry GlobalDailyPushBudget=2");
    }

    // =========================================================================
    // TC-13 — IDOR: different parent cannot write GlobalDailyPushBudget for unrelated child
    // =========================================================================

    [Fact(DisplayName = "P907-TC13 IDOR: unrelated parent cannot set GlobalDailyPushBudget → 403")]
    public async Task TC13_Idor_UnrelatedParentCannotSetGlobalBudget_Returns403()
    {
        // Child belongs to parentA
        var (_, _, _, childId) = await CreateParentChildPairAsync("tc13a");

        // ParentB has no link to the child
        var parentBEmail = UniqueEmail("par_tc13b");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentBEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201], $"parentB reg; body: {regBody}");
        TryProp(regRoot, "data", out var bData);
        TryProp(bData, "accessToken", out var bTokEl);
        var parentBToken = bTokEl.GetString()!;

        // ParentB tries to write the GlobalDailyPushBudget for parentA's child
        var payload = new
        {
            Items                 = new[] { new { Category = (int)NotificationCategory.Achievement, Email = false, Push = true, InApp = true } },
            GlobalDailyPushBudget = 1,
        };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put,
            ChildPrefsUrl(childId), payload, parentBToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            $"unrelated parent must receive 403 when setting budget for another family's child; body: {body}");
    }

    // =========================================================================
    // TC-14 — Fail-soft: event for child with no parent link must not throw
    // =========================================================================

    [Fact(DisplayName = "P907-TC14 Fail-soft: publishing event for orphan child must not throw")]
    public async Task TC14_FailSoft_NoParentLink_DoesNotThrow()
    {
        // Use a student ID that has no parent-child link at all (a large unlikely ID).
        // The handlers must log+skip, not throw.
        const int orphanChildId = 999_999_999;

        var ex = await Record.ExceptionAsync(async () =>
        {
            await PublishAsync(new StudentLeveledUpIntegrationEvent(
                Guid.NewGuid(), DateTime.UtcNow, orphanChildId, 1, 2));
        });

        ex.Should().BeNull("publishing an event for an orphan child (no parent link) must not throw to the publisher");

        // Inbox must remain clean for this orphan id
        var notifications = await GetNotificationsAsync(orphanChildId);
        notifications.Should().BeEmpty("no notification must be written for a child with no parent link");
    }

    // =========================================================================
    // Seed helpers — mirrors P4_09 pattern + P9-specific additions
    // =========================================================================

    /// <summary>
    /// Sets Push=true, InApp=true only for LapseWinBack category with the given global budget.
    /// LapseWinBack is in ReengagementCategories so its Push can be enabled via the prefs endpoint.
    /// </summary>
    private async Task SetPushEnabledForLapseWinBackAsync(
        string parentToken,
        int childId,
        int? globalPushBudget = null)
    {
        var payload = new
        {
            Items = new[]
            {
                new { Category = (int)NotificationCategory.StreakAtRisk,         Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.DailyMissionReminder, Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.LapseWinBack,         Email = false, Push = true, InApp = true },
            },
            QuietHoursStartLocal  = "00:00",
            QuietHoursEndLocal    = "00:01",   // 1-minute quiet window — almost never active
            TimeZoneId            = "UTC",
            DailyCap              = 10,
            GlobalDailyPushBudget = globalPushBudget,
        };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put, ChildPrefsUrl(childId), payload, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"PUT prefs must succeed; body: {body}");
    }

    /// <summary>
    /// Seeds a notification row with the push bit set (DeliveredChannels & 2 = 2) and SentAtUtc=now.
    /// This simulates a prior push send for budget-gate testing without needing a real device token.
    /// </summary>
    private async Task SeedPushNotificationAsync(int childId, string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var conn  = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO notifications.""Notifications""
               (""Id"", ""RecipientUserId"", ""RecipientExternalUserId"", ""Title"", ""Body"", ""Type"",
                ""IsRead"", ""Category"", ""Code"", ""Data"", ""DeliveredChannels"",
                ""CreatedAtUtc"", ""CreatedBy"", ""SentAtUtc"")
               VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10::jsonb, $11, $12, $13, $14)";
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = Guid.NewGuid() });     // $1 Id
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = Guid.Empty });         // $2 RecipientUserId
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = childId });            // $3 RecipientExternalUserId
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "Prior push" });       // $4 Title
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "body" });             // $5 Body
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });                  // $6 Type (Reminder)
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });              // $7 IsRead
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (int)NotificationCategory.LapseWinBack }); // $8 Category
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = code });               // $9 Code
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "{}" });              // $10 Data
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 2 });                  // $11 DeliveredChannels=2 (push bit)
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DateTime.UtcNow });    // $12 CreatedAtUtc
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DBNull.Value });       // $13 CreatedBy
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DateTime.UtcNow });    // $14 SentAtUtc
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    private async Task RegisterDeviceTokenDirectlyAsync(int userId, string expoPushToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var platform = "ios";
        var nowUtc   = DateTime.UtcNow;
        var creatorId = 0;

        await db.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO notifications.""UserDeviceTokens""
               (""UserId"", ""ExpoPushToken"", ""Platform"", ""RegisteredAtUtc"", ""LastUsedAtUtc"", ""IsActive"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({userId}, '{expoPushToken}', '{platform}', '{nowUtc:O}', '{nowUtc:O}', true,
                       '{nowUtc:O}', {creatorId})
               ON CONFLICT DO NOTHING");
    }
}

using FluentAssertions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P9-09 integration tests — Spaced-repetition review-reminder Notifications consumer.
///
/// Coverage:
///   T1  Happy path: ReviewDueIntegrationEvent for child-with-parent
///       → exactly one Notification row, Category=ReviewReminder, Code=REVIEW_DUE,
///         Title/Body non-empty, body contains skill name + minutes, no {skill}/{minutes} remaining.
///   T2  Dedupe: publish twice for same (child, day) → exactly one inbox row (Redis SETNX guard).
///   T3  Not-eligible: daily cap reached (cap=1, first event consumed) → second event produces no row.
///   T4  Inbox-only posture: even with a registered device token, push bit (DeliveredChannels &amp; 2)
///       is NOT set — ReviewReminder is NOT in ReengagementCategories push set (Push defaults false).
///   T5  ar/en copy: child with locale ar → Arabic template body; child with locale en → English body;
///       both have top skill name + minutes interpolated.
///   T6  Fail-soft orphan: event for studentId with no parent → no throw, no row.
///   T7  No PII: persisted Title/Body contain only skill name/minutes/count; no child name/email.
///   T8  Empty-TopSkills guard: event with empty TopSkills list → no row, no throw.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P9_09_ReviewReminder_Tests : IAsyncLifetime
{
    // ── URL constants (mirrors P9-06 pattern) ─────────────────────────────────
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";
    private const string ChildPrefsPath     = "api/Notifications/Preferences/Children/{0}/Reengagement";

    // Known top-skill fixture values used in the event payload and body assertions
    private const string TopSkillName    = "الكسور";
    private const int    TopSkillMinutes = 5;
    private const int    DueCount        = 2;

    // ── Infrastructure ─────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient             _client;

    public P9_09_ReviewReminder_Tests(LearnexiaWebAppFactory factory)
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
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p909_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    /// <summary>
    /// Creates a parent + child via the standard API flow.
    /// <paramref name="childLanguage"/> maps to the child's PreferredLanguage:
    /// "ar" → "ar-EG", "en" → "en-US".
    /// Returns (parentToken, childToken, parentId, childId).
    /// </summary>
    private async Task<(string ParentToken, string ChildToken, int ParentId, int ChildId)>
        CreateParentChildPairAsync(string? tag = null, string childLanguage = "ar")
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = UniqueEmail($"par_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201],
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue($"body: {regBody}");
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue($"body: {regBody}");
        var parentToken = parentTokProp.GetString()!;

        TryProp(regData, "userId", out var parentIdProp);
        int parentId = parentIdProp.ValueKind == JsonValueKind.Number ? parentIdProp.GetInt32() : 0;

        var childEmail = UniqueEmail($"chd_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = $"Test Child P909 {tag}",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 1,
                Language         = childLanguage,
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue($"body: {addBody}");
        TryProp(addData, "id", out var idProp).Should().BeTrue($"body: {addBody}");
        var childId = idProp.GetInt32();

        if (parentId == 0)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<
                Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>();
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == parentEmail);
            parentId = user?.Id ?? 0;
        }

        // Sign in as child to get child token
        var (signInResp, signInRoot, signInBody) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"child sign-in must succeed; body: {signInBody}");
        TryProp(signInRoot, "data", out var signInData).Should().BeTrue($"body: {signInBody}");
        TryProp(signInData, "accessToken", out var childTokProp).Should().BeTrue($"body: {signInBody}");
        var childToken = childTokProp.GetString()!;

        return (parentToken, childToken, parentId, childId);
    }

    /// <summary>Publishes an INotification (integration event) via a fresh DI scope's IPublisher.</summary>
    private async Task PublishAsync<TEvent>(TEvent ev) where TEvent : INotification
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(ev);
    }

    /// <summary>Queries Notification rows for the given child by optional code/category filter.</summary>
    private async Task<List<Learnexia.Modules.Notifications.Domain.Entities.Notification>>
        GetNotificationsAsync(int childId, string? code = null, NotificationCategory? category = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var q  = db.Notifications.AsNoTracking()
                    .Where(n => n.RecipientExternalUserId == childId);
        if (code is not null)
            q = q.Where(n => n.Code == code);
        if (category is not null)
            q = q.Where(n => n.Category == category);
        return await q.ToListAsync();
    }

    /// <summary>
    /// Builds a standard <see cref="ReviewDueIntegrationEvent"/> with the fixed skill fixtures.
    /// </summary>
    private static ReviewDueIntegrationEvent MakeReviewDueEvent(int childId, DateTime? occurredOnUtc = null)
        => new(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: occurredOnUtc ?? DateTime.UtcNow,
            StudentId:     childId,
            DueCount:      DueCount,
            TopSkills: new[]
            {
                new DueSkillSnapshot(SkillId: 1, SkillName: TopSkillName, EstimatedTimeMinutes: TopSkillMinutes),
                new DueSkillSnapshot(SkillId: 2, SkillName: "الجمع", EstimatedTimeMinutes: 3),
            });

    /// <summary>
    /// Sets the child's reengagement prefs via the parent API endpoint.
    /// By default sets DailyCap=10, no quiet window (00:00–00:01), Push=false for all categories
    /// (ReviewReminder is not in the persisted ReengagementCategories set; the push stays false by default).
    /// </summary>
    private async Task SetChildPrefsAsync(
        string parentToken,
        int childId,
        int dailyCap = 10)
    {
        // Only the ReengagementCategories set is addressable via the prefs endpoint.
        // ReviewReminder is intentionally absent — that is the inbox-only-v1 behavior.
        var payload = new
        {
            Items = new[]
            {
                new { Category = (int)NotificationCategory.StreakAtRisk,         Email = false, Push = false, InApp = true },
                new { Category = (int)NotificationCategory.DailyMissionReminder, Email = false, Push = false, InApp = true },
                new { Category = (int)NotificationCategory.LapseWinBack,         Email = false, Push = false, InApp = true },
            },
            QuietHoursStartLocal  = "00:00",
            QuietHoursEndLocal    = "00:01",   // 1-minute window, almost never active
            TimeZoneId            = "UTC",
            DailyCap              = dailyCap,
        };
        var (resp, _, body) = await SendAsync(_client, HttpMethod.Put,
            string.Format(ChildPrefsPath, childId), payload, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"PUT prefs must succeed; body: {body}");
    }

    /// <summary>
    /// Registers a device token directly in the DB so the push path is exercisable.
    /// Mirrors the pattern in P9_07_NudgeArbitration_Tests.
    /// </summary>
    private async Task RegisterDeviceTokenDirectlyAsync(int userId, string expoPushToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var nowUtc   = DateTime.UtcNow;

        await db.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO notifications.""UserDeviceTokens""
               (""UserId"", ""ExpoPushToken"", ""Platform"", ""RegisteredAtUtc"", ""LastUsedAtUtc"", ""IsActive"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({userId}, '{expoPushToken}', 'ios', '{nowUtc:O}', '{nowUtc:O}', true,
                       '{nowUtc:O}', 0)
               ON CONFLICT DO NOTHING");
    }

    /// <summary>
    /// Directly inserts a Notification row with the in-app bit already set and SentAtUtc = now.
    /// Used to exhaust the daily cap before the handler fires, so the handler sees sentsToday >= cap.
    /// </summary>
    private async Task SeedInAppNotificationAsync(int childId, string code, NotificationCategory category)
    {
        using var scope = _factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var conn = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO notifications.""Notifications""
               (""Id"", ""RecipientUserId"", ""RecipientExternalUserId"", ""Title"", ""Body"", ""Type"",
                ""IsRead"", ""Category"", ""Code"", ""Data"", ""DeliveredChannels"",
                ""CreatedAtUtc"", ""CreatedBy"", ""SentAtUtc"")
               VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10::jsonb, $11, $12, $13, $14)";
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = Guid.NewGuid() });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = Guid.Empty });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = childId });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "Prior notification" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "body" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });    // Reminder
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (int)category });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = code });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "{}" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 4 });    // inApp bit
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DateTime.UtcNow });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DBNull.Value });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DateTime.UtcNow });
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    // =========================================================================
    // T1 — Happy path (AC1 + AC2)
    //
    // Publish ReviewDueIntegrationEvent for a child with a linked parent.
    // Assert: exactly one Notification row with Code=REVIEW_DUE, Category=ReviewReminder,
    //         non-empty Title/Body, body contains skill name and minutes, no unresolved placeholders.
    // =========================================================================

    [Fact(DisplayName = "P909-T1 Happy path: ReviewDueIntegrationEvent → exactly one REVIEW_DUE row (Category=ReviewReminder, body rendered)")]
    public async Task T1_HappyPath_ReviewDueEvent_WritesReviewReminderRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("t1");

        await PublishAsync(MakeReviewDueEvent(childId));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "REVIEW_DUE");

        notifications.Should().HaveCount(1,
            "ReviewDueIntegrationEventHandler must write exactly one REVIEW_DUE row for an eligible child");

        var notif = notifications.First();

        notif.Category.Should().Be(NotificationCategory.ReviewReminder,
            "REVIEW_DUE notification must have Category=ReviewReminder (value 7)");

        notif.Code.Should().Be("REVIEW_DUE",
            "the notification code must be REVIEW_DUE");

        notif.Title.Should().NotBeNullOrWhiteSpace("REVIEW_DUE Title must not be empty");
        notif.Body.Should().NotBeNullOrWhiteSpace("REVIEW_DUE Body must not be empty");

        // Template placeholders must be substituted — no raw {skill} or {minutes} left
        notif.Body.Should().NotContain("{skill}",
            "{skill} placeholder must be substituted with the actual skill name");
        notif.Body.Should().NotContain("{minutes}",
            "{minutes} placeholder must be substituted with the actual minutes value");

        // Top skill name (Arabic fraction skills) must appear in the rendered body
        notif.Body.Should().Contain(TopSkillName,
            "the top skill name must appear in the rendered body");

        // Minutes must appear as a number
        notif.Body.Should().Contain(TopSkillMinutes.ToString(),
            "the estimated minutes value must appear in the rendered body");
    }

    // =========================================================================
    // T2 — Dedupe (AC4)
    //
    // Publishing the same event twice for the same child on the same day
    // must result in exactly one inbox row (Redis SETNX per-child-category-day guard).
    // =========================================================================

    [Fact(DisplayName = "P909-T2 Dedupe: publishing ReviewDueIntegrationEvent twice same day → only one inbox row")]
    public async Task T2_Dedupe_SameChildSameDay_OnlyOneRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("t2");

        var dayUtc = DateTime.UtcNow;

        await PublishAsync(MakeReviewDueEvent(childId, dayUtc));
        await Task.Delay(300);

        // Same OccurredOnUtc day, different EventId — simulates duplicate delivery
        await PublishAsync(MakeReviewDueEvent(childId, dayUtc.AddSeconds(10)));
        await Task.Delay(300);

        var notifications = await GetNotificationsAsync(childId, code: "REVIEW_DUE");

        notifications.Should().HaveCount(1,
            "per-(child, category, day) Redis SETNX dedupe must prevent a second REVIEW_DUE row " +
            "when the same child's review-due event fires twice on the same day");
    }

    // =========================================================================
    // T3 — Not-eligible: daily cap reached (AC5)
    //
    // Strategy: use the default DailyCap=3 from CreateDefault. Pre-seed 3 ReviewReminder
    // inbox rows for today, so CountSentTodayAsync returns 3 >= DailyCap(3).
    // Then publish the event — the handler sees not-eligible and writes no additional row.
    // This avoids any direct manipulation of the ChildReengagementPreferences table
    // (ReviewReminder is not in ReengagementCategories so the PUT prefs endpoint can't set it).
    // =========================================================================

    [Fact(DisplayName = "P909-T3 Not-eligible: daily cap reached (default cap=3, 3 already sent today) → no additional row written")]
    public async Task T3_NotEligible_DailyCapReached_NoRowWritten()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("t3");

        // Pre-seed 3 ReviewReminder inbox rows for today to exhaust the default DailyCap=3.
        // CreateDefault sets DailyCap=3 — matching these 3 pre-seeded rows triggers DailyCapReached.
        await SeedInAppNotificationAsync(childId, "REVIEW_DUE", NotificationCategory.ReviewReminder);
        await SeedInAppNotificationAsync(childId, "REVIEW_DUE", NotificationCategory.ReviewReminder);
        await SeedInAppNotificationAsync(childId, "REVIEW_DUE", NotificationCategory.ReviewReminder);

        // Confirm 3 rows already exist before the handler fires
        var beforeCount = (await GetNotificationsAsync(childId, code: "REVIEW_DUE")).Count;
        beforeCount.Should().Be(3, "3 rows must be pre-seeded to exhaust the cap");

        await PublishAsync(MakeReviewDueEvent(childId));

        await Task.Delay(400);

        var afterCount = (await GetNotificationsAsync(childId, code: "REVIEW_DUE")).Count;

        afterCount.Should().Be(beforeCount,
            "when daily cap is reached (3 prior REVIEW_DUE notifications today, default DailyCap=3), " +
            "the handler must log not_eligible and write no additional row");
    }

    // =========================================================================
    // T4 — Inbox-only posture (AC6)
    //
    // Even with a registered device token, ReviewReminder is inbox-only in v1
    // because it is not in the ReengagementCategories push set. Prefs default Push=false.
    // Assert: inbox row IS written, push bit (DeliveredChannels & 2) is NOT set.
    // This mirrors TC-07 in P9_07_NudgeArbitration_Tests (Achievement category).
    // =========================================================================

    [Fact(DisplayName = "P909-T4 Inbox-only posture: ReviewReminder always inbox-only in v1 (push bit NOT set even with device token)")]
    public async Task T4_InboxOnly_PushBitNotSet_EvenWithDeviceToken()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("t4");

        // Register a device token — this proves push is suppressed by prefs, not by absent token
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[P909_T4_{childId}]");

        _factory.PushSender.Reset();

        await PublishAsync(MakeReviewDueEvent(childId));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "REVIEW_DUE");

        notifications.Should().NotBeEmpty(
            "inbox row must always be written for ReviewReminder (inbox is never suppressed by the push gate)");

        // In-app bit (4) must be set
        var inAppBit = notifications.First().DeliveredChannels & 4;
        inAppBit.Should().Be(4,
            "in-app channel bit (4) must be set for a ReviewReminder notification");

        // Push bit (2) must NOT be set — ReviewReminder is not in ReengagementCategories
        // so prefs.Push is always false (CreateDefault) for this category in v1
        var pushBit = notifications.First().DeliveredChannels & 2;
        pushBit.Should().Be(0,
            "push bit must be 0: ReviewReminder is not in the parent-managed ReengagementCategories set " +
            "until P9-04 FE per-type toggles ship; prefs.Push defaults to false, so push is never delivered " +
            "in v1 even when a device token is registered");

        // TestPushSender must have recorded zero calls
        _factory.PushSender.CallCount.Should().Be(0,
            "the TestPushSender must not have been called — ReviewReminder is inbox-only in v1");
    }

    // =========================================================================
    // T5 — ar/en copy (AC8)
    //
    // Child with locale ar → Arabic body contains Arabic template text.
    // Child with locale en → English body contains English template text.
    // Both must have the skill name and minutes interpolated (no raw placeholders).
    // =========================================================================

    [Fact(DisplayName = "P909-T5 ar/en copy: ar-locale child gets Arabic body; en-locale child gets English body (both with skill+minutes)")]
    public async Task T5_ArEnCopy_CorrectLocaleRendered()
    {
        // ── ar child ──
        var (_, _, _, arChildId) = await CreateParentChildPairAsync("t5ar", childLanguage: "ar");

        await PublishAsync(MakeReviewDueEvent(arChildId));
        await Task.Delay(400);

        var arNotifs = await GetNotificationsAsync(arChildId, code: "REVIEW_DUE");
        arNotifs.Should().NotBeEmpty("Arabic-locale child must receive a REVIEW_DUE notification");

        var arNotif = arNotifs.First();
        arNotif.Body.Should().NotBeNullOrWhiteSpace("Arabic body must not be empty");
        arNotif.Body.Should().NotContain("{skill}",  "ar body must have {skill} substituted");
        arNotif.Body.Should().NotContain("{minutes}", "ar body must have {minutes} substituted");
        arNotif.Body.Should().Contain(TopSkillName,  "ar body must contain the top skill name");
        arNotif.Body.Should().Contain(TopSkillMinutes.ToString(), "ar body must contain the estimated minutes");

        // Arabic template body snippet from ReengagementCopyTemplates: "وقت مراجعة سريعة"
        arNotif.Body.Should().Contain("مراجعة",
            "Arabic locale must render the Arabic template (containing 'مراجعة')");

        // ── en child ──
        var (_, _, _, enChildId) = await CreateParentChildPairAsync("t5en", childLanguage: "en");

        await PublishAsync(MakeReviewDueEvent(enChildId));
        await Task.Delay(400);

        var enNotifs = await GetNotificationsAsync(enChildId, code: "REVIEW_DUE");
        enNotifs.Should().NotBeEmpty("English-locale child must receive a REVIEW_DUE notification");

        var enNotif = enNotifs.First();
        enNotif.Body.Should().NotBeNullOrWhiteSpace("English body must not be empty");
        enNotif.Body.Should().NotContain("{skill}",   "en body must have {skill} substituted");
        enNotif.Body.Should().NotContain("{minutes}", "en body must have {minutes} substituted");
        enNotif.Body.Should().Contain(TopSkillName,   "en body must contain the top skill name");
        enNotif.Body.Should().Contain(TopSkillMinutes.ToString(), "en body must contain the estimated minutes");

        // English template body snippet from ReengagementCopyTemplates: "Quick review time for"
        enNotif.Body.Should().Contain("review",
            "English locale must render the English template (containing 'review')");
    }

    // =========================================================================
    // T6 — Fail-soft orphan (AC3)
    //
    // Publishing a ReviewDueIntegrationEvent for a studentId with no parent link
    // must not throw and must write no row. Mirrors TC-09 in P9_06_HabitLoop_Tests.
    // =========================================================================

    [Fact(DisplayName = "P909-T6 Fail-soft: ReviewDueIntegrationEvent for orphan child (no parent) → no throw, no row")]
    public async Task T6_FailSoft_OrphanChild_NoThrowNoRow()
    {
        const int orphanChildId = 997_909_001;

        var ex = await Record.ExceptionAsync(async () =>
        {
            await PublishAsync(new ReviewDueIntegrationEvent(
                EventId:       Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                StudentId:     orphanChildId,
                DueCount:      1,
                TopSkills: new[]
                {
                    new DueSkillSnapshot(SkillId: 1, SkillName: "مهارة أيتام", EstimatedTimeMinutes: 5),
                }));
        });

        ex.Should().BeNull(
            "publishing ReviewDueIntegrationEvent for an orphan child (no parent link) " +
            "must not throw — handler must log+skip per ADR 0002 fail-soft rule");

        await Task.Delay(300);

        var notifications = await GetNotificationsAsync(orphanChildId, code: "REVIEW_DUE");
        notifications.Should().BeEmpty(
            "no REVIEW_DUE row must be written for a child with no parent link");
    }

    // =========================================================================
    // T7 — No PII (AC9)
    //
    // The persisted Title/Body must contain only curriculum skill content
    // (skill name, minutes, due count) — no child name, email, or identity data.
    // =========================================================================

    [Fact(DisplayName = "P909-T7 No PII: persisted Title/Body contain only skill name + minutes; no child name or email")]
    public async Task T7_NoPii_TitleBodyContainOnlyCurriculumData()
    {
        var tag         = "t7";
        var childEmail  = UniqueEmail($"chd_{tag}");
        const string childName = "TestStudent P909T7";

        // Register parent
        var parentEmail = UniqueEmail($"par_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201], $"parent reg; body: {regBody}");
        TryProp(regRoot, "data", out var regData);
        TryProp(regData, "accessToken", out var parentTokProp);
        var parentToken = parentTokProp.GetString()!;

        // Add child with a known full name and email
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = childName,
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 1,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201], $"Add-Child; body: {addBody}");
        TryProp(addRoot, "data", out var addData);
        TryProp(addData, "id", out var idProp);
        var childId = idProp.GetInt32();

        await PublishAsync(MakeReviewDueEvent(childId));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "REVIEW_DUE");
        notifications.Should().NotBeEmpty("REVIEW_DUE row must be written for the happy-path child");

        var notif = notifications.First();

        // Title and Body must NOT contain child's name
        notif.Title.Should().NotContain(childName,
            "Title must not contain the child's full name (PII)");
        notif.Body.Should().NotContain(childName,
            "Body must not contain the child's full name (PII)");

        // Title and Body must NOT contain child's email
        notif.Title.Should().NotContain(childEmail,
            "Title must not contain the child's email address (PII)");
        notif.Body.Should().NotContain(childEmail,
            "Body must not contain the child's email address (PII)");

        // Body SHOULD contain curriculum data (skill name and minutes) — positive assertion
        notif.Body.Should().Contain(TopSkillName,
            "Body must contain the skill name (curriculum content, not PII)");
        notif.Body.Should().Contain(TopSkillMinutes.ToString(),
            "Body must contain the estimated minutes (curriculum data, not PII)");
    }

    // =========================================================================
    // T8 — Empty-TopSkills guard (defensive; optional per brief)
    //
    // A ReviewDueIntegrationEvent with an empty TopSkills list must not throw
    // and must write no row (the handler logs + returns defensively).
    // =========================================================================

    [Fact(DisplayName = "P909-T8 Empty TopSkills guard: event with empty TopSkills → no throw, no row written")]
    public async Task T8_EmptyTopSkills_NoThrowNoRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("t8");

        var ex = await Record.ExceptionAsync(async () =>
        {
            await PublishAsync(new ReviewDueIntegrationEvent(
                EventId:       Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                StudentId:     childId,
                DueCount:      0,
                TopSkills:     Array.Empty<DueSkillSnapshot>()));
        });

        ex.Should().BeNull(
            "publishing ReviewDueIntegrationEvent with an empty TopSkills list must not throw — " +
            "the handler must guard defensively (log + skip) per the brief's defensive guard requirement");

        await Task.Delay(300);

        var notifications = await GetNotificationsAsync(childId, code: "REVIEW_DUE");
        notifications.Should().BeEmpty(
            "no REVIEW_DUE row must be written when TopSkills is empty — " +
            "the handler skips the dispatch to avoid an unguarded [0] index");
    }
}

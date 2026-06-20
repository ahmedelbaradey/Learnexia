using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
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
/// P9-06 Weekly-challenge ending-soon reminder integration tests.
///
/// Two test angles (as specified in the brief):
///
/// ANGLE 1 — Gamification emit (StreakAtRiskJob Pass 3, mirrors P4-09 T09 for the weekly cadence):
///   G1  In-window incomplete weekly mission → StreakAtRiskJob publishes exactly one
///       WeeklyMissionReminderIntegrationEvent, which the Notifications consumer writes as a
///       WeeklyChallenge/WEEKLY_CHALLENGE_REMINDER inbox row with correct Category (=8).
///   G2  Negative: completed weekly mission → no event → no row.
///   G3  Negative: mission out-of-window (PeriodEndUtc > now+48h) → no event → no row.
///   G4  Negative: daily mission in window → no WeeklyChallenge row (cadence filter holds).
///   G5  Multi-mission per student: two weekly missions both in-window → one row (most-urgent dedupe).
///
/// ANGLE 2 — Notifications consumer (mirrors P9_09_ReviewReminder_Tests):
///   N1  Happy path: WeeklyMissionReminderIntegrationEvent → one WEEKLY_CHALLENGE_REMINDER row,
///       Category=WeeklyChallenge(8), non-empty Title/Body, {remaining} substituted correctly.
///   N2  ar/en copy: ar-locale child → Arabic body contains Arabic template text ("تحدي الأسبوع");
///       en-locale child → English body contains "ending soon"; no raw {remaining} placeholder.
///   N3  Period-scoped dedupe (Option A): same PeriodKey published twice → exactly one inbox row.
///       Different PeriodKey → two rows (one per period). This is the key Option-A behavior.
///   N4  Not-eligible: daily cap reached → no additional row written.
///   N5  Fail-soft orphan: WeeklyMissionReminderIntegrationEvent for studentId with no parent
///       → no throw, no row.
///   N6  Inbox-only in v1: push bit (DeliveredChannels &amp; 2) NOT set even with device token.
///   N7  No PII: Title/Body contain only work-remaining counts; no child name or email.
///   N8  Category sentinel guard: persisted Category column value == 8 (WeeklyChallenge), not 6
///       (System default), confirming HasSentinel(-1) fix covers value 8.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P9_06_WeeklyMissionReminder_Tests : IAsyncLifetime
{
    // ── URL constants (mirrors P9_09 pattern) ─────────────────────────────────
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";
    private const string ChildPrefsPath     = "api/Notifications/Preferences/Children/{0}/Reengagement";

    // Fixture event values
    private const int  SampleProgress = 3;
    private const int  SampleTarget   = 15;
    private const string SamplePeriodKey = "W:2099-22"; // far future, unique per test run

    // ── Infrastructure ─────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient             _client;

    public P9_06_WeeklyMissionReminder_Tests(LearnexiaWebAppFactory factory)
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

        // Seed the mission definition catalog (weekly missions live here).
        var missionSeeder = scope.ServiceProvider.GetRequiredService<MissionSeeder>();
        await missionSeeder.SeedAsync();

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
        => $"p906wm_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
                FullName         = $"Test Child P906WM {tag}",
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
    /// Seeds a StudentXpProfile for the given studentId (insert or update).
    /// Required so that StreakAtRiskJob can JOIN StudentMissions → StudentXpProfile.StudentId.
    /// </summary>
    private async Task EnsureXpProfileAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var existing = await db.StudentXpProfiles.FirstOrDefaultAsync(p => p.StudentId == studentId);
        if (existing is null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO gamification.""StudentXpProfiles""
                   (""StudentId"", ""TotalXp"", ""CurrentLevel"", ""LastAwardAtUtc"",
                    ""CurrentStreak"", ""LongestStreak"", ""LastActivityDateUtc"",
                    ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {0}, {1}, {DateTime.UtcNow},
                           {0}, {0}, {(DateTime?)null},
                           {DateTime.UtcNow}, {0})");
        }
    }

    /// <summary>
    /// Returns the StudentXpProfileId for the given studentId.
    /// Must be called AFTER EnsureXpProfileAsync.
    /// </summary>
    private async Task<int> GetXpProfileIdAsync(int studentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var profile = await db.StudentXpProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);
        return profile?.Id ?? 0;
    }

    /// <summary>
    /// Retrieves the first MissionDefinition ID with MissionType.Weekly from the catalog.
    /// MissionSeeder must have run (called in InitializeAsync).
    /// </summary>
    private async Task<(int MissionDefinitionId, int Target)> GetWeeklyMissionDefinitionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var def = await db.MissionDefinitions
            .AsNoTracking()
            .Where(d => d.Cadence == MissionType.Weekly)
            .OrderBy(d => d.SortOrder)
            .FirstOrDefaultAsync();
        def.Should().NotBeNull("MissionSeeder must have inserted at least one weekly mission definition");
        return (def!.Id, def.Target);
    }

    /// <summary>
    /// Retrieves the first daily MissionDefinition ID.
    /// </summary>
    private async Task<(int MissionDefinitionId, int Target)> GetDailyMissionDefinitionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var def = await db.MissionDefinitions
            .AsNoTracking()
            .Where(d => d.Cadence == MissionType.Daily)
            .OrderBy(d => d.SortOrder)
            .FirstOrDefaultAsync();
        def.Should().NotBeNull("MissionSeeder must have inserted at least one daily mission definition");
        return (def!.Id, def.Target);
    }

    /// <summary>
    /// Seeds a StudentMission row directly via raw SQL (bypasses private setters on the domain entity).
    /// PeriodEndUtc is the critical field tested by Pass 3.
    /// </summary>
    private async Task SeedStudentMissionAsync(
        int studentXpProfileId,
        int missionDefinitionId,
        MissionType missionType,
        MissionStatus status,
        DateTime periodEndUtc,
        string periodKey,
        int progress = 0,
        int target   = 15)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var conn = (Npgsql.NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO gamification.""StudentMissions""
               (""StudentXpProfileId"", ""MissionDefinitionId"", ""MissionType"", ""PeriodKey"",
                ""PeriodStartUtc"", ""PeriodEndUtc"", ""Target"", ""Progress"", ""Status"",
                ""CompletedAtUtc"", ""CreatedAt"", ""CreatedBy"")
               VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
               ON CONFLICT DO NOTHING";
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentXpProfileId });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = missionDefinitionId });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (int)missionType });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = periodKey });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = periodEndUtc.AddDays(-7) }); // PeriodStartUtc
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = periodEndUtc });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = target });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = progress });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (int)status });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DBNull.Value });            // CompletedAtUtc
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = DateTime.UtcNow });         // CreatedAt
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 0 });                       // CreatedBy
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }
    }

    /// <summary>
    /// Registers a device token directly in the DB (mirrors P9_09 helper).
    /// </summary>
    private async Task RegisterDeviceTokenDirectlyAsync(int userId, string expoPushToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var nowUtc = DateTime.UtcNow;
        await db.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO notifications.""UserDeviceTokens""
               (""UserId"", ""ExpoPushToken"", ""Platform"", ""RegisteredAtUtc"", ""LastUsedAtUtc"", ""IsActive"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({userId}, '{expoPushToken}', 'ios', '{nowUtc:O}', '{nowUtc:O}', true,
                       '{nowUtc:O}', 0)
               ON CONFLICT DO NOTHING");
    }

    /// <summary>
    /// Seeds an in-app Notification row directly (to exhaust daily cap before the handler fires).
    /// Mirrors P9_09 helper.
    /// </summary>
    private async Task SeedInAppNotificationAsync(int childId, string code, NotificationCategory category)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
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
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (int)category });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = code });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "{}" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 4 }); // inApp bit
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

    /// <summary>Builds a standard WeeklyMissionReminderIntegrationEvent with the given PeriodKey.</summary>
    private static WeeklyMissionReminderIntegrationEvent MakeWeeklyReminderEvent(
        int childId,
        string? periodKey = null,
        int progress     = SampleProgress,
        int target       = SampleTarget,
        int minutesLeft  = 1200) // 20h default
        => new(
            EventId:          Guid.NewGuid(),
            OccurredOnUtc:    DateTime.UtcNow,
            StudentId:        childId,
            Progress:         progress,
            Target:           target,
            MinutesRemaining: minutesLeft,
            PeriodKey:        periodKey ?? SamplePeriodKey);

    // =========================================================================
    // ANGLE 1 — Gamification emit (StreakAtRiskJob Pass 3)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // G1: In-window incomplete weekly mission → StreakAtRiskJob publishes event
    //     → WeeklyChallenge/WEEKLY_CHALLENGE_REMINDER row written (Category=8).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-G1 In-window incomplete weekly mission → StreakAtRiskJob job writes WEEKLY_CHALLENGE_REMINDER row")]
    public async Task G1_InWindow_IncompleteMission_JobWritesWeeklyChallengeRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("g1");
        await EnsureXpProfileAsync(childId);
        var profileId = await GetXpProfileIdAsync(childId);
        var (defId, target) = await GetWeeklyMissionDefinitionAsync();

        // Seed an incomplete weekly mission expiring in 24h (within the 48h lookahead window).
        var periodEnd = DateTime.UtcNow.AddHours(24);
        await SeedStudentMissionAsync(
            profileId, defId, MissionType.Weekly,
            MissionStatus.InProgress,
            periodEnd,
            periodKey: $"W:G1_{childId}",
            progress: 3,
            target: target);

        _factory.PushSender.Reset();

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StreakAtRiskJob>();
        await job.RunAsync(CancellationToken.None);

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");

        notifications.Should().NotBeEmpty(
            "StreakAtRiskJob Pass 3 must produce a WEEKLY_CHALLENGE_REMINDER notification " +
            "for a child with an in-window, incomplete weekly mission");

        // Core assertion: Category must be WeeklyChallenge (value 8).
        notifications.First().Category.Should().Be(NotificationCategory.WeeklyChallenge,
            "WEEKLY_CHALLENGE_REMINDER must have Category=WeeklyChallenge (int=8)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G2: Negative — completed weekly mission → no event → no row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-G2 Negative: completed weekly mission → StreakAtRiskJob emits NO event → no row")]
    public async Task G2_CompletedMission_NoEvent_NoRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("g2");
        await EnsureXpProfileAsync(childId);
        var profileId = await GetXpProfileIdAsync(childId);
        var (defId, target) = await GetWeeklyMissionDefinitionAsync();

        // Seed a COMPLETED weekly mission that would otherwise be in-window.
        var periodEnd = DateTime.UtcNow.AddHours(24);
        await SeedStudentMissionAsync(
            profileId, defId, MissionType.Weekly,
            MissionStatus.Completed,                // <-- key: completed
            periodEnd,
            periodKey: $"W:G2_{childId}",
            progress: target,
            target: target);

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StreakAtRiskJob>();
        await job.RunAsync(CancellationToken.None);

        await Task.Delay(300);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        notifications.Should().BeEmpty(
            "StreakAtRiskJob must NOT emit a WeeklyMissionReminderIntegrationEvent " +
            "for a mission whose Status == Completed — the WHERE clause excludes Completed");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G3: Negative — mission out-of-window (PeriodEndUtc > now+48h) → no row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-G3 Negative: out-of-window weekly mission (PeriodEndUtc > now+48h) → no row")]
    public async Task G3_OutOfWindow_Mission_NoRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("g3");
        await EnsureXpProfileAsync(childId);
        var profileId = await GetXpProfileIdAsync(childId);
        var (defId, target) = await GetWeeklyMissionDefinitionAsync();

        // PeriodEndUtc is 72h in the future — beyond the 48h lookahead window.
        var periodEnd = DateTime.UtcNow.AddHours(72);
        await SeedStudentMissionAsync(
            profileId, defId, MissionType.Weekly,
            MissionStatus.NotStarted,
            periodEnd,
            periodKey: $"W:G3_{childId}",
            target: target);

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StreakAtRiskJob>();
        await job.RunAsync(CancellationToken.None);

        await Task.Delay(300);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        notifications.Should().BeEmpty(
            "StreakAtRiskJob must NOT publish a weekly reminder for a mission whose " +
            "PeriodEndUtc is beyond the 48h lookahead window");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G4: Daily mission in window does NOT produce a WeeklyChallenge row.
    //     This verifies the MissionType filter in Pass 3 (Weekly only).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-G4 Cadence filter: daily mission in window → no WeeklyChallenge row")]
    public async Task G4_DailyMission_NoWeeklyChallengeRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("g4");
        await EnsureXpProfileAsync(childId);
        var profileId = await GetXpProfileIdAsync(childId);
        var (dailyDefId, dailyTarget) = await GetDailyMissionDefinitionAsync();

        // Seed a daily mission in-window (ends in 3h — well within daily 6h lookahead AND 48h weekly).
        var periodEnd = DateTime.UtcNow.AddHours(3);
        await SeedStudentMissionAsync(
            profileId, dailyDefId, MissionType.Daily,
            MissionStatus.InProgress,
            periodEnd,
            periodKey: $"D:G4_{childId}",
            progress: 1,
            target: dailyTarget);

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StreakAtRiskJob>();
        await job.RunAsync(CancellationToken.None);

        await Task.Delay(300);

        var weeklyRows = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        weeklyRows.Should().BeEmpty(
            "Pass 3 of StreakAtRiskJob must only process MissionType.Weekly missions; " +
            "a daily mission in window must NOT produce a WeeklyChallenge row");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G5: Multi-mission per student — two weekly missions both in-window.
    //     The job should publish only ONE event (most-urgent mission wins).
    //     Consumer-side Option-A dedupe ensures only one inbox row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-G5 Multi-mission: two in-window weekly missions for one student → exactly one WEEKLY_CHALLENGE_REMINDER row")]
    public async Task G5_MultiMission_OneEventPerStudent()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("g5");
        await EnsureXpProfileAsync(childId);
        var profileId = await GetXpProfileIdAsync(childId);

        // Get two different weekly mission definitions.
        using var defScope = _factory.Services.CreateScope();
        var db = defScope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var defs = await db.MissionDefinitions
            .AsNoTracking()
            .Where(d => d.Cadence == MissionType.Weekly)
            .OrderBy(d => d.SortOrder)
            .Take(2)
            .ToListAsync();
        defs.Should().HaveCountGreaterThanOrEqualTo(2,
            "MissionSeeder must have inserted at least 2 weekly mission definitions (needed for G5)");

        // Seed two incomplete weekly missions for the same student: one more urgent (24h) one less (36h).
        await SeedStudentMissionAsync(
            studentXpProfileId:  profileId,
            missionDefinitionId: defs[0].Id,
            missionType:         MissionType.Weekly,
            status:              MissionStatus.InProgress,
            periodEndUtc:        DateTime.UtcNow.AddHours(24),    // more urgent
            periodKey:           $"W:G5A_{childId}",
            progress:            5,
            target:              defs[0].Target);

        await SeedStudentMissionAsync(
            studentXpProfileId:  profileId,
            missionDefinitionId: defs[1].Id,
            missionType:         MissionType.Weekly,
            status:              MissionStatus.NotStarted,
            periodEndUtc:        DateTime.UtcNow.AddHours(36),    // less urgent
            periodKey:           $"W:G5B_{childId}",
            progress:            0,
            target:              defs[1].Target);

        using var scope = _factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<StreakAtRiskJob>();
        await job.RunAsync(CancellationToken.None);

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");

        // The Option-A period-scoped dedupe (TryAcquireTierAsync keyed on PeriodKey) means the
        // second in-run event for the second PeriodKey may produce a second row.  However, the
        // StreakAtRiskJob's own GroupBy-First ensures at most one publish per student per run
        // (the most-urgent mission wins). So we expect exactly one row for this student.
        notifications.Should().HaveCount(1,
            "StreakAtRiskJob Pass 3 must publish at most one WeeklyMissionReminderIntegrationEvent " +
            "per student per run (GroupBy-First dedupe picks the most-urgent mission); " +
            "the consumer then writes exactly one WEEKLY_CHALLENGE_REMINDER row");
    }

    // =========================================================================
    // ANGLE 2 — Notifications consumer (publish WeeklyMissionReminderIntegrationEvent directly)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // N1: Happy path — event → one WEEKLY_CHALLENGE_REMINDER row,
    //     Category=WeeklyChallenge(8), {remaining} substituted.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N1 Happy path: WeeklyMissionReminderIntegrationEvent → WEEKLY_CHALLENGE_REMINDER row, Category=WeeklyChallenge(8), body rendered")]
    public async Task N1_HappyPath_EventWritesWeeklyChallengeRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("n1");
        var periodKey = $"W:N1_{childId}_{Guid.NewGuid():N}";

        await PublishAsync(MakeWeeklyReminderEvent(childId,
            periodKey: periodKey,
            progress: SampleProgress,
            target:   SampleTarget));

        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");

        notifications.Should().HaveCount(1,
            "WeeklyMissionReminderIntegrationEventHandler must write exactly one " +
            "WEEKLY_CHALLENGE_REMINDER row for an eligible child");

        var notif = notifications.First();

        notif.Category.Should().Be(NotificationCategory.WeeklyChallenge,
            "WEEKLY_CHALLENGE_REMINDER Category must be WeeklyChallenge (int value 8)");

        notif.Code.Should().Be("WEEKLY_CHALLENGE_REMINDER",
            "the notification code must be WEEKLY_CHALLENGE_REMINDER");

        notif.Title.Should().NotBeNullOrWhiteSpace("Title must not be empty");
        notif.Body.Should().NotBeNullOrWhiteSpace("Body must not be empty");

        // {remaining} placeholder must be substituted (= Target - Progress = 12)
        notif.Body.Should().NotContain("{remaining}",
            "{remaining} placeholder must be substituted with the actual remaining value");
        var expectedRemaining = (SampleTarget - SampleProgress).ToString();
        notif.Body.Should().Contain(expectedRemaining,
            "Body must contain the rendered remaining value (Target - Progress)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N8: Category sentinel guard — persisted Category int == 8 (not 6 = System default).
    //     Mirrors the P9-06 TC-04 sentinel-fix verification for WeeklyChallenge.
    //     HasSentinel(-1) on NotificationConfig forces EF to INSERT value 8 explicitly.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N8 Sentinel guard: persisted Category int==8 (WeeklyChallenge), NOT 6 (System SQL default) — HasSentinel(-1) fix covers value 8")]
    public async Task N8_SentinelGuard_CategoryInt8_NotSystemDefault6()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("n8");
        var periodKey = $"W:N8_{childId}_{Guid.NewGuid():N}";

        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKey));
        await Task.Delay(400);

        using var scope = _factory.Services.CreateScope();
        var notifDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        // Query the raw int stored in the DB column to confirm EF inserted 8, not 6.
        var conn = (Npgsql.NpgsqlConnection)notifDb.Database.GetDbConnection();
        await conn.OpenAsync();
        int? storedCategoryInt = null;
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT ""Category"" FROM notifications.""Notifications""
                                WHERE ""RecipientExternalUserId"" = $1 AND ""Code"" = 'WEEKLY_CHALLENGE_REMINDER'
                                LIMIT 1";
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = childId });
            var result = await cmd.ExecuteScalarAsync();
            if (result is not null and not DBNull)
                storedCategoryInt = Convert.ToInt32(result);
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
                await conn.CloseAsync();
        }

        storedCategoryInt.Should().NotBeNull(
            "A WEEKLY_CHALLENGE_REMINDER notification row must exist for the eligible child");

        storedCategoryInt.Should().Be(8,
            "HasSentinel(-1) on NotificationConfig forces EF Core to INSERT the explicit Category " +
            "value 8 (WeeklyChallenge) rather than omitting the column and letting PostgreSQL apply " +
            "the SQL default value 6 (System). Value 8 ≠ 0 ≠ -1 so no sentinel collision.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N2: ar/en copy — ar locale → Arabic body; en locale → English body.
    //     Both have {remaining} substituted; no raw placeholder remains.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N2 ar/en copy: ar-locale child gets Arabic body ('تحدي الأسبوع'); en-locale child gets English body ('ending soon')")]
    public async Task N2_ArEnCopy_CorrectLocaleRendered()
    {
        // ── ar child ──
        var (_, _, _, arChildId) = await CreateParentChildPairAsync("n2ar", childLanguage: "ar");
        var arPeriodKey = $"W:N2ar_{arChildId}_{Guid.NewGuid():N}";

        await PublishAsync(MakeWeeklyReminderEvent(arChildId, periodKey: arPeriodKey));
        await Task.Delay(400);

        var arNotifs = await GetNotificationsAsync(arChildId, code: "WEEKLY_CHALLENGE_REMINDER");
        arNotifs.Should().NotBeEmpty("Arabic-locale child must receive a WEEKLY_CHALLENGE_REMINDER notification");

        var arNotif = arNotifs.First();
        arNotif.Body.Should().NotBeNullOrWhiteSpace("Arabic body must not be empty");
        arNotif.Body.Should().NotContain("{remaining}", "ar body must have {remaining} substituted");

        // Arabic template body contains Arabic text.
        arNotif.Body.Should().Contain("تحدي الأسبوع",
            "Arabic locale must render the Arabic template (containing 'تحدي الأسبوع')");

        // ── en child ──
        var (_, _, _, enChildId) = await CreateParentChildPairAsync("n2en", childLanguage: "en");
        var enPeriodKey = $"W:N2en_{enChildId}_{Guid.NewGuid():N}";

        await PublishAsync(MakeWeeklyReminderEvent(enChildId, periodKey: enPeriodKey));
        await Task.Delay(400);

        var enNotifs = await GetNotificationsAsync(enChildId, code: "WEEKLY_CHALLENGE_REMINDER");
        enNotifs.Should().NotBeEmpty("English-locale child must receive a WEEKLY_CHALLENGE_REMINDER notification");

        var enNotif = enNotifs.First();
        enNotif.Body.Should().NotBeNullOrWhiteSpace("English body must not be empty");
        enNotif.Body.Should().NotContain("{remaining}", "en body must have {remaining} substituted");

        // English template body: "Your weekly challenge ends soon — {remaining} to go!"
        enNotif.Body.Should().Contain("ends soon",
            "English locale must render the English template (containing 'ends soon')");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N3: Period-scoped dedupe (Option A).
    //     Same PeriodKey published twice → exactly one inbox row.
    //     Different PeriodKey → two rows (one per period).
    //     This tests the key behavior that distinguishes Option-A from the day-keyed Option-B.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N3 Option-A period dedupe: same PeriodKey twice → one row; different PeriodKey → two rows")]
    public async Task N3_PeriodScopedDedupe_SamePeriodKey_OneRow_DifferentKey_TwoRows()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("n3");

        var periodKeyA = $"W:N3A_{childId}_{Guid.NewGuid():N}";
        var periodKeyB = $"W:N3B_{childId}_{Guid.NewGuid():N}";

        // ── Publish twice with the SAME PeriodKey → only one row (dedupe) ──
        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKeyA));
        await Task.Delay(300);

        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKeyA));
        await Task.Delay(300);

        var rowsAfterSamePeriod = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        rowsAfterSamePeriod.Should().HaveCount(1,
            "Option-A period-scoped TryAcquireTierAsync(studentId, 'WEEKLY_CHALLENGE:{periodKey}', 72h) " +
            "must deduplicate: publishing WeeklyMissionReminderIntegrationEvent twice with the SAME " +
            "PeriodKey must result in exactly one WEEKLY_CHALLENGE_REMINDER row");

        // ── Publish with a DIFFERENT PeriodKey → second row written ──
        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKeyB));
        await Task.Delay(300);

        var rowsAfterDifferentPeriod = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        rowsAfterDifferentPeriod.Should().HaveCount(2,
            "A different PeriodKey has a different Redis key and must NOT be blocked by the prior " +
            "period's dedupe lock; a separate weekly period = a separate, legitimate reminder");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N4: Not-eligible — daily cap reached → no additional row.
    //     Strategy: exhaust the default DailyCap(=3) by seeding 3 WeeklyChallenge
    //     inbox rows for today, then publish the event.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N4 Not-eligible: daily cap reached (default cap=3, 3 rows pre-seeded) → no additional row")]
    public async Task N4_NotEligible_DailyCapReached_NoRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("n4");

        // Exhaust the default DailyCap=3 for WeeklyChallenge category.
        await SeedInAppNotificationAsync(childId, "WEEKLY_CHALLENGE_REMINDER", NotificationCategory.WeeklyChallenge);
        await SeedInAppNotificationAsync(childId, "WEEKLY_CHALLENGE_REMINDER", NotificationCategory.WeeklyChallenge);
        await SeedInAppNotificationAsync(childId, "WEEKLY_CHALLENGE_REMINDER", NotificationCategory.WeeklyChallenge);

        var beforeCount = (await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER")).Count;
        beforeCount.Should().Be(3, "3 rows must be pre-seeded before the handler fires");

        var periodKey = $"W:N4_{childId}_{Guid.NewGuid():N}";
        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKey));
        await Task.Delay(400);

        var afterCount = (await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER")).Count;
        afterCount.Should().Be(beforeCount,
            "when the daily cap is reached the handler must log not_eligible and write no additional row");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N5: Fail-soft orphan — event for studentId with no parent → no throw, no row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N5 Fail-soft: WeeklyMissionReminderIntegrationEvent for orphan child (no parent) → no throw, no row")]
    public async Task N5_FailSoft_OrphanChild_NoThrowNoRow()
    {
        const int orphanChildId = 997_906_001; // distinct magic id for P906WM-N5
        var periodKey = $"W:N5_{orphanChildId}_{Guid.NewGuid():N}";

        var ex = await Record.ExceptionAsync(async () =>
        {
            await PublishAsync(MakeWeeklyReminderEvent(orphanChildId, periodKey: periodKey));
        });

        ex.Should().BeNull(
            "publishing WeeklyMissionReminderIntegrationEvent for a child with no parent link " +
            "must not throw — handler must log+skip per ADR 0002 fail-soft rule");

        await Task.Delay(300);

        var notifications = await GetNotificationsAsync(orphanChildId, code: "WEEKLY_CHALLENGE_REMINDER");
        notifications.Should().BeEmpty(
            "no WEEKLY_CHALLENGE_REMINDER row must be written for a child with no parent link");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N6: Inbox-only in v1 — push bit (DeliveredChannels & 2) NOT set even
    //     when a device token is registered. WeeklyChallenge is not in
    //     ReengagementCategories, so prefs.Push defaults false.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N6 Inbox-only in v1: WeeklyChallenge push bit NOT set even with registered device token")]
    public async Task N6_InboxOnly_PushBitNotSet_EvenWithDeviceToken()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("n6");

        // Register a device token to prove push is suppressed by prefs, not by absent token.
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[P906WM_N6_{childId}]");
        _factory.PushSender.Reset();

        var periodKey = $"W:N6_{childId}_{Guid.NewGuid():N}";
        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKey));
        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        notifications.Should().NotBeEmpty(
            "inbox row must be written for eligible child (inbox is never suppressed by the push gate)");

        // In-app bit (4) must be set.
        (notifications.First().DeliveredChannels & 4).Should().Be(4,
            "in-app channel bit (4) must be set for a WeeklyChallenge notification");

        // Push bit (2) must NOT be set — WeeklyChallenge is not in ReengagementCategories,
        // so prefs.Push defaults to false in v1 (inbox-only until P9-04 FE per-type toggles ship).
        (notifications.First().DeliveredChannels & 2).Should().Be(0,
            "push bit must be 0: WeeklyChallenge is not in the parent-managed ReengagementCategories set " +
            "until P9-04 FE per-type toggles ship; prefs.Push defaults false → push never delivered in v1");

        // TestPushSender must have been called zero times.
        _factory.PushSender.CallCount.Should().Be(0,
            "the TestPushSender must not have been called — WeeklyChallenge is inbox-only in v1");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // N7: No PII — Title/Body contain only work-remaining count;
    //     no child name, no email address.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P906WM-N7 No PII: Title/Body contain only remaining count; no child name or email")]
    public async Task N7_NoPii_TitleBodyContainNoChildIdentity()
    {
        const string knownChildName = "TestStudent P906WM N7";
        var childEmail = UniqueEmail("n7_chd");
        var parentEmail = UniqueEmail("n7_par");

        // Register parent
        var (regResp, regRoot, regBody) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201], $"parent reg; body: {regBody}");
        TryProp(regRoot, "data", out var regData);
        TryProp(regData, "accessToken", out var parentTokProp);
        var parentToken = parentTokProp.GetString()!;

        // Add child with known full name and email
        var (addResp, addRoot, addBody) = await SendAsync(_client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = knownChildName,
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

        var periodKey = $"W:N7_{childId}_{Guid.NewGuid():N}";
        await PublishAsync(MakeWeeklyReminderEvent(childId, periodKey: periodKey));
        await Task.Delay(400);

        var notifications = await GetNotificationsAsync(childId, code: "WEEKLY_CHALLENGE_REMINDER");
        notifications.Should().NotBeEmpty("WEEKLY_CHALLENGE_REMINDER row must exist for the eligible child");

        var notif = notifications.First();

        notif.Title.Should().NotContain(knownChildName,
            "Title must not contain the child's full name (PII)");
        notif.Body.Should().NotContain(knownChildName,
            "Body must not contain the child's full name (PII)");

        notif.Title.Should().NotContain(childEmail,
            "Title must not contain the child's email address (PII)");
        notif.Body.Should().NotContain(childEmail,
            "Body must not contain the child's email address (PII)");

        // Positive assertion: the rendered remaining count is present (curriculum data, not PII).
        var expectedRemaining = (SampleTarget - SampleProgress).ToString();
        notif.Body.Should().Contain(expectedRemaining,
            "Body must contain the rendered remaining work count (curriculum content, not PII)");
    }
}

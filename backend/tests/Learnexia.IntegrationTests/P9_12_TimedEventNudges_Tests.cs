using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Enums;
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
/// P9-12 integration tests — Timed-event nudges (Notifications consumers of Gamification events).
///
/// Four handlers under test:
///   1. TimedEventStartedIntegrationEventHandler (JOIN fan-out, 1→many)
///   2. TimedEventParticipationProgressIntegrationEventHandler (1:1, halfway)
///   3. TimedEventParticipationEndingSoonIntegrationEventHandler (1:1, ending-soon)
///   4. TimedEventParticipationCompletedIntegrationEventHandler (1:1, completion)
///
/// Coverage (per brief AC1–AC8):
///   J1  Join fan-out: TimedEventStartedIntegrationEvent with 2 eligible students
///       → both get TIMED_EVENT_LIVE inbox row, Category=TimedEvent(9).
///   J2  Join fan-out: stale-activity student (outside eligibility window) → no row.
///   J3  Join fan-out: orphan student (no parent) in cohort → fail-soft, other students still notified.
///   J4  Join fan-out: dedupe — same TimedEventStartedIntegrationEvent published twice
///       → each eligible student gets exactly one TIMED_EVENT_LIVE row.
///
///   P1  Progress (halfway) 1:1 happy path → TIMED_EVENT_PROGRESS row, Category=9,
///       {progress}/{target} substituted in body.
///   P2  Progress: orphan child (no parent) → no throw, no row.
///   P3  Progress: dedupe — same event+timedEventId published twice → one row.
///   P4  Progress: different timedEventId for the same student → separate rows (phases don't collide).
///
///   E1  Ending-soon 1:1 happy path → TIMED_EVENT_ENDING row, Category=9,
///       {remaining} substituted ({target}-{progress}).
///   E2  Ending-soon: orphan child → no throw, no row.
///   E3  Ending-soon: dedupe — same event published twice → one row.
///
///   C1  Completion 1:1 happy path → TIMED_EVENT_COMPLETED row, Category=9, body rendered.
///   C2  Completion: orphan child → no throw, no row.
///   C3  Completion: dedupe — same event published twice → one row.
///
///   X1  Cross-phase isolation: publishing LIVE + PROGRESS + ENDING + COMPLETED for the
///       same TimedEventId → four distinct rows (dedupe keys are phase-prefixed, no collision).
///   X2  Not-eligible (daily cap exhausted): progress event → no row written.
///   X3  Inbox-only v1: push bit (DeliveredChannels &amp; 2) NOT set even with device token.
///   X4  ar copy: Arabic-locale child → Arabic body text on TIMED_EVENT_LIVE.
///   X5  en copy: English-locale child → English body text on TIMED_EVENT_LIVE.
///   X6  No PII: Title/Body contain no child name or email — only generic copy + numeric scalars.
///   X7  Category sentinel: raw DB Category int == 9 (not 6 SQL default) for TIMED_EVENT_LIVE.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P9_12_TimedEventNudges_Tests : IAsyncLifetime
{
    // ── URL constants (mirrors P9_06 / P9_09 patterns) ────────────────────────
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // Shared fixture scalars
    private const int SampleProgress = 4;
    private const int SampleTarget   = 10;

    // ── Infrastructure ─────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient             _client;

    public P9_12_TimedEventNudges_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

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

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string UniqueEmail(string tag = "")
        => $"p912_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
                FullName         = $"Test Child P912 {tag}",
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
    /// Seeds a StudentXpProfile for the given student, setting LastActivityDateUtc.
    /// The LastActivityDateUtc is the eligibility signal for IEligibleStudentsForTimedEventQuery.
    /// </summary>
    private async Task SeedXpProfileAsync(
        int studentId,
        DateOnly? lastActivityDate = null,
        int hearts = 5,
        int totalXp = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        var existing = await db.StudentXpProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);

        if (existing is null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO gamification.""StudentXpProfiles""
                   (""StudentId"", ""TotalXp"", ""CurrentLevel"", ""LastAwardAtUtc"",
                    ""CurrentStreak"", ""LongestStreak"", ""LastActivityDateUtc"",
                    ""Hearts"", ""LastHeartRefillAtUtc"", ""CurrentTier"",
                    ""FreezeBalance"", ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {totalXp}, {1}, {DateTime.UtcNow},
                           {0}, {0}, {lastActivityDate},
                           {hearts}, {(DateTime?)null}, {(int)LeagueTier.Bronze},
                           {0}, {DateTime.UtcNow}, {0})");
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE gamification.""StudentXpProfiles""
                   SET ""LastActivityDateUtc"" = {lastActivityDate},
                       ""Hearts"" = {hearts},
                       ""TotalXp"" = {totalXp}
                   WHERE ""StudentId"" = {studentId}");
        }
    }

    /// <summary>
    /// Seeds an in-app Notification row to exhaust the daily cap before the handler fires.
    /// Mirrors the helper pattern from P9_09 and P9_06 tests.
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

    /// <summary>
    /// Registers a device token directly in the DB so the push-posture test is exercisable.
    /// </summary>
    private async Task RegisterDeviceTokenDirectlyAsync(int userId, string expoPushToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var nowUtc  = DateTime.UtcNow;
        await db.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO notifications.""UserDeviceTokens""
               (""UserId"", ""ExpoPushToken"", ""Platform"", ""RegisteredAtUtc"", ""LastUsedAtUtc"", ""IsActive"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({userId}, '{expoPushToken}', 'ios', '{nowUtc:O}', '{nowUtc:O}', true,
                       '{nowUtc:O}', 0)
               ON CONFLICT DO NOTHING");
    }

    /// <summary>
    /// Builds a TimedEventStartedIntegrationEvent for the given timedEventId.
    /// StartUtc / EndUtc default to a 24h window around "now".
    /// </summary>
    private static TimedEventStartedIntegrationEvent MakeStartedEvent(
        int timedEventId,
        string code,
        DateTime? startUtc = null,
        DateTime? endUtc   = null)
    {
        var now   = DateTime.UtcNow;
        var start = startUtc ?? now.AddHours(-1);
        var end   = endUtc   ?? now.AddHours(23);
        return new TimedEventStartedIntegrationEvent(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: now,
            TimedEventId:  timedEventId,
            Code:          code,
            Multiplier:    2.0m,
            Scope:         TimedEventScopeDto.AllXp,
            StartUtc:      start,
            EndUtc:        end);
    }

    private static TimedEventParticipationProgressIntegrationEvent MakeProgressEvent(
        int studentId,
        int timedEventId,
        string code,
        int progress = SampleProgress,
        int target   = SampleTarget)
        => new(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     studentId,
            TimedEventId:  timedEventId,
            Code:          code,
            Progress:      progress,
            Target:        target);

    private static TimedEventParticipationEndingSoonIntegrationEvent MakeEndingSoonEvent(
        int studentId,
        int timedEventId,
        string code,
        int progress         = SampleProgress,
        int target           = SampleTarget,
        int minutesRemaining = 120)
        => new(
            EventId:          Guid.NewGuid(),
            OccurredOnUtc:    DateTime.UtcNow,
            StudentId:        studentId,
            TimedEventId:     timedEventId,
            Code:             code,
            Progress:         progress,
            Target:           target,
            MinutesRemaining: minutesRemaining);

    private static TimedEventParticipationCompletedIntegrationEvent MakeCompletedEvent(
        int studentId,
        int timedEventId,
        string code)
        => new(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     studentId,
            TimedEventId:  timedEventId,
            Code:          code);

    // =========================================================================
    //  JOIN FAN-OUT TESTS (J1–J4)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // J1: Happy path join fan-out.
    //
    // Two eligible students (recent LastActivityDateUtc) linked to parents.
    // Publish TimedEventStartedIntegrationEvent.
    // Assert: both students receive exactly one TIMED_EVENT_LIVE row, Category=TimedEvent(9).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-J1 Join fan-out: TimedEventStartedIntegrationEvent → BOTH eligible students get TIMED_EVENT_LIVE row, Category=TimedEvent(9)")]
    public async Task J1_JoinFanOut_TwoEligibleStudents_BothGetLiveRow()
    {
        var now = DateTime.UtcNow;

        // Create two parent-child pairs
        var (_, _, _, childId1) = await CreateParentChildPairAsync("j1a");
        var (_, _, _, childId2) = await CreateParentChildPairAsync("j1b");

        // Seed XP profiles with recent activity (today = within eligibility window)
        await SeedXpProfileAsync(childId1, lastActivityDate: DateOnly.FromDateTime(now));
        await SeedXpProfileAsync(childId2, lastActivityDate: DateOnly.FromDateTime(now));

        // Use a unique timedEventId to avoid collisions with other tests
        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_J1_{Guid.NewGuid():N}";

        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));

        await Task.Delay(600);

        var notifs1 = await GetNotificationsAsync(childId1, code: "TIMED_EVENT_LIVE");
        var notifs2 = await GetNotificationsAsync(childId2, code: "TIMED_EVENT_LIVE");

        notifs1.Should().HaveCount(1,
            "child 1 (eligible: recently active) must receive exactly one TIMED_EVENT_LIVE inbox row " +
            "from the join fan-out handler (AC1)");
        notifs2.Should().HaveCount(1,
            "child 2 (eligible: recently active) must receive exactly one TIMED_EVENT_LIVE inbox row " +
            "from the join fan-out handler (AC1)");

        notifs1.First().Category.Should().Be(NotificationCategory.TimedEvent,
            "TIMED_EVENT_LIVE must have Category=TimedEvent (int value 9)");
        notifs2.First().Category.Should().Be(NotificationCategory.TimedEvent,
            "TIMED_EVENT_LIVE must have Category=TimedEvent (int value 9)");

        notifs1.First().Code.Should().Be("TIMED_EVENT_LIVE",
            "Code must be TIMED_EVENT_LIVE");
        notifs2.First().Code.Should().Be("TIMED_EVENT_LIVE",
            "Code must be TIMED_EVENT_LIVE");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // J2: Stale-activity student is excluded from the fan-out.
    //
    // One eligible student (recent activity) + one stale student (activity 10+ days ago).
    // After the fan-out, only the eligible student has a TIMED_EVENT_LIVE row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-J2 Join fan-out: stale-activity student (outside EligibilityWindow) → no TIMED_EVENT_LIVE row; eligible student still gets one")]
    public async Task J2_JoinFanOut_StaleStudent_ExcludedFromFanOut()
    {
        var now = DateTime.UtcNow;

        var (_, _, _, activeChildId) = await CreateParentChildPairAsync("j2act");
        var (_, _, _, staleChildId)  = await CreateParentChildPairAsync("j2sta");

        // Active student: activity today (within 7-day default eligibility window)
        await SeedXpProfileAsync(activeChildId, lastActivityDate: DateOnly.FromDateTime(now));

        // Stale student: activity 10 days ago (outside default 7-day window)
        await SeedXpProfileAsync(staleChildId, lastActivityDate: DateOnly.FromDateTime(now.AddDays(-10)));

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_J2_{Guid.NewGuid():N}";

        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));

        await Task.Delay(600);

        var activeNotifs = await GetNotificationsAsync(activeChildId, code: "TIMED_EVENT_LIVE");
        var staleNotifs  = await GetNotificationsAsync(staleChildId,  code: "TIMED_EVENT_LIVE");

        activeNotifs.Should().HaveCount(1,
            "the recently-active student must receive a TIMED_EVENT_LIVE row (AC1 — eligible cohort)");
        staleNotifs.Should().BeEmpty(
            "the stale-activity student (LastActivityDateUtc > EligibilityWindowDays) must NOT receive " +
            "a TIMED_EVENT_LIVE row — IEligibleStudentsForTimedEventQuery excludes them (AC1)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // J3: Per-student fail-soft in the join fan-out loop.
    //
    // One orphan student (no parent link, guaranteed by using an unused integer ID)
    // is mixed into the cohort alongside a real eligible student.
    // Assert: orphan throws no exception AND the real eligible student still gets their row.
    //
    // Implementation note: we cannot directly inject the orphan into the
    // IEligibleStudentsForTimedEventQuery result without modifying feature code.
    // Instead we create an orphan student with recent activity (so the eligibility
    // query may return them), and confirm that:
    //   (a) the handler does not throw (fail-soft per ADR 0002), and
    //   (b) the validly-parented student still gets their row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-J3 Join fan-out fail-soft: orphan student in cohort (no parent) → no throw; other eligible students still get TIMED_EVENT_LIVE row")]
    public async Task J3_JoinFanOut_OrphanInCohort_FailSoftOthersUnaffected()
    {
        var now = DateTime.UtcNow;

        // One properly-linked child
        var (_, _, _, childId) = await CreateParentChildPairAsync("j3ok");
        await SeedXpProfileAsync(childId, lastActivityDate: DateOnly.FromDateTime(now));

        // Orphan student: we use a magic ID that has no parent link.
        // We cannot inject it into IEligibleStudentsForTimedEventQuery (would need to modify feature code),
        // so instead we seed an XP profile for a student ID that has no parent in the Identity DB.
        // This tests the handler's per-student try/catch by exercising it through a real cohort scan.
        const int orphanId = 997_912_001;
        await SeedXpProfileAsync(orphanId, lastActivityDate: DateOnly.FromDateTime(now));

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_J3_{Guid.NewGuid():N}";

        Exception? thrown = null;
        try
        {
            await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        await Task.Delay(600);

        thrown.Should().BeNull(
            "TimedEventStartedIntegrationEventHandler must not propagate exceptions from " +
            "per-student failures — the outer and per-student try/catch both ensure fail-soft (ADR 0002)");

        // The legit child (with parent) must still get their TIMED_EVENT_LIVE row
        var childNotifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_LIVE");
        childNotifs.Should().HaveCount(1,
            "the validly-parented eligible student must still receive TIMED_EVENT_LIVE even when " +
            "an orphan student is in the cohort — per-student fail-soft must not abort the loop (AC1)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // J4: Join fan-out dedupe.
    //
    // Same TimedEventStartedIntegrationEvent published twice for the same eligible student.
    // Assert: exactly one TIMED_EVENT_LIVE row per student (Redis SETNX dedupe with TTL).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-J4 Join fan-out dedupe: same TimedEventStartedIntegrationEvent published twice → exactly one TIMED_EVENT_LIVE row per student")]
    public async Task J4_JoinFanOut_Dedupe_SameEventTwice_OneRowPerStudent()
    {
        var now = DateTime.UtcNow;

        var (_, _, _, childId) = await CreateParentChildPairAsync("j4");
        await SeedXpProfileAsync(childId, lastActivityDate: DateOnly.FromDateTime(now));

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_J4_{Guid.NewGuid():N}";

        // First publish
        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        await Task.Delay(400);

        // Second publish — same timedEventId, simulates re-delivery
        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_LIVE");

        notifs.Should().HaveCount(1,
            "TIMED_EVENT_LIVE:{TimedEventId} dedupe key must prevent a second TIMED_EVENT_LIVE row " +
            "when the same TimedEventStartedIntegrationEvent is re-delivered (AC6)");
    }

    // =========================================================================
    //  PROGRESS (HALFWAY) TESTS (P1–P4)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // P1: Progress 1:1 happy path.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-P1 Progress (halfway) happy path: TimedEventParticipationProgressIntegrationEvent → TIMED_EVENT_PROGRESS row, Category=9, placeholders substituted")]
    public async Task P1_Progress_HappyPath_WritesProgressRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("p1");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_P1_{Guid.NewGuid():N}";

        await PublishAsync(MakeProgressEvent(childId, timedEventId, code,
            progress: SampleProgress, target: SampleTarget));

        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_PROGRESS");

        notifs.Should().HaveCount(1,
            "TimedEventParticipationProgressIntegrationEventHandler must write exactly one " +
            "TIMED_EVENT_PROGRESS row for an eligible child (AC2)");

        var notif = notifs.First();

        notif.Category.Should().Be(NotificationCategory.TimedEvent,
            "TIMED_EVENT_PROGRESS must have Category=TimedEvent (int value 9)");

        notif.Code.Should().Be("TIMED_EVENT_PROGRESS", "code must be TIMED_EVENT_PROGRESS");

        notif.Title.Should().NotBeNullOrWhiteSpace("Title must not be empty");
        notif.Body.Should().NotBeNullOrWhiteSpace("Body must not be empty");

        // Placeholders must be substituted
        notif.Body.Should().NotContain("{progress}",
            "{progress} placeholder must be replaced with the actual progress value");
        notif.Body.Should().NotContain("{target}",
            "{target} placeholder must be replaced with the actual target value");

        // The rendered values must appear in the body
        notif.Body.Should().Contain(SampleProgress.ToString(),
            "rendered progress value must appear in the body");
        notif.Body.Should().Contain(SampleTarget.ToString(),
            "rendered target value must appear in the body");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // P2: Orphan child — progress handler skips fail-soft.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-P2 Progress fail-soft: orphan child (no parent) → no throw, no row")]
    public async Task P2_Progress_OrphanChild_NoThrowNoRow()
    {
        const int orphanChildId = 997_912_101;
        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_P2_{Guid.NewGuid():N}";

        var ex = await Record.ExceptionAsync(async () =>
            await PublishAsync(MakeProgressEvent(orphanChildId, timedEventId, code)));

        ex.Should().BeNull(
            "TimedEventParticipationProgressIntegrationEventHandler must not throw for an orphan child — " +
            "fail-soft per ADR 0002 (log + return when parent is null)");

        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(orphanChildId, code: "TIMED_EVENT_PROGRESS");
        notifs.Should().BeEmpty(
            "no TIMED_EVENT_PROGRESS row must be written when the child has no parent link");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // P3: Progress dedupe — same timedEventId published twice → one row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-P3 Progress dedupe: same TimedEventId published twice → exactly one TIMED_EVENT_PROGRESS row")]
    public async Task P3_Progress_Dedupe_SameTimedEventIdTwice_OneRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("p3");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_P3_{Guid.NewGuid():N}";

        // First delivery
        await PublishAsync(MakeProgressEvent(childId, timedEventId, code));
        await Task.Delay(300);

        // Duplicate delivery (same timedEventId, different EventId — simulates re-delivery)
        await PublishAsync(MakeProgressEvent(childId, timedEventId, code));
        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_PROGRESS");

        notifs.Should().HaveCount(1,
            "TIMED_EVENT_PROGRESS:{TimedEventId} dedupe key must prevent a second row on re-delivery (AC6)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // P4: Cross-event isolation — different timedEventIds → separate rows.
    //     Verifies that phase-prefixed dedupe keys don't collide across events.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-P4 Progress cross-event: different TimedEventIds for same student → separate TIMED_EVENT_PROGRESS rows (keys are event-scoped)")]
    public async Task P4_Progress_DifferentTimedEventIds_SeparateRows()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("p4");

        var timedEventIdA = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var timedEventIdB = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        // Ensure distinct IDs
        while (timedEventIdB == timedEventIdA)
            timedEventIdB = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;

        var codeA = $"TE_P4A_{Guid.NewGuid():N}";
        var codeB = $"TE_P4B_{Guid.NewGuid():N}";

        await PublishAsync(MakeProgressEvent(childId, timedEventIdA, codeA));
        await Task.Delay(300);

        await PublishAsync(MakeProgressEvent(childId, timedEventIdB, codeB));
        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_PROGRESS");

        notifs.Should().HaveCount(2,
            "two different TimedEventIds must produce two separate TIMED_EVENT_PROGRESS rows — " +
            "dedupe key is TIMED_EVENT_PROGRESS:{TimedEventId}, so different events have independent locks (AC6)");
    }

    // =========================================================================
    //  ENDING-SOON TESTS (E1–E3)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // E1: Ending-soon 1:1 happy path.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-E1 Ending-soon happy path: TimedEventParticipationEndingSoonIntegrationEvent → TIMED_EVENT_ENDING row, Category=9, {remaining} substituted")]
    public async Task E1_EndingSoon_HappyPath_WritesEndingRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("e1");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code         = $"TE_E1_{Guid.NewGuid():N}";

        await PublishAsync(MakeEndingSoonEvent(childId, timedEventId, code,
            progress: SampleProgress, target: SampleTarget, minutesRemaining: 120));

        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_ENDING");

        notifs.Should().HaveCount(1,
            "TimedEventParticipationEndingSoonIntegrationEventHandler must write exactly one " +
            "TIMED_EVENT_ENDING row for an eligible child (AC3)");

        var notif = notifs.First();

        notif.Category.Should().Be(NotificationCategory.TimedEvent,
            "TIMED_EVENT_ENDING must have Category=TimedEvent (int value 9)");

        notif.Code.Should().Be("TIMED_EVENT_ENDING", "code must be TIMED_EVENT_ENDING");

        notif.Title.Should().NotBeNullOrWhiteSpace("Title must not be empty");
        notif.Body.Should().NotBeNullOrWhiteSpace("Body must not be empty");

        // {remaining} placeholder (= Target - Progress = 10 - 4 = 6) must be substituted
        notif.Body.Should().NotContain("{remaining}",
            "{remaining} placeholder must be replaced with the actual remaining value");

        var expectedRemaining = (SampleTarget - SampleProgress).ToString();
        notif.Body.Should().Contain(expectedRemaining,
            "rendered remaining value (Target - Progress) must appear in the body");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E2: Ending-soon orphan.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-E2 Ending-soon fail-soft: orphan child (no parent) → no throw, no row")]
    public async Task E2_EndingSoon_OrphanChild_NoThrowNoRow()
    {
        const int orphanChildId = 997_912_201;
        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_E2_{Guid.NewGuid():N}";

        var ex = await Record.ExceptionAsync(async () =>
            await PublishAsync(MakeEndingSoonEvent(orphanChildId, timedEventId, code)));

        ex.Should().BeNull(
            "TimedEventParticipationEndingSoonIntegrationEventHandler must not throw for orphan child");

        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(orphanChildId, code: "TIMED_EVENT_ENDING");
        notifs.Should().BeEmpty("no TIMED_EVENT_ENDING row when child has no parent link");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E3: Ending-soon dedupe.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-E3 Ending-soon dedupe: same TimedEventId published twice → exactly one TIMED_EVENT_ENDING row")]
    public async Task E3_EndingSoon_Dedupe_SameTimedEventIdTwice_OneRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("e3");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_E3_{Guid.NewGuid():N}";

        await PublishAsync(MakeEndingSoonEvent(childId, timedEventId, code));
        await Task.Delay(300);

        await PublishAsync(MakeEndingSoonEvent(childId, timedEventId, code));
        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_ENDING");

        notifs.Should().HaveCount(1,
            "TIMED_EVENT_ENDING:{TimedEventId} dedupe key must prevent a second row on re-delivery (AC6)");
    }

    // =========================================================================
    //  COMPLETION TESTS (C1–C3)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // C1: Completion 1:1 happy path.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-C1 Completion happy path: TimedEventParticipationCompletedIntegrationEvent → TIMED_EVENT_COMPLETED row, Category=9, body rendered")]
    public async Task C1_Completion_HappyPath_WritesCompletedRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("c1");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_C1_{Guid.NewGuid():N}";

        await PublishAsync(MakeCompletedEvent(childId, timedEventId, code));

        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_COMPLETED");

        notifs.Should().HaveCount(1,
            "TimedEventParticipationCompletedIntegrationEventHandler must write exactly one " +
            "TIMED_EVENT_COMPLETED row for an eligible child (AC4)");

        var notif = notifs.First();

        notif.Category.Should().Be(NotificationCategory.TimedEvent,
            "TIMED_EVENT_COMPLETED must have Category=TimedEvent (int value 9)");

        notif.Code.Should().Be("TIMED_EVENT_COMPLETED", "code must be TIMED_EVENT_COMPLETED");

        notif.Title.Should().NotBeNullOrWhiteSpace("Title must not be empty");
        notif.Body.Should().NotBeNullOrWhiteSpace("Body must not be empty");

        // Completion has no numeric placeholders — confirm body has celebration copy
        notif.Body.Should().NotContain("{",
            "TIMED_EVENT_COMPLETED has no placeholders — body must contain no raw {…} tokens");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C2: Completion orphan.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-C2 Completion fail-soft: orphan child (no parent) → no throw, no row")]
    public async Task C2_Completion_OrphanChild_NoThrowNoRow()
    {
        const int orphanChildId = 997_912_301;
        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_C2_{Guid.NewGuid():N}";

        var ex = await Record.ExceptionAsync(async () =>
            await PublishAsync(MakeCompletedEvent(orphanChildId, timedEventId, code)));

        ex.Should().BeNull(
            "TimedEventParticipationCompletedIntegrationEventHandler must not throw for orphan child");

        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(orphanChildId, code: "TIMED_EVENT_COMPLETED");
        notifs.Should().BeEmpty("no TIMED_EVENT_COMPLETED row when child has no parent link");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C3: Completion dedupe.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-C3 Completion dedupe: same TimedEventId published twice → exactly one TIMED_EVENT_COMPLETED row")]
    public async Task C3_Completion_Dedupe_SameTimedEventIdTwice_OneRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("c3");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_C3_{Guid.NewGuid():N}";

        await PublishAsync(MakeCompletedEvent(childId, timedEventId, code));
        await Task.Delay(300);

        await PublishAsync(MakeCompletedEvent(childId, timedEventId, code));
        await Task.Delay(300);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_COMPLETED");

        notifs.Should().HaveCount(1,
            "TIMED_EVENT_COMPLETED:{TimedEventId} dedupe key must prevent a second row on re-delivery (AC6)");
    }

    // =========================================================================
    //  CROSS-CUTTING TESTS (X1–X7)
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // X1: Cross-phase isolation.
    //
    // Publishing all four phases for the same TimedEventId, one phase per distinct
    // student, proves that the phase-prefixed tier keys are independent of each other.
    // We use 4 separate students (one per phase) to avoid the daily cap (DailyCap=3)
    // blocking the 4th phase on a single student — the cap is a correct arbiter
    // behavior that is separately tested in X2.
    //
    // Each student receives their phase and no cross-contamination from another phase's
    // dedupe key.
    //
    // Additionally, a single student receives LIVE + PROGRESS dedupe-twice to confirm
    // the phase keys don't collide with each other (LIVE dedupe does not block PROGRESS).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X1 Cross-phase isolation: phase-prefixed dedupe keys are independent — LIVE dedupe does not block PROGRESS; each phase produces exactly one row per student")]
    public async Task X1_CrossPhaseIsolation_DedupeKeysArePhaseScoped()
    {
        var now = DateTime.UtcNow;

        // Use one student who receives both LIVE and PROGRESS to confirm the two
        // phase-prefixed keys do not collide.  The daily cap is 3, so 2 sends is safe.
        var (_, _, _, sharedChildId) = await CreateParentChildPairAsync("x1sh");
        await SeedXpProfileAsync(sharedChildId, lastActivityDate: DateOnly.FromDateTime(now));

        // Use distinct students for ENDING and COMPLETED to keep their daily count at 1.
        var (_, _, _, endingChildId)    = await CreateParentChildPairAsync("x1en");
        var (_, _, _, completedChildId) = await CreateParentChildPairAsync("x1co");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X1_{Guid.NewGuid():N}";

        // Phase 1 (LIVE) → sharedChildId via the fan-out
        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        await Task.Delay(400);

        // Phase 2 (PROGRESS) → sharedChildId (same student as LIVE — keys must not collide)
        await PublishAsync(MakeProgressEvent(sharedChildId, timedEventId, code));
        await Task.Delay(400);

        // Phase 3 (ENDING) → a different student (fresh daily count)
        await PublishAsync(MakeEndingSoonEvent(endingChildId, timedEventId, code));
        await Task.Delay(400);

        // Phase 4 (COMPLETED) → a different student (fresh daily count)
        await PublishAsync(MakeCompletedEvent(completedChildId, timedEventId, code));
        await Task.Delay(400);

        // ── sharedChildId: should have exactly 1 LIVE + 1 PROGRESS (2 rows, daily cap not hit)
        var liveNotifs     = await GetNotificationsAsync(sharedChildId, code: "TIMED_EVENT_LIVE");
        var progressNotifs = await GetNotificationsAsync(sharedChildId, code: "TIMED_EVENT_PROGRESS");

        liveNotifs.Should().HaveCount(1,
            "TIMED_EVENT_LIVE:{TimedEventId} must produce one row for sharedChild (phase 1)");
        progressNotifs.Should().HaveCount(1,
            "TIMED_EVENT_PROGRESS:{TimedEventId} must produce one row for sharedChild (phase 2); " +
            "the LIVE dedupe key must NOT block the PROGRESS phase for the same student (AC6 — phases independent)");

        // ── endingChildId: should have exactly 1 ENDING row
        var endingNotifs = await GetNotificationsAsync(endingChildId, code: "TIMED_EVENT_ENDING");
        endingNotifs.Should().HaveCount(1,
            "TIMED_EVENT_ENDING:{TimedEventId} must produce one row for endingChild (phase 3)");

        // ── completedChildId: should have exactly 1 COMPLETED row
        var completedNotifs = await GetNotificationsAsync(completedChildId, code: "TIMED_EVENT_COMPLETED");
        completedNotifs.Should().HaveCount(1,
            "TIMED_EVENT_COMPLETED:{TimedEventId} must produce one row for completedChild (phase 4)");

        // Publish LIVE again for sharedChildId with the same timedEventId →
        // dedupe must fire (LIVE key already held) → still only 1 LIVE row
        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        await Task.Delay(400);

        var liveAfterRedupe = await GetNotificationsAsync(sharedChildId, code: "TIMED_EVENT_LIVE");
        liveAfterRedupe.Should().HaveCount(1,
            "LIVE re-delivery for the same timedEventId must be blocked by the dedupe lock (still exactly 1 row)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // X2: Not-eligible (daily cap exhausted).
    //
    // Pre-seed 3 TimedEvent inbox rows for today (exhausts the default DailyCap=3)
    // then publish a progress event — the handler sees not_eligible and writes no row.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X2 Not-eligible: daily cap reached (default cap=3, 3 rows pre-seeded today) → no additional TIMED_EVENT_PROGRESS row")]
    public async Task X2_NotEligible_DailyCapReached_NoRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("x2");

        // Exhaust DailyCap=3 for TimedEvent category.
        await SeedInAppNotificationAsync(childId, "TIMED_EVENT_LIVE",     NotificationCategory.TimedEvent);
        await SeedInAppNotificationAsync(childId, "TIMED_EVENT_PROGRESS", NotificationCategory.TimedEvent);
        await SeedInAppNotificationAsync(childId, "TIMED_EVENT_ENDING",   NotificationCategory.TimedEvent);

        var beforeCount = (await GetNotificationsAsync(childId, category: NotificationCategory.TimedEvent)).Count;
        beforeCount.Should().Be(3, "3 rows must be pre-seeded to exhaust the default DailyCap=3");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X2_{Guid.NewGuid():N}";

        await PublishAsync(MakeProgressEvent(childId, timedEventId, code));
        await Task.Delay(400);

        var afterCount = (await GetNotificationsAsync(childId, category: NotificationCategory.TimedEvent)).Count;

        afterCount.Should().Be(beforeCount,
            "when the daily cap is reached the handler must log not_eligible and write no additional row (AC8 + arbiter)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // X3: Inbox-only v1 posture.
    //
    // TimedEvent is NOT in the ReengagementCategories push set, so prefs.Push
    // defaults false. Even with a device token registered, push bit (2) must be 0.
    // Mirrors P9_09 T4 and P9_06 N6.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X3 Inbox-only v1: TimedEvent push bit NOT set even with device token registered (not in ReengagementCategories)")]
    public async Task X3_InboxOnly_PushBitNotSet_EvenWithDeviceToken()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("x3");

        // Register a device token — proves push is suppressed by prefs, not by absent token
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[P912_X3_{childId}]");
        _factory.PushSender.Reset();

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X3_{Guid.NewGuid():N}";

        await PublishAsync(MakeCompletedEvent(childId, timedEventId, code));
        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_COMPLETED");

        notifs.Should().NotBeEmpty(
            "inbox row must be written for an eligible child (inbox is never suppressed by the push gate)");

        // In-app bit (4) must be set
        (notifs.First().DeliveredChannels & 4).Should().Be(4,
            "in-app channel bit (4) must be set for a TimedEvent notification");

        // Push bit (2) must NOT be set — TimedEvent is not in ReengagementCategories,
        // so prefs.Push defaults to false for this category (inbox-only v1 — AC5)
        (notifs.First().DeliveredChannels & 2).Should().Be(0,
            "push bit must be 0: TimedEvent is not in the parent-managed ReengagementCategories set " +
            "until P9-04 FE per-type toggles ship; prefs.Push defaults false → push never delivered in v1 (AC5)");

        // TestPushSender must have received zero calls
        _factory.PushSender.CallCount.Should().Be(0,
            "the TestPushSender must not have been called — TimedEvent is inbox-only in v1");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // X4: Arabic copy.
    //
    // Arabic-locale child publishing TimedEventStartedIntegrationEvent →
    // TIMED_EVENT_LIVE body contains Arabic template text.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X4 Arabic copy: ar-locale child receives Arabic body for TIMED_EVENT_LIVE")]
    public async Task X4_ArCopy_ArLocaleChild_GetsArabicBody()
    {
        var now = DateTime.UtcNow;

        var (_, _, _, childId) = await CreateParentChildPairAsync("x4ar", childLanguage: "ar");
        await SeedXpProfileAsync(childId, lastActivityDate: DateOnly.FromDateTime(now));

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X4_{Guid.NewGuid():N}";

        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        await Task.Delay(500);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_LIVE");
        notifs.Should().NotBeEmpty("ar-locale eligible child must receive TIMED_EVENT_LIVE notification");

        var notif = notifs.First();
        notif.Body.Should().NotBeNullOrWhiteSpace("Arabic body must not be empty");

        // Arabic template body contains Arabic text snippet from ReengagementCopyTemplates
        // "🔥 في تحدي محدود المدة شغّال دلوقتي — اكسب نقاط مضاعفة قبل ما الوقت يخلص!"
        notif.Body.Should().Contain("تحدي",
            "Arabic locale must render the Arabic template (containing 'تحدي')");

        notif.Title.Should().Contain("🔥",
            "Arabic TIMED_EVENT_LIVE title must contain the 🔥 emoji per ReengagementCopyTemplates");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // X5: English copy.
    //
    // English-locale child publishing TimedEventStartedIntegrationEvent →
    // TIMED_EVENT_LIVE body contains English template text.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X5 English copy: en-locale child receives English body for TIMED_EVENT_LIVE")]
    public async Task X5_EnCopy_EnLocaleChild_GetsEnglishBody()
    {
        var now = DateTime.UtcNow;

        var (_, _, _, childId) = await CreateParentChildPairAsync("x5en", childLanguage: "en");
        await SeedXpProfileAsync(childId, lastActivityDate: DateOnly.FromDateTime(now));

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X5_{Guid.NewGuid():N}";

        await PublishAsync(MakeStartedEvent(timedEventId, code, now.AddHours(-1), now.AddHours(23)));
        await Task.Delay(500);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_LIVE");
        notifs.Should().NotBeEmpty("en-locale eligible child must receive TIMED_EVENT_LIVE notification");

        var notif = notifs.First();
        notif.Body.Should().NotBeNullOrWhiteSpace("English body must not be empty");

        // English template body from ReengagementCopyTemplates:
        // "🔥 A limited-time event is on now — earn bonus XP before the clock runs out!"
        notif.Body.Should().Contain("limited-time",
            "English locale must render the English template (containing 'limited-time')");

        notif.Body.Should().Contain("XP",
            "English TIMED_EVENT_LIVE body must mention XP (English template)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // X6: No PII.
    //
    // Title and Body of a TIMED_EVENT_PROGRESS row must not contain the child's
    // full name or email address — only generic copy + numeric scalars.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X6 No PII: Title/Body for TIMED_EVENT_PROGRESS contain no child name or email; only generic copy + numeric scalars")]
    public async Task X6_NoPii_TitleBodyContainNoPiiForProgress()
    {
        const string knownChildName = "TestStudentP912X6";
        var childEmail = UniqueEmail("x6_chd");
        var parentEmail = UniqueEmail("x6_par");

        // Register parent
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

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X6_{Guid.NewGuid():N}";

        await PublishAsync(MakeProgressEvent(childId, timedEventId, code,
            progress: SampleProgress, target: SampleTarget));
        await Task.Delay(400);

        var notifs = await GetNotificationsAsync(childId, code: "TIMED_EVENT_PROGRESS");
        notifs.Should().NotBeEmpty("TIMED_EVENT_PROGRESS row must be written for eligible child");

        var notif = notifs.First();

        notif.Title.Should().NotContain(knownChildName,
            "Title must not contain the child's full name (PII)");
        notif.Body.Should().NotContain(knownChildName,
            "Body must not contain the child's full name (PII)");

        notif.Title.Should().NotContain(childEmail,
            "Title must not contain the child's email address (PII)");
        notif.Body.Should().NotContain(childEmail,
            "Body must not contain the child's email address (PII)");

        // Positive: numeric scalars (progress and target) must appear
        notif.Body.Should().Contain(SampleProgress.ToString(),
            "Body must contain the rendered progress value (curriculum data, not PII)");
        notif.Body.Should().Contain(SampleTarget.ToString(),
            "Body must contain the rendered target value (curriculum data, not PII)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // X7: Category sentinel guard.
    //
    // The raw DB Category column must be 9 (TimedEvent) rather than 6 (SQL default = System).
    // Mirrors N8 in P9_06_WeeklyMissionReminder_Tests (HasSentinel(-1) coverage check).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P912-X7 Sentinel guard: persisted Category int==9 (TimedEvent), NOT 6 (System SQL default) — HasSentinel(-1) fix covers value 9")]
    public async Task X7_SentinelGuard_CategoryInt9_NotSystemDefault6()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("x7");

        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 900_000 + 100_000;
        var code = $"TE_X7_{Guid.NewGuid():N}";

        await PublishAsync(MakeCompletedEvent(childId, timedEventId, code));
        await Task.Delay(400);

        using var scope = _factory.Services.CreateScope();
        var notifDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var conn = (Npgsql.NpgsqlConnection)notifDb.Database.GetDbConnection();
        await conn.OpenAsync();
        int? storedCategoryInt = null;
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT ""Category"" FROM notifications.""Notifications""
                                WHERE ""RecipientExternalUserId"" = $1 AND ""Code"" = 'TIMED_EVENT_COMPLETED'
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
            "a TIMED_EVENT_COMPLETED notification row must exist for the eligible child");

        storedCategoryInt.Should().Be(9,
            "HasSentinel(-1) on NotificationConfig forces EF Core to INSERT the explicit Category " +
            "value 9 (TimedEvent) rather than omitting the column and letting PostgreSQL apply " +
            "the SQL default value 6 (System). Value 9 != 0 != -1 so no sentinel collision.");
    }
}

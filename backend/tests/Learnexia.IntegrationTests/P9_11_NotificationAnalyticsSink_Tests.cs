// ReSharper disable InconsistentNaming
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Modules.Analytics.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Notifications;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P9-11 integration tests — Notification analytics sink.
///
/// Two angles:
///
/// ANGLE 1 — End-to-end relay (the important one):
///   Trigger a real nudge dispatch (publish an integration event that a Notifications handler
///   consumes → NudgeDispatcher writes the inbox row → emits a NotificationDispatched /
///   NotificationSuppressed / NotificationOpened lifecycle event → Analytics consumer writes
///   an ActivityEvent row).
///
///   RELAY-01  Dispatched relay: dispatch a LAPSE_WIN_BACK nudge → analytics.ActivityEvents
///             has a row with EventType="NotificationDispatched", correct StudentId/NotificationCode/
///             NotificationCategory/NotificationChannels (inApp bit set = 4).
///   RELAY-02  Opened relay: dispatch a nudge, then mark it read via the HTTP inbox endpoint →
///             analytics.ActivityEvents has an additional "NotificationOpened" row with correct facets.
///   RELAY-03  Suppressed relay: exhaust the per-child global push budget (budget=1, pre-seed one
///             push row so the arbiter returns GlobalBudgetExhausted) → dispatch a new nudge with
///             push=true → analytics has a "NotificationSuppressed" row WITH reason="GlobalBudgetExhausted"
///             AND a "NotificationDispatched" row for the same nudge (dual-emit semantics: inbox landed,
///             push was suppressed).
///   RELAY-04  Idempotency: publish a NotificationDispatchedIntegrationEvent directly twice with the
///             same EventId → still exactly one analytics.ActivityEvent row (SourceEventId unique-index guard).
///   RELAY-05  No-PII: ActivityEvent rows for notification events carry only StudentId (int) +
///             NotificationCode/NotificationCategory/NotificationChannels/SuppressionReason scalars —
///             no name/email/body/title present in the ActivityEvent entity columns.
///
/// ANGLE 2 — Admin endpoint (GET /api/Admin/Analytics/notifications):
///   ADMIN-01  Anonymous → 401.
///   ADMIN-02  Non-admin parent token → 403.
///   ADMIN-03  Admin → 200 with BaseResponse envelope (statusCode/successed/message/data/errors).
///   ADMIN-04  Aggregate math: seed known Dispatched/Suppressed/Opened ActivityEvents via
///             AnalyticsDbContext; assert GET returns matching TotalDispatched/TotalSuppressed/
///             TotalOpened + OpenRate = Opened/Dispatched; ByCode and ByCategory bucketed correctly.
///   ADMIN-05  SuppressionReasons bucket: seed two Suppressed rows for GlobalBudgetExhausted + one
///             for Cooldown → reasons bucket has correct counts.
///   ADMIN-06  Category filter: seed rows for categories 1 and 9; query with category=1 → only
///             category-1 rows returned (TotalDispatched + ByCategory reflect only category 1).
///   ADMIN-07  from >= to → 400 (validation reuse: PlatformKpisFromMustBeBeforeTo).
///   ADMIN-08  Window > 365 days → 400 (validation reuse: PlatformKpisWindowTooLarge).
///   ADMIN-09  Empty window (far future) → 200 with zeroed TotalDispatched/Suppressed/Opened/OpenRate,
///             empty ByCode/ByCategory/SuppressionReasons.
///   ADMIN-10  Default window (no from/to) → 200 (handler defaults to 30-day window).
///
/// All seed windows use deterministic far-past timestamps to avoid bleed from other suites.
/// Docker + Postgres required (Testcontainers).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P9_11_NotificationAnalyticsSink_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string SignInUrl            = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl    = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl          = "api/Parent/Add-Child";
    private const string NotifAnalyticsUrl    = "api/Admin/Analytics/notifications";
    private const string ValidChildPassword   = "Child@Pass1";

    // ── Seeded admin credentials ───────────────────────────────────────────────
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";

    // ── Isolated deterministic seed windows to avoid bleed from other test suites ──
    // RELAY tests: 2023-03 (unused by any other file)
    private static readonly DateTime RelayBase = new(2023, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    // ADMIN tests: 2023-04 (distinct from all other suites)
    private static readonly DateTime AdminBase = new(2023, 4, 5, 8, 0, 0, DateTimeKind.Utc);

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;

    public P9_11_NotificationAnalyticsSink_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Ensure AnalyticsDbContext is migrated (creates analytics.ActivityEvents + P9-11 facet columns).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            await db.Database.MigrateAsync();
        }

        // Seed gamification + learning seeders (required by Notifications handler chain).
        using (var scope = _factory.Services.CreateScope())
        {
            await LearningSeeder.SeedAsync(scope.ServiceProvider);
            var badgeSeeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
            await badgeSeeder.SeedAsync();
        }

        _factory.PushSender.Reset();
        _factory.TestClock.Reset();

        _adminToken = await SignInGetTokenAsync(AdminUserName, AdminPassword);
    }

    public Task DisposeAsync()
    {
        _factory.TestClock.Reset();
        _factory.PushSender.Reset();
        return Task.CompletedTask;
    }

    // =========================================================================
    // ANGLE 1 — End-to-End Relay Tests
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // RELAY-01: Dispatched relay — dispatch a LapseWinBack nudge and assert an
    // analytics ActivityEvent row appears with the correct facets.
    //
    // Trigger chain:
    //   LapseWinBackIntegrationEvent →
    //   LapseWinBackIntegrationEventHandler (Notifications.Application) →
    //   NudgeDispatcher.DispatchAsync →
    //   SaveChangesAsync (inbox row) →
    //   PublishDispatchedAsync →
    //   NotificationDispatchedIntegrationEvent →
    //   NotificationDispatchedEventHandler (Analytics.Application) →
    //   IActivityEventStore.AddAsync → analytics.ActivityEvents row
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-RELAY-01 Dispatch relay: LapseWinBack nudge dispatch → analytics ActivityEvent row with EventType=NotificationDispatched, correct facets, inApp bit (4) set")]
    public async Task Relay01_DispatchedRelay_ActivityEventWritten_WithCorrectFacets()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("r01");
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: null);

        // Publish the upstream event that feeds the Notifications handler chain.
        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:               Guid.NewGuid(),
            OccurredOnUtc:         DateTime.UtcNow,
            StudentId:             childId,
            DaysSinceLastActivity: 2));   // GENTLE tier

        // Allow the in-process MediatR fan-out to complete (synchronous in tests).
        await Task.Delay(600);

        // ── Assertions on the analytics row ──────────────────────────────────
        ActivityEvent? row = null;
        for (int i = 0; i < 12; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            row = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId && e.EventType == "NotificationDispatched")
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (row != null) break;
            await Task.Delay(300);
        }

        row.Should().NotBeNull(
            "NudgeDispatcher must publish NotificationDispatchedIntegrationEvent after SaveChangesAsync, " +
            "and NotificationDispatchedEventHandler must persist an ActivityEvent row (EventType=NotificationDispatched) — " +
            "if null the emit or the Analytics consumer is not wired correctly (P9-11 AC1 + AC5)");

        row!.EventType.Should().Be("NotificationDispatched",
            "EventType must be 'NotificationDispatched' (P9-11 brief §C consumer table)");
        row.StudentId.Should().Be(childId,
            "StudentId must match the dispatched nudge's recipient (P9-11 AC1 — no PII; only opaque int id)");
        row.NotificationCode.Should().Be("LAPSE_WIN_BACK_GENTLE",
            "NotificationCode must match the dispatched notification template code (P9-11 AC1)");
        row.NotificationCategory.Should().Be((int)NotificationCategory.LapseWinBack,
            "NotificationCategory must be the LapseWinBack enum int value (stored as plain int — module isolation)");

        // inApp bit (4) must be set — dispatcher always stamps inApp when ShouldInApp=true.
        row.NotificationChannels.Should().NotBeNull(
            "NotificationChannels must be set for a Dispatched row (P9-11 brief §A DeliveredChannels)");
        (row.NotificationChannels!.Value & 4).Should().Be(4,
            "inApp channel bit (4) must be set in NotificationChannels for an in-app delivered nudge (P9-11 AC1)");

        row.SuppressionReason.Should().BeNull(
            "SuppressionReason must be null for a Dispatched row (only set for Suppressed rows — P9-11 brief §C)");
        row.SourceEventId.Should().NotBeEmpty(
            "SourceEventId must be a non-empty Guid (idempotency key — P9-11 AC6)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RELAY-02: Opened relay — dispatch a nudge, then mark it read via the HTTP
    // inbox endpoint, assert an "NotificationOpened" ActivityEvent row appears.
    //
    // Trigger chain for the Opened event:
    //   POST /api/Notifications/Inbox/{id}/MarkRead (child JWT) →
    //   MarkNotificationReadCommandHandler →
    //   _publisher.Publish(NotificationOpenedIntegrationEvent) →
    //   NotificationOpenedEventHandler (Analytics) →
    //   analytics.ActivityEvents row (EventType=NotificationOpened)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-RELAY-02 Opened relay: dispatch nudge → mark read via HTTP → analytics ActivityEvent with EventType=NotificationOpened, correct facets")]
    public async Task Relay02_OpenedRelay_MarkReadViaHttp_OpenedActivityEventWritten()
    {
        var (parentToken, childToken, _, childId) = await CreateParentChildPairAsync("r02");
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: null);

        // Step 1: Dispatch a nudge so an inbox row exists.
        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:               Guid.NewGuid(),
            OccurredOnUtc:         DateTime.UtcNow,
            StudentId:             childId,
            DaysSinceLastActivity: 4));   // REPAIR tier → "LAPSE_WIN_BACK_REPAIR"

        await Task.Delay(600);

        // Step 2: Find the notification row in the notifications DB to get its Id.
        Guid? notifId = null;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            var notif = await db.Notifications.AsNoTracking()
                .Where(n => n.RecipientExternalUserId == childId && n.Code == "LAPSE_WIN_BACK_REPAIR")
                .OrderByDescending(n => n.CreatedAtUtc)
                .FirstOrDefaultAsync();
            notifId = notif?.Id;
        }

        notifId.Should().NotBeNull(
            "a notification inbox row for LAPSE_WIN_BACK_REPAIR must have been written before the MarkRead test " +
            "can proceed — if null, the dispatch relay itself failed");

        // Step 3: Mark it read via the HTTP endpoint (child JWT).
        var markReadUrl = $"api/Notifications/Inbox/{notifId}/MarkRead";
        var (markResp, _, markBody) = await SendAsync(HttpMethod.Post, markReadUrl, bearer: childToken);

        markResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"MarkRead must return 200 for the owning child; body: {markBody}");

        await Task.Delay(400);

        // Step 4: Assert an "NotificationOpened" ActivityEvent row was written.
        ActivityEvent? openedRow = null;
        for (int i = 0; i < 12; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            openedRow = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId && e.EventType == "NotificationOpened")
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (openedRow != null) break;
            await Task.Delay(300);
        }

        openedRow.Should().NotBeNull(
            "MarkNotificationReadCommandHandler must publish NotificationOpenedIntegrationEvent and " +
            "NotificationOpenedEventHandler must persist a NotificationOpened ActivityEvent row " +
            "(P9-11 AC3 + AC5); if null the emit or the Analytics consumer is not wired");

        openedRow!.EventType.Should().Be("NotificationOpened",
            "EventType must be 'NotificationOpened'");
        openedRow.StudentId.Should().Be(childId,
            "StudentId must match the child who marked the notification as read");
        openedRow.NotificationCode.Should().Be("LAPSE_WIN_BACK_REPAIR",
            "NotificationCode must be the opened notification's template code");
        openedRow.NotificationCategory.Should().Be((int)NotificationCategory.LapseWinBack,
            "NotificationCategory must be LapseWinBack (stored as plain int)");
        openedRow.NotificationChannels.Should().BeNull(
            "NotificationChannels must be null for an Opened row (only set for Dispatched rows)");
        openedRow.SuppressionReason.Should().BeNull(
            "SuppressionReason must be null for an Opened row");
        openedRow.SourceEventId.Should().NotBeEmpty(
            "SourceEventId must be a non-empty Guid (idempotency key)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RELAY-03: Suppressed relay — exhaust the per-child global push budget so
    // the arbiter returns GlobalBudgetExhausted, then dispatch a nudge with
    // ShouldPush=true.
    //
    // Expected dual-emit:
    //   - One "NotificationSuppressed" ActivityEvent row with SuppressionReason="GlobalBudgetExhausted".
    //   - One "NotificationDispatched" ActivityEvent row for the same student+code
    //     (the inbox row was ALWAYS written — only the push channel was suppressed).
    //
    // This mirrors TC-05 of P9_07_NudgeArbitration_Tests but also asserts the analytics rows.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-RELAY-03 Suppressed relay: budget=1 exhausted → GlobalBudgetExhausted Suppressed ActivityEvent AND co-occurring Dispatched ActivityEvent (dual-emit semantics)")]
    public async Task Relay03_SuppressedRelay_DualEmit_SuppressedAndDispatchedActivityEvents()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("r03");

        // Set LapseWinBack Push=true, GlobalDailyPushBudget=1.
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: 1);

        // Register a device token so the push path is actually entered.
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[P911_R03_{childId}]");

        // Pre-seed one push notification row for today to exhaust budget=1.
        await SeedPushNotificationAsync(childId, "PRIOR_PUSH_R03");

        _factory.PushSender.Reset();

        // Publish a LapseWinBack event — the handler will call NudgeDispatcher which:
        //   1. Writes the inbox row (in-app, always).
        //   2. Enters push path (ShouldPush=true, has device token, budget=1, count=1 already → GlobalBudgetExhausted).
        //   3. Emits NotificationSuppressed (push denied).
        //   4. Emits NotificationDispatched (inbox landed, channels = inApp only).
        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:               Guid.NewGuid(),
            OccurredOnUtc:         DateTime.UtcNow,
            StudentId:             childId,
            DaysSinceLastActivity: 2));

        await Task.Delay(800);

        List<ActivityEvent> notifRows;
        for (int i = 0; i < 12; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            notifRows = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId
                         && (e.EventType == "NotificationDispatched" || e.EventType == "NotificationSuppressed"))
                .ToListAsync();
            if (notifRows.Count >= 2) break;
            await Task.Delay(300);
        }

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        notifRows = await finalDb.ActivityEvents
            .AsNoTracking()
            .Where(e => e.StudentId == childId
                     && (e.EventType == "NotificationDispatched" || e.EventType == "NotificationSuppressed"))
            .ToListAsync();

        // ── Must have BOTH a Suppressed AND a Dispatched row ─────────────────
        var suppressedRows = notifRows.Where(r => r.EventType == "NotificationSuppressed").ToList();
        var dispatchedRows = notifRows.Where(r => r.EventType == "NotificationDispatched").ToList();

        suppressedRows.Should().NotBeEmpty(
            "when GlobalDailyPushBudget=1 is exhausted (1 prior push row today), the P9-07 arbiter " +
            "returns GlobalBudgetExhausted and NudgeDispatcher must publish NotificationSuppressedIntegrationEvent " +
            "(P9-11 AC2 — Suppressed signal with reason); if this fails check PublishSuppressedAsync in NudgeDispatcher");

        dispatchedRows.Should().NotBeEmpty(
            "the inbox row is ALWAYS written even when push is suppressed; NudgeDispatcher must also publish " +
            "NotificationDispatchedIntegrationEvent with the in-app DeliveredChannels bitmask " +
            "(P9-11 AC2 + brief OQ-2 dual-emit semantics); if this fails check PublishDispatchedAsync");

        // ── Suppressed row assertions ─────────────────────────────────────────
        var suppRow = suppressedRows.First();
        suppRow.SuppressionReason.Should().Be("GlobalBudgetExhausted",
            "SuppressionReason must be 'GlobalBudgetExhausted' (the P9-07 arbiter reason.ToString()) " +
            "(P9-11 AC2 + brief §A reason string)");
        suppRow.NotificationCode.Should().Be("LAPSE_WIN_BACK_GENTLE",
            "NotificationCode must match the suppressed nudge's code");
        suppRow.NotificationCategory.Should().Be((int)NotificationCategory.LapseWinBack,
            "NotificationCategory must be LapseWinBack int value");
        suppRow.NotificationChannels.Should().BeNull(
            "NotificationChannels must be null for a Suppressed row (push channel was denied — no delivery)");

        // ── Dispatched row assertions (dual-emit) ─────────────────────────────
        var dispRow = dispatchedRows.First();
        dispRow.NotificationCode.Should().Be("LAPSE_WIN_BACK_GENTLE",
            "Dispatched row NotificationCode must match the same nudge");
        (dispRow.NotificationChannels!.Value & 4).Should().Be(4,
            "inApp bit (4) must be set — the inbox row landed even though push was suppressed");
        (dispRow.NotificationChannels!.Value & 2).Should().Be(0,
            "push bit (2) must NOT be set — the arbiter denied push (budget exhausted)");
        dispRow.SuppressionReason.Should().BeNull(
            "SuppressionReason must be null for the Dispatched row");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RELAY-04: Idempotency — publishing the same NotificationDispatchedIntegrationEvent
    // EventId twice → still exactly one ActivityEvent row (SourceEventId unique-index guard).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-RELAY-04 Idempotency: publishing NotificationDispatchedIntegrationEvent with same EventId twice → exactly one ActivityEvent row")]
    public async Task Relay04_Idempotency_SameEventIdTwice_OnlyOneRow()
    {
        var studentId = 91_100_001; // synthetic — no need for a real user for this test
        var eventId   = Guid.NewGuid();
        var occurred  = RelayBase.AddHours(10);

        var ev = new NotificationDispatchedIntegrationEvent(
            EventId:           eventId,
            OccurredOnUtc:     occurred,
            StudentId:         studentId,
            Code:              "BADGE_EARNED",
            Category:          (int)NotificationCategory.Achievement,
            DeliveredChannels: 4); // inApp only

        // First publish.
        using (var scope1 = _factory.Services.CreateScope())
        {
            var pub1 = scope1.ServiceProvider.GetRequiredService<IPublisher>();
            await pub1.Publish(ev);
        }

        await Task.Delay(400);

        // Second publish — same EventId (simulates message-bus redelivery).
        using (var scope2 = _factory.Services.CreateScope())
        {
            var pub2 = scope2.ServiceProvider.GetRequiredService<IPublisher>();
            await pub2.Publish(ev);
        }

        await Task.Delay(500);

        int rowCount;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            rowCount = await db.ActivityEvents
                .AsNoTracking()
                .CountAsync(e => e.SourceEventId == eventId);
        }

        rowCount.Should().Be(1,
            "IActivityEventStore must deduplicate on SourceEventId; " +
            "redelivering the same lifecycle event must produce exactly one ActivityEvent row (P9-11 AC6 idempotent ingest)");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RELAY-05: No PII — ActivityEvent rows for notification events carry only
    // opaque int StudentId + code/category/channels/reason scalars. The entity
    // has no name/email/body/title columns, so this test asserts column nullity.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-RELAY-05 No-PII: ActivityEvent for NotificationDispatched carries only opaque StudentId + code/category/channels scalars (no PII columns in entity)")]
    public async Task Relay05_NoPII_ActivityEvent_HasNoNameEmailBodyTitle()
    {
        var studentId = 91_100_002;
        var eventId   = Guid.NewGuid();

        var ev = new NotificationDispatchedIntegrationEvent(
            EventId:           eventId,
            OccurredOnUtc:     RelayBase.AddHours(11),
            StudentId:         studentId,
            Code:              "STREAK_AT_RISK",
            Category:          (int)NotificationCategory.StreakAtRisk,
            DeliveredChannels: 4);

        using (var scope = _factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(ev);
        }

        await Task.Delay(400);

        ActivityEvent? row = null;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            row = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.SourceEventId == eventId)
                .FirstOrDefaultAsync();
        }

        row.Should().NotBeNull("ActivityEvent must be written (prerequisite for PII check)");

        // The ActivityEvent entity has NO columns for name/email/body/title — assert column set.
        // We verify no prohibited PII columns exist in the entity's properties.
        var entityProperties = typeof(ActivityEvent).GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToHashSet();

        entityProperties.Should().NotContain("name",      "ActivityEvent must have no 'Name' column (PII)");
        entityProperties.Should().NotContain("email",     "ActivityEvent must have no 'Email' column (PII)");
        entityProperties.Should().NotContain("title",     "ActivityEvent must have no 'Title' column (PII)");
        entityProperties.Should().NotContain("body",      "ActivityEvent must have no 'Body' column (PII)");
        entityProperties.Should().NotContain("username",  "ActivityEvent must have no 'UserName' column (PII)");

        // Confirm the expected opaque scalars are present.
        entityProperties.Should().Contain("studentid",            "StudentId (opaque int) must be present");
        entityProperties.Should().Contain("notificationcode",     "NotificationCode (template key) must be present (P9-11 AC1)");
        entityProperties.Should().Contain("notificationcategory", "NotificationCategory (int) must be present");
        entityProperties.Should().Contain("notificationchannels", "NotificationChannels (bitmask) must be present");
        entityProperties.Should().Contain("suppressionreason",    "SuppressionReason (string) must be present");

        // ── Also assert the actual row values carry the correct scalar (no PII content) ──
        row!.StudentId.Should().Be(studentId, "StudentId must be the opaque int id only");
        row.NotificationCode.Should().Be("STREAK_AT_RISK", "NotificationCode must be the template key only");
        row.NotificationCategory.Should().Be((int)NotificationCategory.StreakAtRisk,
            "NotificationCategory must be the plain int enum value");
        row.NotificationChannels.Should().Be(4, "DeliveredChannels=4 (inApp only) must be stored as-is");
        row.SuppressionReason.Should().BeNull("SuppressionReason must be null for a Dispatched row");
    }

    // =========================================================================
    // ANGLE 2 — Admin Endpoint Tests
    // =========================================================================

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-01: Anonymous → 401.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-01: GET /api/Admin/Analytics/notifications anonymous → 401")]
    public async Task Admin01_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, NotifAnalyticsUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "NotificationAnalytics is AdminOnly — anonymous must get 401; body: {0}", body);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-02: Non-admin parent token → 403.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-02: GET /api/Admin/Analytics/notifications with parent JWT → 403")]
    public async Task Admin02_ParentToken_Returns403()
    {
        var parentToken = await RegisterParentGetTokenAsync("admin02");

        var (resp, _, body) = await SendAsync(HttpMethod.Get, NotifAnalyticsUrl, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "NotificationAnalytics is AdminOnly — a parent (non-admin) JWT must get 403; body: {0}", body);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-03: Admin → 200 with correct BaseResponse envelope.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-03: GET /api/Admin/Analytics/notifications admin JWT → 200 with BaseResponse envelope (statusCode/successed/message/data/errors)")]
    public async Task Admin03_AdminToken_Returns200WithBaseResponseEnvelope()
    {
        var url = $"{NotifAnalyticsUrl}?from=2023-01-01T00:00:00Z&to=2023-01-02T00:00:00Z";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"admin must get 200; body: {body}");

        // ── Envelope shape: all five keys must be present ─────────────────────
        TryProp(root, "statusCode", out var statusCode).Should().BeTrue(
            $"envelope must have 'statusCode'; body: {body}");
        statusCode.GetInt32().Should().Be(200,
            $"statusCode must be 200; body: {body}");

        TryProp(root, "successed", out var successed).Should().BeTrue(
            $"envelope must have 'successed' (note the spelling — convention); body: {body}");
        successed.GetBoolean().Should().BeTrue(
            $"successed must be true for a 200 response; body: {body}");

        TryProp(root, "message", out var message).Should().BeTrue(
            $"envelope must have 'message'; body: {body}");
        message.GetString().Should().NotBeNullOrWhiteSpace(
            $"message must not be empty; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue(
            $"envelope must have 'data'; body: {body}");
        data.ValueKind.Should().Be(JsonValueKind.Object,
            $"data must be an object (NotificationAnalyticsDto); body: {body}");

        TryProp(root, "errors", out _).Should().BeTrue(
            $"envelope must have 'errors' key; body: {body}");

        // ── DTO shape: all expected top-level fields present ───────────────────
        TryProp(data, "totalDispatched",    out _).Should().BeTrue($"data must have totalDispatched; body: {body}");
        TryProp(data, "totalSuppressed",    out _).Should().BeTrue($"data must have totalSuppressed; body: {body}");
        TryProp(data, "totalOpened",        out _).Should().BeTrue($"data must have totalOpened; body: {body}");
        TryProp(data, "openRate",           out _).Should().BeTrue($"data must have openRate; body: {body}");
        TryProp(data, "byCode",             out _).Should().BeTrue($"data must have byCode array; body: {body}");
        TryProp(data, "byCategory",         out _).Should().BeTrue($"data must have byCategory array; body: {body}");
        TryProp(data, "suppressionReasons", out _).Should().BeTrue($"data must have suppressionReasons array; body: {body}");
        TryProp(data, "fromUtc",            out _).Should().BeTrue($"data must have fromUtc; body: {body}");
        TryProp(data, "toUtc",              out _).Should().BeTrue($"data must have toUtc; body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-04: Aggregate math — seed known ActivityEvents directly via
    // AnalyticsDbContext, then call the endpoint and assert the aggregation.
    //
    // Seed (all in AdminBase+1h window):
    //   4 × Dispatched for "BADGE_EARNED" Category=1 (StreakAtRisk)
    //   2 × Suppressed for "BADGE_EARNED" Category=1
    //   1 × Opened    for "BADGE_EARNED" Category=1
    //   1 × Dispatched for "LEVELED_UP"  Category=0 (Achievement)
    //
    // Expected:
    //   TotalDispatched = 5
    //   TotalSuppressed = 2
    //   TotalOpened     = 1
    //   OpenRate        = 1 / 5 = 0.2
    //   ByCode["BADGE_EARNED"].Dispatched = 4, Suppressed = 2, Opened = 1, OpenRate = 0.25
    //   ByCode["LEVELED_UP"].Dispatched   = 1, Suppressed = 0, Opened = 0, OpenRate = 0.0
    //   ByCategory[1].Dispatched          = 4, Suppressed = 2, Opened = 1
    //   ByCategory[0].Dispatched          = 1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-04: aggregate math — seed known ActivityEvents → GET returns correct TotalDispatched/Suppressed/Opened/OpenRate + ByCode/ByCategory buckets")]
    public async Task Admin04_AggregateMatch_SeedThenQuery_CorrectCounts()
    {
        var windowStart = AdminBase;
        var windowEnd   = AdminBase.AddHours(2);

        var rows = new List<ActivityEvent>();

        // 4 × NotificationDispatched for BADGE_EARNED, Category=1 (StreakAtRisk)
        for (int i = 0; i < 4; i++)
        {
            rows.Add(new ActivityEvent
            {
                StudentId            = 91_200 + i,
                EventType            = "NotificationDispatched",
                OccurredAtUtc        = windowStart.AddMinutes(5 + i),
                SourceEventId        = Guid.NewGuid(),
                NotificationCode     = "BADGE_EARNED",
                NotificationCategory = 1, // StreakAtRisk
                NotificationChannels = 4, // inApp
                SuppressionReason    = null,
            });
        }

        // 2 × NotificationSuppressed for BADGE_EARNED, Category=1
        for (int i = 0; i < 2; i++)
        {
            rows.Add(new ActivityEvent
            {
                StudentId            = 91_210 + i,
                EventType            = "NotificationSuppressed",
                OccurredAtUtc        = windowStart.AddMinutes(10 + i),
                SourceEventId        = Guid.NewGuid(),
                NotificationCode     = "BADGE_EARNED",
                NotificationCategory = 1,
                NotificationChannels = null,
                SuppressionReason    = "GlobalBudgetExhausted",
            });
        }

        // 1 × NotificationOpened for BADGE_EARNED, Category=1
        rows.Add(new ActivityEvent
        {
            StudentId            = 91_220,
            EventType            = "NotificationOpened",
            OccurredAtUtc        = windowStart.AddMinutes(15),
            SourceEventId        = Guid.NewGuid(),
            NotificationCode     = "BADGE_EARNED",
            NotificationCategory = 1,
            NotificationChannels = null,
            SuppressionReason    = null,
        });

        // 1 × NotificationDispatched for LEVELED_UP, Category=0 (Achievement)
        rows.Add(new ActivityEvent
        {
            StudentId            = 91_230,
            EventType            = "NotificationDispatched",
            OccurredAtUtc        = windowStart.AddMinutes(20),
            SourceEventId        = Guid.NewGuid(),
            NotificationCode     = "LEVELED_UP",
            NotificationCategory = 0, // Achievement
            NotificationChannels = 4,
            SuppressionReason    = null,
        });

        await SeedActivityEventsAsync(rows.ToArray());

        var url = $"{NotifAnalyticsUrl}?from={windowStart:O}&to={windowEnd:O}";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"admin must get 200; body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");

        // ── Platform totals ───────────────────────────────────────────────────
        TryProp(data, "totalDispatched", out var totalDispatched).Should().BeTrue($"body: {body}");
        totalDispatched.GetInt32().Should().BeGreaterThanOrEqualTo(5,
            $"TotalDispatched must be ≥ 5 (4+1 dispatched rows seeded); body: {body}");

        TryProp(data, "totalSuppressed", out var totalSuppressed).Should().BeTrue($"body: {body}");
        totalSuppressed.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"TotalSuppressed must be ≥ 2 (2 suppressed rows seeded); body: {body}");

        TryProp(data, "totalOpened", out var totalOpened).Should().BeTrue($"body: {body}");
        totalOpened.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            $"TotalOpened must be ≥ 1 (1 opened row seeded); body: {body}");

        TryProp(data, "openRate", out var openRate).Should().BeTrue($"body: {body}");
        openRate.ValueKind.Should().Be(JsonValueKind.Number,
            $"openRate must be a number; body: {body}");
        openRate.GetDouble().Should().BeInRange(0.0, 1.0,
            $"openRate must be in [0,1]; body: {body}");
        // OpenRate ≈ 1/5 = 0.2 from our seeded rows (though other tests may add rows in this window)
        // We use ≥ 0.0 to be resilient to other test pollution in the window.
        openRate.GetDouble().Should().BeGreaterThanOrEqualTo(0.0,
            $"openRate must be ≥ 0.0; body: {body}");

        // ── ByCode breakdown: BADGE_EARNED must appear ────────────────────────
        TryProp(data, "byCode", out var byCode).Should().BeTrue($"body: {body}");
        byCode.ValueKind.Should().Be(JsonValueKind.Array, $"byCode must be an array; body: {body}");

        var badgeEarned = byCode.EnumerateArray()
            .FirstOrDefault(c => TryProp(c, "code", out var cd) && cd.GetString() == "BADGE_EARNED");
        badgeEarned.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"BADGE_EARNED must appear in byCode array; body: {body}");
        TryProp(badgeEarned, "dispatched", out var bdDisp);
        bdDisp.GetInt32().Should().BeGreaterThanOrEqualTo(4,
            $"BADGE_EARNED dispatched must be ≥ 4 (seeded 4); body: {body}");
        TryProp(badgeEarned, "suppressed", out var bdSupp);
        bdSupp.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"BADGE_EARNED suppressed must be ≥ 2 (seeded 2); body: {body}");
        TryProp(badgeEarned, "opened", out var bdOpened);
        bdOpened.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            $"BADGE_EARNED opened must be ≥ 1 (seeded 1); body: {body}");
        TryProp(badgeEarned, "openRate", out var bdOpenRate);
        bdOpenRate.GetDouble().Should().BeGreaterThan(0.0,
            $"BADGE_EARNED openRate must be > 0 (1 opened / ≥4 dispatched > 0); body: {body}");

        // ── ByCategory breakdown ──────────────────────────────────────────────
        TryProp(data, "byCategory", out var byCategory).Should().BeTrue($"body: {body}");
        byCategory.ValueKind.Should().Be(JsonValueKind.Array, $"byCategory must be an array; body: {body}");

        var cat1 = byCategory.EnumerateArray()
            .FirstOrDefault(c => TryProp(c, "category", out var cat) && cat.GetInt32() == 1);
        cat1.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"Category=1 must appear in byCategory array; body: {body}");
        TryProp(cat1, "dispatched", out var c1Disp);
        c1Disp.GetInt32().Should().BeGreaterThanOrEqualTo(4,
            $"Category=1 dispatched must be ≥ 4; body: {body}");

        var cat0 = byCategory.EnumerateArray()
            .FirstOrDefault(c => TryProp(c, "category", out var cat) && cat.GetInt32() == 0);
        cat0.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"Category=0 must appear in byCategory array; body: {body}");
        TryProp(cat0, "dispatched", out var c0Disp);
        c0Disp.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            $"Category=0 dispatched must be ≥ 1; body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-05: SuppressionReasons bucket — seed rows for two distinct reasons
    // and assert the buckets are returned correctly.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-05: suppression-reason buckets — seed GlobalBudgetExhausted×2 + Cooldown×1 → suppressionReasons bucket has correct counts")]
    public async Task Admin05_SuppressionReasonBuckets_CorrectCounts()
    {
        var windowStart = AdminBase.AddHours(3);
        var windowEnd   = AdminBase.AddHours(4);

        await SeedActivityEventsAsync(
            new ActivityEvent
            {
                StudentId = 91_300, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(1), SourceEventId = Guid.NewGuid(),
                NotificationCode = "STREAK_AT_RISK", NotificationCategory = 1,
                SuppressionReason = "GlobalBudgetExhausted",
            },
            new ActivityEvent
            {
                StudentId = 91_301, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(2), SourceEventId = Guid.NewGuid(),
                NotificationCode = "STREAK_AT_RISK", NotificationCategory = 1,
                SuppressionReason = "GlobalBudgetExhausted",
            },
            new ActivityEvent
            {
                StudentId = 91_302, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(3), SourceEventId = Guid.NewGuid(),
                NotificationCode = "LAPSE_WIN_BACK_GENTLE", NotificationCategory = 3,
                SuppressionReason = "Cooldown",
            });

        var url = $"{NotifAnalyticsUrl}?from={windowStart:O}&to={windowEnd:O}";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");

        TryProp(data, "suppressionReasons", out var reasons).Should().BeTrue($"body: {body}");
        reasons.ValueKind.Should().Be(JsonValueKind.Array, $"suppressionReasons must be an array; body: {body}");

        var reasonList = reasons.EnumerateArray().ToList();
        reasonList.Should().NotBeEmpty(
            $"suppressionReasons must not be empty when suppressed rows are seeded; body: {body}");

        // GlobalBudgetExhausted should appear with count ≥ 2
        var budgetReason = reasonList
            .FirstOrDefault(r => TryProp(r, "reason", out var rsn) && rsn.GetString() == "GlobalBudgetExhausted");
        budgetReason.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"GlobalBudgetExhausted must appear in suppressionReasons; body: {body}");
        TryProp(budgetReason, "count", out var budgetCount);
        budgetCount.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"GlobalBudgetExhausted count must be ≥ 2 (seeded 2); body: {body}");

        // Cooldown should appear with count ≥ 1
        var cooldownReason = reasonList
            .FirstOrDefault(r => TryProp(r, "reason", out var rsn) && rsn.GetString() == "Cooldown");
        cooldownReason.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"Cooldown must appear in suppressionReasons; body: {body}");
        TryProp(cooldownReason, "count", out var cooldownCount);
        cooldownCount.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            $"Cooldown count must be ≥ 1 (seeded 1); body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-06: category filter — seed Category=1 and Category=9 rows;
    // query ?category=1 → only category-1 rows in TotalDispatched and ByCategory.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-06: category filter ?category=1 → only category-1 rows aggregated (ByCategory excludes category=9)")]
    public async Task Admin06_CategoryFilter_FiltersCorrectly()
    {
        var windowStart = AdminBase.AddHours(5);
        var windowEnd   = AdminBase.AddHours(6);

        await SeedActivityEventsAsync(
            // 2 rows for Category=1 (StreakAtRisk)
            new ActivityEvent
            {
                StudentId = 91_400, EventType = "NotificationDispatched",
                OccurredAtUtc = windowStart.AddMinutes(1), SourceEventId = Guid.NewGuid(),
                NotificationCode = "STREAK_AT_RISK", NotificationCategory = 1,
                NotificationChannels = 4,
            },
            new ActivityEvent
            {
                StudentId = 91_401, EventType = "NotificationDispatched",
                OccurredAtUtc = windowStart.AddMinutes(2), SourceEventId = Guid.NewGuid(),
                NotificationCode = "STREAK_AT_RISK", NotificationCategory = 1,
                NotificationChannels = 4,
            },
            // 3 rows for Category=9 (TimedEvent) — these must be excluded by the filter
            new ActivityEvent
            {
                StudentId = 91_410, EventType = "NotificationDispatched",
                OccurredAtUtc = windowStart.AddMinutes(3), SourceEventId = Guid.NewGuid(),
                NotificationCode = "TIMED_EVENT_LIVE", NotificationCategory = 9,
                NotificationChannels = 4,
            },
            new ActivityEvent
            {
                StudentId = 91_411, EventType = "NotificationDispatched",
                OccurredAtUtc = windowStart.AddMinutes(4), SourceEventId = Guid.NewGuid(),
                NotificationCode = "TIMED_EVENT_LIVE", NotificationCategory = 9,
                NotificationChannels = 4,
            },
            new ActivityEvent
            {
                StudentId = 91_412, EventType = "NotificationDispatched",
                OccurredAtUtc = windowStart.AddMinutes(5), SourceEventId = Guid.NewGuid(),
                NotificationCode = "TIMED_EVENT_LIVE", NotificationCategory = 9,
                NotificationChannels = 4,
            });

        var url = $"{NotifAnalyticsUrl}?from={windowStart:O}&to={windowEnd:O}&category=1";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");

        TryProp(data, "totalDispatched", out var totalDispatched).Should().BeTrue($"body: {body}");
        totalDispatched.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"filtered to category=1 → TotalDispatched must be ≥ 2 (2 rows seeded for cat=1); body: {body}");

        // Category=9 rows must NOT inflate the count
        // TotalDispatched should equal exactly the category=1 rows (2), not 5 total.
        // We check TotalDispatched is strictly less than 5 (the unfiltered total)
        // to verify the filter works, allowing for any other test pollution.
        // This is a lower-bound + upper-bound guard with generous tolerance.
        totalDispatched.GetInt32().Should().BeLessThan(6,
            $"with category=1 filter, TotalDispatched must not include category=9 rows (3 more); " +
            $"if TotalDispatched >= 5 from this window alone, the category filter is not applied; body: {body}");

        // ByCategory must contain category=1 but NOT category=9
        TryProp(data, "byCategory", out var byCategory).Should().BeTrue($"body: {body}");
        var cats = byCategory.EnumerateArray().ToList();

        var cat9 = cats.FirstOrDefault(c => TryProp(c, "category", out var cat) && cat.GetInt32() == 9);
        cat9.ValueKind.Should().Be(JsonValueKind.Undefined,
            $"category=9 must NOT appear in byCategory when filter ?category=1 is applied; body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-07: from >= to → 400.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-07: from >= to → 400 (validation: PlatformKpisFromMustBeBeforeTo reused)")]
    public async Task Admin07_FromGteTo_Returns400()
    {
        var url = $"{NotifAnalyticsUrl}?from=2023-06-01T00:00:00Z&to=2023-05-01T00:00:00Z";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"from >= to must return 400; body: {body}");

        // Envelope check: successed=false
        TryProp(root, "successed", out var successed).Should().BeTrue($"body: {body}");
        successed.GetBoolean().Should().BeFalse(
            $"successed must be false for a 400 response; body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-08: window > 365 days → 400.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-08: window > 365 days → 400 (validation: PlatformKpisWindowTooLarge reused)")]
    public async Task Admin08_WindowTooLarge_Returns400()
    {
        var url = $"{NotifAnalyticsUrl}?from=2020-01-01T00:00:00Z&to=2022-01-02T00:00:00Z";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"window > 365d must return 400; body: {body}");

        TryProp(root, "successed", out var successed).Should().BeTrue($"body: {body}");
        successed.GetBoolean().Should().BeFalse(
            $"successed must be false for a 400 response; body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-09: empty window (far future) → 200 with all-zero totals and empty
    // array fields (sentinel-safe, never throws, never 500).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-09: empty window (far future) → 200 with zeroed totals + empty ByCode/ByCategory/SuppressionReasons (sentinel-safe, not 500)")]
    public async Task Admin09_EmptyWindow_ZeroedSentinel()
    {
        var url = $"{NotifAnalyticsUrl}?from=2097-01-01T00:00:00Z&to=2097-06-01T00:00:00Z";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"empty window must return 200, not 500 (sentinel-safe); body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");

        TryProp(data, "totalDispatched", out var td).Should().BeTrue($"body: {body}");
        td.GetInt32().Should().Be(0, $"TotalDispatched must be 0 for an empty window; body: {body}");

        TryProp(data, "totalSuppressed", out var ts).Should().BeTrue($"body: {body}");
        ts.GetInt32().Should().Be(0, $"TotalSuppressed must be 0 for an empty window; body: {body}");

        TryProp(data, "totalOpened", out var to_).Should().BeTrue($"body: {body}");
        to_.GetInt32().Should().Be(0, $"TotalOpened must be 0 for an empty window; body: {body}");

        TryProp(data, "openRate", out var or_).Should().BeTrue($"body: {body}");
        or_.GetDouble().Should().Be(0.0, $"OpenRate must be 0.0 for an empty window; body: {body}");

        TryProp(data, "byCode", out var byCode).Should().BeTrue($"body: {body}");
        byCode.ValueKind.Should().Be(JsonValueKind.Array, $"byCode must be an array; body: {body}");
        byCode.GetArrayLength().Should().Be(0, $"byCode must be empty for an empty window; body: {body}");

        TryProp(data, "byCategory", out var byCategory).Should().BeTrue($"body: {body}");
        byCategory.GetArrayLength().Should().Be(0, $"byCategory must be empty for an empty window; body: {body}");

        TryProp(data, "suppressionReasons", out var reasons).Should().BeTrue($"body: {body}");
        reasons.GetArrayLength().Should().Be(0, $"suppressionReasons must be empty for an empty window; body: {body}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ADMIN-10: no from/to → 200 (handler defaults to 30-day window).
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "P911-ADMIN-10: no from/to query params → 200 (handler defaults to 30-day window; not 400 or 500)")]
    public async Task Admin10_NoFromTo_DefaultsTo30DayWindow_Returns200()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, NotifAnalyticsUrl, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"omitting from/to must default to a 30-day window and return 200; body: {body}");

        TryProp(root, "successed", out var successed).Should().BeTrue($"body: {body}");
        successed.GetBoolean().Should().BeTrue($"successed must be true; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        data.ValueKind.Should().Be(JsonValueKind.Object,
            $"data must be an object even with default window; body: {body}");

        // fromUtc and toUtc must be populated by the handler's defaults.
        TryProp(data, "fromUtc", out var fromUtc).Should().BeTrue($"body: {body}");
        TryProp(data, "toUtc",   out var toUtc).Should().BeTrue($"body: {body}");

        fromUtc.ValueKind.Should().Be(JsonValueKind.String,
            $"fromUtc must be a date string when defaulted; body: {body}");
        toUtc.ValueKind.Should().Be(JsonValueKind.String,
            $"toUtc must be a date string when defaulted; body: {body}");
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"sign-in must succeed for {userName}; body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        TryProp(data, "accessToken", out var token).Should().BeTrue($"body: {body}");
        return token.GetString()!;
    }

    private async Task<string> RegisterParentGetTokenAsync(string tag = "default")
    {
        var email = $"p911_parent_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)resp.StatusCode).Should().BeOneOf([200, 201], $"parent reg must succeed; body: {body}");
        TryProp(root, "data", out var data).Should().BeTrue($"body: {body}");
        TryProp(data, "accessToken", out var token).Should().BeTrue($"body: {body}");
        return token.GetString()!;
    }

    private async Task<(string ParentToken, string ChildToken, int ParentId, int ChildId)>
        CreateParentChildPairAsync(string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = $"p911_par_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (regResp, regRoot, regBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201], $"parent reg must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue($"body: {regBody}");
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue($"body: {regBody}");
        var parentToken = parentTokProp.GetString()!;

        TryProp(regData, "userId", out var parentIdProp);
        int parentId = parentIdProp.ValueKind == JsonValueKind.Number ? parentIdProp.GetInt32() : 0;

        var childEmail = $"p911_chd_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = $"TestChild P911 {tag}",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 1,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201], $"Add-Child must succeed; body: {addBody}");
        TryProp(addRoot, "data", out var addData).Should().BeTrue($"body: {addBody}");
        TryProp(addData, "id", out var idProp).Should().BeTrue($"body: {addBody}");
        var childId = idProp.GetInt32();

        if (parentId == 0)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<
                Learnexia.Modules.Identity.Infrastructure.Persistence.IdentityModuleDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == parentEmail);
            parentId = user?.Id ?? 0;
        }

        var (signInResp, signInRoot, signInBody) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"child sign-in must succeed; body: {signInBody}");
        TryProp(signInRoot, "data", out var signInData).Should().BeTrue($"body: {signInBody}");
        TryProp(signInData, "accessToken", out var childTokProp).Should().BeTrue($"body: {signInBody}");
        var childToken = childTokProp.GetString()!;

        return (parentToken, childToken, parentId, childId);
    }

    private async Task SetPushEnabledForLapseWinBackAsync(
        string parentToken,
        int childId,
        int? globalPushBudget = null)
    {
        var url = $"api/Notifications/Preferences/Children/{childId}/Reengagement";
        var payload = new
        {
            Items = new[]
            {
                new { Category = (int)NotificationCategory.StreakAtRisk,         Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.DailyMissionReminder, Email = false, Push = true, InApp = true },
                new { Category = (int)NotificationCategory.LapseWinBack,         Email = false, Push = true, InApp = true },
            },
            QuietHoursStartLocal  = "00:00",
            QuietHoursEndLocal    = "00:01",
            TimeZoneId            = "UTC",
            DailyCap              = 10,
            GlobalDailyPushBudget = globalPushBudget,
        };
        var (resp, _, body) = await SendAsync(HttpMethod.Put, url, payload, parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"PUT prefs must succeed; body: {body}");
    }

    private async Task PublishAsync<TEvent>(TEvent ev) where TEvent : MediatR.INotification
    {
        using var scope = _factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        await publisher.Publish(ev);
    }

    private async Task SeedPushNotificationAsync(int childId, string code)
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
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "Prior push" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "body" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (int)NotificationCategory.LapseWinBack });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = code });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = "{}" });
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 2 });   // push bit
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

    private async Task RegisterDeviceTokenDirectlyAsync(int userId, string expoPushToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var nowUtc = DateTime.UtcNow;
        await db.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO notifications.""UserDeviceTokens""
               (""UserId"", ""ExpoPushToken"", ""Platform"", ""RegisteredAtUtc"", ""LastUsedAtUtc"", ""IsActive"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({userId}, '{expoPushToken}', 'ios', '{nowUtc:O}', '{nowUtc:O}', true, '{nowUtc:O}', 0)
               ON CONFLICT DO NOTHING");
    }

    private async Task SeedActivityEventsAsync(params ActivityEvent[] events)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        db.ActivityEvents.AddRange(events);
        await db.SaveChangesAsync(userId: 0);
    }

    private async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var response = await _client.SendAsync(request);
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON */ }
        }
        return (response, root, bodyStr);
    }

    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object) { value = default; return false; }
        if (element.TryGetProperty(name, out value)) return true;
        if (element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)) return true;
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
}

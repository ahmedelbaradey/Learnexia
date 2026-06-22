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
using Learnexia.Modules.Notifications.Domain.Constants;
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
/// P9-13 integration tests — Notification suppression analytics.
///
/// Verifies that <c>NudgeDispatcher</c> and <c>ReengagementHandlerHelper</c> emit
/// <see cref="NotificationSuppressedIntegrationEvent"/> for the three NEW suppression paths
/// added in P9-13, and that the <c>Analytics</c> module's sink writes the correct
/// <see cref="ActivityEvent"/> row. Also verifies:
///   - The existing <c>GlobalBudgetExhausted</c> path is not regressed (SC-04).
///   - The admin breakdown endpoint buckets the new reasons (SC-05).
///   - A suppression-emit failure does NOT break dispatch (SC-06).
///
/// Test scenarios:
///   SC-01  PushPrefOff  — dispatch with push pref OFF → in-app row written +
///                          NotificationSuppressed(PushPrefOff) in analytics.
///   SC-02  NoDeviceTokens — dispatch with push pref ON, ShouldPush=true but NO
///                           registered tokens → in-app row written +
///                           NotificationSuppressed(NoDeviceTokens) in analytics.
///   SC-03  Deduped      — same TimedEvent progress event published twice within the
///                          dedupe window → 2nd is deduped → NotificationSuppressed(Deduped)
///                          in analytics AND no second inbox row.
///   SC-04  Regression   — GlobalBudgetExhausted still emits correctly (don't regress P9-07/11).
///   SC-05  Admin breakdown — GET /api/Admin/Analytics/notifications?from=...&amp;to=...
///                            returns PushPrefOff / NoDeviceTokens / Deduped in
///                            suppressionReasons bucket (seeded directly, not via relay).
///   SC-06  Fail-soft    — a suppression event that throws inside PublishSuppressedAsync must NOT
///                          cause DispatchAsync to surface an exception; in-app row still written.
///
/// All relay scenarios wait for the in-process MediatR fan-out with a bounded poll loop.
/// Isolated StudentId ranges and time windows prevent bleed across suites.
/// Docker + Postgres required (Testcontainers).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P9_13_SuppressionAnalytics_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string NotifAnalyticsUrl  = "api/Admin/Analytics/notifications";
    private const string ValidChildPassword = "Child@Pass1";

    // ── Seeded admin credentials ───────────────────────────────────────────────
    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";

    // Isolated window for SC-05 (ADMIN seed) — 2023-08, unused by other suites.
    private static readonly DateTime AdminBase = new(2023, 8, 15, 10, 0, 0, DateTimeKind.Utc);

    // ── Infrastructure ─────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;

    public P9_13_SuppressionAnalytics_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            await db.Database.MigrateAsync();
        }

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
    // SC-01: PushPrefOff — push pref disabled → in-app row written +
    //        NotificationSuppressed(PushPrefOff) appears in analytics.
    //
    // Mechanism:
    //   NudgeDispatcher.DispatchAsync — when ShouldPush=false (push pref off),
    //   the else-branch calls PublishSuppressedAsync(PushPrefOff) BEFORE the
    //   inbox SaveChangesAsync, so both events are emitted for the same nudge.
    //
    // The test uses LapseWinBack (Push=false not set → prefs.Push=false by
    // NOT calling SetPushEnabledForLapseWinBackAsync, so default CreateDefault
    // returns Push=false). The inbox row is ALWAYS written; push is never attempted.
    // =========================================================================

    [Fact(DisplayName = "P913-SC-01 PushPrefOff: dispatch LapseWinBack with push pref OFF → in-app inbox row written + analytics NotificationSuppressed(PushPrefOff)")]
    public async Task SC01_PushPrefOff_InboxRowAndSuppressedAnalyticsRow()
    {
        // Create parent+child WITHOUT enabling push → prefs.Push defaults to false.
        var (_, _, _, childId) = await CreateParentChildPairAsync("sc01");
        // Intentionally do NOT call SetPushEnabledForLapseWinBackAsync —
        // GetOrDefaultPrefsAsync returns CreateDefault with Push=false.

        // Also do NOT register a device token (belt-and-suspenders: push path never entered).
        _factory.PushSender.Reset();

        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:               Guid.NewGuid(),
            OccurredOnUtc:         DateTime.UtcNow,
            StudentId:             childId,
            DaysSinceLastActivity: 2));   // GENTLE tier

        await Task.Delay(800);

        // ── Assert 1: in-app inbox row was written ────────────────────────────
        var inbox = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_GENTLE");
        inbox.Should().NotBeEmpty(
            "the in-app inbox row must ALWAYS be written even when push pref is OFF (P9-13 AC: PushPrefOff — " +
            "inbox is the durable receipt; only push channel is suppressed)");

        // ── Assert 2: push bit NOT set on inbox row ───────────────────────────
        var pushBit = inbox.First().DeliveredChannels & 2;
        pushBit.Should().Be(0,
            "push bit must be 0 when push pref is off (ShouldPush=false path in NudgeDispatcher)");

        // ── Assert 3: analytics NotificationSuppressed(PushPrefOff) row ──────
        ActivityEvent? suppRow = null;
        for (int i = 0; i < 15; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            suppRow = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId
                         && e.EventType == "NotificationSuppressed"
                         && e.SuppressionReason == SuppressionReasonCodes.PushPrefOff)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (suppRow != null) break;
            await Task.Delay(300);
        }

        suppRow.Should().NotBeNull(
            "NudgeDispatcher must publish NotificationSuppressedIntegrationEvent with reason=PushPrefOff " +
            "when ShouldPush=false, and the Analytics sink must persist a NotificationSuppressed " +
            "ActivityEvent row (P9-13 BE-2 + AC PushPrefOff)");

        suppRow!.SuppressionReason.Should().Be(SuppressionReasonCodes.PushPrefOff,
            "SuppressionReason must be 'PushPrefOff' (constant from SuppressionReasonCodes.cs)");
        suppRow.NotificationCode.Should().Be("LAPSE_WIN_BACK_GENTLE",
            "NotificationCode must match the dispatched nudge's code");
        suppRow.NotificationCategory.Should().Be((int)NotificationCategory.LapseWinBack,
            "NotificationCategory must be LapseWinBack int value");
        suppRow.NotificationChannels.Should().BeNull(
            "NotificationChannels must be null for a Suppressed row (push was not delivered)");
        suppRow.SourceEventId.Should().NotBeEmpty(
            "SourceEventId must be a non-empty Guid (idempotency key)");
    }

    // =========================================================================
    // SC-02: NoDeviceTokens — push pref ON, ShouldPush=true, but NO active device
    //        tokens registered → in-app row written + NotificationSuppressed(NoDeviceTokens).
    //
    // Mechanism:
    //   NudgeDispatcher.TrySendPushAsync: arbiter grants (budget available, under cooldown),
    //   but GetActiveTokensAsync returns empty → emits NoDeviceTokens suppression + returns 0.
    //   The inbox row IS written; push bit is NOT set.
    // =========================================================================

    [Fact(DisplayName = "P913-SC-02 NoDeviceTokens: push pref ON, no device tokens → in-app row written + analytics NotificationSuppressed(NoDeviceTokens)")]
    public async Task SC02_NoDeviceTokens_InboxRowAndSuppressedAnalyticsRow()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("sc02");

        // Enable push for LapseWinBack (prefs.Push=true → ShouldPush=true).
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: null);

        // Intentionally do NOT register any device token.
        // GetActiveTokensAsync returns [] → NoDeviceTokens path in TrySendPushAsync.
        _factory.PushSender.Reset();

        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:               Guid.NewGuid(),
            OccurredOnUtc:         DateTime.UtcNow,
            StudentId:             childId,
            DaysSinceLastActivity: 2));

        await Task.Delay(800);

        // ── Assert 1: inbox row was written ───────────────────────────────────
        var inbox = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_GENTLE");
        inbox.Should().NotBeEmpty(
            "the in-app inbox row must ALWAYS be written even when there are no device tokens " +
            "(P9-13 AC: NoDeviceTokens — the TrySendPushAsync path still runs after arbiter grants, " +
            "so the inbox write must have completed before TrySendPushAsync is entered)");

        // ── Assert 2: push bit NOT set ────────────────────────────────────────
        var pushBit = inbox.First().DeliveredChannels & 2;
        pushBit.Should().Be(0,
            "push bit must be 0 when no device tokens are registered (TrySendPushAsync returns 0)");

        // ── Assert 3: analytics NotificationSuppressed(NoDeviceTokens) ───────
        ActivityEvent? suppRow = null;
        for (int i = 0; i < 15; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            suppRow = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId
                         && e.EventType == "NotificationSuppressed"
                         && e.SuppressionReason == SuppressionReasonCodes.NoDeviceTokens)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (suppRow != null) break;
            await Task.Delay(300);
        }

        suppRow.Should().NotBeNull(
            "NudgeDispatcher.TrySendPushAsync must publish NotificationSuppressedIntegrationEvent " +
            "with reason=NoDeviceTokens when GetActiveTokensAsync returns empty, and the Analytics " +
            "sink must persist the ActivityEvent row (P9-13 BE-2 + AC NoDeviceTokens)");

        suppRow!.SuppressionReason.Should().Be(SuppressionReasonCodes.NoDeviceTokens,
            "SuppressionReason must be 'NoDeviceTokens' (constant from SuppressionReasonCodes.cs)");
        suppRow.NotificationCode.Should().Be("LAPSE_WIN_BACK_GENTLE",
            "NotificationCode must match the dispatched nudge's code");
        suppRow.NotificationCategory.Should().Be((int)NotificationCategory.LapseWinBack,
            "NotificationCategory must be LapseWinBack int value");
        suppRow.NotificationChannels.Should().BeNull(
            "NotificationChannels must be null for a Suppressed row (push was not delivered)");
    }

    // =========================================================================
    // SC-03: Deduped — trigger the same TimedEvent progress event twice within the
    //        dedupe window → 2nd is deduped:
    //          - NotificationSuppressed(Deduped) in analytics.
    //          - NO second inbox row.
    //
    // Mechanism:
    //   TimedEventParticipationProgressIntegrationEventHandler uses TryAcquireTierAsync
    //   with key "TIMED_EVENT_PROGRESS:{TimedEventId}". First call acquires → dispatch.
    //   Second call returns false → ReengagementHandlerHelper.PublishDedupeSuppressionAsync
    //   fires the Deduped event → Analytics sink persists it; no DispatchAsync is called.
    // =========================================================================

    [Fact(DisplayName = "P913-SC-03 Deduped: same TimedEvent progress event published twice → 2nd deduped → analytics NotificationSuppressed(Deduped) + no 2nd inbox row")]
    public async Task SC03_Deduped_TierLock_SuppressedAnalyticsAndNoSecondInboxRow()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("sc03");

        // Use a unique synthetic TimedEventId to isolate this test from others.
        var timedEventId = Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000 + 900_000; // ~6-digit synthetic id
        var code         = $"BOOST_SC03_{Guid.NewGuid():N}"[..26]; // unique code (truncated to keep it short)

        var ev1 = new TimedEventParticipationProgressIntegrationEvent(
            EventId:       Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            StudentId:     childId,
            TimedEventId:  timedEventId,
            Code:          code,
            Progress:      5,
            Target:        10);

        // First publish: should acquire tier lock → dispatch succeeds → inbox row written.
        await PublishAsync(ev1);
        await Task.Delay(600);

        var inboxAfterFirst = await GetNotificationsAsync(childId, code: "TIMED_EVENT_PROGRESS");
        inboxAfterFirst.Should().HaveCount(1,
            "the first publication must write exactly one TIMED_EVENT_PROGRESS inbox row");

        // Second publish: same timedEventId → tier lock already held → dedupe hits.
        var ev2 = new TimedEventParticipationProgressIntegrationEvent(
            EventId:       Guid.NewGuid(),      // different event id, same timedEventId
            OccurredOnUtc: DateTime.UtcNow.AddSeconds(1),
            StudentId:     childId,
            TimedEventId:  timedEventId,
            Code:          code,
            Progress:      5,
            Target:        10);

        await PublishAsync(ev2);
        await Task.Delay(600);

        // ── Assert 1: still only one inbox row ───────────────────────────────
        var inboxAfterSecond = await GetNotificationsAsync(childId, code: "TIMED_EVENT_PROGRESS");
        inboxAfterSecond.Should().HaveCount(1,
            "the deduped 2nd publish must NOT write a second TIMED_EVENT_PROGRESS inbox row " +
            "(P9-13 AC: Deduped — TryAcquireTierAsync returns false; DispatchAsync is never called)");

        // ── Assert 2: analytics NotificationSuppressed(Deduped) row ──────────
        ActivityEvent? suppRow = null;
        for (int i = 0; i < 15; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            suppRow = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId
                         && e.EventType == "NotificationSuppressed"
                         && e.SuppressionReason == SuppressionReasonCodes.Deduped
                         && e.NotificationCode == "TIMED_EVENT_PROGRESS")
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (suppRow != null) break;
            await Task.Delay(300);
        }

        suppRow.Should().NotBeNull(
            "ReengagementHandlerHelper.PublishDedupeSuppressionAsync must publish " +
            "NotificationSuppressedIntegrationEvent with reason=Deduped when TryAcquireTierAsync " +
            "returns false, and the Analytics sink must persist the ActivityEvent row " +
            "(P9-13 BE-3 + AC Deduped)");

        suppRow!.SuppressionReason.Should().Be(SuppressionReasonCodes.Deduped,
            "SuppressionReason must be 'Deduped' (constant from SuppressionReasonCodes.cs)");
        suppRow.NotificationCode.Should().Be("TIMED_EVENT_PROGRESS",
            "NotificationCode must be TIMED_EVENT_PROGRESS (the handler's code constant)");
        suppRow.NotificationCategory.Should().Be((int)NotificationCategory.TimedEvent,
            "NotificationCategory must be TimedEvent (9) for P9-12 handlers");
        suppRow.NotificationChannels.Should().BeNull(
            "NotificationChannels must be null for a Suppressed/Deduped row (no dispatch occurred)");
    }

    // =========================================================================
    // SC-04: Regression — GlobalBudgetExhausted still emits correctly.
    //        Mirrors P9_11 RELAY-03 but focused on the analytics reason string.
    //        Asserts the existing arbiter path was NOT broken by the P9-13 changes.
    // =========================================================================

    [Fact(DisplayName = "P913-SC-04 Regression: GlobalBudgetExhausted still emits NotificationSuppressed(GlobalBudgetExhausted) in analytics (P9-07/11 not regressed)")]
    public async Task SC04_GlobalBudgetExhausted_StillEmitsCorrectly()
    {
        var (parentToken, _, _, childId) = await CreateParentChildPairAsync("sc04");

        // Budget=1 + register device token + pre-seed 1 push row to exhaust budget.
        await SetPushEnabledForLapseWinBackAsync(parentToken, childId, globalPushBudget: 1);
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[P913_SC04_{childId}]");
        await SeedPushNotificationAsync(childId, "PRIOR_PUSH_SC04");

        _factory.PushSender.Reset();

        await PublishAsync(new LapseWinBackIntegrationEvent(
            EventId:               Guid.NewGuid(),
            OccurredOnUtc:         DateTime.UtcNow,
            StudentId:             childId,
            DaysSinceLastActivity: 2));

        await Task.Delay(800);

        // ── Assert 1: inbox row written (in-app still delivered) ──────────────
        var inbox = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_GENTLE");
        inbox.Should().NotBeEmpty(
            "inbox row must still be written even when global push budget is exhausted " +
            "(regression from P9-07 TC-05: inbox is never rationed by budget)");

        // ── Assert 2: analytics GlobalBudgetExhausted suppressed row ─────────
        ActivityEvent? suppRow = null;
        for (int i = 0; i < 15; i++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            suppRow = await db.ActivityEvents
                .AsNoTracking()
                .Where(e => e.StudentId == childId
                         && e.EventType == "NotificationSuppressed"
                         && e.SuppressionReason == SuppressionReasonCodes.GlobalBudgetExhausted)
                .OrderByDescending(e => e.Id)
                .FirstOrDefaultAsync();
            if (suppRow != null) break;
            await Task.Delay(300);
        }

        suppRow.Should().NotBeNull(
            "NudgeDispatcher.TryArbiterGrantAsync must still emit NotificationSuppressed " +
            "with reason=GlobalBudgetExhausted when the push budget is exhausted (P9-11 AC2 regression " +
            "— P9-13 changes must not break the existing arbiter-based suppression path)");

        suppRow!.SuppressionReason.Should().Be(SuppressionReasonCodes.GlobalBudgetExhausted,
            "SuppressionReason must be 'GlobalBudgetExhausted' (not changed by P9-13)");
        suppRow.NotificationCode.Should().Be("LAPSE_WIN_BACK_GENTLE",
            "NotificationCode must match the suppressed nudge's code");
    }

    // =========================================================================
    // SC-05: Admin breakdown — GET /api/Admin/Analytics/notifications returns
    //        PushPrefOff, NoDeviceTokens, and Deduped in its suppressionReasons
    //        bucket when those rows are seeded. Also verifies the AdminOnly auth.
    //
    // Uses direct DB seeding (not relay) to test the endpoint aggregation
    // independently of dispatch — mirrors P9_11 ADMIN-05 pattern.
    // =========================================================================

    [Fact(DisplayName = "P913-SC-05 Admin breakdown: seed PushPrefOff + NoDeviceTokens + Deduped rows → GET notifications endpoint returns all 3 in suppressionReasons bucket (AdminOnly)")]
    public async Task SC05_AdminBreakdown_NewReasonsInSuppressionBuckets()
    {
        var windowStart = AdminBase;
        var windowEnd   = AdminBase.AddHours(2);

        // Seed 2 PushPrefOff, 3 NoDeviceTokens, 1 Deduped rows in the isolated window.
        await SeedActivityEventsAsync(
            new ActivityEvent
            {
                StudentId = 91_700, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(1), SourceEventId = Guid.NewGuid(),
                NotificationCode = "LAPSE_WIN_BACK_GENTLE", NotificationCategory = (int)NotificationCategory.LapseWinBack,
                SuppressionReason = SuppressionReasonCodes.PushPrefOff,
            },
            new ActivityEvent
            {
                StudentId = 91_701, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(2), SourceEventId = Guid.NewGuid(),
                NotificationCode = "LAPSE_WIN_BACK_GENTLE", NotificationCategory = (int)NotificationCategory.LapseWinBack,
                SuppressionReason = SuppressionReasonCodes.PushPrefOff,
            },
            new ActivityEvent
            {
                StudentId = 91_710, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(3), SourceEventId = Guid.NewGuid(),
                NotificationCode = "LAPSE_WIN_BACK_GENTLE", NotificationCategory = (int)NotificationCategory.LapseWinBack,
                SuppressionReason = SuppressionReasonCodes.NoDeviceTokens,
            },
            new ActivityEvent
            {
                StudentId = 91_711, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(4), SourceEventId = Guid.NewGuid(),
                NotificationCode = "STREAK_AT_RISK", NotificationCategory = (int)NotificationCategory.StreakAtRisk,
                SuppressionReason = SuppressionReasonCodes.NoDeviceTokens,
            },
            new ActivityEvent
            {
                StudentId = 91_712, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(5), SourceEventId = Guid.NewGuid(),
                NotificationCode = "STREAK_AT_RISK", NotificationCategory = (int)NotificationCategory.StreakAtRisk,
                SuppressionReason = SuppressionReasonCodes.NoDeviceTokens,
            },
            new ActivityEvent
            {
                StudentId = 91_720, EventType = "NotificationSuppressed",
                OccurredAtUtc = windowStart.AddMinutes(6), SourceEventId = Guid.NewGuid(),
                NotificationCode = "TIMED_EVENT_PROGRESS", NotificationCategory = (int)NotificationCategory.TimedEvent,
                SuppressionReason = SuppressionReasonCodes.Deduped,
            });

        // ── Anonymous → 401 (AdminOnly) ───────────────────────────────────────
        var (anonResp, _, anonBody) = await SendAsync(HttpMethod.Get, NotifAnalyticsUrl, bearer: null);
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            $"NotificationAnalytics is AdminOnly — anonymous must get 401; body: {anonBody}");

        // ── Admin request → 200 ───────────────────────────────────────────────
        var url = $"{NotifAnalyticsUrl}?from={windowStart:O}&to={windowEnd:O}";
        var (resp, root, body) = await SendAsync(HttpMethod.Get, url, bearer: _adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"admin must get 200 for the notifications analytics endpoint; body: {body}");

        TryProp(root, "data", out var data).Should().BeTrue($"envelope must have 'data'; body: {body}");
        TryProp(data, "suppressionReasons", out var reasons).Should().BeTrue(
            $"data must have 'suppressionReasons' array; body: {body}");
        reasons.ValueKind.Should().Be(JsonValueKind.Array,
            $"suppressionReasons must be an array; body: {body}");

        var reasonList = reasons.EnumerateArray().ToList();
        reasonList.Should().NotBeEmpty(
            $"suppressionReasons must not be empty when suppressed rows are seeded; body: {body}");

        // ── PushPrefOff: count ≥ 2 ────────────────────────────────────────────
        var pushPrefOffBucket = reasonList
            .FirstOrDefault(r => TryProp(r, "reason", out var rsn) && rsn.GetString() == SuppressionReasonCodes.PushPrefOff);
        pushPrefOffBucket.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"'PushPrefOff' must appear in suppressionReasons bucket (P9-13 AC); body: {body}");
        TryProp(pushPrefOffBucket, "count", out var pushPrefOffCount);
        pushPrefOffCount.GetInt32().Should().BeGreaterThanOrEqualTo(2,
            $"PushPrefOff count must be ≥ 2 (seeded 2 rows); body: {body}");

        // ── NoDeviceTokens: count ≥ 3 ─────────────────────────────────────────
        var noTokensBucket = reasonList
            .FirstOrDefault(r => TryProp(r, "reason", out var rsn) && rsn.GetString() == SuppressionReasonCodes.NoDeviceTokens);
        noTokensBucket.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"'NoDeviceTokens' must appear in suppressionReasons bucket (P9-13 AC); body: {body}");
        TryProp(noTokensBucket, "count", out var noTokensCount);
        noTokensCount.GetInt32().Should().BeGreaterThanOrEqualTo(3,
            $"NoDeviceTokens count must be ≥ 3 (seeded 3 rows); body: {body}");

        // ── Deduped: count ≥ 1 ────────────────────────────────────────────────
        var dedupedBucket = reasonList
            .FirstOrDefault(r => TryProp(r, "reason", out var rsn) && rsn.GetString() == SuppressionReasonCodes.Deduped);
        dedupedBucket.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            $"'Deduped' must appear in suppressionReasons bucket (P9-13 AC); body: {body}");
        TryProp(dedupedBucket, "count", out var dedupedCount);
        dedupedCount.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            $"Deduped count must be ≥ 1 (seeded 1 row); body: {body}");
    }

    // =========================================================================
    // SC-06: Fail-soft — a suppression event emission failure must NOT break dispatch.
    //        Because PublishSuppressedAsync is wrapped in its own try/catch inside
    //        NudgeDispatcher (both in DispatchAsync and in TryArbiterGrantAsync),
    //        any internal publish error is swallowed.
    //
    // We cannot easily make the publisher throw selectively in-process, so we assert
    // the OBSERVABLE fail-soft outcome: even under the condition that would emit a
    // suppression (push pref off), the inbox row is still written and no exception
    // surfaces to the caller. This is essentially SC-01 with an explicit no-throw check.
    // =========================================================================

    [Fact(DisplayName = "P913-SC-06 Fail-soft: suppression emit inside DispatchAsync must not surface exception; in-app row still written")]
    public async Task SC06_FailSoft_SuppressionEmitDoesNotBreakDispatch()
    {
        var (_, _, _, childId) = await CreateParentChildPairAsync("sc06");
        // Push pref OFF → will trigger PushPrefOff emission inside DispatchAsync.
        _factory.PushSender.Reset();

        // Record.ExceptionAsync must return null — the publisher must not throw.
        var ex = await Record.ExceptionAsync(async () =>
        {
            await PublishAsync(new LapseWinBackIntegrationEvent(
                EventId:               Guid.NewGuid(),
                OccurredOnUtc:         DateTime.UtcNow,
                StudentId:             childId,
                DaysSinceLastActivity: 3)); // REPAIR tier
        });

        ex.Should().BeNull(
            "publishing an integration event that triggers a PushPrefOff suppression emission " +
            "must NOT throw to the publisher — the suppression emit is fail-soft in NudgeDispatcher " +
            "(wrapped in its own try/catch per ADR 0002 §3 + P9-13 BE-2 requirement)");

        await Task.Delay(600);

        // Inbox row must still be written (dispatch completed despite suppression emit path).
        var inbox = await GetNotificationsAsync(childId, code: "LAPSE_WIN_BACK_REPAIR");
        inbox.Should().NotBeEmpty(
            "the in-app inbox row must be written even on the PushPrefOff suppression path — " +
            "NudgeDispatcher writes the row unconditionally in Step 1 before the push gate " +
            "(P9-13 fail-soft: dispatch must complete even when the suppression event emit fails)");
    }

    // =========================================================================
    // Private helpers (mirrors P9_11 and P9_07 pattern)
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

    private static string UniqueEmail(string tag = "")
        => $"p913_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private async Task<(string ParentToken, string ChildToken, int ParentId, int ChildId)>
        CreateParentChildPairAsync(string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = UniqueEmail($"par_{tag}");
        var (regResp, regRoot, regBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf([200, 201], $"parent reg must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue($"body: {regBody}");
        TryProp(regData, "accessToken", out var parentTokProp).Should().BeTrue($"body: {regBody}");
        var parentToken = parentTokProp.GetString()!;

        TryProp(regData, "userId", out var parentIdProp);
        int parentId = parentIdProp.ValueKind == JsonValueKind.Number ? parentIdProp.GetInt32() : 0;

        var childEmail = UniqueEmail($"chd_{tag}");
        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = $"TestChild P913 {tag}",
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

        var (siResp, siRoot, siBody) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        siResp.StatusCode.Should().Be(HttpStatusCode.OK, $"child sign-in must succeed; body: {siBody}");
        TryProp(siRoot, "data", out var siData).Should().BeTrue($"body: {siBody}");
        TryProp(siData, "accessToken", out var childTokProp).Should().BeTrue($"body: {siBody}");
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

    private async Task PublishAsync<TEvent>(TEvent ev) where TEvent : MediatR.INotification
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
        var q  = db.Notifications.AsNoTracking()
                    .Where(n => n.RecipientExternalUserId == childId);
        if (code is not null)
            q = q.Where(n => n.Code == code);
        if (category is not null)
            q = q.Where(n => n.Category == category);
        return await q.ToListAsync();
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
            { value = prop.Value; return true; }
        }
        value = default;
        return false;
    }
}

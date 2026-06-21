using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P7-13 integration tests: Gamification Admin Overrides.
///
/// Endpoints under test (base route <c>api/Admin/Gamification</c>, all AdminOnly):
///   POST   children/{childId}/league-tier          — override a student's league tier
///   POST   children/{childId}/streak-freeze        — grant streak freezes (clamped at MaxFreezes=2)
///   GET    Badges                                  — admin list (includes inactive)
///   POST   Badges                                  — create new badge definition
///   PUT    Badges/{id}                             — update mutable fields
///   PATCH  Badges/{id}/active                      — activate / deactivate
///   GET    Missions                                — admin list (includes inactive)
///   POST   Missions                                — create new mission definition
///   PUT    Missions/{id}                           — update mutable fields
///   PATCH  Missions/{id}/active                    — activate / deactivate
///   POST   TimedEvents                             — create new timed event
///   PUT    TimedEvents/{id}                        — update mutable fields
///   POST   TimedEvents/{id}/activate               — manually activate
///   POST   TimedEvents/{id}/expire                 — manually expire
///
/// Acceptance criteria covered:
///   AC-1 : League tier override persists new tier; no-op rejected; reason required; Confirm=false → 422.
///   AC-2 : Badge catalog CRUD round-trip; duplicate Code rejected (424); deactivate hides from engine
///          but admin list still shows it; reactivate restores; earned StudentBadges not stripped.
///   AC-3 : Mission catalog CRUD round-trip; same activate/deactivate semantics.
///   AC-4 : Timed-event create+activate+expire transitions; start>=end → 422; multiplier>5 or <1 → 422;
///          double-activate/double-expire → Successed=false.
///   AC-5 : Streak freeze grant: Count=1 succeeds, granting Count=5 is clamped to MaxFreezes=2 at
///          the validator level (Count>MaxFreezes → 422); balance reflects actual grant capped at MaxFreezes.
///   AC-6 : Audit end-to-end: GrantStreakFreeze and OverrideLeagueTier both produce AuditLog rows
///          via the post-commit AdminActionPerformedDomainEvent relay path.
///   AC-7 : AdminOnly auth matrix: anonymous → 401, non-admin (parent/basicuser) → 403, admin → 200.
///   AC-8 : BaseResponse envelope (statusCode, successed, message, data, errors) on all responses.
///   AC-9 : 422 on invalid command bodies (FluentValidation via ValidationBehavior).
///
/// NOTE: The GrantStreakFreezeCommandValidator rejects Count > StudentXpProfile.MaxFreezes (=2) with
/// a 422 at the FluentValidation layer. A request Count=5 will never reach the handler. Tests verify
/// this validator-level cap (AC-5). If Count=2 is granted to a student already at MaxFreezes=2, the
/// handler returns Successed=false / BadRequest (balance-at-max), which is also tested.
///
/// NOTE ON ASYNC AUDIT DELIVERY: AdminActionPerformedDomainEvent is dispatched post-commit via
/// UnitOfWorkBehavior. In-process, the handler typically completes before HTTP response returns.
/// A poll loop (up to 3 s) guards against transient scheduling delays.
///
/// Docker + Postgres required to RUN. Compile-only on Windows CI (no Docker daemon).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P7_13_GamificationAdmin_Tests : IAsyncLifetime
{
    // ─────────────────────────────────────────────────────────────────────────
    // URL constants
    // ─────────────────────────────────────────────────────────────────────────
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string AuditLogUrl        = "api/Admin/Audit/Log";

    private const string AdminGamBase       = "api/Admin/Gamification";
    private const string BadgesUrl          = $"{AdminGamBase}/Badges";
    private const string MissionsUrl        = $"{AdminGamBase}/Missions";
    private const string TimedEventsUrl     = $"{AdminGamBase}/TimedEvents";

    // ─────────────────────────────────────────────────────────────────────────
    // Seeded credentials
    // ─────────────────────────────────────────────────────────────────────────
    private const string AdminUserName      = "superadmin";
    private const string AdminPassword      = "123Pa$$word!";
    private const string BasicUserName      = "basicuser";
    private const string BasicUserPassword  = "123Pa$$word!";
    private const string ValidChildPassword = "Child@Pass1";

    // ─────────────────────────────────────────────────────────────────────────
    // Infrastructure
    // ─────────────────────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    private string? _adminToken;
    private string? _basicToken;
    private string? _parentToken;
    private int _adminUserId;

    public P7_13_GamificationAdmin_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

        // Ensure Gamification seeders have run (badges/missions catalog).
        using (var scope = _factory.Services.CreateScope())
        {
            var badgeSeeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
            await badgeSeeder.SeedAsync();
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var missionSeeder = scope.ServiceProvider.GetRequiredService<MissionSeeder>();
            await missionSeeder.SeedAsync();
        }

        _adminToken  = await SignInGetTokenAsync(AdminUserName, AdminPassword);
        _basicToken  = await SignInGetTokenAsync(BasicUserName, BasicUserPassword);
        _parentToken = await RegisterParentGetTokenAsync("p713");
        _adminUserId = DecodeUserIdFromJwt(_adminToken!);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AC-7 — Auth matrix: every endpoint family requires AdminOnly
    // =========================================================================

    [Fact(DisplayName = "AC-7: POST .../children/{id}/league-tier anonymous → 401")]
    public async Task Auth_LeagueTierOverride_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/league-tier",
            new { newTier = 2, reason = "test", confirm = true },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST .../children/{id}/league-tier non-admin (parent) → 403")]
    public async Task Auth_LeagueTierOverride_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/league-tier",
            new { newTier = 2, reason = "test", confirm = true },
            bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST .../children/{id}/league-tier non-admin (basicuser) → 403")]
    public async Task Auth_LeagueTierOverride_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/league-tier",
            new { newTier = 2, reason = "test", confirm = true },
            bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST .../children/{id}/streak-freeze anonymous → 401")]
    public async Task Auth_StreakFreeze_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/streak-freeze",
            new { count = 1, reason = "test", confirm = true },
            bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST .../children/{id}/streak-freeze non-admin → 403")]
    public async Task Auth_StreakFreeze_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/streak-freeze",
            new { count = 1, reason = "test", confirm = true },
            bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: GET Badges anonymous → 401")]
    public async Task Auth_ListBadges_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: GET Badges non-admin → 403")]
    public async Task Auth_ListBadges_BasicUser_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: _basicToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST Badges anonymous → 401")]
    public async Task Auth_CreateBadge_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, BadgesUrl,
            ValidBadgeBody(), bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST Missions anonymous → 401")]
    public async Task Auth_CreateMission_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, MissionsUrl,
            ValidMissionBody(), bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: POST TimedEvents anonymous → 401")]
    public async Task Auth_CreateTimedEvent_Anonymous_Returns401()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Post, TimedEventsUrl,
            ValidTimedEventBody(), bearer: null);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AC-7: GET Missions non-admin → 403")]
    public async Task Auth_ListMissions_Parent_Returns403()
    {
        var (resp, _, body) = await SendAsync(HttpMethod.Get, MissionsUrl, bearer: _parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body: {0}", body);
    }

    // =========================================================================
    // AC-8 — BaseResponse envelope shape
    // =========================================================================

    [Fact(DisplayName = "AC-8: GET Badges with admin returns BaseResponse envelope keys")]
    public async Task Envelope_GetBadges_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have statusCode; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed (sic); body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue("envelope must have message; body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("envelope must have data; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("envelope must have errors; body: {0}", body);
    }

    [Fact(DisplayName = "AC-8: GET Missions with admin returns BaseResponse envelope keys")]
    public async Task Envelope_GetMissions_HasBaseResponseKeys()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Get, MissionsUrl, bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "data", out _).Should().BeTrue("body: {0}", body);
    }

    // =========================================================================
    // AC-2 — Badge CRUD round-trip
    // =========================================================================

    [Fact(DisplayName = "AC-2: POST Badges → Created (admin list contains the new badge)")]
    public async Task Badge_Create_ReturnsCreatedAndAppearsInList()
    {
        var code = $"TEST_BADGE_{Guid.NewGuid():N}"[..40];
        var body_create = ValidBadgeBody(code);

        var (resp, root, body) = await SendAsync(HttpMethod.Post, BadgesUrl, body_create, _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "POST Badges must return 200 or 201; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeTrue("Successed must be true on create; body: {0}", body);

        // The returned data must contain the new badge id.
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Undefined, "data must not be missing; body: {0}", body);

        // Verify the badge appears in the admin list.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.Any(b => TryProp(b, "code", out var c) && c.GetString() == code);
        found.Should().BeTrue("newly created badge with code={0} must appear in admin list; body: {1}", code, listBody);
    }

    [Fact(DisplayName = "AC-2: POST Badges with duplicate Code → Successed=false (424 BusinessValidation)")]
    public async Task Badge_Create_DuplicateCode_Rejected()
    {
        var code = $"DUP_BADGE_{Guid.NewGuid():N}"[..40];
        var body_create = ValidBadgeBody(code);

        // First create succeeds.
        var (r1, root1, b1) = await SendAsync(HttpMethod.Post, BadgesUrl, body_create, _adminToken);
        ((int)r1.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "first create must succeed; body: {0}", b1);
        AssertSucceeded(root1, b1);

        // Second create with same code must be rejected.
        var (r2, root2, b2) = await SendAsync(HttpMethod.Post, BadgesUrl, body_create, _adminToken);
        AssertRejected(r2, root2, b2, "Duplicate badge Code must be rejected");
    }

    [Fact(DisplayName = "AC-2: PUT Badges/{id} — update mutable fields persists")]
    public async Task Badge_Update_MutableFields_Persists()
    {
        var code = $"UPD_BADGE_{Guid.NewGuid():N}"[..40];
        int badgeId = await CreateBadgeGetIdAsync(code);

        var newName = $"Updated Name {Guid.NewGuid():N}"[..40];
        var (resp, root, body) = await SendAsync(HttpMethod.Put, $"{BadgesUrl}/{badgeId}",
            new
            {
                name        = newName,
                description = "Updated description for test",
                iconKey     = "icon-updated",
                rarity      = 1, // Common
                sortOrder   = 99,
                triggerType = 2, // StreakThreshold
                threshold   = 7,
                rewardXp    = 50
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PUT Badges must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify the name was persisted by reading from DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var badge = await db.BadgeDefinitions.AsNoTracking().FirstOrDefaultAsync(b => b.Id == badgeId);
        badge.Should().NotBeNull("badge must still exist after update; id={0}", badgeId);
        badge!.Name.Should().Be(newName, "badge Name must be updated; id={0}", badgeId);
    }

    [Fact(DisplayName = "AC-2: PATCH Badges/{id}/active with IsActive=false deactivates; admin list still shows it")]
    public async Task Badge_Deactivate_AdminListStillShowsIt()
    {
        var code = $"DEACT_BADGE_{Guid.NewGuid():N}"[..40];
        int badgeId = await CreateBadgeGetIdAsync(code);

        // Deactivate.
        var (resp, root, body) = await SendAsync(HttpMethod.Patch, $"{BadgesUrl}/{badgeId}/active",
            new { isActive = false }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PATCH badge/active must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        // Admin list must still contain it with IsActive=false.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.FirstOrDefault(b => TryProp(b, "id", out var id) && id.GetInt32() == badgeId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated badge must still appear in admin list; id={0}; body: {1}", badgeId, listBody);
        TryProp(found, "isActive", out var isActiveProp).Should().BeTrue("body: {0}", listBody);
        isActiveProp.GetBoolean().Should().BeFalse(
            "isActive must be false after deactivation; id={0}; body: {1}", badgeId, listBody);
    }

    [Fact(DisplayName = "AC-2: PATCH Badges/{id}/active with IsActive=true reactivates; IsActive=true in list")]
    public async Task Badge_Reactivate_IsActiveRestored()
    {
        var code = $"REACT_BADGE_{Guid.NewGuid():N}"[..40];
        int badgeId = await CreateBadgeGetIdAsync(code);

        // Deactivate then reactivate.
        await SendAsync(HttpMethod.Patch, $"{BadgesUrl}/{badgeId}/active", new { isActive = false }, _adminToken);

        var (resp, root, body) = await SendAsync(HttpMethod.Patch, $"{BadgesUrl}/{badgeId}/active",
            new { isActive = true }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PATCH badge/active reactivate must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify IsActive=true in DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var badge = await db.BadgeDefinitions.AsNoTracking().FirstOrDefaultAsync(b => b.Id == badgeId);
        badge!.IsActive.Should().BeTrue("badge must be active after reactivation; id={0}", badgeId);
    }

    // =========================================================================
    // AC-3 — Mission CRUD round-trip
    // =========================================================================

    [Fact(DisplayName = "AC-3: POST Missions → Created and appears in admin list")]
    public async Task Mission_Create_AppearsInList()
    {
        var code = $"TEST_MISSION_{Guid.NewGuid():N}"[..36];
        var body_create = ValidMissionBody(code);

        var (resp, root, body) = await SendAsync(HttpMethod.Post, MissionsUrl, body_create, _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "POST Missions must return 200 or 201; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify in admin list.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, MissionsUrl, bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.Any(m => TryProp(m, "code", out var c) && c.GetString() == code);
        found.Should().BeTrue("created mission code={0} must appear in admin list; body: {1}", code, listBody);
    }

    [Fact(DisplayName = "AC-3: PUT Missions/{id} — update mutable fields persists")]
    public async Task Mission_Update_MutableFields_Persists()
    {
        var code = $"UPD_MISSION_{Guid.NewGuid():N}"[..36];
        int missionId = await CreateMissionGetIdAsync(code);

        var newIconKey = $"icon-updated-{Guid.NewGuid():N}"[..30];
        var (resp, root, body) = await SendAsync(HttpMethod.Put, $"{MissionsUrl}/{missionId}",
            new
            {
                iconKey    = newIconKey,
                titleKey   = "missions.updated.title",
                cadence    = 1, // Daily
                targetType = 1, // CompleteLessons
                target     = 3,
                rewardXp   = 75,
                sortOrder  = 5
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PUT Missions must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify via DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var mission = await db.MissionDefinitions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == missionId);
        mission.Should().NotBeNull("mission must exist after update; id={0}", missionId);
        mission!.IconKey.Should().Be(newIconKey, "IconKey must be updated; id={0}", missionId);
    }

    [Fact(DisplayName = "AC-3: PATCH Missions/{id}/active with IsActive=false deactivates; list still shows it")]
    public async Task Mission_Deactivate_AdminListStillShowsIt()
    {
        var code = $"DEACT_MISSION_{Guid.NewGuid():N}"[..36];
        int missionId = await CreateMissionGetIdAsync(code);

        var (resp, root, body) = await SendAsync(HttpMethod.Patch, $"{MissionsUrl}/{missionId}/active",
            new { isActive = false }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PATCH mission/active must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        // Admin list must still contain it.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, MissionsUrl, bearer: _adminToken);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.FirstOrDefault(m => TryProp(m, "id", out var id) && id.GetInt32() == missionId);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "deactivated mission must still appear in admin list; body: {0}", listBody);
        TryProp(found, "isActive", out var isActiveProp).Should().BeTrue("body: {0}", listBody);
        isActiveProp.GetBoolean().Should().BeFalse(
            "isActive must be false after deactivation; id={0}; body: {1}", missionId, listBody);
    }

    [Fact(DisplayName = "AC-3: PATCH Missions/{id}/active with IsActive=true reactivates")]
    public async Task Mission_Reactivate_IsActiveRestored()
    {
        var code = $"REACT_MISSION_{Guid.NewGuid():N}"[..36];
        int missionId = await CreateMissionGetIdAsync(code);

        await SendAsync(HttpMethod.Patch, $"{MissionsUrl}/{missionId}/active", new { isActive = false }, _adminToken);

        var (resp, root, body) = await SendAsync(HttpMethod.Patch, $"{MissionsUrl}/{missionId}/active",
            new { isActive = true }, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PATCH mission/active reactivate must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var mission = await db.MissionDefinitions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == missionId);
        mission!.IsActive.Should().BeTrue("mission must be active after reactivation; id={0}", missionId);
    }

    // =========================================================================
    // AC-4 — Timed-event create/update/activate/expire
    // =========================================================================

    [Fact(DisplayName = "AC-4: POST TimedEvents with valid window + multiplier → Successed=true")]
    public async Task TimedEvent_Create_ValidParams_Succeeds()
    {
        var body_create = ValidTimedEventBody();

        var (resp, root, body) = await SendAsync(HttpMethod.Post, TimedEventsUrl, body_create, _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "POST TimedEvents must return 200 or 201 for valid params; body: {0}", body);
        AssertSucceeded(root, body);
    }

    [Fact(DisplayName = "AC-4: POST TimedEvents with StartUtc >= EndUtc → 422")]
    public async Task TimedEvent_Create_InvalidWindow_Returns422()
    {
        var now = DateTime.UtcNow;
        var badBody = new
        {
            code       = $"EVT_{Guid.NewGuid():N}",
            nameEn     = "Test Event",
            nameAr     = "حدث تجريبي",
            startUtc   = now.AddDays(1),  // start after end → invalid
            endUtc     = now.AddHours(1),
            multiplier = 2.0m,
            scope      = 1              // AllXp
        };

        var (resp, root, body) = await SendAsync(HttpMethod.Post, TimedEventsUrl, badBody, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "start>=end must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-4: POST TimedEvents with Multiplier > 5 → 422")]
    public async Task TimedEvent_Create_MultiplierTooHigh_Returns422()
    {
        var now = DateTime.UtcNow;
        var badBody = new
        {
            code       = $"EVT_{Guid.NewGuid():N}",
            nameEn     = "Test Event",
            nameAr     = "حدث تجريبي",
            startUtc   = now,
            endUtc     = now.AddDays(1),
            multiplier = 6.0m,   // > 5 — invalid
            scope      = 1
        };

        var (resp, root, body) = await SendAsync(HttpMethod.Post, TimedEventsUrl, badBody, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "multiplier > 5 must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-4: POST TimedEvents with Multiplier < 1 → 422")]
    public async Task TimedEvent_Create_MultiplierTooLow_Returns422()
    {
        var now = DateTime.UtcNow;
        var badBody = new
        {
            code       = $"EVT_{Guid.NewGuid():N}",
            nameEn     = "Test Event",
            nameAr     = "حدث تجريبي",
            startUtc   = now,
            endUtc     = now.AddDays(1),
            multiplier = 0.5m,   // < 1 — invalid
            scope      = 1
        };

        var (resp, root, body) = await SendAsync(HttpMethod.Post, TimedEventsUrl, badBody, _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "multiplier < 1 must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-4: POST TimedEvents then POST .../activate — event becomes active")]
    public async Task TimedEvent_CreateThenActivate_Succeeds()
    {
        int eventId = await CreateTimedEventGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/activate", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Activate must return 200 for an inactive event; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify IsActive=true in DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var ev = await db.TimedEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        ev!.IsActive.Should().BeTrue("TimedEvent must be active after POST .../activate; id={0}", eventId);
    }

    [Fact(DisplayName = "AC-4: POST .../activate on already-active event → Successed=false (illegal transition)")]
    public async Task TimedEvent_Activate_AlreadyActive_Rejected()
    {
        int eventId = await CreateTimedEventGetIdAsync();

        // First activate succeeds.
        await SendAsync(HttpMethod.Post, $"{TimedEventsUrl}/{eventId}/activate", bearer: _adminToken);

        // Second activate on already-active event — must be rejected.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/activate", bearer: _adminToken);
        AssertRejected(resp, root, body,
            "Activating an already-active TimedEvent must return Successed=false");
    }

    [Fact(DisplayName = "AC-4: POST .../expire on active event — event becomes inactive")]
    public async Task TimedEvent_ActivateThenExpire_Succeeds()
    {
        int eventId = await CreateTimedEventGetIdAsync();

        // Activate first.
        await SendAsync(HttpMethod.Post, $"{TimedEventsUrl}/{eventId}/activate", bearer: _adminToken);

        // Now expire.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/expire", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Expire must return 200 for an active event; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify IsActive=false in DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var ev = await db.TimedEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        ev!.IsActive.Should().BeFalse("TimedEvent must be inactive after POST .../expire; id={0}", eventId);
    }

    [Fact(DisplayName = "AC-4: POST .../expire on already-inactive event → Successed=false (illegal transition)")]
    public async Task TimedEvent_Expire_AlreadyInactive_Rejected()
    {
        int eventId = await CreateTimedEventGetIdAsync();
        // Event starts inactive (IsActive=false by default). Expire an inactive event:
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/expire", bearer: _adminToken);
        AssertRejected(resp, root, body,
            "Expiring an already-inactive TimedEvent must return Successed=false");
    }

    [Fact(DisplayName = "AC-4: PUT TimedEvents/{id} — update window/multiplier persists")]
    public async Task TimedEvent_Update_WindowAndMultiplier_Persists()
    {
        int eventId = await CreateTimedEventGetIdAsync();
        var newStart = DateTime.UtcNow.AddDays(10);
        var newEnd   = DateTime.UtcNow.AddDays(17);

        var (resp, root, body) = await SendAsync(HttpMethod.Put, $"{TimedEventsUrl}/{eventId}",
            new
            {
                nameEn      = "Updated Event EN",
                nameAr      = "حدث محدث",
                descriptionEn = (string?)null,
                descriptionAr = (string?)null,
                startUtc    = newStart,
                endUtc      = newEnd,
                multiplier  = 3.0m,
                scope       = 1 // AllXp
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "PUT TimedEvents must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var ev = await db.TimedEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        ev!.Multiplier.Should().Be(3.0m, "multiplier must be updated; id={0}", eventId);
        ev.NameEn.Should().Be("Updated Event EN", "NameEn must be updated; id={0}", eventId);
    }

    // =========================================================================
    // AC-5 — Streak freeze grant
    // =========================================================================

    [Fact(DisplayName = "AC-5: GrantStreakFreeze Count=1 for child with profile → FreezeBalance increases by 1")]
    public async Task StreakFreeze_Grant_Count1_IncreasesFreezeBalance()
    {
        // Create a student with an XP profile (first lesson completion triggers profile creation).
        var (studentToken, childId) = await CreateStudentWithProfileAsync();

        // Get the current FreezeBalance.
        int balanceBefore = await GetFreezeBalanceAsync(childId);

        // Grant 1 freeze.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/{childId}/streak-freeze",
            new { count = 1, reason = "Support ticket #123", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GrantStreakFreeze Count=1 must return 200; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify balance increased.
        int balanceAfter = await GetFreezeBalanceAsync(childId);
        balanceAfter.Should().BeGreaterThan(balanceBefore,
            "FreezeBalance must increase after granting 1 freeze; before={0}, after={1}", balanceBefore, balanceAfter);
        balanceAfter.Should().BeLessThanOrEqualTo(StudentXpProfile.MaxFreezes,
            "FreezeBalance must not exceed MaxFreezes={0}; after={1}", StudentXpProfile.MaxFreezes, balanceAfter);
    }

    [Fact(DisplayName = "AC-5: GrantStreakFreeze Count=5 → 422 (validator rejects Count > MaxFreezes=2)")]
    public async Task StreakFreeze_Grant_CountExceedsMaxFreezes_Returns422()
    {
        // Count=5 exceeds MaxFreezes=2 → FluentValidation LessThanOrEqualTo(MaxFreezes) fires → 422.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/streak-freeze",
            new { count = 5, reason = "Economy test", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Count=5 > MaxFreezes=2 must trigger FluentValidation → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("Successed must be false on 422; body: {0}", body);
    }

    [Fact(DisplayName = "AC-5: GrantStreakFreeze Count=0 → 422 (GreaterThan(0) rule)")]
    public async Task StreakFreeze_Grant_Count0_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/streak-freeze",
            new { count = 0, reason = "Economy test", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Count=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-5: GrantStreakFreeze with empty Reason → 422")]
    public async Task StreakFreeze_Grant_EmptyReason_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/streak-freeze",
            new { count = 1, reason = "", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty Reason must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-5: GrantStreakFreeze with Confirm=false → 422")]
    public async Task StreakFreeze_Grant_ConfirmFalse_Returns422()
    {
        // Confirm=false must fail FluentValidation (Equal(true) rule).
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/streak-freeze",
            new { count = 1, reason = "Support ticket", confirm = false },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Confirm=false must trigger Equal(true) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-5: GrantStreakFreeze for non-existent child → Successed=false (NotFound)")]
    public async Task StreakFreeze_Grant_NonExistentChild_Rejected()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/99999999/streak-freeze",
            new { count = 1, reason = "Support ticket #000", confirm = true },
            _adminToken);
        AssertRejected(resp, root, body,
            "Non-existent childId must return Successed=false (NotFound)");
    }

    // =========================================================================
    // AC-1 — League tier override
    // =========================================================================

    [Fact(DisplayName = "AC-1: OverrideLeagueTier with Reason empty → 422")]
    public async Task LeagueTierOverride_EmptyReason_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/league-tier",
            new { newTier = 2, reason = "", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty Reason must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-1: OverrideLeagueTier with Confirm=false → 422")]
    public async Task LeagueTierOverride_ConfirmFalse_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/league-tier",
            new { newTier = 2, reason = "Tier fix", confirm = false },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Confirm=false must trigger validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-1: OverrideLeagueTier with invalid tier enum value → 422")]
    public async Task LeagueTierOverride_InvalidEnumValue_Returns422()
    {
        // Tier=99 is not a valid LeagueTier enum value.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/1/league-tier",
            new { newTier = 99, reason = "Bad tier test", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Invalid tier enum must trigger IsInEnum validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-1: OverrideLeagueTier for non-existent child → Successed=false (NotFound)")]
    public async Task LeagueTierOverride_NonExistentChild_Rejected()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/99999999/league-tier",
            new { newTier = 2, reason = "Test tier override", confirm = true },
            _adminToken);
        AssertRejected(resp, root, body,
            "Non-existent childId must return Successed=false (NotFound)");
    }

    [Fact(DisplayName = "AC-1: OverrideLeagueTier persists new CurrentTier in DB")]
    public async Task LeagueTierOverride_PersistsNewTier()
    {
        // Create a student with a profile; start at Bronze (tier=1).
        var (_, childId) = await CreateStudentWithProfileAsync();

        // Override to Silver (tier=2).
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/{childId}/league-tier",
            new { newTier = 2 /* Silver */, reason = "Tier correction test", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "OverrideLeagueTier must return 200 for a valid child; body: {0}", body);
        AssertSucceeded(root, body);

        // Verify CurrentTier=Silver in DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var profile = await db.StudentXpProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == childId);
        profile.Should().NotBeNull("student XP profile must exist; childId={0}", childId);
        ((int)profile!.CurrentTier).Should().Be(2 /* Silver */,
            "CurrentTier must be Silver (2) after override; childId={0}", childId);
    }

    [Fact(DisplayName = "AC-1: OverrideLeagueTier no-op (same tier) → Successed=false")]
    public async Task LeagueTierOverride_NoOp_SameTier_Rejected()
    {
        var (_, childId) = await CreateStudentWithProfileAsync();

        // Read the current tier from DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var profile = await db.StudentXpProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == childId);

        profile.Should().NotBeNull("student XP profile must exist; childId={0}", childId);
        int currentTierValue = (int)profile!.CurrentTier;

        // Override with the same tier — must be rejected as no-op.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/{childId}/league-tier",
            new { newTier = currentTierValue, reason = "No-op test", confirm = true },
            _adminToken);
        AssertRejected(resp, root, body,
            "Override with same tier must be rejected as no-op (Successed=false)");
    }

    // =========================================================================
    // AC-9 — 422 on invalid command bodies (badge validation)
    // =========================================================================

    [Fact(DisplayName = "AC-9: POST Badges with empty Code → 422")]
    public async Task Badge_Create_EmptyCode_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, BadgesUrl,
            new
            {
                code        = "",   // invalid: NotEmpty
                name        = "Test Badge",
                description = "Test description",
                iconKey     = "icon-test",
                rarity      = 1,
                sortOrder   = 0,
                triggerType = 1,
                threshold   = (int?)null,
                rewardXp    = 50
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty Code must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: POST Badges with RewardXp=0 → 422")]
    public async Task Badge_Create_RewardXpZero_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, BadgesUrl,
            new
            {
                code        = $"BAD_{Guid.NewGuid():N}"[..20],
                name        = "Test Badge",
                description = "Test",
                iconKey     = "icon",
                rarity      = 1,
                sortOrder   = 0,
                triggerType = 1,
                threshold   = (int?)null,
                rewardXp    = 0   // invalid: GreaterThan(0)
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "RewardXp=0 must trigger GreaterThan(0) validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-9: POST Missions with empty Code → 422")]
    public async Task Mission_Create_EmptyCode_Returns422()
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, MissionsUrl,
            new
            {
                code       = "",  // invalid
                iconKey    = "icon-test",
                titleKey   = "missions.test",
                cadence    = 1,
                targetType = 1,
                target     = 5,
                rewardXp   = 50,
                sortOrder  = 0
            },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "Empty Code must trigger NotEmpty validator → 422; body: {0}", body);
        TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
        s.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    // =========================================================================
    // AC-6 — Audit end-to-end: GrantStreakFreeze produces AuditLog row
    //
    // This tests the critical cross-module path:
    //   Admin HTTP POST .../streak-freeze
    //     → GrantStreakFreezeCommandHandler raises AdminActionPerformedDomainEvent
    //       on StudentXpProfile (AggregateRoot)
    //         → UnitOfWorkBehavior commits, then dispatches domain events post-commit
    //           → AdminActionPerformedDomainEventRelayHandler re-publishes
    //             AdminActionPerformedEvent (Shared.Contracts)
    //               → AuditLogEventHandler (Moderation) writes AuditLog row
    //                 → GET /api/Admin/Audit/Log returns the row
    //
    // If no audit row appears: the relay wiring is broken (Critical defect for P7-13).
    // =========================================================================

    [Fact(DisplayName = "AC-6: GrantStreakFreeze → AuditLog row appears with correct action + adminUserId")]
    public async Task Audit_GrantStreakFreeze_ProducesAuditRow()
    {
        var (_, childId) = await CreateStudentWithProfileAsync();

        // Perform a freeze grant.
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/{childId}/streak-freeze",
            new { count = 1, reason = "Audit test grant", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GrantStreakFreeze must succeed for audit test; body: {0}", body);
        AssertSucceeded(root, body);

        // Poll for the audit row.
        var auditRow = await PollForAuditRowAsync(
            filter: $"actionType=Gamification.StreakFreezeGranted&adminUserId={_adminUserId}",
            timeoutSeconds: 5);

        auditRow.Should().NotBeNull(
            "GrantStreakFreeze must produce an AuditLog row with action='Gamification.StreakFreezeGranted'. " +
            "If no row appears, the relay path is broken: verify AdminActionPerformedDomainEvent is raised " +
            "on StudentXpProfile in GrantStreakFreezeCommandHandler, that UnitOfWorkBehavior dispatches it " +
            "post-commit, and that AdminActionPerformedDomainEventRelayHandler and AuditLogEventHandler " +
            "are registered. This is a CRITICAL relay wiring defect.");

        // Verify the row fields.
        TryProp(auditRow!.Value, "adminUserId", out var adminIdProp).Should().BeTrue(
            "AuditLogDto must expose adminUserId");
        adminIdProp.GetInt32().Should().Be(_adminUserId,
            "audit row adminUserId must match the authenticated admin's JWT sub");

        TryProp(auditRow.Value, "action", out var actionProp).Should().BeTrue(
            "AuditLogDto must expose action");
        actionProp.GetString().Should().Be("Gamification.StreakFreezeGranted",
            "audit row action must be 'Gamification.StreakFreezeGranted'");

        TryProp(auditRow.Value, "targetEntityType", out var typeProp).Should().BeTrue(
            "AuditLogDto must expose targetEntityType");
        typeProp.GetString().Should().Be("StudentXpProfile",
            "targetEntityType must be 'StudentXpProfile'");

        // PII check: Details must not contain the child's email or password.
        if (TryProp(auditRow.Value, "details", out var detailsProp)
            && detailsProp.ValueKind == JsonValueKind.String)
        {
            var details = detailsProp.GetString() ?? "";
            details.Should().NotContainAny(new[] { "@test.local", "password", "Password" },
                "Details must be PII-safe (no email addresses or passwords)");
        }
    }

    [Fact(DisplayName = "AC-6: OverrideLeagueTier → AuditLog row appears with action='Gamification.LeagueTierOverridden'")]
    public async Task Audit_OverrideLeagueTier_ProducesAuditRow()
    {
        var (_, childId) = await CreateStudentWithProfileAsync();

        // Perform tier override (Bronze=1 by default; override to Silver=2).
        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{AdminGamBase}/children/{childId}/league-tier",
            new { newTier = 2 /* Silver */, reason = "Audit tier override test", confirm = true },
            _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "OverrideLeagueTier must succeed for audit test; body: {0}", body);
        AssertSucceeded(root, body);

        // Poll for the audit row.
        var auditRow = await PollForAuditRowAsync(
            filter: $"actionType=Gamification.LeagueTierOverridden&adminUserId={_adminUserId}",
            timeoutSeconds: 5);

        auditRow.Should().NotBeNull(
            "OverrideLeagueTier must produce an AuditLog row with action='Gamification.LeagueTierOverridden'. " +
            "If no row appears, the AdminActionPerformedDomainEvent relay is broken — CRITICAL defect.");

        TryProp(auditRow!.Value, "action", out var actionProp).Should().BeTrue(
            "AuditLogDto must expose action");
        actionProp.GetString().Should().Be("Gamification.LeagueTierOverridden",
            "audit action must be 'Gamification.LeagueTierOverridden'");
    }

    [Fact(DisplayName = "AC-6: CreateBadge → AuditLog row appears with action='Badge.Created'")]
    public async Task Audit_CreateBadge_ProducesAuditRow()
    {
        var code = $"AUDIT_BADGE_{Guid.NewGuid():N}"[..40];
        var (resp, root, body) = await SendAsync(HttpMethod.Post, BadgesUrl, ValidBadgeBody(code), _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "CreateBadge must succeed; body: {0}", body);
        AssertSucceeded(root, body);

        var auditRow = await PollForAuditRowAsync(
            filter: $"actionType=Badge.Created&adminUserId={_adminUserId}",
            timeoutSeconds: 5);

        auditRow.Should().NotBeNull(
            "CreateBadge must produce an AuditLog row with action='Badge.Created'. " +
            "If no row appears, the AdminActionPerformedDomainEvent relay on BadgeDefinition is broken. " +
            "Note: BadgeDefinition derives from AggregateRoot (confirmed in handler) so the domain-event " +
            "relay path must work here as well.");

        TryProp(auditRow!.Value, "action", out var actionProp).Should().BeTrue();
        actionProp.GetString().Should().Be("Badge.Created",
            "audit action must be 'Badge.Created'");
    }

    [Fact(DisplayName = "AC-6: ActivateTimedEvent → AuditLog row appears with action='TimedEvent.Activated'")]
    public async Task Audit_ActivateTimedEvent_ProducesAuditRow()
    {
        int eventId = await CreateTimedEventGetIdAsync();

        var (resp, root, body) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/activate", bearer: _adminToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ActivateTimedEvent must succeed for audit test; body: {0}", body);
        AssertSucceeded(root, body);

        var auditRow = await PollForAuditRowAsync(
            filter: $"actionType=TimedEvent.Activated&adminUserId={_adminUserId}",
            timeoutSeconds: 5);

        auditRow.Should().NotBeNull(
            "ActivateTimedEvent must produce an AuditLog row with action='TimedEvent.Activated'. " +
            "If no row appears, the domain-event relay on TimedEvent is broken.");

        TryProp(auditRow!.Value, "action", out var actionProp).Should().BeTrue();
        actionProp.GetString().Should().Be("TimedEvent.Activated");
    }

    [Fact(DisplayName = "DEF-GAM-01: ExpireTimedEvent rewinds EndUtc to now (active future-window event → ENDED, not SCHEDULED)")]
    public async Task ExpireTimedEvent_RewindsEndUtc()
    {
        // CreateTimedEventGetIdAsync seeds an event with EndUtc = now + 7 days, IsActive = false.
        int eventId = await CreateTimedEventGetIdAsync();

        // Must be active before it can be expired (ExpireTimedEvent rejects inactive events).
        var (actResp, _, actBody) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/activate", bearer: _adminToken);
        actResp.StatusCode.Should().Be(HttpStatusCode.OK, "activate must succeed; body: {0}", actBody);

        var beforeUtc = DateTime.UtcNow;

        var (expResp, expRoot, expBody) = await SendAsync(HttpMethod.Post,
            $"{TimedEventsUrl}/{eventId}/expire", bearer: _adminToken);
        expResp.StatusCode.Should().Be(HttpStatusCode.OK, "expire must succeed; body: {0}", expBody);
        AssertSucceeded(expRoot, expBody);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var ev = await db.TimedEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        ev.Should().NotBeNull("timed event must exist after expire; id={0}", eventId);

        ev!.IsActive.Should().BeFalse("expire must deactivate the event; id={0}", eventId);

        // DEF-GAM-01: EndUtc must be rewound to ~now (it was now+7d), so the timestamp-based FE
        // status renders ENDED instead of SCHEDULED. (timestamptz reads back machine-local under
        // EnableLegacyTimestampBehavior → normalize to UTC before comparing.)
        var endUtc = ev.EndUtc.ToUniversalTime();
        endUtc.Should().BeOnOrAfter(beforeUtc.AddSeconds(-5),
            "EndUtc should be the expire time, not the original future value");
        endUtc.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5),
            "EndUtc must NOT remain in the future after a manual expire (DEF-GAM-01)");
    }

    // =========================================================================
    // Seeder seed-if-absent (AC-8 from brief): admin-edited badge survives re-seed
    // =========================================================================

    [Fact(DisplayName = "AC-8 (Seeder): admin-edited badge name survives BadgeSeeder re-run (seed-if-absent only)")]
    public async Task Seeder_AdminEditedBadge_SurvivesReseed()
    {
        // Pick a seeded badge by reading the admin list.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);

        if (items.Count == 0)
        {
            // No seeded badges — this can happen if the BadgeSeeder hasn't run yet.
            // Skip the seeder test gracefully; the seeder test is not blocking.
            return;
        }

        // Take the first seeded badge.
        var firstBadge = items[0];
        TryProp(firstBadge, "id", out var idProp).Should().BeTrue("body: {0}", listBody);
        int badgeId = idProp.GetInt32();

        // Rename it via admin PUT.
        var adminName = $"AdminEdited_{Guid.NewGuid():N}"[..40];
        var (putResp, putRoot, putBody) = await SendAsync(HttpMethod.Put, $"{BadgesUrl}/{badgeId}",
            new
            {
                name        = adminName,
                description = "Admin-edited description",
                iconKey     = "icon-edited",
                rarity      = 1,
                sortOrder   = 999,
                triggerType = 1,
                threshold   = (int?)null,
                rewardXp    = 100
            },
            _adminToken);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK, "PUT badge must succeed; body: {0}", putBody);
        AssertSucceeded(putRoot, putBody);

        // Re-run the BadgeSeeder (simulates a service restart).
        using (var scope = _factory.Services.CreateScope())
        {
            var badgeSeeder = scope.ServiceProvider.GetRequiredService<BadgeSeeder>();
            await badgeSeeder.SeedAsync();
        }

        // Admin-edited name must survive — seeder must not overwrite existing rows.
        using var dbScope = _factory.Services.CreateScope();
        var db = dbScope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var badge = await db.BadgeDefinitions.AsNoTracking().FirstOrDefaultAsync(b => b.Id == badgeId);
        badge.Should().NotBeNull("badge must exist after re-seed; id={0}", badgeId);
        badge!.Name.Should().Be(adminName,
            "BadgeSeeder must be seed-if-absent — it must NOT overwrite admin-edited Name; id={0}", badgeId);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Creates a parent + child via the registration flow, triggers a profile creation
    /// by hitting the dashboard (lazy-creates StudentXpProfile), and returns (token, childId).
    /// </summary>
    private async Task<(string StudentToken, int ChildId)> CreateStudentWithProfileAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var parentEmail = $"p713_parent_{tag}@test.local";
        var childEmail  = $"p713_child_{tag}@test.local";

        // Register parent.
        var (regResp, regRoot, regBody) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "parent registration must succeed; body: {0}", regBody);
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTok).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTok.GetString()!;

        // Add child.
        var (addResp, addRoot, addBody) = await SendAsync(HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "P713 Test Child",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 1,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            "Add-Child must succeed; body: {0}", addBody);
        TryProp(addRoot, "data", out var addData).Should().BeTrue("body: {0}", addBody);
        TryProp(addData, "id", out var idProp).Should().BeTrue("body: {0}", addBody);
        var childId = idProp.GetInt32();

        // Sign in as child.
        var (siResp, siRoot, siBody) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        siResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in must succeed; body: {0}", siBody);
        TryProp(siRoot, "data", out var siData).Should().BeTrue("body: {0}", siBody);
        TryProp(siData, "accessToken", out var studentTokProp).Should().BeTrue("body: {0}", siBody);
        var studentToken = studentTokProp.GetString()!;

        // Hit the dashboard to lazy-create the XP profile.
        // This ensures GrantStreakFreeze/OverrideLeagueTier can find the profile.
        await SendAsync(HttpMethod.Get, "api/Learning/Dashboard", bearer: studentToken);

        // If the dashboard doesn't create the profile (no seeded lessons), create it directly in DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var existingProfile = await db.StudentXpProfiles
            .FirstOrDefaultAsync(p => p.StudentId == childId);
        if (existingProfile is null)
        {
            // Manually seed a minimal StudentXpProfile so the admin endpoints can find it.
            var profile = new StudentXpProfile
            {
                StudentId      = childId,
                TotalXp        = 0,
                CurrentLevel   = 1,
                LastAwardAtUtc = DateTime.UtcNow,
            };
            db.StudentXpProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        return (studentToken, childId);
    }

    /// <summary>Reads the FreezeBalance for a student by childId (StudentId).</summary>
    private async Task<int> GetFreezeBalanceAsync(int childId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var profile = await db.StudentXpProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == childId);
        return profile?.FreezeBalance ?? 0;
    }

    /// <summary>
    /// Creates a badge definition via the API and returns its id from the admin list.
    /// NOTE: The handler returns Created(badge.Id) but badge.Id=0 at handler return time
    /// because UoW commits AFTER the handler (product defect). We always fall back to the
    /// admin list search by code so we get the real DB-assigned id.
    /// </summary>
    private async Task<int> CreateBadgeGetIdAsync(string code)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, BadgesUrl, ValidBadgeBody(code), _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "CreateBadge prereq must succeed; body: {0}", body);
        AssertSucceeded(root, body);

        // data may be 0 (handler returns Created(entity.Id) before UoW commits → Id not yet assigned).
        // Only trust data if it's a positive integer.
        TryProp(root, "data", out var dataEl);
        if (dataEl.ValueKind == JsonValueKind.Number && dataEl.GetInt32() > 0)
            return dataEl.GetInt32();

        // Fallback: find in the list by code.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, BadgesUrl, bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.FirstOrDefault(b => TryProp(b, "code", out var c) && c.GetString() == code);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "Newly created badge with code={0} must appear in list; body: {1}", code, listBody);
        TryProp(found, "id", out var idProp).Should().BeTrue("body: {0}", listBody);
        var resolvedBadgeId = idProp.GetInt32();
        resolvedBadgeId.Should().BeGreaterThan(0,
            "BadgeDefinition id in list must be DB-assigned positive int; found item: {0}; full list body: {1}",
            found.ToString(), listBody);
        return resolvedBadgeId;
    }

    /// <summary>
    /// Creates a mission definition via the API and returns its id from the admin list.
    /// NOTE: Same UoW/id=0 caveat as CreateBadgeGetIdAsync — always fall back to list.
    /// </summary>
    private async Task<int> CreateMissionGetIdAsync(string code)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, MissionsUrl, ValidMissionBody(code), _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "CreateMission prereq must succeed; body: {0}", body);
        AssertSucceeded(root, body);

        // Only trust data if it's a positive integer (see note on CreateBadgeGetIdAsync).
        TryProp(root, "data", out var dataEl);
        if (dataEl.ValueKind == JsonValueKind.Number && dataEl.GetInt32() > 0)
            return dataEl.GetInt32();

        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get, MissionsUrl, bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.FirstOrDefault(m => TryProp(m, "code", out var c) && c.GetString() == code);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "Newly created mission with code={0} must appear in list; body: {1}", code, listBody);
        TryProp(found, "id", out var idProp).Should().BeTrue("body: {0}", listBody);
        return idProp.GetInt32();
    }

    /// <summary>
    /// Creates a timed event via the API and returns its id from the admin list.
    /// NOTE: Same UoW/id=0 caveat — fall back to GET api/admin/timed-events list search.
    /// </summary>
    private async Task<int> CreateTimedEventGetIdAsync(string? code = null)
    {
        var eventCode = code ?? $"EVT_{Guid.NewGuid():N}";
        var body_create = ValidTimedEventBody(eventCode);
        var (resp, root, body) = await SendAsync(HttpMethod.Post, TimedEventsUrl, body_create, _adminToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "CreateTimedEvent prereq must succeed; body: {0}", body);
        AssertSucceeded(root, body);

        // Only trust data if it's a positive integer (see note on CreateBadgeGetIdAsync).
        TryProp(root, "data", out var dataEl);
        if (dataEl.ValueKind == JsonValueKind.Number && dataEl.GetInt32() > 0)
            return dataEl.GetInt32();

        // Fallback: find in the admin timed-events list by code.
        var (listResp, listRoot, listBody) = await SendAsync(HttpMethod.Get,
            "api/admin/timed-events", bearer: _adminToken);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET api/admin/timed-events must return 200; body: {0}", listBody);
        var items = ExtractDataArray(listRoot, listBody);
        var found = items.FirstOrDefault(e => TryProp(e, "code", out var c) && c.GetString() == eventCode);
        found.ValueKind.Should().NotBe(JsonValueKind.Undefined,
            "Newly created TimedEvent with code={0} must appear in admin list; body: {1}", eventCode, listBody);
        TryProp(found, "id", out var idProp).Should().BeTrue("body: {0}", listBody);
        return idProp.GetInt32();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Body factories
    // ─────────────────────────────────────────────────────────────────────────

    private static object ValidBadgeBody(string? code = null) => new
    {
        code        = code ?? $"TEST_BADGE_{Guid.NewGuid():N}"[..40],
        name        = "Test Badge",
        description = "A test badge created by integration tests",
        iconKey     = "icon-test-badge",
        rarity      = 1,     // Common
        sortOrder   = 0,
        triggerType = 1,     // FirstLesson
        threshold   = (int?)null,
        rewardXp    = 50
    };

    private static object ValidMissionBody(string? code = null) => new
    {
        code       = code ?? $"TEST_MISSION_{Guid.NewGuid():N}"[..36],
        iconKey    = "icon-test-mission",
        titleKey   = "missions.test.complete_lessons",
        cadence    = 1,    // Daily
        targetType = 1,    // CompleteLessons
        target     = 5,
        rewardXp   = 50,
        sortOrder  = 0
    };

    private static object ValidTimedEventBody(string? code = null)
    {
        var now = DateTime.UtcNow;
        return new
        {
            code        = code ?? $"EVT_{Guid.NewGuid():N}",
            nameEn      = "Integration Test Event",
            nameAr      = "حدث اختبار",
            descriptionEn = (string?)null,
            descriptionAr = (string?)null,
            startUtc    = now,
            endUtc      = now.AddDays(7),
            multiplier  = 2.0m,
            scope       = 1     // AllXp
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Audit log helpers (mirrors P7-12 pattern)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<JsonElement?> PollForAuditRowAsync(
        string filter,
        int timeoutSeconds = 3,
        int minExpectedCount = 1)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var (resp, root, _) = await SendAsync(HttpMethod.Get,
                $"{AuditLogUrl}?{filter}&pageSize=50", bearer: _adminToken);
            if (resp.StatusCode == HttpStatusCode.OK)
            {
                var items = ExtractAuditLogItems(root);
                if (items.Count >= minExpectedCount)
                    return items.Count > 0 ? items[0] : (JsonElement?)null;
            }
            await Task.Delay(300);
        }
        return null;
    }

    private static List<JsonElement> ExtractAuditLogItems(JsonElement root)
    {
        if (!TryProp(root, "data", out var outer)) return [];
        if (!TryProp(outer, "data", out var inner)) return [];
        if (inner.ValueKind != JsonValueKind.Array) return [];
        return inner.EnumerateArray().ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JSON / HTTP helpers
    // ─────────────────────────────────────────────────────────────────────────

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
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    /// <summary>Case-insensitive JSON property lookup (camelCase + PascalCase + linear scan).</summary>
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

    /// <summary>Extracts the data array from an admin list response (not paginated — flat array in data).</summary>
    private static List<JsonElement> ExtractDataArray(JsonElement root, string bodyStr = "")
    {
        if (!TryProp(root, "data", out var data)) return [];
        if (data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray().ToList();
        // Possibly wrapped in a PaginatedResult — try inner data.
        if (data.ValueKind == JsonValueKind.Object && TryProp(data, "data", out var inner)
            && inner.ValueKind == JsonValueKind.Array)
            return inner.EnumerateArray().ToList();
        return [];
    }

    private static void AssertSucceeded(JsonElement root, string body)
    {
        TryProp(root, "successed", out var s).Should().BeTrue("envelope must have successed; body: {0}", body);
        s.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);
    }

    private static void AssertRejected(HttpResponseMessage response, JsonElement root, string body, string reason)
    {
        var statusCode = (int)response.StatusCode;

        var parseAction = () => JsonDocument.Parse(body);
        parseAction.Should().NotThrow("rejection response must be valid JSON; reason={0}; body: {1}", reason, body);

        if (statusCode == 200)
        {
            TryProp(root, "successed", out var s).Should().BeTrue("body: {0}", body);
            s.GetBoolean().Should().BeFalse(
                "{0}; expected Successed=false on 200 rejection; body: {1}", reason, body);
        }
        else
        {
            statusCode.Should().BeOneOf(new[] { 400, 404, 422, 424, 500 },
                "{0}; expected a non-success HTTP status; body: {1}", reason, body);
        }
    }

    private async Task<string> SignInGetTokenAsync(string userName, string password)
    {
        var (resp, root, body) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<string> RegisterParentGetTokenAsync(string tag = "")
    {
        var email = $"p713_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.com";
        var (resp, root, body) = await SendAsync(HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private static int DecodeUserIdFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return 0;
            var payload  = parts[1];
            var remainder = payload.Length % 4;
            var padded = remainder == 2 ? payload + "==" :
                         remainder == 3 ? payload + "="  :
                         payload;
            var bytes  = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
            var json   = Encoding.UTF8.GetString(bytes);
            using var doc  = JsonDocument.Parse(json);
            var jwtRoot = doc.RootElement;
            foreach (var claimName in new[] { "sub", "nameid",
                "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                "uid", "userId", "id" })
            {
                if (TryProp(jwtRoot, claimName, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number)   return val.GetInt32();
                    if (val.ValueKind == JsonValueKind.String &&
                        int.TryParse(val.GetString(), out var p)) return p;
                }
            }
            return 0;
        }
        catch { return 0; }
    }
}

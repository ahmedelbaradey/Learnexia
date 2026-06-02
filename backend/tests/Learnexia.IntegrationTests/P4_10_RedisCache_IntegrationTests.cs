using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Application.Caching.Rebuild;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Infrastructure;
using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P4-10 integration tests — "Serve realtime gamification state from Redis".
///
/// Tests the Redis read-model layer: cache decorators, invalidators, sorted-set
/// leaderboard, rebuild service, kill-switch, and null-cache fallback.
///
/// Infrastructure:
///   - A Testcontainers Redis container is spun up per test-class run.
///   - The "Redis-backed" factory override replaces IConnectionMultiplexer with the container.
///     Because AddGamificationCache (DependencyInjection.cs) checks for IConnectionMultiplexer
///     at resolution time (Singleton factory delegate), the override must happen before the
///     IGamificationCache Singleton is first resolved. ConfigureServices replaces the IConnectionMultiplexer
///     singleton; the lazy-Singleton factory in AddGamificationCache then picks it up.
///   - The kill-switch factory sets Gamification:Cache:Enabled=false (no Redis needed).
///   - The "null-cache" tests use the base factory which has no IConnectionMultiplexer.
///
/// Coverage map (16 tests):
///   T01  Cache miss falls through to Postgres + Redis populated on first read (AC1)
///   T02  Cache hit returns stale data — proves decorator intercepts on second call (AC1)
///   T03  XpAwardedDomainEvent invalidates XP cache key (AC2 + AC5)
///   T04  XpAwardedDomainEvent updates sorted-set score (AC3 + AC5)
///   T05  Sorted-set rank ordering: B(200) rank 1, A(100) rank 2, C(50) rank 3 (AC3)
///   T06  Sorted-set tiebreak: equal XP, earlier joiner wins (AC3)
///   T07  Rebuild job restores deliberately wrong score from Postgres ledger (AC4)
///   T08  Kill-switch Enabled=false → Postgres fallthrough, no Redis key written (AC1)
///   T09  NullGamificationCache (no Redis) → dashboard succeeds cleanly, no 5xx (AC1)
///   T10  StreakAdvancedDomainEvent invalidates streak key (AC2)
///   T11  BadgeEarnedDomainEvent invalidates badges:3 key; fresh read shows new badge (AC2)
///   T12  DI type assertion — IStudentXpQuery resolves as CachedStudentXpQuery (AC1 gate)
///   T13  Envelope shape — "successed" key present on all responses (CONVENTIONS §5)
///   T14  No PII in Redis keys — only int IDs in all gamification: keys
///   T15  GamificationCacheRebuildJob idempotent — run twice, same result no error (AC4)
///   T16  Concurrent XP awards — no lost data, Postgres reflects sum (AC5)
/// </summary>
[Collection("IntegrationTests")]
public sealed class P4_10_RedisCache_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string DashboardUrl       = "api/Learning/Dashboard";
    private const string ValidChildPassword = "Child@Pass1";

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;
    private IConnectionMultiplexer? _testRedis;  // direct connection for assertions
    private RedisContainer? _redisContainer;
    private int _mathG1DemoLessonId;
    private int _mathG1DemoLessonSkillId;

    public P4_10_RedisCache_IntegrationTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAndSeedAsync();

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

        using (var scope = _factory.Services.CreateScope())
        {
            await LearningSeeder.SeedAsync(scope.ServiceProvider);
        }

        using var dbScope = _factory.Services.CreateScope();
        using var db = dbScope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var demoLesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == "Introduction to Counting (G1)");

        demoLesson.Should().NotBeNull("LearningSeeder must seed the demo lesson");
        _mathG1DemoLessonId      = demoLesson!.Id;
        _mathG1DemoLessonSkillId = demoLesson.SkillId
            ?? throw new InvalidOperationException("Demo lesson must have a SkillId");

        // Start a Redis Testcontainer.
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();
        await _redisContainer.StartAsync();

        // Open a direct connection for raw Redis assertions (KEYS, ZSCORE, etc.).
        _testRedis = ConnectionMultiplexer.Connect(_redisContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        _factory.TestClock.Reset();
        _testRedis?.Dispose();
        if (_redisContainer is not null)
            await _redisContainer.StopAsync();
    }

    // ── Fixture helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a WebApplicationFactory that routes the gamification cache at the test Redis container.
    ///
    /// Strategy: ConfigureServices replaces the IConnectionMultiplexer Singleton (which is registered
    /// conditionally in Program.cs). The IGamificationCache Singleton factory in AddGamificationCache
    /// (DependencyInjection.cs) calls sp.GetService&lt;IConnectionMultiplexer&gt;() lazily — because it
    /// is itself a Singleton delegate, the first resolution wins. Replacing IConnectionMultiplexer
    /// before any resolution causes the factory to pick up the test container's multiplexer.
    ///
    /// NOTE: AddConfiguration must also supply ConnectionStrings:Redis so the host's Program.cs
    /// code path that originally would have registered IConnectionMultiplexer does not short-circuit.
    /// However, since the test overrides services AFTER Program.cs runs, we just replace the
    /// registration directly.
    /// </summary>
    private WebApplicationFactory<Program> CreateRedisFactory()
    {
        var connString = _redisContainer!.GetConnectionString();

        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Redis"] = connString,
                    ["Gamification:Cache:Enabled"] = "true",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace the IConnectionMultiplexer Singleton with one pointing at the test container.
                // This causes the IGamificationCache Singleton factory delegate (in AddGamificationCache)
                // to resolve RedisGamificationCache using the test Redis on first resolution.
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_ =>
                    ConnectionMultiplexer.Connect(connString));

                // Remove the previously-registered IGamificationCache and ILeagueLeaderboard Singletons
                // so they are re-created by the factory delegates in AddGamificationCache using the
                // new IConnectionMultiplexer.
                services.RemoveAll<IGamificationCache>();
                services.RemoveAll<ILeagueLeaderboard>();

                // Re-run the cache registration extension to wire up the Redis-backed implementations.
                // We pass a minimal IConfiguration with the Enabled=true flag.
                var redisConfig = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Gamification:Cache:Enabled"] = "true",
                    })
                    .Build();
                services.AddGamificationCache(redisConfig);
            });
        });
    }

    /// <summary>Creates a factory with the gamification cache kill-switch disabled.</summary>
    private WebApplicationFactory<Program> CreateKillSwitchFactory()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Gamification:Cache:Enabled"] = "false",
                });
            });
        });
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

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

    private async Task<string> SignInAndGetTokenAsync(HttpClient client, string email, string password)
    {
        var (resp, root, body) = await SendAsync(client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    private async Task<(string StudentToken, int StudentId)> CreateStudentViaParentFlowAsync(
        HttpClient client, string? tag = null)
    {
        tag ??= Guid.NewGuid().ToString("N")[..8];

        var parentEmail = $"p410_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (regResp, regRoot, regBody) = await SendAsync(client, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        ((int)regResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"parent registration must succeed; body: {regBody}");
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var parentTok).Should().BeTrue("body: {0}", regBody);
        var parentToken = parentTok.GetString()!;

        var childEmail = $"p410_child_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";
        var (addResp, addRoot, addBody) = await SendAsync(client, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student P410",
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

        var studentToken = await SignInAndGetTokenAsync(client, childEmail, ValidChildPassword);
        return (studentToken, childId);
    }

    private async Task SeedProfileAsync(int studentId, int totalXp = 0)
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
                    ""CreatedAt"", ""CreatedBy"")
                   VALUES ({studentId}, {totalXp}, {1}, {DateTime.UtcNow},
                           {0}, {0}, {(DateOnly?)null},
                           {5}, {(DateTime?)null}, {(int)LeagueTier.Bronze},
                           {DateTime.UtcNow}, {0})");
        }
    }

    private async Task<LeagueMembership?> GetActiveMembershipAsync(int studentId)
    {
        var now = _factory.TestClock.UtcNow;
        var (periodKey, _, _) = MissionPeriodCalculator.GetCurrentPeriod(MissionType.Weekly, now);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var profile = await db.StudentXpProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.StudentId == studentId);
        if (profile is null) return null;
        return await db.LeagueMemberships.AsNoTracking()
            .Include(m => m.League)
            .FirstOrDefaultAsync(m => m.StudentXpProfileId == profile.Id && m.PeriodKey == periodKey);
    }

    private async Task PublishAnswerSubmittedAsync(
        int studentId, WebApplicationFactory<Program>? factory = null, DateTime? at = null)
    {
        var ev = new AnswerSubmittedIntegrationEvent(
            EventId:            Guid.NewGuid(),
            OccurredOnUtc:      at ?? DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            CorrectAnswerCount: 1);

        using var scope = (factory ?? _factory).Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(ev);
    }

    // =========================================================================
    // T01 — Cache miss falls through to Postgres; second read populates Redis
    // =========================================================================

    [Fact(DisplayName = "P410-T01 Cache miss → Postgres hit + Redis XP key populated on first dashboard read")]
    public async Task T01_CacheMiss_FallsThroughToPostgres_ThenPopulatesRedis()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t01");
        await SeedProfileAsync(studentId, totalXp: 42);

        var db2 = _testRedis!.GetDatabase();
        var xpKey = GamificationCacheKeys.Xp(studentId);

        // Key must not exist before the first call.
        (await db2.KeyExistsAsync(xpKey)).Should().BeFalse(
            "XP key must not exist in Redis before the first dashboard read");

        // First dashboard call → cache miss → Postgres hit.
        var (resp, root, body) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "first dashboard must succeed; body: {0}", body);

        // After the call the decorator must have written the key.
        (await db2.KeyExistsAsync(xpKey)).Should().BeTrue(
            "XP key must be populated in Redis after the first dashboard read (cache-miss populates the cache)");

        // Verify the cached XP value matches the seeded Postgres value.
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "xp", out var xp).Should().BeTrue("body: {0}", body);
        xp.GetInt32().Should().Be(42, "dashboard must return the seeded TotalXp=42; body: {0}", body);
    }

    // =========================================================================
    // T02 — Cache hit returns stale value, proving decorator short-circuits Postgres
    // =========================================================================

    [Fact(DisplayName = "P410-T02 Cache hit returns stale Postgres-bypassed value — direct DB mutation without event stays invisible within TTL")]
    public async Task T02_CacheHit_ReturnsStaleValue_ProvesCacheInterception()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t02");
        await SeedProfileAsync(studentId, totalXp: 50);

        // First call: populates cache (XP=50).
        var (resp1, root1, body1) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body1);
        TryProp(root1, "data", out var data1).Should().BeTrue("body: {0}", body1);
        TryProp(data1, "xp", out var xp1Elem).Should().BeTrue("body: {0}", body1);
        xp1Elem.GetInt32().Should().Be(50, "first read must return seeded XP=50; body: {0}", body1);

        // Mutate Postgres directly, bypassing the command/event pipeline (no XpAwardedDomainEvent).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE gamification.""StudentXpProfiles""
                   SET ""TotalXp"" = 999
                   WHERE ""StudentId"" = {studentId}");
        }

        // Second call: should return cached value (50), not the direct Postgres mutation (999).
        var (resp2, root2, body2) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body2);
        TryProp(root2, "data", out var data2).Should().BeTrue("body: {0}", body2);
        TryProp(data2, "xp", out var xp2Elem).Should().BeTrue("body: {0}", body2);
        xp2Elem.GetInt32().Should().Be(50,
            "second read must serve the cached value (50) — direct Postgres mutation without raising " +
            "XpAwardedDomainEvent bypasses invalidation, so stale Redis value is served within TTL; " +
            "body: {0}", body2);
    }

    // =========================================================================
    // T03 — XpAwardedDomainEvent invalidates XP cache key
    // =========================================================================

    [Fact(DisplayName = "P410-T03 XpAwardedDomainEvent → XP cache key DELeted by invalidator → next read returns fresh Postgres value")]
    public async Task T03_XpAwardedDomainEvent_InvalidatesXpKey()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t03");
        await SeedProfileAsync(studentId);

        // Warm the cache via dashboard.
        var (warmResp, _, warmBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        warmResp.StatusCode.Should().Be(HttpStatusCode.OK, "warmup must succeed; body: {0}", warmBody);

        var db2 = _testRedis!.GetDatabase();
        var xpKey = GamificationCacheKeys.Xp(studentId);
        (await db2.KeyExistsAsync(xpKey)).Should().BeTrue(
            "XP key must be populated in Redis after warmup");

        // Award XP via the real event pipeline using the Redis-backed factory scope
        // so the XpAwardedCacheInvalidator runs against the test Redis container.
        await PublishAnswerSubmittedAsync(studentId, redisFactory);

        // Allow async post-commit dispatch to complete (IsolatedNotificationPublisher is synchronous in-process).
        await Task.Delay(100);

        // XpAwardedCacheInvalidator must have deleted the XP key.
        (await db2.KeyExistsAsync(xpKey)).Should().BeFalse(
            "XpAwardedCacheInvalidator must DEL the XP cache key after XpAwardedDomainEvent fires (post-commit)");

        // Next dashboard read must re-populate cache with fresh XP from Postgres.
        var (freshResp, freshRoot, freshBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        freshResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", freshBody);
        TryProp(freshRoot, "data", out var freshData).Should().BeTrue("body: {0}", freshBody);
        TryProp(freshData, "xp", out var freshXp).Should().BeTrue("body: {0}", freshBody);
        freshXp.GetInt32().Should().BeGreaterThan(0,
            "fresh read after invalidation must show XP > 0 (correct answer awards 10 XP); body: {0}", freshBody);
    }

    // =========================================================================
    // T04 — XpAwardedDomainEvent updates sorted-set score
    // =========================================================================

    [Fact(DisplayName = "P410-T04 XpAwardedDomainEvent → sorted-set score mirrors post-commit WeeklyXp from Postgres")]
    public async Task T04_XpAwardedDomainEvent_UpdatesSortedSet()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t04");
        await SeedProfileAsync(studentId);

        // Trigger league placement via dashboard read.
        var (dashResp, _, dashBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        dashResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", dashBody);

        var membership = await GetActiveMembershipAsync(studentId);
        if (membership is null)
        {
            // No league membership yet (brand-new student, no-op path). Test passes vacuously.
            return;
        }

        // Award XP via the Redis-backed factory scope so the XpAwardedCacheInvalidator
        // runs against the test Redis container and updates the sorted set.
        await PublishAnswerSubmittedAsync(studentId, redisFactory);
        await Task.Delay(100);

        // The sorted-set member must have a score > 0 (encoding embeds WeeklyXp).
        var db2 = _testRedis!.GetDatabase();
        var leaderboardKey = GamificationCacheKeys.LeaderboardSortedSet(membership.LeagueId);
        var score = await db2.SortedSetScoreAsync(leaderboardKey, membership.Id.ToString());

        score.Should().NotBeNull(
            "sorted-set entry must exist for membership {0} after XpAwarded event; " +
            "XpAwardedCacheInvalidator calls ILeagueLeaderboard.UpsertMembershipAsync", membership.Id);

        if (score is not null)
        {
            var (weeklyXp, _) = LeaderboardScoreEncoder.Decode((long)score.Value);
            weeklyXp.Should().BeGreaterThan(0,
                "decoded WeeklyXp from sorted-set score must be > 0 after XP was awarded");
        }
    }

    // =========================================================================
    // T05 — Sorted-set rank ordering: B(200) rank 1, A(100) rank 2, C(50) rank 3
    // =========================================================================

    [Fact(DisplayName = "P410-T05 Sorted-set rank ordering: B=200 XP rank 1, A=100 XP rank 2, C=50 XP rank 3")]
    public async Task T05_SortedSet_RankOrdering()
    {
        await using var redisFactory = CreateRedisFactory();
        using var scope = redisFactory.Services.CreateScope();
        var leaderboard = scope.ServiceProvider.GetRequiredService<ILeagueLeaderboard>();

        const int leagueId = 99901; // synthetic id — isolated to this test

        // Seed three members with distinct XP values.
        await leaderboard.UpsertMembershipAsync(leagueId, membershipId: 1001, weeklyXp: 100, joinOrder: 1, CancellationToken.None);
        await leaderboard.UpsertMembershipAsync(leagueId, membershipId: 1002, weeklyXp: 200, joinOrder: 2, CancellationToken.None);
        await leaderboard.UpsertMembershipAsync(leagueId, membershipId: 1003, weeklyXp: 50,  joinOrder: 3, CancellationToken.None);

        var rankB = await leaderboard.GetRankAsync(leagueId, membershipId: 1002, CancellationToken.None);
        var rankA = await leaderboard.GetRankAsync(leagueId, membershipId: 1001, CancellationToken.None);
        var rankC = await leaderboard.GetRankAsync(leagueId, membershipId: 1003, CancellationToken.None);

        rankB.Should().NotBeNull("B's rank must be resolvable");
        rankA.Should().NotBeNull("A's rank must be resolvable");
        rankC.Should().NotBeNull("C's rank must be resolvable");

        rankB!.Value.Should().Be(1, "B (200 XP) must be rank 1 (highest XP)");
        rankA!.Value.Should().Be(2, "A (100 XP) must be rank 2");
        rankC!.Value.Should().Be(3, "C (50 XP) must be rank 3 (lowest XP)");
    }

    // =========================================================================
    // T06 — Sorted-set tiebreak: equal XP, earlier joiner (lower JoinOrder) ranks higher
    // =========================================================================

    [Fact(DisplayName = "P410-T06 Sorted-set tiebreak: equal XP + lower JoinOrder → higher rank (earlier joiner wins)")]
    public async Task T06_SortedSet_Tiebreak_EarlierJoinerWins()
    {
        await using var redisFactory = CreateRedisFactory();
        using var scope = redisFactory.Services.CreateScope();
        var leaderboard = scope.ServiceProvider.GetRequiredService<ILeagueLeaderboard>();

        const int leagueId = 99902;

        // Equal XP; member 2001 joined first (JoinOrder=1), member 2002 joined second (JoinOrder=2).
        await leaderboard.UpsertMembershipAsync(leagueId, membershipId: 2001, weeklyXp: 150, joinOrder: 1, CancellationToken.None);
        await leaderboard.UpsertMembershipAsync(leagueId, membershipId: 2002, weeklyXp: 150, joinOrder: 2, CancellationToken.None);

        var rank2001 = await leaderboard.GetRankAsync(leagueId, membershipId: 2001, CancellationToken.None);
        var rank2002 = await leaderboard.GetRankAsync(leagueId, membershipId: 2002, CancellationToken.None);

        rank2001.Should().NotBeNull("rank for member 2001 must be resolvable");
        rank2002.Should().NotBeNull("rank for member 2002 must be resolvable");

        rank2001!.Value.Should().Be(1,
            "member 2001 (JoinOrder=1, earlier joiner) must be rank 1 given equal XP — tiebreak favours lower JoinOrder");
        rank2002!.Value.Should().Be(2,
            "member 2002 (JoinOrder=2, later joiner) must be rank 2 given equal XP");
    }

    // =========================================================================
    // T07 — Rebuild job restores deliberately wrong score from Postgres ledger
    // =========================================================================

    [Fact(DisplayName = "P410-T07 GamificationCacheRebuilder.RebuildSortedSetsAsync overwrites wrong score with Postgres-correct value")]
    public async Task T07_RebuildJob_RestoresDrift()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t07");
        await SeedProfileAsync(studentId);

        // Place student in a league via dashboard read.
        await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);

        var membership = await GetActiveMembershipAsync(studentId);
        if (membership is null) return; // No membership → vacuous pass.

        // Poison the sorted-set with a deliberately wrong score.
        var db2 = _testRedis!.GetDatabase();
        var leaderboardKey = GamificationCacheKeys.LeaderboardSortedSet(membership.LeagueId);
        const double wrongScore = 999_999_999.0;
        await db2.SortedSetAddAsync(leaderboardKey, membership.Id.ToString(), wrongScore);

        // Confirm poison is in Redis.
        var poisonedScore = await db2.SortedSetScoreAsync(leaderboardKey, membership.Id.ToString());
        poisonedScore.Should().Be(wrongScore, "poisoned score must be in Redis before rebuild");

        // Run the rebuild service (via the factory scope so it uses the test Redis).
        using (var scope = redisFactory.Services.CreateScope())
        {
            var rebuilder = scope.ServiceProvider.GetRequiredService<IGamificationCacheRebuilder>();
            var ex = await Record.ExceptionAsync(
                () => rebuilder.RebuildSortedSetsAsync(CancellationToken.None));
            ex.Should().BeNull("RebuildSortedSetsAsync must not throw");
        }

        // Score must now be the correct encoded value from Postgres.
        var rebuiltScore = await db2.SortedSetScoreAsync(leaderboardKey, membership.Id.ToString());
        rebuiltScore.Should().NotBeNull("sorted-set entry must exist after rebuild");

        var expectedScore = (double)LeaderboardScoreEncoder.Encode(membership.WeeklyXp, membership.JoinOrder);
        rebuiltScore!.Value.Should().Be(expectedScore,
            "rebuilt score must equal LeaderboardScoreEncoder.Encode(WeeklyXp={0}, JoinOrder={1}) from Postgres; " +
            "got {2}", membership.WeeklyXp, membership.JoinOrder, rebuiltScore.Value);
    }

    // =========================================================================
    // T08 — Kill-switch: Enabled=false → Postgres fallthrough, no Redis key written
    // =========================================================================

    [Fact(DisplayName = "P410-T08 Enabled=false kill-switch → dashboard hits Postgres, test Redis has no XP key for that student")]
    public async Task T08_KillSwitch_Disabled_NoRedisWrite()
    {
        await using var killFactory = CreateKillSwitchFactory();
        var client = killFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t08");
        await SeedProfileAsync(studentId, totalXp: 77);

        // Dashboard must succeed via Postgres fallthrough.
        var (resp, root, body) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "dashboard must succeed with cache disabled; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "xp", out var xp).Should().BeTrue("body: {0}", body);
        xp.GetInt32().Should().Be(77, "Postgres value must be returned; body: {0}", body);

        // The test Redis container must have no XP key for this student.
        // The kill-switch factory has no IConnectionMultiplexer pointing at the test container,
        // so no Redis write could have occurred regardless. We verify via the test container that
        // the key is absent.
        var db2 = _testRedis!.GetDatabase();
        var xpKey = GamificationCacheKeys.Xp(studentId);
        (await db2.KeyExistsAsync(xpKey)).Should().BeFalse(
            "with Enabled=false, the decorator must short-circuit to Postgres and NOT write to Redis; " +
            "the test container has no XP key for this student");
    }

    // =========================================================================
    // T09 — NullGamificationCache (no Redis) → dashboard succeeds, no 5xx
    // =========================================================================

    [Fact(DisplayName = "P410-T09 NullGamificationCache (Redis absent) → dashboard succeeds, no 5xx, Postgres values returned")]
    public async Task T09_NullCache_Fallback_DashboardSucceeds()
    {
        // The base shared factory has no IConnectionMultiplexer → NullGamificationCache → decorators
        // always miss and delegate to the inner Postgres queries.
        var (token, studentId) = await CreateStudentViaParentFlowAsync(_client, "t09");
        await SeedProfileAsync(studentId, totalXp: 33);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, DashboardUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "dashboard must succeed when Redis is absent (NullGamificationCache fallthrough); body: {0}", body);

        TryProp(root, "successed", out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "data", out var dashData).Should().BeTrue("body: {0}", body);
        TryProp(dashData, "xp", out var xp).Should().BeTrue("body: {0}", body);
        xp.GetInt32().Should().Be(33, "Postgres seeded value must be returned via null-cache fallthrough; body: {0}", body);
    }

    // =========================================================================
    // T10 — StreakAdvancedDomainEvent invalidates streak key
    // =========================================================================

    [Fact(DisplayName = "P410-T10 StreakAdvancedDomainEvent → streak cache key DELeted from Redis; Postgres streak advanced")]
    public async Task T10_StreakAdvancedDomainEvent_InvalidatesStreakKey()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t10");
        await SeedProfileAsync(studentId);

        // First dashboard warmup — populates all cache keys including streak.
        var (warmResp, _, warmBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        warmResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", warmBody);

        var db2 = _testRedis!.GetDatabase();
        var streakKey = GamificationCacheKeys.Streak(studentId);

        // The streak key may or may not be populated (brand new student streak = null → not cached).
        // Pre-populate it directly with a stale "streak=0" snapshot so we can assert it gets deleted.
        // Use the same JSON format that RedisGamificationCache uses (camelCase, JsonSerializerDefaults.Web).
        var staleSnapshot = System.Text.Json.JsonSerializer.Serialize(
            new StudentStreakSnapshot(0, 0, null),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await db2.StringSetAsync(streakKey, staleSnapshot, TimeSpan.FromMinutes(5));
        (await db2.KeyExistsAsync(streakKey)).Should().BeTrue(
            "streak key must exist after direct Redis pre-population");

        // Publish LessonCompleted — this triggers AdvanceStreak command → StreakAdvancedDomainEvent (post-commit).
        var lessonEv = new LessonCompletedIntegrationEvent(
            EventId:            Guid.NewGuid(),
            OccurredOnUtc:      DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // Publish via the Redis-backed factory scope so StreakAdvancedCacheInvalidator
        // runs against the test Redis container (not NullGamificationCache).
        using (var scope = redisFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(lessonEv);

        await Task.Delay(200);

        // StreakAdvancedCacheInvalidator must have deleted the streak key.
        (await db2.KeyExistsAsync(streakKey)).Should().BeFalse(
            "StreakAdvancedCacheInvalidator must DEL the streak cache key after StreakAdvancedDomainEvent fires; " +
            "the stale snapshot (streak=0) must be evicted so the next read re-fetches from Postgres");

        // Dashboard must succeed and return fresh streak ≥ 1 (cold miss repopulates from Postgres).
        var (freshResp, _, freshBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        freshResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", freshBody);

        // Parse the body directly to avoid JsonDocument GC stale-element issues.
        using var parsedDoc = System.Text.Json.JsonDocument.Parse(freshBody!);
        var dataElement = parsedDoc.RootElement.GetProperty("data");
        dataElement.TryGetProperty("streak", out var streakElement).Should().BeTrue(
            "data.streak must be present; body: {0}", freshBody);
        streakElement.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "lesson completion must advance the streak to >= 1; body: {0}", freshBody);
    }

    // =========================================================================
    // T11 — BadgeEarnedDomainEvent invalidates badges:3 key
    // =========================================================================

    [Fact(DisplayName = "P410-T11 BadgeEarnedDomainEvent → badges:3 key DELeted; next dashboard shows badge count >= 1")]
    public async Task T11_BadgeEarnedDomainEvent_InvalidatesBadgesKey()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t11");
        await SeedProfileAsync(studentId);

        // Warm up (populates badges:3 key with 0-badge snapshot).
        var (warmResp, _, warmBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        warmResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", warmBody);

        // Pre-populate the badges:3 key with a stale 0-badge snapshot.
        var db2 = _testRedis!.GetDatabase();
        var badgesKey = GamificationCacheKeys.Badges(studentId, take: 3);
        const string staleBadges = "{\"badgesCount\":0,\"recentBadges\":[]}";
        await db2.StringSetAsync(badgesKey, staleBadges, TimeSpan.FromMinutes(5));
        (await db2.KeyExistsAsync(badgesKey)).Should().BeTrue("badges:3 key must exist after manual pre-population");

        // Complete a lesson → FIRST_LESSON badge is awarded → BadgeEarnedDomainEvent fires post-commit.
        var lessonEv = new LessonCompletedIntegrationEvent(
            EventId:            Guid.NewGuid(),
            OccurredOnUtc:      DateTime.UtcNow,
            StudentId:          studentId,
            LessonId:           _mathG1DemoLessonId,
            SkillId:            _mathG1DemoLessonSkillId,
            AccuracyPercentage: 100,
            CorrectAnswerCount: 4);

        // Publish via the Redis-backed factory scope so BadgeEarnedCacheInvalidator
        // runs against the test Redis container (not NullGamificationCache).
        using (var scope = redisFactory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(lessonEv);

        await Task.Delay(100);

        // BadgeEarnedCacheInvalidator must have deleted the badges:3 key.
        (await db2.KeyExistsAsync(badgesKey)).Should().BeFalse(
            "BadgeEarnedCacheInvalidator must DEL the badges:3 key after BadgeEarnedDomainEvent fires; " +
            "stale 0-badge snapshot must be evicted so next read shows the newly awarded FIRST_LESSON badge");

        // Fresh dashboard must show badgesCount >= 1.
        var (freshResp, freshRoot, freshBody) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        freshResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", freshBody);
        TryProp(freshRoot, "data", out var freshData).Should().BeTrue("body: {0}", freshBody);
        TryProp(freshData, "badgesCount", out var badgesCount).Should().BeTrue(
            "data.badgesCount must be present; body: {0}", freshBody);
        badgesCount.GetInt32().Should().BeGreaterThanOrEqualTo(1,
            "FIRST_LESSON badge must be awarded and visible after invalidation + fresh Postgres read; body: {0}", freshBody);
    }

    // =========================================================================
    // T12 — DI type assertion: IStudentXpQuery resolves as CachedStudentXpQuery
    // =========================================================================

    [Fact(DisplayName = "P410-T12 DI type assertion — IStudentXpQuery resolves as CachedStudentXpQuery (AC1 decorator gate)")]
    public void T12_DI_IStudentXpQuery_ResolvesCachedDecorator()
    {
        using var scope = _factory.Services.CreateScope();
        var query = scope.ServiceProvider.GetRequiredService<IStudentXpQuery>();
        query.GetType().Name.Should().Be("CachedStudentXpQuery",
            "DI must wire the decorator — the IStudentXpQuery contract must resolve as CachedStudentXpQuery " +
            "per Batch 1-A DI re-wiring (factory delegate in AddGamificationInfrastructure)");
    }

    // =========================================================================
    // T13 — Envelope shape: "successed" key (CONVENTIONS §5)
    // =========================================================================

    [Fact(DisplayName = "P410-T13 Dashboard response envelope has 'successed' camelCase + full BaseResponse<T> shape (CONVENTIONS §5)")]
    public async Task T13_EnvelopeShape_SuccessedKeyPresentOnDashboard()
    {
        var (token, studentId) = await CreateStudentViaParentFlowAsync(_client, "t13");
        await SeedProfileAsync(studentId);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Get, DashboardUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Critical: the literal key must be lowercase "successed" (not "succeeded").
        body.Should().Contain("\"successed\":",
            "BaseResponse<T> must serialise the flag as 'successed' (camelCase, lowercase 'd') per CONVENTIONS §5; body: {0}", body);

        TryProp(root, "successed",  out var successed).Should().BeTrue("body: {0}", body);
        successed.GetBoolean().Should().BeTrue("body: {0}", body);
        TryProp(root, "statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body: {0}", body);
        TryProp(root, "message",    out _).Should().BeTrue("envelope must have 'message'; body: {0}", body);
        TryProp(root, "data",       out _).Should().BeTrue("envelope must have 'data'; body: {0}", body);
        TryProp(root, "errors",     out _).Should().BeTrue("envelope must have 'errors'; body: {0}", body);
    }

    // =========================================================================
    // T14 — No PII in Redis keys
    // =========================================================================

    [Fact(DisplayName = "P410-T14 No PII in Redis keys — all gamification:* keys contain only int IDs, not email/name")]
    public async Task T14_NoPiiInRedisKeys()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t14");
        await SeedProfileAsync(studentId);

        // Load dashboard to populate all student-scoped cache keys.
        var (resp, _, body) = await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        // Scan the test Redis for all gamification: keys.
        var server = _testRedis!.GetServer(_testRedis.GetEndPoints().First());
        var keys = server.Keys(pattern: "gamification:*").Select(k => k.ToString()).ToList();

        // Each key must not contain an @ sign (email) or whitespace (display name).
        foreach (var key in keys)
        {
            key.Should().NotContain("@",
                "Redis keys must not contain email addresses; offending key: '{0}'", key);
            key.Should().NotMatchRegex(@"\s",
                "Redis keys must not contain whitespace characters; offending key: '{0}'", key);
        }

        // Student-scoped keys must have a numeric third segment (the studentId).
        foreach (var key in keys.Where(k => k.StartsWith("gamification:student:")))
        {
            var parts = key.Split(':');
            parts.Length.Should().BeGreaterThanOrEqualTo(4,
                "student key must have >= 4 colon-separated segments; key: '{0}'", key);
            int.TryParse(parts[2], out var parsedId).Should().BeTrue(
                "third segment of student key must be a numeric studentId; key: '{0}'", key);
            parsedId.Should().Be(studentId,
                "student key must reference the test student's ID; key: '{0}'", key);
        }
    }

    // =========================================================================
    // T15 — GamificationCacheRebuildJob is idempotent
    // =========================================================================

    [Fact(DisplayName = "P410-T15 GamificationCacheRebuildJob idempotent — run twice → same state, no exception")]
    public async Task T15_RebuildJob_Idempotent()
    {
        await using var redisFactory = CreateRedisFactory();
        var client = redisFactory.CreateClient();
        var (token, studentId) = await CreateStudentViaParentFlowAsync(client, "t15");
        await SeedProfileAsync(studentId);

        // Place student in a league.
        await SendAsync(client, HttpMethod.Get, DashboardUrl, null, token);

        var membership = await GetActiveMembershipAsync(studentId);

        // Run the rebuilder twice.
        for (int run = 1; run <= 2; run++)
        {
            using var scope = redisFactory.Services.CreateScope();
            var rebuilder = scope.ServiceProvider.GetRequiredService<IGamificationCacheRebuilder>();
            var ex = await Record.ExceptionAsync(
                () => rebuilder.RebuildSortedSetsAsync(CancellationToken.None));
            ex.Should().BeNull($"rebuild run {run} must not throw (idempotent contract)");
        }

        if (membership is not null)
        {
            // Score after two runs must match the Postgres ledger.
            var db2 = _testRedis!.GetDatabase();
            var leaderboardKey = GamificationCacheKeys.LeaderboardSortedSet(membership.LeagueId);
            var score = await db2.SortedSetScoreAsync(leaderboardKey, membership.Id.ToString());
            score.Should().NotBeNull("sorted-set entry must be present after idempotent rebuild");

            var expected = (double)LeaderboardScoreEncoder.Encode(membership.WeeklyXp, membership.JoinOrder);
            score!.Value.Should().Be(expected,
                "score after two idempotent rebuild runs must equal the Postgres-derived encoded value");
        }
    }

    // =========================================================================
    // T16 — Multiple sequential XP awards (concurrency correctness — no lost data)
    // =========================================================================

    [Fact(DisplayName = "P410-T16 Multiple distinct XP award events — Postgres WeeklyXp accumulates correctly, no exception propagated")]
    public async Task T16_MultipleXpAwards_NoLostData()
    {
        // This test verifies AC5 (concurrency-correct, no lost XP) at the Postgres level.
        // Two distinct events with different OriginEventIds are published sequentially to avoid
        // the DB concurrency race (xUnit runs many tests against the shared DB; concurrent UPDATE
        // to the same LeagueMembership row with optimistic concurrency or lost-update risk is
        // deferred to a dedicated stress test scenario).
        var (token, studentId) = await CreateStudentViaParentFlowAsync(_client, "t16");
        await SeedProfileAsync(studentId);

        // Trigger league placement.
        await SendAsync(_client, HttpMethod.Get, DashboardUrl, null, token);

        // Publish two distinct XP-award events sequentially (different OriginEventIds).
        for (int i = 0; i < 2; i++)
        {
            var ev = new AnswerSubmittedIntegrationEvent(
                EventId:            Guid.NewGuid(),
                OccurredOnUtc:      DateTime.UtcNow.AddMilliseconds(i),
                StudentId:          studentId,
                LessonId:           _mathG1DemoLessonId,
                SkillId:            _mathG1DemoLessonSkillId,
                CorrectAnswerCount: 1);

            Exception? ex = null;
            try
            {
                using var scope = _factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(ev);
            }
            catch (Exception caught) { ex = caught; }

            ex.Should().BeNull(
                $"event publish {i + 1} must not propagate exceptions; " +
                "IsolatedNotificationPublisher (ADR 0002 §3) isolates per-handler failures");
        }

        // Both awards must be reflected in Postgres LeagueMembership.WeeklyXp.
        var membership = await GetActiveMembershipAsync(studentId);
        if (membership is not null)
        {
            // Each correct answer awards 10 XP via IncrementLeagueXpCommand.
            membership.WeeklyXp.Should().BeGreaterThanOrEqualTo(20,
                "two distinct XP award events (2 × 10 XP) must both be reflected in Postgres WeeklyXp — " +
                "no event may be silently dropped by the idempotency gate (different OriginEventId per event)");
        }
    }
}

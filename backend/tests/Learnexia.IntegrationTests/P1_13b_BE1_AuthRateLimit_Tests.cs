using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AspNetCoreRateLimit;
using FluentAssertions;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Learnexia.IntegrationTests;

// ---------------------------------------------------------------------------
// Dedicated WebApplicationFactory for P1-13b rate-limit tests.
//
// The key difference from LearnexiaWebAppFactory is that it does NOT disable
// rate limiting with int.MaxValue. Instead it overrides the IpRateLimitOptions
// with a deliberately tiny limit (Limit=2 per 1m) on the sign-in endpoint so
// that 3 rapid requests deterministically trigger a 429 — no hammering needed.
//
// The factory uses a separate Postgres database ("LearnexiaRateLimit") to avoid
// shared-state interference with the main "IntegrationTests" collection.
// ---------------------------------------------------------------------------
public sealed class RateLimitWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("LearnexiaRateLimit")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Npgsql legacy timestamp switch must be set before any host code runs.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var connectionString = _postgres.GetConnectionString();

            // Wire all module DbContexts to the Testcontainers instance.
            ReplaceDbContext<IdentityModuleDbContext>(services, connectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, connectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, connectionString, "learning");

            // ----------------------------------------------------------------
            // CRITICAL: Override IpRateLimitOptions with a tiny limit so
            // rate-limit tests are deterministic without hammering 100 req/s.
            //
            // We set Limit=2 per 1m on the sign-in endpoint. This means the
            // 3rd POST to /api/Users/Authentication/Sign-In from the same IP
            // returns 429, while the 1st and 2nd succeed (or fail with 400/401
            // depending on credentials, but NOT 429).
            //
            // This PostConfigure runs AFTER ConfigureRateLimitingOptions in
            // ServiceExtensions.cs so it wins and overrides the production rules
            // for this test host only.
            // ----------------------------------------------------------------
            services.PostConfigure<IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = true;
                // Replace ALL general rules with a single tiny sign-in rule so
                // the limit is reached predictably without any production-rule
                // side-effects from parallel test runners.
                opt.GeneralRules = new List<RateLimitRule>
                {
                    new() { Endpoint = "post:/api/users/authentication/sign-in", Limit = 2, Period = "1m" },
                    // Keep a high-water-mark global rule so all OTHER endpoints
                    // in this factory remain unthrottled during normal tests.
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" },
                };
                // Health probes stay whitelisted (production parity).
                opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
            });
        });

        // Override ConnectionStrings:Default so Hangfire (which reads IConfiguration lazily at DI
        // resolution time) uses the Testcontainers container rather than localhost:5432.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString()
            });
        });
    }

    /// <summary>Apply migrations and seed all module schemas (idempotent).</summary>
    public async Task ApplyMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<LearningDbContext>().Database.MigrateAsync();

        await IdentityModule.SeedAsync(sp);

        var userManager = sp.GetRequiredService<UserManager<User>>();
        var roleManager = sp.GetRequiredService<RoleManager<Role>>();
        await UserSeeder.SeedBasicUserAsync(userManager, roleManager);
        await UserSeeder.SeedSuperAdminAsync(userManager, roleManager);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
    }

    private static void ReplaceDbContext<TContext>(
        IServiceCollection services,
        string connectionString,
        string schema) where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory", schema)
                    .MigrationsAssembly(typeof(TContext).Assembly.FullName)));
    }
}

// ---------------------------------------------------------------------------
// xUnit collection definition — isolates this factory from the main suite
// ---------------------------------------------------------------------------
[CollectionDefinition("RateLimitTests")]
public sealed class RateLimitTestsCollection : ICollectionFixture<RateLimitWebAppFactory> { }

// ---------------------------------------------------------------------------
// P1-13b BE-1 integration tests: per-endpoint IP rate limiting on auth routes
//
// Feature: ConfigureRateLimitingOptions (ServiceExtensions.cs) now adds
//   EnableEndpointRateLimiting=true and per-endpoint rules (Limit=100, Period="1s")
//   on 5 anonymous auth endpoints, on top of the global * 200/min rule.
//   Store = MemoryCacheRateLimitCounterStore. Exceeding a rule → HTTP 429.
//
// Test strategy — deterministic (no hammering):
//   RateLimitWebAppFactory overrides IpRateLimitOptions via PostConfigure<> with
//   Limit=2/1m on sign-in. The 3rd call to that endpoint returns 429; calls 1 and 2
//   return their normal status (400/401 for bad credentials). This avoids any need
//   to fire 100 real req/s in the test suite.
//
// Acceptance criteria covered:
//   AC-RL-1  429 on exceed: 3rd POST to Sign-In (invalid creds) returns 429.
//   AC-RL-2  Under the limit passes: 1st and 2nd POSTs return a non-429 code
//            (rate-limiting does not break normal flow).
//   AC-RL-3  Whitelist intact: GET /health is not throttled (whitelisted endpoint),
//            10 rapid calls all return 200, none 429.
//   AC-RL-4  Regression: normal test traffic on OTHER endpoints does not trip the
//            tiny sign-in rule (other endpoints use the int.MaxValue global rule).
//   AC-RL-5  Rate-limit middleware is registered: 429 response body is non-empty
//            (AspNetCoreRateLimit emits a plain-text or JSON body on throttle).
// ---------------------------------------------------------------------------
[Collection("RateLimitTests")]
public sealed class P1_13b_BE1_AuthRateLimit_Tests : IAsyncLifetime
{
    private const string SignInUrl = "/api/Users/Authentication/Sign-In";
    private const string RegisterParentUrl = "/api/Users/Authentication/Register-Parent";
    private const string ForgotPasswordUrl = "/api/Users/Authentication/Forgot-Password";
    private const string ResetPasswordUrl = "/api/Users/Authentication/Reset-Password";
    private const string GoogleSignInUrl = "/api/Users/Authentication/Google-SignIn";
    private const string HealthUrl = "/health";
    private const string HealthLiveUrl = "/health/live";

    private readonly RateLimitWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_13b_BE1_AuthRateLimit_Tests(RateLimitWebAppFactory factory)
    {
        _factory = factory;
        // Each test creates its own client from the same factory instance.
        // The rate-limit store is in-memory and shared across the factory,
        // so clients sharing the same X-Real-IP / RemoteIpAddress will share
        // the counter. The factory client uses 127.0.0.1 as the client IP.
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
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

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PostSignInAsync(HttpClient client, string userName = "notexist", string password = "WrongPass1@")
    {
        var body = new { UserName = userName, Password = password };
        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(SignInUrl, content);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body is fine — 429 may return plain text */ }
        }
        return (response, root, bodyStr);
    }

    // =========================================================================
    // AC-RL-1  429 on exceed
    //
    // The factory overrides the sign-in rule to Limit=2/1m.
    // Sending 3 POSTs from the same (in-process) client IP must trigger 429
    // on the 3rd call. Invalid credentials are used so the real handler either
    // returns 400 or 401 — either is acceptable for calls 1 and 2. The test
    // only asserts that call 3 is 429.
    //
    // IMPORTANT: xUnit may share a factory instance across tests in the same
    // [Collection]. Because the rate-limit counter is in-memory and the counter
    // key includes the client IP, tests in the same collection that call Sign-In
    // accumulate against the same counter. Therefore this test creates a FRESH
    // factory client that sends its own 3 requests and checks the 3rd is 429.
    //
    // To guarantee isolation from other tests calling Sign-In in this collection,
    // we use a distinct X-Forwarded-For header so the rate-limit store sees a
    // different IP for this test's burst.
    // =========================================================================

    [Fact(DisplayName = "AC-RL-1: 3rd POST to Sign-In returns 429 Too Many Requests (limit=2/1m override)")]
    public async Task AC_RL_1_ThirdSignInRequest_Returns429()
    {
        // Use a unique spoofed IP per test run so counter isolation is guaranteed
        // even when the test collection reuses the factory instance.
        var testIp = $"10.0.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        // Calls 1 and 2 — should NOT be 429 (rate limit not yet reached).
        var (r1, _, b1) = await PostSignInAsync(client);
        ((int)r1.StatusCode).Should().NotBe(429,
            "call 1 is below the limit (Limit=2/1m on sign-in); must not return 429. " +
            "Status: {0}; body: {1}", (int)r1.StatusCode, b1);

        var (r2, _, b2) = await PostSignInAsync(client);
        ((int)r2.StatusCode).Should().NotBe(429,
            "call 2 is at the limit (Limit=2/1m on sign-in); must not return 429. " +
            "Status: {0}; body: {1}", (int)r2.StatusCode, b2);

        // Call 3 — must be 429.
        var (r3, _, b3) = await PostSignInAsync(client);
        r3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "call 3 exceeds the Limit=2/1m override on post:/api/users/authentication/sign-in; " +
            "IpRateLimitMiddleware must return HTTP 429. " +
            "Got {0}; body: {1}", (int)r3.StatusCode, b3);
    }

    // =========================================================================
    // AC-RL-2  Under the limit passes
    //
    // Confirms that the 1st and 2nd Sign-In calls receive their normal (non-429)
    // responses regardless of the rate-limit middleware being active. Uses a
    // distinct IP to avoid counter pollution from AC-RL-1.
    // =========================================================================

    [Fact(DisplayName = "AC-RL-2: 1st and 2nd Sign-In requests are NOT 429 (under limit; normal flow preserved)")]
    public async Task AC_RL_2_FirstTwoSignInRequests_AreNot429()
    {
        var testIp = $"10.1.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        var (r1, root1, body1) = await PostSignInAsync(client);
        ((int)r1.StatusCode).Should().NotBe(429,
            "the 1st call must not be rate-limited. Actual status: {0}; body: {1}",
            (int)r1.StatusCode, body1);

        // The normal response for bad credentials is 400 or 401 — either proves
        // the middleware passed the request through to the handler.
        ((int)r1.StatusCode).Should().BeOneOf(
            new[] { 400, 401 },
            "the 1st call with invalid credentials must return 400 or 401 (bad creds rejected by handler); " +
            "got {0}; body: {1}", (int)r1.StatusCode, body1);

        var (r2, root2, body2) = await PostSignInAsync(client);
        ((int)r2.StatusCode).Should().NotBe(429,
            "the 2nd call must not be rate-limited. Actual status: {0}; body: {1}",
            (int)r2.StatusCode, body2);

        ((int)r2.StatusCode).Should().BeOneOf(
            new[] { 400, 401 },
            "the 2nd call with invalid credentials must return 400 or 401; " +
            "got {0}; body: {1}", (int)r2.StatusCode, body2);
    }

    // =========================================================================
    // AC-RL-3  Whitelist intact
    //
    // GET /health is in the EndpointWhitelist ("get:/health") and must never be
    // throttled. We confirm 10 rapid calls all return 200 (no 429).
    // =========================================================================

    [Fact(DisplayName = "AC-RL-3: GET /health is whitelisted — 10 rapid calls return 200, none 429")]
    public async Task AC_RL_3_HealthEndpoint_IsWhitelisted_No429()
    {
        const int iterations = 10;
        var statusCodes = new List<HttpStatusCode>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var r = await _client.GetAsync(HealthUrl);
            statusCodes.Add(r.StatusCode);
        }

        statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests,
            "/health is in the EndpointWhitelist and must never be throttled by the rate-limit middleware. " +
            "Observed status codes: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));

        statusCodes.Should().AllBeEquivalentTo(HttpStatusCode.OK,
            "all 10 rapid GET /health calls must return 200. Observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));
    }

    // =========================================================================
    // AC-RL-3b  /health/live is also whitelisted
    // =========================================================================

    [Fact(DisplayName = "AC-RL-3b: GET /health/live is whitelisted — 10 rapid calls return 200, none 429")]
    public async Task AC_RL_3b_HealthLiveEndpoint_IsWhitelisted_No429()
    {
        const int iterations = 10;
        var statusCodes = new List<HttpStatusCode>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var r = await _client.GetAsync(HealthLiveUrl);
            statusCodes.Add(r.StatusCode);
        }

        statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests,
            "/health/live is in the EndpointWhitelist and must never return 429. " +
            "Observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));

        statusCodes.Should().AllBeEquivalentTo(HttpStatusCode.OK,
            "all 10 rapid GET /health/live calls must return 200. Observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));
    }

    // =========================================================================
    // AC-RL-4  Regression — other auth endpoints not blocked by sign-in rule
    //
    // The global "*" rule in the factory is int.MaxValue. Sending a few requests
    // to Register-Parent must NOT trigger 429 — only the sign-in endpoint is
    // constrained to Limit=2/1m. This proves the endpoint-specific rule does not
    // leak to other routes.
    // =========================================================================

    [Fact(DisplayName = "AC-RL-4: Register-Parent is NOT throttled by the sign-in limit (global rule = int.MaxValue)")]
    public async Task AC_RL_4_RegisterParent_NotThrottledBySigInLimit()
    {
        var testIp = $"10.2.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        // Send 5 requests to Register-Parent (which would exceed any Limit=2 rule if it applied).
        // We use invalid-email bodies so the server returns 422 (validation error), not 200.
        // Any 422 confirms the handler was reached (not rate-limited).
        var statusCodes = new List<int>();

        for (var i = 0; i < 5; i++)
        {
            var body = new { Email = "not-an-email", Password = "weak", AcceptedTerms = true };
            var content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(RegisterParentUrl, content);
            statusCodes.Add((int)r.StatusCode);
        }

        statusCodes.Should().NotContain(429,
            "Register-Parent is only constrained by the global int.MaxValue rule; " +
            "the endpoint-specific Limit=2/1m on sign-in must not bleed over. " +
            "Observed status codes: [{0}]",
            string.Join(", ", statusCodes));

        // All 5 must be 422 (validation fires; handler reached).
        statusCodes.Should().AllBeEquivalentTo(422,
            "Register-Parent with invalid email must return 422 from ValidationBehavior. " +
            "Observed: [{0}]",
            string.Join(", ", statusCodes));
    }

    // =========================================================================
    // AC-RL-5  Rate-limit middleware is active
    //
    // When the 3rd call returns 429, the response body must be non-empty.
    // AspNetCoreRateLimit writes a plain-text message such as
    //   "API calls quota exceeded! maximum admitted 2 per 1m."
    // This confirms the middleware is registered and responding, not just returning
    // an empty 429 from the framework.
    // =========================================================================

    [Fact(DisplayName = "AC-RL-5: 429 response body is non-empty (middleware is active and responding)")]
    public async Task AC_RL_5_RateLimitedResponse_HasNonEmptyBody()
    {
        var testIp = $"10.3.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        // Exhaust the limit (2 calls).
        await PostSignInAsync(client);
        await PostSignInAsync(client);

        // 3rd call — must be 429.
        var (r, _, body) = await PostSignInAsync(client);

        r.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "3rd sign-in call must return 429; got {0}; body: {1}", (int)r.StatusCode, body);

        body.Should().NotBeNullOrWhiteSpace(
            "the 429 response body must not be empty; AspNetCoreRateLimit always writes a body " +
            "when it throttles a request (default quota-exceeded message or a configured QuotaExceededResponse). " +
            "Empty body suggests the middleware is not active or is not wired correctly in Program.cs.");
    }

    // =========================================================================
    // AC-RL-6  Normal (main) integration suite is unaffected
    //
    // The main "IntegrationTests" collection uses LearnexiaWebAppFactory which
    // sets GeneralRules=[{Endpoint="*", Limit=int.MaxValue}]. This factory
    // (RateLimitWebAppFactory) is in a separate "RateLimitTests" collection with
    // its own PostgreSQL container, so neither collection's rate-limit counters
    // bleed into the other. This test is a lightweight sanity check: a valid
    // sign-in within this factory (which has Limit=2/1m on sign-in) still works
    // when we haven't yet exceeded the limit.
    // =========================================================================

    [Fact(DisplayName = "AC-RL-6: Valid seeded-user Sign-In still returns 200 when under the rate limit")]
    public async Task AC_RL_6_ValidSignIn_UnderLimit_Returns200()
    {
        var testIp = $"10.4.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        // First call — valid credentials. Must reach the handler and succeed.
        var (response, root, body) = await PostSignInAsync(client, "superadmin", "123Pa$$word!");

        ((int)response.StatusCode).Should().NotBe(429,
            "a valid sign-in below the rate limit must not return 429; " +
            "got {0}; body: {1}", (int)response.StatusCode, body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "valid seeded-superadmin sign-in must return 200; got {0}; body: {1}",
            (int)response.StatusCode, body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true for a valid sign-in under the rate limit; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "AccessToken must be non-empty on a successful sign-in; body: {0}", body);
    }

    // =========================================================================
    // BE-TC-02  Register-Parent: exceeding the limit returns 429
    //
    // A derived factory (WithWebHostBuilder) adds a second PostConfigure that
    // replaces the rules with register-parent=2/1m + global=MaxValue.
    // Three invalid-body calls (returns 422 from ValidationBehavior) exhaust the
    // counter; the 3rd must return 429 instead of 422.
    // Traces to: AC-1.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-02: 3rd POST to Register-Parent returns 429 (limit=2/1m override)")]
    public async Task BE_TC_02_ThirdRegisterParentRequest_Returns429()
    {
        // Derived factory that overrides the PostConfigure rules so only the
        // register-parent endpoint is constrained (global stays MaxValue).
        using var derivedFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
                svcs.PostConfigure<IpRateLimitOptions>(opt =>
                {
                    opt.EnableEndpointRateLimiting = true;
                    opt.GeneralRules = new List<RateLimitRule>
                    {
                        new() { Endpoint = "post:/api/users/authentication/register-parent", Limit = 2, Period = "1m" },
                        new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" },
                    };
                    opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
                })));

        var testIp = $"10.10.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = derivedFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        async Task<(HttpResponseMessage r, string b)> PostRegister()
        {
            var payload = new { Email = "not-an-email", Password = "weak", AcceptedTerms = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(RegisterParentUrl, content);
            var b = await r.Content.ReadAsStringAsync();
            return (r, b);
        }

        var (r1, b1) = await PostRegister();
        ((int)r1.StatusCode).Should().NotBe(429,
            "call 1 is below the register-parent limit=2/1m; must not return 429. Status: {0}; body: {1}",
            (int)r1.StatusCode, b1);

        var (r2, b2) = await PostRegister();
        ((int)r2.StatusCode).Should().NotBe(429,
            "call 2 is at the register-parent limit=2/1m; must not return 429. Status: {0}; body: {1}",
            (int)r2.StatusCode, b2);

        var (r3, b3) = await PostRegister();
        r3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "call 3 exceeds the Limit=2/1m override on post:/api/users/authentication/register-parent; " +
            "IpRateLimitMiddleware must return HTTP 429. Got {0}; body: {1}", (int)r3.StatusCode, b3);
    }

    // =========================================================================
    // BE-TC-03  Forgot-Password: exceeding the limit returns 429
    //
    // The forgot-password endpoint is the highest-value abuse target for
    // enumeration / reset-spam. It should return a generic 200 regardless
    // of account existence (anti-enumeration); rate limiting fires before
    // the email path. A derived factory constrains forgot-password=2/1m.
    // Traces to: AC-1.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-03: 3rd POST to Forgot-Password returns 429 (limit=2/1m override)")]
    public async Task BE_TC_03_ThirdForgotPasswordRequest_Returns429()
    {
        using var derivedFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
                svcs.PostConfigure<IpRateLimitOptions>(opt =>
                {
                    opt.EnableEndpointRateLimiting = true;
                    opt.GeneralRules = new List<RateLimitRule>
                    {
                        new() { Endpoint = "post:/api/users/authentication/forgot-password", Limit = 2, Period = "1m" },
                        new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" },
                    };
                    opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
                })));

        var testIp = $"10.11.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = derivedFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        async Task<(HttpResponseMessage r, string b)> PostForgot()
        {
            var payload = new { Email = "someone@example.com" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(ForgotPasswordUrl, content);
            var b = await r.Content.ReadAsStringAsync();
            return (r, b);
        }

        var (r1, b1) = await PostForgot();
        ((int)r1.StatusCode).Should().NotBe(429,
            "call 1 must reach the handler (not 429). Status: {0}; body: {1}", (int)r1.StatusCode, b1);

        var (r2, b2) = await PostForgot();
        ((int)r2.StatusCode).Should().NotBe(429,
            "call 2 must reach the handler (not 429). Status: {0}; body: {1}", (int)r2.StatusCode, b2);

        var (r3, b3) = await PostForgot();
        r3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "call 3 exceeds forgot-password Limit=2/1m; IpRateLimitMiddleware must return 429. " +
            "Got {0}; body: {1}", (int)r3.StatusCode, b3);
    }

    // =========================================================================
    // BE-TC-04  Reset-Password: exceeding the limit returns 429
    //
    // Blocks brute-forcing reset tokens beyond the per-IP ceiling.
    // Junk body (bad email + bad token) returns a generic failure; the 3rd call
    // from the same IP must be 429 before the handler is reached.
    // Traces to: AC-1.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-04: 3rd POST to Reset-Password returns 429 (limit=2/1m override)")]
    public async Task BE_TC_04_ThirdResetPasswordRequest_Returns429()
    {
        using var derivedFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
                svcs.PostConfigure<IpRateLimitOptions>(opt =>
                {
                    opt.EnableEndpointRateLimiting = true;
                    opt.GeneralRules = new List<RateLimitRule>
                    {
                        new() { Endpoint = "post:/api/users/authentication/reset-password", Limit = 2, Period = "1m" },
                        new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" },
                    };
                    opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
                })));

        var testIp = $"10.12.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = derivedFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        async Task<(HttpResponseMessage r, string b)> PostReset()
        {
            var payload = new { Email = "nobody@example.com", Token = "junk-token-xyz", NewPassword = "P@ss123!" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(ResetPasswordUrl, content);
            var b = await r.Content.ReadAsStringAsync();
            return (r, b);
        }

        var (r1, b1) = await PostReset();
        ((int)r1.StatusCode).Should().NotBe(429,
            "call 1 must not be rate-limited. Status: {0}; body: {1}", (int)r1.StatusCode, b1);

        var (r2, b2) = await PostReset();
        ((int)r2.StatusCode).Should().NotBe(429,
            "call 2 must not be rate-limited. Status: {0}; body: {1}", (int)r2.StatusCode, b2);

        var (r3, b3) = await PostReset();
        r3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "call 3 exceeds reset-password Limit=2/1m; IpRateLimitMiddleware must return 429. " +
            "Got {0}; body: {1}", (int)r3.StatusCode, b3);
    }

    // =========================================================================
    // BE-TC-05  Google-SignIn: exceeding the limit returns 429
    //
    // Google ID token verification will fail with a junk token (400/424/401 from
    // the handler) — acceptable; we assert that the 3rd call is 429 before the
    // handler logic runs.
    // Traces to: AC-1 / AC-1d (no collateral breakage — handler is still reached
    //            on calls 1 & 2).
    // =========================================================================

    [Fact(DisplayName = "BE-TC-05: 3rd POST to Google-SignIn returns 429 (limit=2/1m override)")]
    public async Task BE_TC_05_ThirdGoogleSignInRequest_Returns429()
    {
        using var derivedFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
                svcs.PostConfigure<IpRateLimitOptions>(opt =>
                {
                    opt.EnableEndpointRateLimiting = true;
                    opt.GeneralRules = new List<RateLimitRule>
                    {
                        new() { Endpoint = "post:/api/users/authentication/google-signin", Limit = 2, Period = "1m" },
                        new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" },
                    };
                    opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
                })));

        var testIp = $"10.13.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = derivedFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        async Task<(HttpResponseMessage r, string b)> PostGoogleSignIn()
        {
            var payload = new { IdToken = "invalid-google-id-token" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(GoogleSignInUrl, content);
            var b = await r.Content.ReadAsStringAsync();
            return (r, b);
        }

        var (r1, b1) = await PostGoogleSignIn();
        ((int)r1.StatusCode).Should().NotBe(429,
            "call 1 must reach the handler (not 429). Status: {0}; body: {1}", (int)r1.StatusCode, b1);

        var (r2, b2) = await PostGoogleSignIn();
        ((int)r2.StatusCode).Should().NotBe(429,
            "call 2 must reach the handler (not 429). Status: {0}; body: {1}", (int)r2.StatusCode, b2);

        var (r3, b3) = await PostGoogleSignIn();
        r3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "call 3 exceeds google-signin Limit=2/1m; IpRateLimitMiddleware must return 429. " +
            "Got {0}; body: {1}", (int)r3.StatusCode, b3);
    }

    // =========================================================================
    // BE-TC-06  IP counter is independent across distinct client IPs
    //
    // Confirms the limit is keyed per client IP, not a shared global counter.
    // IP-A exhausts its counter (3rd call = 429). IP-B's first call must NOT
    // be 429 — it has its own fresh counter.
    // Traces to: AC-1 (per-IP semantics).
    // =========================================================================

    [Fact(DisplayName = "BE-TC-06: Rate limit is per-IP — exhausting IP-A does not throttle IP-B")]
    public async Task BE_TC_06_PerIpCounterIsIndependent()
    {
        // The base factory already has sign-in=2/1m, so we can drive both clients
        // against that rule using the same factory — just different spoofed IPs.
        var ipA = $"10.20.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        var ipB = $"10.21.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", ipA);

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", ipB);

        // Exhaust IP-A counter (Limit=2 → 3rd must be 429).
        var (a1, _, ab1) = await PostSignInAsync(clientA);
        ((int)a1.StatusCode).Should().NotBe(429, "IP-A call 1 should not be throttled; body: {0}", ab1);

        var (a2, _, ab2) = await PostSignInAsync(clientA);
        ((int)a2.StatusCode).Should().NotBe(429, "IP-A call 2 should not be throttled; body: {0}", ab2);

        var (a3, _, ab3) = await PostSignInAsync(clientA);
        a3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "IP-A call 3 should be throttled (limit exhausted); body: {0}", ab3);

        // IP-B's first call must NOT be 429 — it has its own fresh counter.
        var (b1, _, bb1) = await PostSignInAsync(clientB);
        ((int)b1.StatusCode).Should().NotBe(429,
            "IP-B first call must NOT be 429 — per-IP limiting means IP-A's counter exhaustion " +
            "does not affect IP-B's fresh counter. Got {0}; body: {1}", (int)b1.StatusCode, bb1);
    }

    // =========================================================================
    // BE-TC-07  One endpoint's burst does NOT throttle a different endpoint
    //
    // The base factory constrains ONLY sign-in=2/1m; global=MaxValue.
    // Exhaust sign-in (3rd = 429) then send 5× Register-Parent — all must be
    // 422 (handler reached), none 429.
    // This is an extended version of AC-RL-4: it first confirms sign-in itself
    // is exhausted before verifying register-parent is not affected.
    // Traces to: AC-1a.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-07: Exhausting sign-in does not throttle Register-Parent (per-endpoint isolation)")]
    public async Task BE_TC_07_SignInExhausted_RegisterParentUnaffected()
    {
        // Same IP for both endpoints: proves the isolation is per-endpoint, not per-IP.
        var testIp = $"10.30.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        // Step 1: Exhaust sign-in (3 calls — 3rd must be 429).
        await PostSignInAsync(client);
        await PostSignInAsync(client);
        var (r3, _, b3) = await PostSignInAsync(client);
        r3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "sign-in 3rd call must be 429 before checking register-parent isolation; body: {0}", b3);

        // Step 2: Same IP — 5 calls to Register-Parent. All must be 422 (not 429).
        var registerStatusCodes = new List<int>();
        for (var i = 0; i < 5; i++)
        {
            var payload = new { Email = "not-an-email", Password = "weak", AcceptedTerms = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(RegisterParentUrl, content);
            registerStatusCodes.Add((int)r.StatusCode);
        }

        registerStatusCodes.Should().NotContain(429,
            "sign-in's per-endpoint limit must not bleed over to register-parent " +
            "(EnableEndpointRateLimiting=true ensures separate counters per endpoint). " +
            "Observed: [{0}]", string.Join(", ", registerStatusCodes));

        registerStatusCodes.Should().AllBeEquivalentTo(422,
            "register-parent with invalid body must return 422 from ValidationBehavior; " +
            "got [{0}]", string.Join(", ", registerStatusCodes));
    }

    // =========================================================================
    // BE-TC-08  EnableEndpointRateLimiting is on — per-endpoint accounting
    //
    // Constrain both sign-in and forgot-password to Limit=2/1m each.
    // Each endpoint must independently trip 429 only on its own 3rd call.
    // If EnableEndpointRateLimiting were false, the two endpoints would share
    // the global counter and the 2nd endpoint's 3rd call would be the 5th
    // overall hit — which might still be 429 for the wrong reason (global).
    // The per-endpoint proof: the 1st and 2nd calls on endpoint B are NOT 429
    // even though endpoint A was already exhausted.
    // Traces to: AC-1a.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-08: EnableEndpointRateLimiting=true — sign-in and forgot-password each reach 429 independently")]
    public async Task BE_TC_08_EnableEndpointRateLimiting_TwoEndpointsAccountIndependently()
    {
        using var derivedFactory = _factory.WithWebHostBuilder(b =>
            b.ConfigureServices(svcs =>
                svcs.PostConfigure<IpRateLimitOptions>(opt =>
                {
                    opt.EnableEndpointRateLimiting = true;
                    opt.GeneralRules = new List<RateLimitRule>
                    {
                        new() { Endpoint = "post:/api/users/authentication/sign-in", Limit = 2, Period = "1m" },
                        new() { Endpoint = "post:/api/users/authentication/forgot-password", Limit = 2, Period = "1m" },
                        new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" },
                    };
                    opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
                })));

        var testIp = $"10.40.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = derivedFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        async Task<(HttpResponseMessage r, string b)> PostForgot()
        {
            var payload = new { Email = "someone@example.com" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var r = await client.PostAsync(ForgotPasswordUrl, content);
            return (r, await r.Content.ReadAsStringAsync());
        }

        // Exhaust endpoint A (sign-in): 3 calls → 3rd = 429.
        var (s1, _, sb1) = await PostSignInAsync(client);
        ((int)s1.StatusCode).Should().NotBe(429, "sign-in call 1 must not be throttled; body: {0}", sb1);

        var (s2, _, sb2) = await PostSignInAsync(client);
        ((int)s2.StatusCode).Should().NotBe(429, "sign-in call 2 must not be throttled; body: {0}", sb2);

        var (s3, _, sb3) = await PostSignInAsync(client);
        s3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "sign-in 3rd call must be 429 (endpoint A exhausted); body: {0}", sb3);

        // Endpoint B (forgot-password): calls 1 & 2 must NOT be 429 even though
        // endpoint A is already exhausted — confirming per-endpoint separate counters.
        var (f1, fb1) = await PostForgot();
        ((int)f1.StatusCode).Should().NotBe(429,
            "forgot-password call 1 must NOT be 429 even after sign-in is exhausted " +
            "(separate per-endpoint counter); got {0}; body: {1}", (int)f1.StatusCode, fb1);

        var (f2, fb2) = await PostForgot();
        ((int)f2.StatusCode).Should().NotBe(429,
            "forgot-password call 2 must NOT be 429; got {0}; body: {1}", (int)f2.StatusCode, fb2);

        var (f3, fb3) = await PostForgot();
        f3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "forgot-password 3rd call must be 429 (endpoint B now exhausted independently); " +
            "got {0}; body: {1}", (int)f3.StatusCode, fb3);
    }

    // =========================================================================
    // BE-TC-10  429 response standard rate-limit metadata (Retry-After / headers)
    //
    // This is a P2 / informational test. The current AspNetCoreRateLimit
    // configuration does not customise QuotaExceededResponse, so Retry-After
    // may or may not be present. We document the actual behaviour and assert it
    // is > 0 if present; we do NOT fail the build if the header is absent.
    // See README open question #4 (lead decision required).
    // Traces to: AC-1b.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-10 (P2 informational): 429 response Retry-After / rate-limit headers — document actual behaviour")]
    public async Task BE_TC_10_RateLimitedResponse_RetryAfterHeader_DocumentActualBehaviour()
    {
        var testIp = $"10.50.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Real-IP", testIp);

        // Exhaust the sign-in limit (base factory = 2/1m on sign-in).
        await PostSignInAsync(client);
        await PostSignInAsync(client);

        // 3rd call — must be 429.
        var (response, _, body) = await PostSignInAsync(client);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "3rd sign-in call must return 429; body: {0}", body);

        // Inspect Retry-After header — informational check, does not fail the build.
        var hasRetryAfter = response.Headers.TryGetValues("Retry-After", out var retryAfterValues);
        if (hasRetryAfter)
        {
            var retryAfterStr = retryAfterValues!.FirstOrDefault();
            if (int.TryParse(retryAfterStr, out var retryAfterSeconds))
            {
                retryAfterSeconds.Should().BeGreaterThan(0,
                    "if Retry-After is present it must be a positive number of seconds; got {0}", retryAfterStr);
            }
        }
        // Note: if Retry-After is absent this is a finding, not a build failure.
        // See execution report BE-TC-10 observation for the lead (open question #4).

        // Also capture any X-Rate-Limit-* headers for the record.
        var rateLimitHeaders = response.Headers
            .Where(h => h.Key.StartsWith("X-Rate-Limit", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}")
            .ToList();

        // The body must be non-empty regardless (middleware active — asserted in BE-TC-09 / AC-RL-5).
        body.Should().NotBeNullOrWhiteSpace(
            "429 body must not be empty; AspNetCoreRateLimit must emit a quota-exceeded message; body: {0}", body);

        // Record observed state in the test output (xUnit ITestOutputHelper not injected here;
        // FluentAssertions assertion message carries the info).
        var headerInfo = hasRetryAfter
            ? $"Retry-After present: {retryAfterValues!.FirstOrDefault()}"
            : "Retry-After NOT present (open question #4 — lead to decide if required)";
        headerInfo.Should().NotBeNull("informational: {0}", headerInfo);
    }

    // =========================================================================
    // BE-TC-12  Production tightened limits — BLOCKED
    //
    // The standard Testing-env factory resolves the loose (100/1s) branch, and
    // the deterministic factory overrides the rules entirely via PostConfigure.
    // Exercising the Production branch of ConfigureRateLimitingOptions requires
    // UseEnvironment("Production") plus all required prod env vars so the host
    // boots (JwtSettings__Secret etc.). Marked BLOCKED per test-case spec.
    // See README open question #3.
    // Traces to: AC-1e.
    // =========================================================================

    [Fact(DisplayName = "BE-TC-12: BLOCKED — Production tightened limits require a dedicated Prod-env test host; see README open question #3")]
    public void BE_TC_12_ProductionTightenedLimits_BLOCKED()
    {
        // BLOCKED: this case is intentionally not implemented.
        // Reason: the RateLimitWebAppFactory runs UseEnvironment("Testing") which resolves
        // the loose (100/1s) branch of ConfigureRateLimitingOptions, and its PostConfigure
        // overrides the GeneralRules entirely. To exercise the Production branch we would
        // need a separate WebApplicationFactory with UseEnvironment("Production") plus all
        // required production secrets (JwtSettings__Secret, Google:ClientId, etc.) so the
        // host starts. This is out of scope for P1-13b; the env-gating code path is covered
        // by code review (mirrors the GuardJwtSecret precedent). If the lead decides this
        // warrants a dedicated factory, it should be implemented in a follow-up task.
        Assert.True(true, "BLOCKED — not implemented; see BE-TC-12 in execution-report.md");
    }

    // =========================================================================
    // BE-TC-13  Multi-instance (Redis-backed) shared counter — NOT TESTABLE
    //
    // The store is intentionally in-memory (MemoryCacheRateLimitCounterStore)
    // for Phase 1 / single-instance. Redis promotion is deferred to P6-06-BE-4.
    // This placeholder exists so the case is not silently dropped.
    // Traces to: AC-1f (deferred).
    // =========================================================================

    [Fact(DisplayName = "BE-TC-13: NOT TESTABLE in P1-13b — Redis multi-instance store deferred to P6-06-BE-4")]
    public void BE_TC_13_RedisMultiInstance_NotTestable()
    {
        // NOT TESTABLE: the counter store is MemoryCacheRateLimitCounterStore.
        // Each in-process test host has its own in-memory store; there is no Redis
        // backend in Phase 1. Cross-replica counter sharing (where two app instances
        // share a single Redis counter) is deferred to P6-06-BE-4. This test exists
        // solely to record the known limitation and its tracking ID.
        Assert.True(true, "NOT TESTABLE — deferred to P6-06-BE-4; see BE-TC-13 in execution-report.md");
    }
}

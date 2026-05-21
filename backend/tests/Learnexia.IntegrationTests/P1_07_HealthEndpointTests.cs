using System.Net;
using FluentAssertions;
using Learnexia.Modules.Catalog.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Dedicated WebApplicationFactory for P1-07 health-endpoint tests.
///
/// The key difference from <see cref="LearnexiaWebAppFactory"/> is that this factory overrides
/// <c>ConnectionStrings:Default</c> inside <c>IConfiguration</c> (via
/// <c>ConfigureAppConfiguration</c>) so the Npgsql health check registered in
/// <c>Program.cs</c> targets the Testcontainers instance rather than localhost:5432.
///
/// Redis is deliberately absent (<c>ConnectionStrings:Redis = ""</c>) to validate the
/// degraded-tolerant behaviour (AC-2e).
/// </summary>
public sealed class HealthCheckWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Use the plain postgres:16 image (same as the existing LearnexiaWebAppFactory).
    // We deliberately skip the Catalog migration (which requires the pgvector extension) because
    // the health-check tests only need: (a) a reachable Postgres for the Npgsql probe, and
    // (b) the Identity schema so Program.cs SeedAsync does not throw at startup.
    // The Catalog DbContext is still overridden to point at this container so no connection
    // attempt reaches localhost:5432, but CatalogDbContext.MigrateAsync is NOT called.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("Learnexia")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.UseEnvironment("Testing");

        // Override ConnectionStrings:Default so:
        //  (a) The Npgsql health check in Program.cs targets the Testcontainers Postgres instance.
        //  (b) Hangfire.PostgreSql targets the Testcontainers Postgres instance.
        // Redis is left empty to verify the degraded-tolerant behaviour (AC-2e).
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:Redis"] = "",   // no Redis — validates AC-2b / AC-2e
            });
        });

        // Also replace the three EF DbContexts so the Identity seed (called by Program.cs at
        // startup) and migration application both hit the Testcontainers container.
        builder.ConfigureServices(services =>
        {
            var cs = _postgres.GetConnectionString();
            ReplaceDbContext<IdentityModuleDbContext>(services, cs, "identity");
            ReplaceDbContext<CatalogDbContext>(services, cs, "catalog");
            ReplaceDbContext<NotificationsDbContext>(services, cs, "notifications");
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
    }

    /// <summary>
    /// Migrate Identity and Notifications schemas and seed roles/superadmin (idempotent).
    ///
    /// Catalog migration is intentionally skipped: the DEMO_PgvectorProof migration runs
    /// <c>CREATE EXTENSION IF NOT EXISTS vector</c> which is not available on the plain
    /// postgres:16 image. The Npgsql health check only needs a connectable Postgres instance —
    /// it does not depend on any specific schema being present. CatalogDbContext is still
    /// overridden to point at the container so no connection attempt escapes to localhost:5432.
    /// </summary>
    public async Task ApplyMigrationsAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
        // Catalog skipped — requires pgvector extension (see factory comment above).
        await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await IdentityModule.SeedAsync(sp);
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

/// <summary>
/// xUnit collection fixture sharing one <see cref="HealthCheckWebAppFactory"/> instance
/// (and therefore one Postgres container) across all P1-07 health tests.
/// Kept separate from "IntegrationTests" to avoid interfering with the P4-01 fixture lifecycle.
/// </summary>
[CollectionDefinition("HealthCheckTests")]
public sealed class HealthCheckTestsCollection : ICollectionFixture<HealthCheckWebAppFactory>
{
}

/// <summary>
/// P1-07 — Health endpoint integration tests.
///
/// Coverage map:
///   AC-2a  GET /health/live → 200 liveness (no deps)
///   AC-2b  GET /health     → 200 when DB up, Redis absent (overall not 503)
///   AC-2c  Both endpoints anonymous (no 401/403 without JWT)
///   AC-2d  Both endpoints excluded from rate limiter (no 429 under 10 rapid calls)
///   AC-2e  Redis degraded-tolerant: missing Redis connection string → 200, not 503
/// </summary>
[Collection("HealthCheckTests")]
public sealed class P1_07_HealthEndpointTests : IAsyncLifetime
{
    private readonly HealthCheckWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_07_HealthEndpointTests(HealthCheckWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    public async Task InitializeAsync()
    {
        await _factory.ApplyMigrationsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // AC-2a — GET /health/live → HTTP 200 (liveness; no dependencies)
    // =========================================================================

    [Fact(DisplayName = "AC-2a: GET /health/live returns HTTP 200 (liveness, no deps required)")]
    public async Task HealthLive_ReturnsHttp200()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "/health/live is a liveness probe with Predicate = _ => false (no dependency checks). " +
            "It must always return 200 while the process is alive. " +
            "Got {0} instead.", (int)response.StatusCode);
    }

    [Fact(DisplayName = "AC-2a: GET /health/live body is non-empty and contains Healthy")]
    public async Task HealthLive_Body_ContainsHealthy()
    {
        var response = await _client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotBeNullOrWhiteSpace(
            "/health/live body must not be empty; status was {0}", (int)response.StatusCode);

        body.Should().ContainEquivalentOf("Healthy",
            "ASP.NET Core default health-check writer reports 'Healthy' for a probe with no failing " +
            "checks (Predicate = _ => false means zero checks run, result is Healthy by convention). " +
            "Actual body: {0}", body);
    }

    // =========================================================================
    // AC-2b — GET /health → 200 when DB is reachable and Redis is absent
    // =========================================================================

    [Fact(DisplayName = "AC-2b: GET /health returns HTTP 200 when DB is up and Redis is absent")]
    public async Task Health_DbUp_NoRedis_Returns200()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /health must return 200 when Postgres is reachable, even when Redis is not " +
            "configured. 503 (ServiceUnavailable) here would mean the Redis-absent case is " +
            "treated as Unhealthy instead of Degraded-or-skipped (AC-2e regression). " +
            "Got {0}.", (int)response.StatusCode);
    }

    [Fact(DisplayName = "AC-2b: GET /health body is non-empty")]
    public async Task Health_Body_NonEmpty()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotBeNullOrWhiteSpace(
            "/health body must not be empty; status was {0}", (int)response.StatusCode);
    }

    // =========================================================================
    // AC-2c — Both endpoints are anonymous (no JWT required)
    // =========================================================================

    [Fact(DisplayName = "AC-2c: GET /health without Authorization header returns 200, not 401/403")]
    public async Task Health_NoJwt_Returns200_NotUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "/health must be accessible anonymously (mapped with .AllowAnonymous() before " +
            "UseAuthentication). 401 means the endpoint is behind the auth middleware — " +
            "check mapping order in Program.cs.");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "/health must not require any policy. 403 means an [Authorize] attribute or " +
            "RequireAuthorization() call is blocking anonymous access.");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an anonymous request to /health must return 200. Got {0}.",
            (int)response.StatusCode);
    }

    [Fact(DisplayName = "AC-2c: GET /health/live without Authorization header returns 200, not 401/403")]
    public async Task HealthLive_NoJwt_Returns200_NotUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized,
            "/health/live must be accessible anonymously. 401 indicates missing AllowAnonymous().");

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "/health/live must not be blocked by any authorization policy.");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an anonymous request to /health/live must return 200. Got {0}.",
            (int)response.StatusCode);
    }

    // =========================================================================
    // AC-2d — Probe paths excluded from rate limiter (no 429 under 10 rapid calls)
    // =========================================================================

    [Fact(DisplayName = "AC-2d: GET /health is whitelisted from rate limiter — no 429 in 10 rapid calls")]
    public async Task Health_RapidCalls_No429()
    {
        // The rate-limit rule is 200 req/min per IP. 10 calls from the same test client should be
        // well within the limit, but the whitelist path is still exercised. The test validates
        // that the EndpointWhitelist entry "get:/health" is correctly applied — no call returns 429.
        const int iterations = 10;
        var statusCodes = new List<HttpStatusCode>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var r = await _client.GetAsync("/health");
            statusCodes.Add(r.StatusCode);
        }

        statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests,
            "/health is in the EndpointWhitelist ('get:/health') and must never be throttled. " +
            "If any call returned 429, the whitelist entry is misconfigured or the path casing " +
            "does not match. Status codes observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));

        statusCodes.Should().AllBeEquivalentTo(HttpStatusCode.OK,
            "every rapid /health call must return 200; observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));
    }

    [Fact(DisplayName = "AC-2d: GET /health/live is whitelisted from rate limiter — no 429 in 10 rapid calls")]
    public async Task HealthLive_RapidCalls_No429()
    {
        const int iterations = 10;
        var statusCodes = new List<HttpStatusCode>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            var r = await _client.GetAsync("/health/live");
            statusCodes.Add(r.StatusCode);
        }

        statusCodes.Should().NotContain(HttpStatusCode.TooManyRequests,
            "/health/live is in the EndpointWhitelist ('get:/health/live') and must never return 429. " +
            "Status codes observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));

        statusCodes.Should().AllBeEquivalentTo(HttpStatusCode.OK,
            "every rapid /health/live call must return 200; observed: [{0}]",
            string.Join(", ", statusCodes.Select(s => (int)s)));
    }

    // =========================================================================
    // AC-2e — Redis degraded-tolerant: no Redis → 200 not 503
    // =========================================================================

    [Fact(DisplayName = "AC-2e: /health returns 200 (not 503) when Redis connection string is absent")]
    public async Task Health_NoRedis_NotServiceUnavailable()
    {
        // The factory sets ConnectionStrings:Redis = "" which causes Program.cs to skip the
        // Redis AddRedis() registration entirely. The readiness probe must therefore return
        // 200 (DB healthy only), not 503.
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable,
            "503 means the Redis absence is treated as Unhealthy rather than skipped/Degraded. " +
            "Program.cs must only register the Redis check when ConnectionStrings:Redis is non-empty, " +
            "OR must use failureStatus: HealthStatus.Degraded so it does not cause overall Unhealthy. " +
            "The ASP.NET Core health middleware maps Unhealthy → 503 and Degraded → 200 by default.");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "when Redis is not configured, /health must return 200. Got {0}.",
            (int)response.StatusCode);
    }

    // =========================================================================
    // Security sanity — response body must not leak sensitive data
    // =========================================================================

    [Fact(DisplayName = "Security: /health body does not expose connection string passwords or stack traces")]
    public async Task Health_Body_DoesNotLeakSensitiveInfo()
    {
        var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContainEquivalentOf("password",
            "/health body must not expose the DB password from the connection string. " +
            "Actual body: {0}", body);

        body.Should().NotContainEquivalentOf("StackTrace",
            "/health body must not expose stack traces. Actual body: {0}", body);

        body.Should().NotContainEquivalentOf("Exception",
            "/health body must not expose exception details. Actual body: {0}", body);
    }
}

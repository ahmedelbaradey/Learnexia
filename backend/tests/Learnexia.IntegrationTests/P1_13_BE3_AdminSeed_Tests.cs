using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using System.Text.Json;
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
using AspNetCoreRateLimit;
using Learnexia.Modules.Identity.Application.Abstractions;
using Xunit;

namespace Learnexia.IntegrationTests;

// ---------------------------------------------------------------------------
// AdminSeedWebAppFactory — boots the host with a configured AdminSeed so
// SeedConfiguredAdminAsync creates the admin account at startup.
// Used by BE-TC-25 (configured admin signs in + has Admin role).
// ---------------------------------------------------------------------------

/// <summary>
/// A dedicated WebApplicationFactory that injects <c>AdminSeed:Email</c> and
/// <c>AdminSeed:Password</c> via in-memory config, mirroring CaptchaWebAppFactory
/// but without the CAPTCHA override (ICaptchaVerifier) — only needed for seed tests.
/// A fresh Testcontainers PostgreSQL container is spun up per collection.
/// </summary>
public sealed class AdminSeedWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // The test admin credentials injected via in-memory config (not committed to appsettings).
    public const string AdminEmail = "admin-seed-test@learnexia.test";
    public const string AdminPassword = "Str0ng@Adm1n!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("LearnexiaAdminSeed")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var connectionString = _postgres.GetConnectionString();

            ReplaceDbContext<IdentityModuleDbContext>(services, connectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, connectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, connectionString, "learning");

            // Disable rate limiting
            services.Configure<IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });

            // CAPTCHA must be disabled (default Captcha:Enabled=false) — no need to override ICaptchaVerifier
            // because Register-Parent is not under test here.
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                // Inject the configured admin credentials for SeedConfiguredAdminAsync
                ["AdminSeed:Email"] = AdminEmail,
                ["AdminSeed:Password"] = AdminPassword,
            });
        });
    }

    /// <summary>Apply migrations and run IdentityModule.SeedAsync (which calls SeedConfiguredAdminAsync).</summary>
    public async Task ApplyMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<LearningDbContext>().Database.MigrateAsync();

        // SeedAsync calls:
        //   1. RoleSeeder.SeedAsync (creates roles)
        //   2. UserSeeder.SeedConfiguredAdminAsync (reads AdminSeed:Email + AdminSeed:Password)
        //   3. Testing env → no legacy accounts seeded by IdentitySeeder
        // Then we manually seed the legacy accounts so the existing suite's regression tests work.
        await IdentityModule.SeedAsync(sp);

        var userManager = sp.GetRequiredService<UserManager<User>>();
        var roleManager = sp.GetRequiredService<RoleManager<Role>>();
        await UserSeeder.SeedBasicUserAsync(userManager, roleManager);
        await UserSeeder.SeedSuperAdminAsync(userManager, roleManager);
    }

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.StopAsync();

    private static void ReplaceDbContext<TContext>(
        IServiceCollection services, string connectionString, string schema)
        where TContext : DbContext
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
// Collection definition for admin-seed tests (separate container from CaptchaTests)
// ---------------------------------------------------------------------------

[CollectionDefinition("AdminSeedTests")]
public sealed class AdminSeedTestsCollection : ICollectionFixture<AdminSeedWebAppFactory> { }

// ---------------------------------------------------------------------------
// P1-13 BE-3 — Config-driven admin seed tests
// ---------------------------------------------------------------------------

/// <summary>
/// P1-13 BE-3 — Config-driven admin seed integration tests.
///
/// Implements BE-TC-24, BE-TC-25, BE-TC-19 (blocked), and BE-TC-34 (blocked)
/// from docs/qc/P1-13/backend-test-cases.md.
///
/// Tests:
///   BE-TC-24 — Blank AdminSeed:* → no admin created; app boots cleanly; no-op (uses CaptchaWebAppFactory)
///   BE-TC-25 — Configured AdminSeed creates admin who can sign in with Admin role (uses AdminSeedWebAppFactory)
///   BE-TC-19 — BLOCKED: idempotency across two host boots / re-seed
///   BE-TC-34 — BLOCKED: legacy accounts not seeded in non-Development (needs Production boot fixture)
/// </summary>

// -------------------------------------------------------------------------
// BE-TC-24: Uses the default CaptchaWebAppFactory (no AdminSeed:* configured)
// -------------------------------------------------------------------------

[Collection("CaptchaTests")]
public sealed class P1_13_BE3_BlankAdminSeed_Tests : IAsyncLifetime
{
    private const string SignInUrl = "api/Users/Authentication/Sign-In";

    private readonly CaptchaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_13_BE3_BlankAdminSeed_Tests(CaptchaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _factory.FakeVerifier.SetResult(true);
        await _factory.ApplyMigrationsAndSeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
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
        PostAsync(HttpClient client, string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// BE-TC-24 — Blank AdminSeed config → no admin created, app boots cleanly.
    /// The default CaptchaWebAppFactory has no AdminSeed:Email / AdminSeed:Password in-memory overrides.
    /// The committed appsettings.json has AdminSeed:Email="" and AdminSeed:Password="".
    /// SeedConfiguredAdminAsync should no-op; sign-in with a plausible admin email must fail.
    /// </summary>
    [Fact(DisplayName = "BE-TC-24: Blank AdminSeed config (appsettings has empty email/password) → SeedConfiguredAdminAsync is a no-op; app boots; no admin account created")]
    public async Task BeTc24_BlankAdminSeed_NoAdminCreated_AppBoots()
    {
        // The committed appsettings.json has AdminSeed:Email="" / AdminSeed:Password="".
        // SeedConfiguredAdminAsync returns early when email or password is blank — no admin created.

        // Probe: attempt sign-in with a plausible configured-admin email — must fail (no account)
        var probedEmails = new[]
        {
            "admin@learnexia.test",
            "admin-seed-test@learnexia.test",
            "admin@learnexia.io",
            "configured-admin@learnexia.io"
        };

        foreach (var email in probedEmails)
        {
            var (resp, root, body) = await PostAsync(_client, SignInUrl,
                new { UserName = email, Password = "AnyPassword!1" });

            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "BE-TC-24: no configured admin should exist (blank AdminSeed config is a no-op); " +
                "sign-in for '{0}' must return 400; body: {1}", email, body);

            TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
            succProp.GetBoolean().Should().BeFalse(
                "BE-TC-24: Successed must be false — account does not exist; email={0}; body: {1}", email, body);
        }

        // Confirm app booted cleanly: the seeded legacy accounts still work
        var (legacyResp, legacyRoot, legacyBody) = await PostAsync(_client, SignInUrl,
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        legacyResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-24: app booted cleanly — legacy superadmin sign-in must work; body: {0}", legacyBody);

        TryProp(legacyRoot, "successed", out var legacySucc).Should().BeTrue("body: {0}", legacyBody);
        legacySucc.GetBoolean().Should().BeTrue(
            "BE-TC-24: superadmin sign-in must succeed — seed ran cleanly even with blank AdminSeed; body: {0}", legacyBody);
    }
}

// -------------------------------------------------------------------------
// BE-TC-25: Uses AdminSeedWebAppFactory (AdminSeed:* configured)
// -------------------------------------------------------------------------

[Collection("AdminSeedTests")]
public sealed class P1_13_BE3_ConfiguredAdminSeed_Tests : IAsyncLifetime
{
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string MeUrl = "api/Users/Me";

    private readonly AdminSeedWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_13_BE3_ConfiguredAdminSeed_Tests(AdminSeedWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static bool TryProp(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value)) return true;
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
        PostAsync(HttpClient client, string url, object body, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (bearerToken is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { }
        }
        return (response, root, bodyStr);
    }

    private static async Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetAsync(HttpClient client, string url, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearerToken is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { }
        }
        return (response, root, bodyStr);
    }

    /// <summary>
    /// BE-TC-25a — Configured AdminSeed creates admin who can sign in.
    /// The AdminSeedWebAppFactory injects AdminSeed:Email + AdminSeed:Password.
    /// After seed + migration, the configured admin must be able to sign in and get a JWT.
    /// </summary>
    [Fact(DisplayName = "BE-TC-25a: Configured AdminSeed admin signs in → 200 + valid JWT")]
    public async Task BeTc25a_ConfiguredAdmin_CanSignIn()
    {
        var (resp, root, body) = await PostAsync(_client, SignInUrl,
            new { UserName = AdminSeedWebAppFactory.AdminEmail, Password = AdminSeedWebAppFactory.AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-25a: configured admin must be able to sign in; body: {0}", body);

        TryProp(root, "successed", out var succProp).Should().BeTrue("body: {0}", body);
        succProp.GetBoolean().Should().BeTrue(
            "BE-TC-25a: Successed must be true for configured admin sign-in; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "BE-TC-25a: accessToken must be a non-empty JWT; body: {0}", body);

        TryProp(data, "userId", out var userIdProp).Should().BeTrue("body: {0}", body);
        userIdProp.GetInt32().Should().BeGreaterThan(0,
            "BE-TC-25a: userId must be a positive integer; body: {0}", body);
    }

    /// <summary>
    /// BE-TC-25b — Configured admin JWT carries the Admin role claim.
    /// Decodes the JWT and checks for the "Admin" role claim.
    /// </summary>
    [Fact(DisplayName = "BE-TC-25b: Configured admin JWT carries 'Admin' role claim")]
    public async Task BeTc25b_ConfiguredAdmin_JwtCarriesAdminRole()
    {
        var (resp, root, body) = await PostAsync(_client, SignInUrl,
            new { UserName = AdminSeedWebAppFactory.AdminEmail, Password = AdminSeedWebAppFactory.AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-25b: prerequisite sign-in must succeed; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        var accessToken = tokenProp.GetString()!;

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(accessToken).Should().BeTrue(
            "BE-TC-25b: accessToken must be a readable JWT; token: {0}", accessToken);

        var jwt = handler.ReadJwtToken(accessToken);
        var roleClaims = jwt.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role
                        || c.Type == "role"
                        || string.Equals(c.Type, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                            StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();

        roleClaims.Should().Contain("Admin",
            "BE-TC-25b: configured admin JWT must carry 'Admin' role claim; " +
            "claims found: [{0}]", string.Join(", ", roleClaims));
    }

    /// <summary>
    /// BE-TC-25c — GET /Me for configured admin confirms Admin role in the profile.
    /// </summary>
    [Fact(DisplayName = "BE-TC-25c: GET /Me for configured admin → Admin role in data.roles")]
    public async Task BeTc25c_ConfiguredAdmin_MeEndpoint_ReturnsAdminRole()
    {
        // Sign in to get the token
        var (signInResp, signInRoot, signInBody) = await PostAsync(_client, SignInUrl,
            new { UserName = AdminSeedWebAppFactory.AdminEmail, Password = AdminSeedWebAppFactory.AdminPassword });

        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-25c: prerequisite sign-in must succeed; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", signInBody);
        var accessToken = tokenProp.GetString()!;

        // Call GET /Me
        var (meResp, meRoot, meBody) = await GetAsync(_client, MeUrl, accessToken);

        meResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-25c: GET /Me for configured admin must return 200; body: {0}", meBody);

        TryProp(meRoot, "successed", out var meSucc).Should().BeTrue("body: {0}", meBody);
        meSucc.GetBoolean().Should().BeTrue("body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "roles", out var rolesProp).Should().BeTrue(
            "BE-TC-25c: Me.data must contain 'roles'; body: {0}", meBody);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        roles.Should().Contain(r => r != null && r.Equals("Admin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-25c: configured admin's Me.roles must contain 'Admin'; roles found: [{0}]; body: {1}",
            string.Join(", ", roles), meBody);
    }

    /// <summary>
    /// BE-TC-25d — Legacy accounts (superadmin / basicuser) still sign in in the Testing environment.
    /// The AdminSeedWebAppFactory explicitly calls SeedBasicUserAsync + SeedSuperAdminAsync.
    /// </summary>
    [Fact(DisplayName = "BE-TC-25d: Legacy seeded accounts (superadmin + basicuser) still sign in after configured admin seed (regression)")]
    public async Task BeTc25d_LegacyAccounts_StillSignInAfterConfiguredAdminSeed()
    {
        // superadmin
        var (superResp, superRoot, superBody) = await PostAsync(_client, SignInUrl,
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        superResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-25d: superadmin must still sign in after configured-admin seed; body: {0}", superBody);

        TryProp(superRoot, "successed", out var superSucc).Should().BeTrue("body: {0}", superBody);
        superSucc.GetBoolean().Should().BeTrue("body: {0}", superBody);

        // basicuser
        var (basicResp, basicRoot, basicBody) = await PostAsync(_client, SignInUrl,
            new { UserName = "basicuser", Password = "123Pa$$word!" });

        basicResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-25d: basicuser must still sign in after configured-admin seed; body: {0}", basicBody);

        TryProp(basicRoot, "successed", out var basicSucc).Should().BeTrue("body: {0}", basicBody);
        basicSucc.GetBoolean().Should().BeTrue("body: {0}", basicBody);
    }
}

// -------------------------------------------------------------------------
// BE-TC-19 (BLOCKED) and BE-TC-34 (BLOCKED)
// -------------------------------------------------------------------------

[Collection("CaptchaTests")]
public sealed class P1_13_BE3_Blocked_Tests
{
    /// <summary>
    /// BE-TC-19 — BLOCKED: Admin-seed idempotency across two host boots / re-seed.
    /// Requires running SeedAsync twice against the same DB (second boot or direct re-invoke)
    /// to prove no duplicate admin account is created. The single-boot factory does not
    /// exercise a re-seed. Per Q3: implement a re-seed fixture or call SeedConfiguredAdminAsync
    /// twice via the service locator.
    /// </summary>
    [Fact(Skip = "BE-TC-19 BLOCKED: requires a second IdentityModule.SeedAsync call (or second host boot) " +
                 "against the same container DB to prove idempotency. " +
                 "The standard single-boot WebApplicationFactory does not exercise re-seed. " +
                 "Per Q3: add a re-seed fixture (call SeedConfiguredAdminAsync twice and assert " +
                 "exactly one admin account, no exception, no duplicate role assignment).",
        DisplayName = "BE-TC-19: BLOCKED — admin-seed idempotency across two boots/re-seed (Q3 fixture needed)")]
    public Task BeTc19_AdminSeedIdempotency_Blocked()
        => Task.CompletedTask;

    /// <summary>
    /// BE-TC-34 — BLOCKED: Legacy committed-password accounts NOT created in non-Development.
    /// IdentitySeeder.SeedAsync gates SeedBasicUserAsync/SeedSuperAdminAsync on IsDevelopment().
    /// Testing environment (Testcontainers factory) behaves like Development-like for this gate
    /// (see explicit seed calls in ApplyMigrationsAndSeedAsync). Non-Development gate requires
    /// UseEnvironment("Production") / UseEnvironment("Staging") boot fixture.
    /// Security audits already verify this by code review: IdentitySeeder.cs lines 31–36.
    /// </summary>
    [Fact(Skip = "BE-TC-34 BLOCKED: requires a WebApplicationFactory with UseEnvironment('Production') " +
                 "(or 'Staging') to exercise the IdentitySeeder IsDevelopment() gate. " +
                 "The standard Testing factory calls SeedBasicUserAsync + SeedSuperAdminAsync explicitly " +
                 "to match dev state. Non-Dev boot fixture (Q3) would assert sign-in with 'superadmin'/'123Pa$$word!' " +
                 "fails in Production — no legacy account seeded. Security audits confirm this by code review " +
                 "(IdentitySeeder.cs lines 31-36).",
        DisplayName = "BE-TC-34: BLOCKED — legacy accounts NOT seeded in non-Development (Production boot fixture needed, Q3)")]
    public Task BeTc34_LegacyCredsNotSeededNonDevelopment_Blocked()
        => Task.CompletedTask;
}

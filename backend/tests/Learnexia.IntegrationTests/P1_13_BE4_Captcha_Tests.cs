using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AspNetCoreRateLimit;
using FluentAssertions;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Identity.Application.Abstractions;
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
// Fake ICaptchaVerifier — controllable per-test
// ---------------------------------------------------------------------------

/// <summary>
/// Thread-safe fake that each test drives via <see cref="SetResult"/>.
/// true  → CAPTCHA verification passes (allow registration).
/// false → CAPTCHA verification fails (reject registration with 400).
/// </summary>
public sealed class FakeCaptchaVerifier : ICaptchaVerifier
{
    private volatile int _result = 1; // 1 = true, 0 = false; volatile int works around volatile bool

    /// <summary>Configures what the fake will return for all subsequent VerifyAsync calls.</summary>
    public void SetResult(bool result) => _result = result ? 1 : 0;

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
        => Task.FromResult(_result == 1);
}

// ---------------------------------------------------------------------------
// Self-contained WebApplicationFactory with fake verifier injected
// ---------------------------------------------------------------------------

/// <summary>
/// A WebApplicationFactory that mirrors <see cref="LearnexiaWebAppFactory"/> (Testcontainers
/// PostgreSQL, rate-limit bypass, migrations + seed) but additionally replaces the production
/// <see cref="ICaptchaVerifier"/> (registered as a typed HttpClient) with a
/// <see cref="FakeCaptchaVerifier"/> so no real Cloudflare Turnstile network call is needed (P1-13 BE-4).
/// </summary>
public sealed class CaptchaWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("LearnexiaCaptcha")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    /// <summary>The fake verifier instance that each test configures.</summary>
    public FakeCaptchaVerifier FakeVerifier { get; } = new FakeCaptchaVerifier();

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

            // Replace all module DbContexts with the Testcontainers connection.
            ReplaceDbContext<IdentityModuleDbContext>(services, connectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, connectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, connectionString, "learning");

            // Disable rate limiting for tests (the suite sends many requests in quick succession).
            services.Configure<IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });

            // Replace the real ICaptchaVerifier (registered via AddHttpClient<ICaptchaVerifier, …>)
            // with the controllable fake. RemoveAll removes both the typed-client descriptor and any
            // direct service descriptors so neither the real TurnstileCaptchaVerifier nor the typed
            // HttpClient factory try to construct it. AddScoped injects the shared fake instance.
            services.RemoveAll<ICaptchaVerifier>();
            services.AddScoped<ICaptchaVerifier>(_ => FakeVerifier);
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

    /// <summary>Apply migrations and seed all module schemas after the host is built.</summary>
    public async Task ApplyMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<LearningDbContext>().Database.MigrateAsync();

        // Seed roles + superadmin (idempotent).
        await IdentityModule.SeedAsync(sp);

        // The Testing environment does not auto-seed the dev accounts — do it here so tests
        // that rely on seeded accounts (superadmin / basicuser) work correctly.
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
// xUnit collection definition
// ---------------------------------------------------------------------------

[CollectionDefinition("CaptchaTests")]
public sealed class CaptchaTestsCollection : ICollectionFixture<CaptchaWebAppFactory> { }

// ---------------------------------------------------------------------------
// Test class
// ---------------------------------------------------------------------------

/// <summary>
/// P1-13 BE-4 integration tests: CAPTCHA anti-automation gate on Register-Parent.
///
/// Endpoint: [AllowAnonymous] POST /api/Users/Authentication/Register-Parent
/// Contract:  BaseResponse&lt;JwtAuthResponse&gt;
///
/// The production <see cref="ICaptchaVerifier"/> (Cloudflare Turnstile, registered as a typed
/// HttpClient) is replaced with <see cref="FakeCaptchaVerifier"/> so no real Turnstile server
/// or valid token is required in CI (P1-13 BE-4).
///
/// Acceptance criteria covered:
///   AC-DEF  Default (Captcha:Enabled=false) regression: register with NO captchaToken
///           → 200, Successed=true, AccessToken present. Gate is a no-op by default.
///   AC-FAIL Captcha enabled + verification FAILS (fake returns false): register → 400,
///           Successed=false; no account created (follow-up sign-in fails / email not registered).
///   AC-PASS Captcha enabled + verification PASSES (fake returns true): register with a token
///           → 200, Successed=true, AccessToken present; account created (verified via sign-in or /Me).
///   AC-NULL Missing token treated as fail: fake returns false for null token → 400, Successed=false,
///           no account created.
///   AC-ENV  BaseResponse envelope shape (statusCode, successed, message, errors) present on both
///           success (200) and failure (400).
/// </summary>
[Collection("CaptchaTests")]
public sealed class P1_13_BE4_Captcha_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URL constants
    // ---------------------------------------------------------------------------
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string MeUrl = "api/Users/Me";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly CaptchaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_13_BE4_Captcha_Tests(CaptchaWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string UniqueEmail(string tag = "")
        => $"captcha_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@captchatest.test";

    /// <summary>
    /// Case-insensitive property lookup. The project has two JSON serialisation paths:
    /// controller responses (Newtonsoft, camelCase) and middleware/422 (System.Text.Json, PascalCase).
    /// </summary>
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
        SendAsync(HttpClient client, HttpMethod method, string url, object? body = null, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        var response = await client.SendAsync(request);
        var bodyStr = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body; leave root default */ }
        }
        return (response, root, bodyStr);
    }

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PostRegisterAsync(object body)
        => SendAsync(_client, HttpMethod.Post, RegisterParentUrl, body);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        PostSignInAsync(object body)
        => SendAsync(_client, HttpMethod.Post, SignInUrl, body);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMeAsync(string bearerToken)
        => SendAsync(_client, HttpMethod.Get, MeUrl, null, bearerToken);

    // ---------------------------------------------------------------------------
    // AC-DEF: Default (Captcha:Enabled=false) regression
    //
    // When Captcha:Enabled=false (the committed default) TurnstileCaptchaVerifier.VerifyAsync
    // returns true immediately without a token or network call. This test suite substitutes the
    // full fake (FakeCaptchaVerifier), so to model the "disabled" behavior we pre-configure the
    // fake to return true — matching exactly what the real verifier does when disabled.
    //
    // These tests confirm the gate is a no-op by default:
    //   - No captchaToken field → 200, Successed=true, AccessToken present.
    //   - null captchaToken → 200 (fake=true models disabled pass-through).
    //   - Arbitrary captchaToken string → 200 (verifier doesn't care about token value when disabled).
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-DEF-1: Default disabled (fake=true), no captchaToken → 200 Successed=true")]
    public async Task ACDef1_DefaultDisabled_NoCaptchaToken_Returns200_WithAccessToken()
    {
        // Fake models the "Captcha:Enabled=false" no-op: always return true.
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("def-no-token");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true };
        // Note: no CaptchaToken field in the request body.

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "register with no captchaToken must succeed when CAPTCHA is disabled (fake=true); body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true when the CAPTCHA gate is a no-op; body: {0}", bodyStr);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", bodyStr);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", bodyStr);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "AccessToken must be a non-empty JWT on success; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-DEF-2: Default disabled (fake=true), explicit null captchaToken → 200 Successed=true")]
    public async Task ACDef2_DefaultDisabled_NullCaptchaToken_Returns200()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("def-null-token");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true, CaptchaToken = (string?)null };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "register with null captchaToken must succeed when CAPTCHA is disabled (fake=true); body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true when CAPTCHA is disabled regardless of token value; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-DEF-3: Default disabled (fake=true), IsFirstLogin=true on fresh registration")]
    public async Task ACDef3_DefaultDisabled_IsFirstLogin_IsTrue()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("def-firstlogin");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "prerequisite: registration must succeed with CAPTCHA disabled; body: {0}", bodyStr);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", bodyStr);
        TryProp(data, "isFirstLogin", out var isFirstLoginProp).Should().BeTrue("body: {0}", bodyStr);
        isFirstLoginProp.GetBoolean().Should().BeTrue(
            "IsFirstLogin must be true on a fresh registration; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-DEF-4: Default disabled (fake=true), registered parent can sign in immediately")]
    public async Task ACDef4_DefaultDisabled_RegisteredParent_CanSignIn()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("def-roundtrip");
        const string password = "Str0ng@Pass";

        // Register
        var (regResponse, _, regBody) = await PostRegisterAsync(
            new { Email = email, Password = password, AcceptedTerms = true });
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration prerequisite must succeed; body: {0}", regBody);

        // Sign in with the same credentials to confirm persistence.
        var (signInResponse, signInRoot, signInBody) = await PostSignInAsync(
            new { UserName = email, Password = password });

        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in with just-registered parent credentials must return 200; body: {0}", signInBody);

        TryProp(signInRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", signInBody);
        succeededProp.GetBoolean().Should().BeTrue(
            "sign-in successed must be true; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", signInBody);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "sign-in must return a non-empty AccessToken; body: {0}", signInBody);
    }

    // ---------------------------------------------------------------------------
    // AC-FAIL: Captcha enabled + verification FAILS
    //
    // Fake returns false (models the "Captcha:Enabled=true, bad/missing token" path).
    // Expected: HTTP 400, Successed=false, no account created.
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-FAIL-1: CaptchaFails (fake=false) → 400 Successed=false")]
    public async Task ACFail1_CaptchaFails_Returns400_SuccessedFalse()
    {
        // Fake models a failed CAPTCHA verification.
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("fail-basic");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true, CaptchaToken = "bad-captcha-token" };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "register with a failed CAPTCHA must return HTTP 400 (handler BadRequest path); body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false when CAPTCHA verification fails; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-FAIL-2: CaptchaFails (fake=false) → 400 with non-empty message")]
    public async Task ACFail2_CaptchaFails_Returns400_WithMessage()
    {
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("fail-msg");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true, CaptchaToken = "bad-token" };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", bodyStr);

        TryProp(root, "message", out var messageProp).Should().BeTrue(
            "400 response must contain a 'message' key; body: {0}", bodyStr);
        messageProp.GetString().Should().NotBeNullOrWhiteSpace(
            "message must be a non-empty localized string; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-FAIL-3: CaptchaFails (fake=false) → no account created (sign-in fails for that email)")]
    public async Task ACFail3_CaptchaFails_NoAccountCreated()
    {
        // First confirm the registration fails.
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("fail-no-account");
        const string password = "Str0ng@Pass";
        var body = new { Email = email, Password = password, AcceptedTerms = true, CaptchaToken = "bad-token" };

        var (regResponse, _, regBody) = await PostRegisterAsync(body);
        regResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "registration must fail when CAPTCHA fails (prerequisite); body: {0}", regBody);

        // Now switch fake back to pass, then try to sign in — must fail because no account was created.
        // (We switch to pass so the sign-in endpoint itself is not CAPTCHA-gated, only register is.)
        _factory.FakeVerifier.SetResult(true);

        var (signInResponse, signInRoot, signInBody) = await PostSignInAsync(
            new { UserName = email, Password = password });

        // Sign-In must fail: either 404 (user not found) or 400 (invalid credentials after anti-enum hardening).
        // Either proves no account was persisted.
        var signInStatus = (int)signInResponse.StatusCode;
        signInStatus.Should().BeOneOf(
            new[] { 400, 404 },
            "sign-in must fail for an email whose registration was blocked by CAPTCHA; " +
            "status={0}; body: {1}", signInStatus, signInBody);

        TryProp(signInRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", signInBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "sign-in successed must be false — no account was created; body: {0}", signInBody);
    }

    [Fact(DisplayName = "AC-FAIL-4: CaptchaFails (fake=false) → 400 BaseResponse envelope shape")]
    public async Task ACFail4_CaptchaFails_Returns400_EnvelopeShape()
    {
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("fail-env");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true, CaptchaToken = "bad-token" };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", bodyStr);

        // Full BaseResponse envelope must be present on the 400 failure path (controller path, camelCase).
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue(
            "envelope must contain 'statusCode'; body: {0}", bodyStr);
        statusCodeProp.GetInt32().Should().Be(400,
            "statusCode field must be 400 on a BadRequest response; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue(
            "envelope must contain 'successed'; body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse("body: {0}", bodyStr);

        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must contain 'message'; body: {0}", bodyStr);
        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must contain 'errors'; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-FAIL-5: CaptchaFails (fake=false) → response message does not leak internals")]
    public async Task ACFail5_CaptchaFails_ResponseDoesNotLeakInternals()
    {
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("fail-no-leak");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true, CaptchaToken = "bad-token" };

        var (response, _, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", bodyStr);

        // No stack trace or raw exception text must appear in the response body.
        bodyStr.Should().NotContain("Exception", "stack trace must not appear in response; body: {0}", bodyStr);
        bodyStr.Should().NotContain("   at ", "stack trace must not appear in response; body: {0}", bodyStr);
    }

    // ---------------------------------------------------------------------------
    // AC-PASS: Captcha enabled + verification PASSES
    //
    // Fake returns true (models "Captcha:Enabled=true, valid token").
    // Expected: HTTP 200, Successed=true, AccessToken present, account created.
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-PASS-1: CaptchaPass (fake=true) with explicit token → 200 Successed=true")]
    public async Task ACPass1_CaptchaPass_WithToken_Returns200()
    {
        // Fake models a successful CAPTCHA verification.
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("pass-basic");
        var body = new
        {
            Email = email,
            Password = "Str0ng@Pass",
            AcceptedTerms = true,
            CaptchaToken = "valid-captcha-token-from-client"  // token present; fake returns true
        };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "register with a passing CAPTCHA must return HTTP 200; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true when CAPTCHA verification passes; body: {0}", bodyStr);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", bodyStr);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", bodyStr);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "AccessToken must be a non-empty JWT; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-PASS-2: CaptchaPass (fake=true) → account created, sign-in succeeds")]
    public async Task ACPass2_CaptchaPass_AccountCreated_SignInSucceeds()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("pass-persist");
        const string password = "Str0ng@Pass";
        var body = new
        {
            Email = email,
            Password = password,
            AcceptedTerms = true,
            CaptchaToken = "valid-captcha-token"
        };

        // Register
        var (regResponse, _, regBody) = await PostRegisterAsync(body);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration with passing CAPTCHA must succeed; body: {0}", regBody);

        // Sign in to confirm the account was persisted.
        var (signInResponse, signInRoot, signInBody) = await PostSignInAsync(
            new { UserName = email, Password = password });

        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed after a CAPTCHA-gated registration; body: {0}", signInBody);

        TryProp(signInRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", signInBody);
        succeededProp.GetBoolean().Should().BeTrue(
            "sign-in successed must be true — account must have been created; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", signInBody);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "sign-in must return a non-empty AccessToken; body: {0}", signInBody);
    }

    [Fact(DisplayName = "AC-PASS-3: CaptchaPass (fake=true) → account created, GET /Me confirms user exists")]
    public async Task ACPass3_CaptchaPass_AccountCreated_MeConfirmsUser()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("pass-me");
        const string password = "Str0ng@Pass";
        var body = new
        {
            Email = email,
            Password = password,
            AcceptedTerms = true,
            CaptchaToken = "valid-captcha-token-for-me"
        };

        // Register
        var (regResponse, regRoot, regBody) = await PostRegisterAsync(body);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "registration with passing CAPTCHA must succeed; body: {0}", regBody);

        // Extract the access token from the registration response.
        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", regBody);
        var accessToken = tokenProp.GetString();
        accessToken.Should().NotBeNullOrWhiteSpace("access token must be present; body: {0}", regBody);

        // Call GET /Me to confirm the account exists in the system.
        var (meResponse, meRoot, meBody) = await GetMeAsync(accessToken!);

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me must succeed with the token from CAPTCHA-gated registration; body: {0}", meBody);

        TryProp(meRoot, "successed", out var meSucceeded).Should().BeTrue("body: {0}", meBody);
        meSucceeded.GetBoolean().Should().BeTrue(
            "GET /Me successed must be true — account must exist; body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "id", out var userIdProp).Should().BeTrue("body: {0}", meBody);
        userIdProp.GetInt32().Should().BeGreaterThan(0,
            "userId must be a positive integer; body: {0}", meBody);
    }

    [Fact(DisplayName = "AC-PASS-4: CaptchaPass (fake=true) → 200 BaseResponse envelope shape correct")]
    public async Task ACPass4_CaptchaPass_Returns200_EnvelopeShape()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("pass-env");
        var body = new
        {
            Email = email,
            Password = "Str0ng@Pass",
            AcceptedTerms = true,
            CaptchaToken = "valid-token"
        };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", bodyStr);

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue("body: {0}", bodyStr);
        statusCodeProp.GetInt32().Should().Be(200, "body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", bodyStr);

        TryProp(root, "message", out _).Should().BeTrue("body: {0}", bodyStr);
        TryProp(root, "errors", out _).Should().BeTrue("body: {0}", bodyStr);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", bodyStr);
        data.ValueKind.Should().NotBe(JsonValueKind.Null,
            "data must not be null on a successful registration; body: {0}", bodyStr);
    }

    // ---------------------------------------------------------------------------
    // AC-NULL: Missing/null token treated as fail when fake models that behavior
    //
    // When CAPTCHA is enabled and the token is null/missing the real TurnstileCaptchaVerifier
    // returns false immediately (fail-closed). We model this by setting fake=false and sending
    // no token. This confirms the handler rejects without even trying to verify.
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-NULL-1: Null/absent captchaToken with fake=false → 400 Successed=false")]
    public async Task ACNull1_NullToken_FakeFalse_Returns400()
    {
        // Models: CAPTCHA enabled, null/absent token → verifier returns false (fail-closed).
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("null-token");
        // No CaptchaToken in the request (it's optional on the command).
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = true };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "register with null token (fake=false) must return 400; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false when CAPTCHA fake returns false for null token; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "AC-NULL-2: Null/absent token with fake=false → no account created")]
    public async Task ACNull2_NullToken_FakeFalse_NoAccountCreated()
    {
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("null-no-account");
        const string password = "Str0ng@Pass";
        // No CaptchaToken in the request.
        var body = new { Email = email, Password = password, AcceptedTerms = true };

        var (regResponse, _, regBody) = await PostRegisterAsync(body);
        regResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "registration must fail when CAPTCHA fake returns false for null token; body: {0}", regBody);

        // Restore fake to pass and try sign-in — must fail because no account was created.
        _factory.FakeVerifier.SetResult(true);

        var (signInResponse, signInRoot, signInBody) = await PostSignInAsync(
            new { UserName = email, Password = password });

        var signInStatus = (int)signInResponse.StatusCode;
        signInStatus.Should().BeOneOf(
            new[] { 400, 404 },
            "sign-in must fail — no account was persisted when CAPTCHA blocked registration; " +
            "status={0}; body: {1}", signInStatus, signInBody);

        TryProp(signInRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", signInBody);
        succeededProp.GetBoolean().Should().BeFalse(
            "sign-in successed must be false when no account exists; body: {0}", signInBody);
    }

    // ---------------------------------------------------------------------------
    // Transition test: fail then pass with same email — account created only on pass
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "Transition: CAPTCHA fail then pass with same email → account created on pass, not on fail")]
    public async Task Transition_FailThenPass_AccountOnlyCreatedOnPass()
    {
        var email = UniqueEmail("transition");
        const string password = "Str0ng@Pass";
        var bodyWithToken = new { Email = email, Password = password, AcceptedTerms = true, CaptchaToken = "some-token" };

        // First attempt: CAPTCHA fails → rejected.
        _factory.FakeVerifier.SetResult(false);
        var (failResponse, _, failBody) = await PostRegisterAsync(bodyWithToken);
        failResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "first attempt (CAPTCHA fail) must return 400; body: {0}", failBody);

        // Second attempt with same email: CAPTCHA passes → should succeed.
        _factory.FakeVerifier.SetResult(true);
        var (passResponse, passRoot, passBody) = await PostRegisterAsync(bodyWithToken);
        passResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "second attempt (CAPTCHA pass) must succeed for the same email; body: {0}", passBody);

        TryProp(passRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", passBody);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true on successful registration after a prior CAPTCHA failure; body: {0}", passBody);

        // The account was created on the pass attempt — sign in to confirm.
        var (signInResponse, signInRoot, signInBody) = await PostSignInAsync(
            new { UserName = email, Password = password });
        signInResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "sign-in must succeed after a CAPTCHA-gated registration; body: {0}", signInBody);

        TryProp(signInRoot, "successed", out var signInSucceeded).Should().BeTrue("body: {0}", signInBody);
        signInSucceeded.GetBoolean().Should().BeTrue("body: {0}", signInBody);
    }

    // ---------------------------------------------------------------------------
    // Regression: existing endpoints still work after CAPTCHA wiring
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "Regression: seeded superadmin can still sign in after CAPTCHA wiring")]
    public async Task Regression_SuperAdmin_CanStillSignIn()
    {
        // CAPTCHA only gates Register-Parent; sign-in is unaffected.
        _factory.FakeVerifier.SetResult(true);

        var (response, root, bodyStr) = await PostSignInAsync(
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "seeded superadmin sign-in must still work after CAPTCHA wiring; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "superadmin sign-in successed must be true; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "Regression: seeded basicuser can still sign in after CAPTCHA wiring")]
    public async Task Regression_BasicUser_CanStillSignIn()
    {
        _factory.FakeVerifier.SetResult(true);

        var (response, root, bodyStr) = await PostSignInAsync(
            new { UserName = "basicuser", Password = "123Pa$$word!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "seeded basicuser sign-in must still work after CAPTCHA wiring; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeTrue(
            "basicuser sign-in successed must be true; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "Regression: password validation (AcceptedTerms=false) still returns 422 before CAPTCHA check")]
    public async Task Regression_AcceptedTermsFalse_Returns422_BeforeCaptchaCheck()
    {
        // Even if fake returns false, ValidationBehavior runs before the handler (including before
        // the CAPTCHA check inside the handler). AcceptedTerms=false → 422 (validator). This
        // confirms validation still fires first and CAPTCHA is not bypassed for invalid input.
        _factory.FakeVerifier.SetResult(false);

        var email = UniqueEmail("terms-false");
        var body = new { Email = email, Password = "Str0ng@Pass", AcceptedTerms = false, CaptchaToken = "token" };

        var (response, root, bodyStr) = await PostRegisterAsync(body);

        // ValidationBehavior catches AcceptedTerms=false → 422 before the CAPTCHA check in the handler.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "AcceptedTerms=false must return 422 from ValidationBehavior regardless of CAPTCHA state; body: {0}", bodyStr);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", bodyStr);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false on a validation failure; body: {0}", bodyStr);
    }

    [Fact(DisplayName = "Regression: invalid email format (fake=true) still returns 422")]
    public async Task Regression_InvalidEmail_Returns422()
    {
        _factory.FakeVerifier.SetResult(true);

        var body = new { Email = "not-an-email", Password = "Str0ng@Pass", AcceptedTerms = true, CaptchaToken = "token" };

        var (response, _, bodyStr) = await PostRegisterAsync(body);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "invalid email must return 422 from ValidationBehavior regardless of CAPTCHA; body: {0}", bodyStr);
    }

    // ---------------------------------------------------------------------------
    // BE-TC-32: Register-Parent cannot inject a role (product override: no Student/Teacher self-register)
    //
    // The RegisterParentCommand has no Roles/Role/IsActive field; extra JSON fields are ignored by
    // the model binder. The account is assigned only the server-side 'Parent' role, never
    // Admin/Student/Teacher/SuperAdmin via the anonymous registration path.
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "BE-TC-32: Over-posting Roles/IsActive in Register-Parent body → ignored; account gets only 'Parent' role (product override: no Student/Teacher self-register)")]
    public async Task BeTc32_RegisterParent_IgnoresRoleOverPost_OnlyParentRoleAssigned()
    {
        _factory.FakeVerifier.SetResult(true);

        var email = UniqueEmail("tc32-role-inject");
        const string password = "Str0ng@Pass";

        // Attempt over-posting of Role / Roles / IsActive — these fields do not exist on
        // RegisterParentCommand; model binding ignores extra JSON fields.
        var body = new
        {
            Email = email,
            Password = password,
            AcceptedTerms = true,
            CaptchaToken = "valid-token",
            // Over-post attempts — must be ignored by the model binder
            Role = "Admin",
            Roles = new[] { "Admin", "SuperAdmin", "Student" },
            IsActive = true
        };

        var (regResponse, regRoot, regBody) = await PostRegisterAsync(body);

        // Registration must succeed (extra fields ignored)
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-32: Register-Parent with extra role fields must succeed (extra fields are ignored); body: {0}", regBody);

        TryProp(regRoot, "successed", out var regSucceeded).Should().BeTrue("body: {0}", regBody);
        regSucceeded.GetBoolean().Should().BeTrue(
            "BE-TC-32: Successed must be true on successful registration; body: {0}", regBody);

        TryProp(regRoot, "data", out var regData).Should().BeTrue("body: {0}", regBody);
        TryProp(regData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", regBody);
        var accessToken = tokenProp.GetString()!;
        accessToken.Should().NotBeNullOrWhiteSpace("BE-TC-32: accessToken must be present; body: {0}", regBody);

        // Call GET /Me to inspect the roles actually assigned
        var (meResponse, meRoot, meBody) = await GetMeAsync(accessToken);

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "BE-TC-32: GET /Me must return 200 for the just-registered account; body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "roles", out var rolesProp).Should().BeTrue(
            "BE-TC-32: Me.data must contain 'roles'; body: {0}", meBody);

        var roles = rolesProp.EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        // Must contain exactly Parent (server-assigned) — no injected roles
        roles.Should().Contain(r => r != null && r.Equals("Parent", StringComparison.OrdinalIgnoreCase),
            "BE-TC-32: account must have the 'Parent' role (server-assigned); roles: [{0}]; body: {1}",
            string.Join(", ", roles), meBody);

        roles.Should().NotContain(r => r != null && r.Equals("Admin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-32: over-posted 'Admin' role must be ignored; roles: [{0}]; body: {1}",
            string.Join(", ", roles), meBody);

        roles.Should().NotContain(r => r != null && r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase),
            "BE-TC-32: over-posted 'SuperAdmin' role must be ignored; roles: [{0}]; body: {1}",
            string.Join(", ", roles), meBody);

        roles.Should().NotContain(r => r != null && r.Equals("Student", StringComparison.OrdinalIgnoreCase),
            "BE-TC-32: over-posted 'Student' role must be ignored; roles: [{0}]; body: {1}",
            string.Join(", ", roles), meBody);
    }
}

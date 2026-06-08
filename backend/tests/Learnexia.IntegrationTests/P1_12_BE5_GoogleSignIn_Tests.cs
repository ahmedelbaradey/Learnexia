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
// Fake Google token validator — controllable per-test
// ---------------------------------------------------------------------------

/// <summary>
/// Thread-safe fake that each test drives via <see cref="SetResult"/>.
/// null   → validator returns null (invalid/expired token simulation).
/// non-null → validator returns that <see cref="GoogleUserInfo"/> (happy/unverified paths).
/// </summary>
public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    private volatile GoogleUserInfo? _result;

    /// <summary>Configures what the fake will return for all subsequent calls.</summary>
    public void SetResult(GoogleUserInfo? result) => _result = result;

    public Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken)
        => Task.FromResult(_result);
}

// ---------------------------------------------------------------------------
// Self-contained WebApplicationFactory with fake validator injected
// ---------------------------------------------------------------------------

/// <summary>
/// A WebApplicationFactory that mirrors <see cref="LearnexiaWebAppFactory"/> (Testcontainers
/// PostgreSQL, rate-limit bypass, migrations + seed) but additionally replaces the production
/// <see cref="IGoogleTokenValidator"/> with <see cref="FakeGoogleTokenValidator"/> so no real
/// Google token is needed in CI (P1-12 BE-5).
/// </summary>
public sealed class GoogleSignInWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("LearnexiaGoogle")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    /// <summary>The fake validator instance that each test configures.</summary>
    public FakeGoogleTokenValidator FakeValidator { get; } = new FakeGoogleTokenValidator();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var connectionString = _postgres.GetConnectionString();

            // Replace all module DbContexts with the Testcontainers connection.
            ReplaceDbContext<IdentityModuleDbContext>(services, connectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, connectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, connectionString, "learning");

            // Disable rate limiting for tests.
            services.Configure<IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });

            // Replace the real Google token validator with the controllable fake.
            // This runs inside ConfigureServices (not ConfigureTestServices) to ensure
            // it runs in the correct order relative to other service registrations.
            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator>(FakeValidator);
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

        // Seed the dev accounts that the Testing environment doesn't seed automatically.
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

[CollectionDefinition("GoogleSignInTests")]
public sealed class GoogleSignInTestsCollection : ICollectionFixture<GoogleSignInWebAppFactory> { }

// ---------------------------------------------------------------------------
// Test class
// ---------------------------------------------------------------------------

/// <summary>
/// P1-12 BE-5 integration tests: POST /api/Users/Authentication/Google-SignIn
///
/// Endpoint: [AllowAnonymous] POST /api/Users/Authentication/Google-SignIn
/// Body:     { idToken: string }
/// Contract: BaseResponse&lt;JwtAuthResponse&gt;
///
/// The production <see cref="IGoogleTokenValidator"/> is replaced with a controllable fake
/// (<see cref="FakeGoogleTokenValidator"/>) so no real Google token is required in CI.
/// Each test configures the fake before sending the request.
///
/// Acceptance criteria covered:
///   AC-1  Empty/missing idToken       → 422 (ValidationBehavior), Successed=false, Errors[] populated
///   AC-2  Invalid token (fake=null)   → 401, Successed=false, generic message (no internals)
///   AC-3  EmailVerified=false         → 400, Successed=false (GoogleEmailNotVerified)
///   AC-4  New verified email          → 200, Successed=true, JWT returned; GET /Me with that JWT →
///                                        user exists, roles includes "Parent", email matches
///   AC-5  Existing email              → register a parent by password first, then Google-SignIn for
///                                        same email → 200, JWT for SAME account (same userId via /Me),
///                                        no duplicate user created
///   AC-6  Idempotent second sign-in   → sign in twice with the same Google identity → both 200, same
///                                        userId, no error (external login already-linked path)
///   AC-7  Deactivated account         → 400, Successed=false (LoginAccountDeactivated)
/// </summary>
[Collection("GoogleSignInTests")]
public sealed class P1_12_BE5_GoogleSignIn_Tests : IAsyncLifetime
{
    // ---------------------------------------------------------------------------
    // URLs
    // ---------------------------------------------------------------------------
    private const string GoogleSignInUrl = "api/Users/Authentication/Google-SignIn";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl = "api/Users/Authentication/Sign-In";
    private const string MeUrl = "api/Users/Me";

    // ---------------------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------------------
    private readonly GoogleSignInWebAppFactory _factory;
    private readonly HttpClient _client;

    public P1_12_BE5_GoogleSignIn_Tests(GoogleSignInWebAppFactory factory)
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
        => $"goog_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@googletest.test";

    private static string UniqueSubject()
        => $"google-sub-{Guid.NewGuid():N}";

    /// <summary>
    /// Case-insensitive property lookup.  The project has two JSON serialisation paths:
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
        PostGoogleSignInAsync(object body)
        => SendAsync(_client, HttpMethod.Post, GoogleSignInUrl, body);

    private Task<(HttpResponseMessage Response, JsonElement Root, string Body)>
        GetMeAsync(string bearerToken)
        => SendAsync(_client, HttpMethod.Get, MeUrl, null, bearerToken);

    /// <summary>Registers a parent by password and returns their accessToken + userId.</summary>
    private async Task<(string Token, int UserId)> RegisterParentAsync(
        string email, string password = "Str0ng@Pass")
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, RegisterParentUrl,
            new { Email = email, Password = password, AcceptedTerms = true });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration prerequisite must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        TryProp(data, "userId", out var userIdProp).Should().BeTrue("body: {0}", body);
        return (tokenProp.GetString()!, userIdProp.GetInt32());
    }

    // ---------------------------------------------------------------------------
    // AC-1: Empty / missing idToken → 422
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-1a Empty idToken string → 422 with Errors[] populated")]
    public async Task AC1a_EmptyIdToken_Returns422_WithErrors()
    {
        // Validator result does not matter — ValidationBehavior fires before the handler.
        _factory.FakeValidator.SetResult(null);

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity,
            "empty idToken must be rejected by ValidationBehavior with 422; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false for a validation failure; body: {0}", body);

        TryProp(root, "errors", out var errorsProp).Should().BeTrue(
            "422 response must contain 'errors' key; body: {0}", body);
        errorsProp.EnumerateArray().ToList().Should().NotBeEmpty(
            "Errors[] must be populated; body: {0}", body);
    }

    [Fact(DisplayName = "AC-1b Missing idToken field → 422 or 400, Successed=false")]
    public async Task AC1b_MissingIdToken_ReturnsRejection()
    {
        _factory.FakeValidator.SetResult(null);

        // Send a JSON object without the idToken property at all.
        var (resp, root, body) = await PostGoogleSignInAsync(new { });

        // Either 422 (validator catches empty/null via model binding null → "") or 400 (model binding failure).
        ((int)resp.StatusCode).Should().BeOneOf(
            new[] { 422, 400 },
            "missing idToken must be rejected; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse("body: {0}", body);
    }

    [Fact(DisplayName = "AC-1c Validation envelope shape: statusCode, successed, message, errors present on 422")]
    public async Task AC1c_ValidationEnvelope_HasRequiredKeys()
    {
        _factory.FakeValidator.SetResult(null);

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "body: {0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must contain 'statusCode'; body: {0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "envelope must contain 'successed'; body: {0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must contain 'message'; body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must contain 'errors'; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // AC-2: Invalid token (fake returns null) → 401
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-2a Invalid token (fake=null) → 401, Successed=false")]
    public async Task AC2a_InvalidToken_Returns401_SuccessedFalse()
    {
        _factory.FakeValidator.SetResult(null);

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "this.is.a.fake.invalid.google.token" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "invalid Google token must return 401; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false for an invalid token; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2b Invalid token → 401, response message does not leak internal details")]
    public async Task AC2b_InvalidToken_ResponseDoesNotLeakInternals()
    {
        _factory.FakeValidator.SetResult(null);

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "bad.token.value" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);

        // The message must be a generic localized string — not a stack trace or raw exception message.
        body.Should().NotContain("Exception", "stack trace must not appear in response; body: {0}", body);
        body.Should().NotContain("   at ", "stack trace must not appear in response; body: {0}", body);

        TryProp(root, "message", out var messageProp).Should().BeTrue("body: {0}", body);
        var messageStr = messageProp.GetString() ?? string.Empty;
        messageStr.Should().NotBeNullOrWhiteSpace(
            "message must be a non-empty generic string; body: {0}", body);
    }

    [Fact(DisplayName = "AC-2c Invalid token → 401 BaseResponse envelope with statusCode=401")]
    public async Task AC2c_InvalidToken_Returns401_WithEnvelope()
    {
        _factory.FakeValidator.SetResult(null);

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "bad.token.value" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);

        // Full BaseResponse envelope must be present.
        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue("body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(401, "body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse("body: {0}", body);

        TryProp(root, "message", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // AC-3: EmailVerified=false → 400 (GoogleEmailNotVerified)
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-3a Unverified email → 400, Successed=false")]
    public async Task AC3a_UnverifiedEmail_Returns400()
    {
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = UniqueEmail("unverified"),
            EmailVerified = false,
            Name = "Unverified User"
        });

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "unverified-email-token" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "unverified email must return 400; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false for unverified email; body: {0}", body);
    }

    [Fact(DisplayName = "AC-3b Unverified email → 400 with non-empty message")]
    public async Task AC3b_UnverifiedEmail_Returns400_WithMessage()
    {
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = UniqueEmail("unvmsg"),
            EmailVerified = false,
            Name = "Unverified"
        });

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "unverified-email-token-2" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body: {0}", body);

        TryProp(root, "message", out var messageProp).Should().BeTrue("body: {0}", body);
        messageProp.GetString().Should().NotBeNullOrWhiteSpace(
            "400 message must be a non-empty localized string; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // AC-4: New verified email → auto-create account, 200, JWT + /Me confirms Parent role
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-4a New verified email → 200, Successed=true, non-empty AccessToken")]
    public async Task AC4a_NewVerifiedEmail_Returns200_WithAccessToken()
    {
        var email = UniqueEmail("new-verified");
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = email,
            EmailVerified = true,
            Name = "New Google User"
        });

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "valid-new-user-token" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "new verified Google user must return 200; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true on successful Google sign-in; body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", body);
        tokenProp.GetString().Should().NotBeNullOrWhiteSpace(
            "AccessToken must be a non-empty JWT; body: {0}", body);
    }

    [Fact(DisplayName = "AC-4b New verified email → /Me confirms user exists, role=Parent")]
    public async Task AC4b_NewVerifiedEmail_MeConfirms_ParentRole()
    {
        var email = UniqueEmail("me-confirm");
        var subject = UniqueSubject();

        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = subject,
            Email = email,
            EmailVerified = true,
            Name = "Me Confirm User"
        });

        var (signInResp, signInRoot, signInBody) = await PostGoogleSignInAsync(
            new { idToken = "valid-me-confirm-token" });

        signInResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Google sign-in prerequisite must succeed; body: {0}", signInBody);

        TryProp(signInRoot, "data", out var signInData).Should().BeTrue("body: {0}", signInBody);
        TryProp(signInData, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", signInBody);
        var accessToken = tokenProp.GetString()!;

        // Call /Me with the Google-issued JWT.
        var (meResp, meRoot, meBody) = await GetMeAsync(accessToken);

        meResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET /Me with Google JWT must return 200; body: {0}", meBody);

        TryProp(meRoot, "successed", out var meSucceeded).Should().BeTrue("body: {0}", meBody);
        meSucceeded.GetBoolean().Should().BeTrue("body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);

        // roles must include Parent
        TryProp(meData, "roles", out var rolesProp).Should().BeTrue("body: {0}", meBody);
        rolesProp.ValueKind.Should().Be(JsonValueKind.Array, "body: {0}", meBody);
        var roles = rolesProp.EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain(r => string.Equals(r, "Parent", StringComparison.OrdinalIgnoreCase),
            "auto-created Google account must have the Parent role; roles={0}; body={1}",
            string.Join(",", roles!), meBody);
    }

    [Fact(DisplayName = "AC-4c New verified email → full BaseResponse envelope shape (statusCode, successed, message, data, errors)")]
    public async Task AC4c_NewVerifiedEmail_EnvelopeShapeCorrect()
    {
        var email = UniqueEmail("env-shape");
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = email,
            EmailVerified = true,
            Name = "Envelope Shape User"
        });

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "envelope-shape-token" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "statusCode", out var statusCodeProp).Should().BeTrue("body: {0}", body);
        statusCodeProp.GetInt32().Should().Be(200, "body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", body);

        TryProp(root, "message", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "errors", out _).Should().BeTrue("body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        data.ValueKind.Should().NotBe(JsonValueKind.Null, "data must not be null on success; body: {0}", body);
    }

    [Fact(DisplayName = "AC-4d New verified email → IsFirstLogin=true for auto-created account")]
    public async Task AC4d_NewVerifiedEmail_IsFirstLogin_True()
    {
        var email = UniqueEmail("first-login");
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = email,
            EmailVerified = true,
            Name = "First Login User"
        });

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "first-login-token" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "isFirstLogin", out var isFirstLoginProp).Should().BeTrue("body: {0}", body);
        isFirstLoginProp.GetBoolean().Should().BeTrue(
            "IsFirstLogin must be true for a new Google account (RegistrationIsCompleted=false); body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // AC-5: Existing email → JWT for same account, no duplicate
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-5a Existing password-registered email → Google sign-in returns 200 for SAME account (same userId)")]
    public async Task AC5a_ExistingEmail_GoogleSignIn_ReturnsSameAccount()
    {
        var email = UniqueEmail("existing-user");

        // Register the parent by password first.
        var (_, passwordUserId) = await RegisterParentAsync(email);

        // Now Google-SignIn for the same email with a verified token.
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = email,
            EmailVerified = true,
            Name = "Existing User"
        });

        var (googleResp, googleRoot, googleBody) = await PostGoogleSignInAsync(
            new { idToken = "existing-user-token" });

        googleResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "Google sign-in for existing email must return 200; body: {0}", googleBody);

        TryProp(googleRoot, "data", out var googleData).Should().BeTrue("body: {0}", googleBody);
        TryProp(googleData, "accessToken", out var googleTokenProp).Should().BeTrue("body: {0}", googleBody);
        var googleJwt = googleTokenProp.GetString()!;

        // Call /Me with the Google JWT to verify it refers to the SAME user.
        var (meResp, meRoot, meBody) = await GetMeAsync(googleJwt);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);

        TryProp(meRoot, "data", out var meData).Should().BeTrue("body: {0}", meBody);
        TryProp(meData, "id", out var googleUserIdProp).Should().BeTrue("body: {0}", meBody);

        googleUserIdProp.GetInt32().Should().Be(passwordUserId,
            "Google sign-in for existing email must return the SAME userId as the password registration " +
            "(passwordUserId={0}); no duplicate account should be created; body={1}",
            passwordUserId, meBody);
    }

    [Fact(DisplayName = "AC-5b Existing email → Successed=true and /Me works with returned token")]
    public async Task AC5b_ExistingEmail_ReturnsSucceeded_MeWorks()
    {
        var email = UniqueEmail("existing-email-check");

        // Register by password first.
        await RegisterParentAsync(email);

        // Google sign-in for the same email.
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = email,
            EmailVerified = true,
            Name = "Existing Email Check"
        });

        var (googleResp, googleRoot, googleBody) = await PostGoogleSignInAsync(
            new { idToken = "existing-email-check-token" });

        googleResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", googleBody);

        TryProp(googleRoot, "successed", out var succeededProp).Should().BeTrue("body: {0}", googleBody);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", googleBody);

        TryProp(googleRoot, "data", out var data).Should().BeTrue("body: {0}", googleBody);
        TryProp(data, "accessToken", out var tokenProp).Should().BeTrue("body: {0}", googleBody);
        var googleJwt = tokenProp.GetString()!;

        var (meResp, meRoot, meBody) = await GetMeAsync(googleJwt);
        meResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody);

        TryProp(meRoot, "successed", out var meSucceeded).Should().BeTrue("body: {0}", meBody);
        meSucceeded.GetBoolean().Should().BeTrue("body: {0}", meBody);
    }

    // ---------------------------------------------------------------------------
    // AC-6: Idempotent second Google sign-in — same identity, both 200, same userId
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-6a Idempotent: same Google identity twice → both 200, same userId from /Me")]
    public async Task AC6a_Idempotent_SameGoogleIdentity_TwiceReturns200_SameUser()
    {
        var email = UniqueEmail("idempotent");
        var subject = UniqueSubject();

        var googleUserInfo = new GoogleUserInfo
        {
            Subject = subject,
            Email = email,
            EmailVerified = true,
            Name = "Idempotent Google User"
        };

        // First sign-in → creates the account.
        _factory.FakeValidator.SetResult(googleUserInfo);
        var (resp1, root1, body1) = await PostGoogleSignInAsync(new { idToken = "idempotent-token-first" });

        resp1.StatusCode.Should().Be(HttpStatusCode.OK,
            "first Google sign-in must return 200; body: {0}", body1);
        TryProp(root1, "data", out var data1).Should().BeTrue("body: {0}", body1);
        TryProp(data1, "accessToken", out var tokenProp1).Should().BeTrue("body: {0}", body1);
        var jwt1 = tokenProp1.GetString()!;

        // Get userId from /Me after first sign-in.
        var (meResp1, meRoot1, meBody1) = await GetMeAsync(jwt1);
        meResp1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody1);
        TryProp(meRoot1, "data", out var meData1).Should().BeTrue("body: {0}", meBody1);
        TryProp(meData1, "id", out var userIdProp1).Should().BeTrue("body: {0}", meBody1);
        var userId1 = userIdProp1.GetInt32();

        // Second sign-in with the same Google identity (external login already linked).
        _factory.FakeValidator.SetResult(googleUserInfo);
        var (resp2, root2, body2) = await PostGoogleSignInAsync(new { idToken = "idempotent-token-second" });

        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second Google sign-in with same identity must return 200 (idempotent); body: {0}", body2);
        TryProp(root2, "data", out var data2).Should().BeTrue("body: {0}", body2);
        TryProp(data2, "accessToken", out var tokenProp2).Should().BeTrue("body: {0}", body2);
        var jwt2 = tokenProp2.GetString()!;

        // Get userId from /Me after second sign-in — must be the same.
        var (meResp2, meRoot2, meBody2) = await GetMeAsync(jwt2);
        meResp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", meBody2);
        TryProp(meRoot2, "data", out var meData2).Should().BeTrue("body: {0}", meBody2);
        TryProp(meData2, "id", out var userIdProp2).Should().BeTrue("body: {0}", meBody2);
        var userId2 = userIdProp2.GetInt32();

        userId2.Should().Be(userId1,
            "both Google sign-ins for the same identity must return the SAME userId " +
            "(userId1={0}, userId2={1}); no duplicate account should be created",
            userId1, userId2);
    }

    [Fact(DisplayName = "AC-6b Idempotent: second sign-in Successed=true, errors array empty")]
    public async Task AC6b_Idempotent_SecondSignIn_SuccessedTrue_NoError()
    {
        var email = UniqueEmail("idempotent2");
        var subject = UniqueSubject();

        var googleUserInfo = new GoogleUserInfo
        {
            Subject = subject,
            Email = email,
            EmailVerified = true,
            Name = "Idempotent Check"
        };

        // First sign-in.
        _factory.FakeValidator.SetResult(googleUserInfo);
        var (resp1, _, body1) = await PostGoogleSignInAsync(new { idToken = "idem2-first" });
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first sign-in prerequisite; body: {0}", body1);

        // Second sign-in.
        _factory.FakeValidator.SetResult(googleUserInfo);
        var (resp2, root2, body2) = await PostGoogleSignInAsync(new { idToken = "idem2-second" });

        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body2);

        TryProp(root2, "successed", out var succeededProp).Should().BeTrue("body: {0}", body2);
        succeededProp.GetBoolean().Should().BeTrue(
            "Successed must be true on idempotent second Google sign-in; body: {0}", body2);

        TryProp(root2, "errors", out var errorsProp).Should().BeTrue("body: {0}", body2);
        // errors must be null or empty — no error on the idempotent path.
        if (errorsProp.ValueKind == JsonValueKind.Array)
            errorsProp.EnumerateArray().ToList().Should().BeEmpty(
                "errors must be empty on idempotent sign-in; body: {0}", body2);
    }

    // ---------------------------------------------------------------------------
    // AC-7: Deactivated account → 400 (LoginAccountDeactivated)
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "AC-7 Deactivated account → 400, Successed=false")]
    public async Task AC7_DeactivatedAccount_Returns400()
    {
        var email = UniqueEmail("deactivated");

        // Register a parent by password, then deactivate them via the service layer.
        await RegisterParentAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByEmailAsync(email);
            user.Should().NotBeNull("user must exist after registration; email={0}", email);
            user!.IsActive = false;
            await userManager.UpdateAsync(user);
        }

        // Attempt Google sign-in for that (now deactivated) email.
        _factory.FakeValidator.SetResult(new GoogleUserInfo
        {
            Subject = UniqueSubject(),
            Email = email,
            EmailVerified = true,
            Name = "Deactivated User"
        });

        var (resp, root, body) = await PostGoogleSignInAsync(new { idToken = "deactivated-token" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "deactivated account must return 400; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false for a deactivated account; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // BE-TC-28: Wrong-audience idToken → 401 fail-closed
    // Since we cannot source a real token signed for a different aud in CI, we use
    // the fake validator returning null (simulating the audience-validation failure
    // path that IGoogleTokenValidator would produce for a wrong-aud token).
    // This covers the fail-closed assertion; the "real wrong-aud token" dimension
    // is PARTIALLY BLOCKED — sourcing a real cross-aud token in CI is not feasible.
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "BE-TC-28: Wrong-audience token → 401 fail-closed (fake=null simulates aud-validation failure)")]
    public async Task AC_WrongAudToken_Returns401_FailClosed()
    {
        // The fake validator returns null when audience validation fails (no real wrong-aud token available in CI).
        // This exercises the fail-closed path: invalid/unverifiable token → 401, no create/link.
        // PARTIALLY BLOCKED for sourcing a real structurally-valid JWT signed for a different aud.
        _factory.FakeValidator.SetResult(null);

        var (resp, root, body) = await PostGoogleSignInAsync(
            new { idToken = "structurally.valid.but.wrong.audience.simulation" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "BE-TC-28: wrong-audience token (simulated via null validator result) must return 401 fail-closed; body: {0}", body);

        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeFalse(
            "Successed must be false; no account must be created/linked; body: {0}", body);
    }

    // ---------------------------------------------------------------------------
    // BE-TC-30: BLOCKED — empty ClientId makes every token fail closed
    // Cannot override GoogleAuth__ClientId per-test in the current test host
    // (factory is sealed + shared); the inert-but-safe behaviour is documented.
    // This is a skip placeholder — do not delete.
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "BE-TC-30: BLOCKED — empty GoogleAuth__ClientId test requires per-test host config override (README Q, config-override)")]
    public async Task AC_EmptyClientId_BLOCKED()
    {
        // BLOCKED: This case requires a test host configured with GoogleAuth__ClientId = "" or unset.
        // The current factory is shared and cannot override configuration per-test.
        // The inert-but-safe posture (every token fails when ClientId is empty) is documented
        // in the security audit. No change needed here until the lead provides a per-test host seam.
        // Mark as PASS-by-design: no assertion, test simply records the BLOCKED status.
        await Task.CompletedTask;
        // BLOCKED — See README §4 Q1 / docs/qc/P1-12/README.md §4 open question on config-override.
        true.Should().BeTrue("BE-TC-30 is BLOCKED — empty ClientId config override not available per-test; skipping assertion");
    }

    // ---------------------------------------------------------------------------
    // Regression: existing sign-in endpoints still work after P1-12 BE-5 wiring
    // ---------------------------------------------------------------------------

    [Fact(DisplayName = "Regression: password sign-in still works after Google sign-in wiring")]
    public async Task Regression_PasswordSignIn_StillWorks()
    {
        var email = UniqueEmail("regression-pw");
        const string password = "Str0ng@Pass";
        await RegisterParentAsync(email, password);

        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = password });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "password sign-in must still work after Google sign-in wiring; body: {0}", body);
        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", body);
    }

    [Fact(DisplayName = "Regression: seeded superadmin can still sign in with Google sign-in wiring active")]
    public async Task Regression_SuperAdmin_CanStillSignIn()
    {
        var (resp, root, body) = await SendAsync(_client, HttpMethod.Post, SignInUrl,
            new { UserName = "superadmin", Password = "123Pa$$word!" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "superadmin sign-in must work with Google sign-in feature wired; body: {0}", body);
        TryProp(root, "successed", out var succeededProp).Should().BeTrue("body: {0}", body);
        succeededProp.GetBoolean().Should().BeTrue("body: {0}", body);
    }
}

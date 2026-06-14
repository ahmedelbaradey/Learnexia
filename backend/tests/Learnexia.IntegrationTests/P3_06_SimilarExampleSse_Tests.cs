using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence;

namespace Learnexia.IntegrationTests;

// ============================================================================
// P3-06 — SSE SimilarExample endpoint integration tests
//
// POST /api/AiTutor/SimilarExample  [Authorize(Roles="Student")]
//
// Pinned SSE wire contract (identical to P3-04 — no deviation; P3-12 FE consumes this):
//   event: message  data: {"content":"<approved text>"}           — content delivery
//   event: redirect data: {"type":"lesson","targetId":"<skillId>"}— no-context refuse-and-redirect
//   event: error    data: {"code":"…","message":"…"}             — safety/gateway/validation failure
//   event: done     data: [DONE]                                  — stream terminator (not on error)
//
// KEY DIFFERENCE from P3-05 Hint:
//   SimilarExample has NO hint-level preamble frame. Every event:message carries {"content":"…"}.
//
// Stubs:
//   ISafetyLayer             — StubSafetyLayer    (shared in namespace from P3_04_ExplainSse_Tests.cs)
//   ILearningContextProvider — StubContextProvider (shared from P3_04_ExplainSse_Tests.cs)
//   ILessonContextContract   — StubLessonContextContract (shared)
//
// Each test creates its own SimilarExampleSseTestFactory with a fresh AiTutorRateLimiter
// so stub state and rate-limit counters are fully isolated per test.
// DB migrations run once via the shared LearnexiaWebAppFactory fixture.
// ============================================================================

/// <summary>
/// Specialised WebApplicationFactory for P3-06 SimilarExample SSE tests.
/// Replaces <see cref="ISafetyLayer"/>, <see cref="ILearningContextProvider"/>,
/// and <see cref="ILessonContextContract"/> with configurable stubs, and resets
/// <see cref="AiTutorRateLimiter"/> so each test starts with a clean rate-limit window.
/// </summary>
public sealed class SimilarExampleSseTestFactory : WebApplicationFactory<Program>
{
    private readonly StubSafetyLayer _safetyStub;
    private readonly StubContextProvider _contextStub;
    private readonly string _postgresConnectionString;

    public SimilarExampleSseTestFactory(
        string postgresConnectionString,
        StubSafetyLayer safetyStub,
        StubContextProvider contextStub)
    {
        _postgresConnectionString = postgresConnectionString;
        _safetyStub               = safetyStub;
        _contextStub              = contextStub;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ----------------------------------------------------------------
            // Redirect all module DbContexts to the Testcontainers Postgres DB.
            // Without this, module DbContexts would point at production localhost.
            // ----------------------------------------------------------------
            ReplaceDbContext<IdentityModuleDbContext>(services, _postgresConnectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, _postgresConnectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, _postgresConnectionString, "learning");
            ReplaceDbContext<ParentDbContext>(services, _postgresConnectionString, "parent");
            ReplaceDbContext<GamificationDbContext>(services, _postgresConnectionString, "gamification");
            ReplaceDbContext<ModerationDbContext>(services, _postgresConnectionString, "moderation");

            // Swap ISafetyLayer with the configurable stub.
            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ => _safetyStub);

            // Swap ILearningContextProvider with the configurable stub.
            services.RemoveAll<ILearningContextProvider>();
            services.AddTransient<ILearningContextProvider>(_ => _contextStub);

            // Swap ILessonContextContract (not used directly by SimilarExample handler, but
            // the PromptBuilder may call it when SubjectId=0 needs enrichment — stub it to
            // avoid DB calls and ensure SubjectId=1=Math is always returned).
            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            // Reset the rate limiter so each test starts with a clean per-student window.
            // Removing the singleton and re-registering ensures the ConcurrentDictionary is empty.
            services.RemoveAll<AiTutorRateLimiter>();
            services.AddSingleton<AiTutorRateLimiter>();

            // Disable IP rate limiter (same pattern as LearnexiaWebAppFactory / SseTestFactory).
            services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new System.Collections.Generic.List<AspNetCoreRateLimit.RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(
                new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _postgresConnectionString
                });
        });
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

// ============================================================================
// Test class
// ============================================================================

/// <summary>
/// P3-06 integration tests for POST /api/AiTutor/SimilarExample (SSE endpoint).
///
/// Cases covered:
///   TC-1  Happy path: non-empty context + safety-allows → event:message {content} + event:done; no stack trace.
///   TC-2  Refuse-and-redirect: empty-context stub → event:redirect {type,targetId}; NO event:message.
///   TC-3  Safety-block: safety stub blocks → event:error {code,message}; no content leaked; no event:done.
///   TC-4  Provider-error: safety stub throws → event:error with typed payload; no raw stack trace.
///   TC-5  Validation: missing/0 SkillId → event:error {code:"ValidationError"}.
///   TC-6a Auth: no bearer → 401.
///   TC-6b Auth: non-Student JWT (Parent) → 403.
///   TC-7a WireFormat(happy): strict grammar — Content-Type, message shape, [DONE] terminator.
///   TC-7b WireFormat(redirect): exact redirect shape {type,targetId} + done; NO hint-level preamble.
///   TC-7c WireFormat(error): exact error shape {code,message}; no event:done.
///   TC-7d WireFormat: NO hint-level preamble in ANY event:message frame (P3-06 vs P3-05 contract delta).
///   TC-8  Rate limit: 11th request → event:error (rate-limited).
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_06_SimilarExampleSse_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string SimilarExampleUrl  = "api/AiTutor/SimilarExample";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _sharedFactory;
    private readonly HttpClient _setupClient; // for parent/child provisioning only

    public P3_06_SimilarExampleSse_Tests(LearnexiaWebAppFactory factory)
    {
        _sharedFactory = factory;
        _setupClient   = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _sharedFactory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string UniqueEmail(string tag = "")
        => $"p306_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// Case-insensitive JSON property lookup with PascalCase fallback.
    /// Mirrors the helper used in P3_04 and P3_05 tests.
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
        var bodyStr  = await response.Content.ReadAsStringAsync();
        JsonElement root = default;
        if (!string.IsNullOrWhiteSpace(bodyStr))
        {
            try { root = JsonDocument.Parse(bodyStr).RootElement; }
            catch { /* non-JSON body */ }
        }
        return (response, root, bodyStr);
    }

    private async Task<string> SignInAndGetTokenAsync(HttpClient client, string userName, string password)
    {
        var (resp, root, body) = await SendAsync(client, HttpMethod.Post, SignInUrl,
            new { UserName = userName, Password = password });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "sign-in must succeed; body: {0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("body: {0}", body);
        TryProp(data, "accessToken", out var token).Should().BeTrue("body: {0}", body);
        return token.GetString()!;
    }

    /// <summary>
    /// Registers a parent, adds a Student child, signs in as the child.
    /// Returns the Student JWT. Grade=3, Language=ar by default.
    /// </summary>
    private async Task<string> CreateStudentJwtAsync(string languageOverride = "ar")
    {
        var parentEmail = UniqueEmail("parent");
        var (prResp, prRoot, prBody) = await SendAsync(_setupClient, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", prBody);
        TryProp(prRoot, "data", out var prData).Should().BeTrue("body: {0}", prBody);
        TryProp(prData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", prBody);
        var parentToken = parentTokProp.GetString()!;

        var childEmail = UniqueEmail("child");
        var (addResp, _, addBody) = await SendAsync(_setupClient, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName         = "Test Student P306",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 3,
                Language         = languageOverride,
                Country          = "EG",
                LearningLanguage = languageOverride,
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");

        return await SignInAndGetTokenAsync(_setupClient, childEmail, ValidChildPassword);
    }

    /// <summary>Registers only a Parent and returns the Parent JWT (role = Parent, not Student).</summary>
    private async Task<string> CreateParentJwtAsync()
    {
        var parentEmail = UniqueEmail("parent_notstudent");
        var (prResp, prRoot, prBody) = await SendAsync(_setupClient, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body: {0}", prBody);
        TryProp(prRoot, "data", out var prData).Should().BeTrue("body: {0}", prBody);
        TryProp(prData, "accessToken", out var tok).Should().BeTrue("body: {0}", prBody);
        return tok.GetString()!;
    }

    /// <summary>Retrieves the Postgres connection string from the shared factory.</summary>
    private string GetSharedConnectionString()
    {
        using var scope = _sharedFactory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        return cfg["ConnectionStrings:Default"]!;
    }

    /// <summary>Builds a fresh SimilarExampleSseTestFactory using the shared Postgres container.</summary>
    private SimilarExampleSseTestFactory BuildFactory(StubSafetyLayer safety, StubContextProvider context)
        => new SimilarExampleSseTestFactory(GetSharedConnectionString(), safety, context);

    /// <summary>
    /// Parses the raw SSE body into a list of (eventName, data) pairs.
    /// SSE frame format: "event: {name}\ndata: {json}\n\n"
    /// </summary>
    private static List<(string Event, string Data)> ParseSseFrames(string rawBody)
    {
        var frames = new List<(string, string)>();
        var blocks = rawBody.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? eventName = null;
            string? data      = null;
            foreach (var line in lines)
            {
                if (line.StartsWith("event: "))
                    eventName = line["event: ".Length..].Trim();
                else if (line.StartsWith("data: "))
                    data = line["data: ".Length..].Trim();
            }
            if (eventName is not null && data is not null)
                frames.Add((eventName, data));
        }
        return frames;
    }

    /// <summary>
    /// Minimal valid SimilarExample body: SkillId > 0 (required by validator), QuestionId optional.
    /// </summary>
    private static object ValidBody(int skillId = 42, int? questionId = null)
        => new { SkillId = skillId, QuestionId = questionId };

    // =========================================================================
    // TC-1 — Happy path: non-empty context + safety-allows
    //        → event:message {content} + event:done; no stack trace.
    // =========================================================================

    [Fact(DisplayName = "TC-1 HappyPath: non-empty context + safety-allows → event:message {content} then event:done [DONE]; no stack trace")]
    public async Task TC1_HappyPath_NonEmptyContext_SafetyAllows_StreamsMessageAndDone()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync("ar");
        const string approvedContent = "مثال مشابه: إذا كان لديك 3 أجزاء من 4 فهذا يعني ¾.";

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = approvedContent };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 for an authorized Student; body: {0}", body);

        // Parse SSE frames
        var frames = ParseSseFrames(body);
        frames.Should().HaveCountGreaterOrEqualTo(2,
            "happy path must emit at least [event:message, event:done]; body: {0}", body);

        // event:message must be present
        var msgFrame = frames.FirstOrDefault(f => f.Event == "message");
        msgFrame.Should().NotBe(default,
            "a 'message' event frame must be present in the SSE stream; body: {0}", body);

        // event:message data must be JSON with a non-empty "content" key
        var msgJson = JsonDocument.Parse(msgFrame.Data).RootElement;
        TryProp(msgJson, "content", out var contentProp).Should().BeTrue(
            "event:message data must contain 'content' key; data: {0}", msgFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace(
            "event:message content must be non-empty text; body: {0}", body);

        // event:done must be present and carry the [DONE] terminator
        var doneFrame = frames.FirstOrDefault(f => f.Event == "done");
        doneFrame.Should().NotBe(default,
            "event:done must terminate the happy-path stream; body: {0}", body);
        doneFrame.Data.Should().Be("[DONE]",
            "event:done data must be exactly '[DONE]'; body: {0}", body);

        // No stack trace in any frame — child-safe requirement
        body.Should().NotContain("StackTrace",
            "stack traces must never appear in SSE responses to students; body: {0}", body);
        body.Should().NotContain("at Learnexia",
            "stack trace lines must not appear in the response; body: {0}", body);

        // No error or redirect in happy path
        frames.Should().NotContain(f => f.Event == "error",
            "no error frame in happy path; body: {0}", body);
        frames.Should().NotContain(f => f.Event == "redirect",
            "no redirect frame in happy path; body: {0}", body);
    }

    // =========================================================================
    // TC-2 — Refuse-and-redirect: empty-context stub
    //        → event:redirect {type:"lesson",targetId}; NO event:message.
    //        HelpDeclined instrumentation event emitted (AC-7, AC-8).
    // =========================================================================

    [Fact(DisplayName = "TC-2 RefuseAndRedirect: empty context → event:redirect {type:lesson,targetId}; NO event:message (AC-7, HelpDeclined)")]
    public async Task TC2_EmptyContext_EmitsRedirect_NoMessageFrame()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed }; // must NOT be called
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act — SkillId=7 is the active skill; empty context triggers refuse-and-redirect
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody(skillId: 7)), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 even on redirect (in-band); body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:redirect must be present
        var redirectFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        redirectFrame.Should().NotBe(default,
            "empty context must produce event:redirect (AC-7); body: {0}", body);

        // Redirect data must be JSON with type="lesson" and a targetId string
        var rdJson = JsonDocument.Parse(redirectFrame.Data).RootElement;
        TryProp(rdJson, "type", out var typeProp).Should().BeTrue(
            "redirect data must contain 'type'; data: {0}", redirectFrame.Data);
        typeProp.GetString().Should().Be("lesson",
            "redirect type must be 'lesson' per pinned wire contract; data: {0}", redirectFrame.Data);
        TryProp(rdJson, "targetId", out var targetIdProp).Should().BeTrue(
            "redirect data must contain 'targetId'; data: {0}", redirectFrame.Data);
        targetIdProp.ValueKind.Should().Be(JsonValueKind.String,
            "targetId must be a string per pinned wire contract; data: {0}", redirectFrame.Data);

        // NO event:message — safety/LLM must not be called (AC-7 hard criterion)
        frames.Should().NotContain(f => f.Event == "message",
            "LLM must NOT be called when context is empty (AC-7); body: {0}", body);

        // event:done must follow redirect (controller emits done after redirect case)
        frames.Any(f => f.Event == "done").Should().BeTrue(
            "event:done must follow event:redirect per controller implementation; body: {0}", body);

        // No stack trace
        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-3 — Safety-block: safety stub blocks
    //        → event:error {code,message}; no content leaked; no event:done.
    // =========================================================================

    [Fact(DisplayName = "TC-3 SafetyBlock: ISafetyLayer.Blocked → event:error {code,message}; NO event:message; NO event:done")]
    public async Task TC3_SafetyBlock_EmitsError_NoContentLeaks_NoDone()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — HTTP 200 (error is in-band via SSE)
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 even when safety blocks — error is in-band; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error must be present
        var errorFrame = frames.FirstOrDefault(f => f.Event == "error");
        errorFrame.Should().NotBe(default,
            "safety block must produce event:error; body: {0}", body);

        // event:error data must be JSON with 'code' and 'message' keys
        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue(
            "event:error data must contain 'code'; data: {0}", errorFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue(
            "event:error data must contain 'message'; data: {0}", errorFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace(
            "error code must be non-empty; body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace(
            "error message must be non-empty; body: {0}", body);

        // NO event:message — blocked content must NOT be streamed
        frames.Should().NotContain(f => f.Event == "message",
            "blocked content must NOT be streamed as event:message; body: {0}", body);

        // NO event:done on error (wire contract)
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after a safety-block error (wire contract); body: {0}", body);

        // No stack trace (child-safe endpoint)
        body.Should().NotContain("StackTrace", "body: {0}", body);
        body.Should().NotContain("at Learnexia", "body: {0}", body);
    }

    // =========================================================================
    // TC-4 — Provider-error: safety stub throws
    //        → event:error with a gentle typed payload; no raw stack trace.
    // =========================================================================

    [Fact(DisplayName = "TC-4 ProviderError: ISafetyLayer throws → event:error {code,message}; no raw stack trace (AC-9)")]
    public async Task TC4_ProviderError_EmitsTypedError_NoStackTrace()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.ThrowOnCall };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200; errors are in-band via event:error; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error must be present
        var errorFrame = frames.FirstOrDefault(f => f.Event == "error");
        errorFrame.Should().NotBe(default,
            "provider exception must yield event:error (gentle typed payload); body: {0}", body);

        // event:error data must have code and message
        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue(
            "event:error data must have 'code'; data: {0}", errorFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue(
            "event:error data must have 'message'; data: {0}", errorFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace(
            "error code must be non-empty; body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace(
            "error message must be non-empty; body: {0}", body);

        // CRITICAL: no raw stack trace must reach the student (child-safe endpoint)
        body.Should().NotContain("StackTrace",
            "raw stack traces must not reach the SSE stream (child-safe — AC-9); body: {0}", body);
        body.Should().NotContain("at Learnexia",
            "stack trace lines must not appear in student responses; body: {0}", body);
        // The provider's exception message must not leak either
        body.Should().NotContain("Simulated provider failure",
            "internal exception messages must not leak to the student; body: {0}", body);

        // No content or done on error
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame should be emitted on provider error; body: {0}", body);
        frames.Should().NotContain(f => f.Event == "done",
            "no event:done on error; body: {0}", body);
    }

    // =========================================================================
    // TC-5 — Validation: missing/0 SkillId
    //        → event:error {code:"ValidationError"} (in-band for SSE).
    //        The SSE contract returns 200 with the error in-band, not HTTP 422.
    // =========================================================================

    [Fact(DisplayName = "TC-5a Validation: SkillId=0 → event:error {code:ValidationError} (in-band for SSE)")]
    public async Task TC5a_Validation_SkillIdZero_EmitsValidationError()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // SkillId=0 violates the GreaterThan(0) validator rule
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { SkillId = 0, QuestionId = (int?)null }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // SSE returns 200; the validation error is surfaced as event:error
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200; validation error is in-band via event:error; body: {0}", body);

        var frames = ParseSseFrames(body);

        var errorFrame = frames.FirstOrDefault(f => f.Event == "error");
        errorFrame.Should().NotBe(default,
            "SkillId=0 must surface as event:error (ValidationBehavior fires); body: {0}", body);

        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue(
            "error frame must have 'code'; data: {0}", errorFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue(
            "error frame must have 'message'; data: {0}", errorFrame.Data);

        // Must be ValidationError — not UnhandledError — so P3-12 FE can distinguish
        codeProp.GetString().Should().Be("ValidationError",
            "SkillId=0 must surface as code=ValidationError, not UnhandledError; data: {0}", errorFrame.Data);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace(
            "validation error message must be non-empty; body: {0}", body);

        // LLM must not be called
        frames.Should().NotContain(f => f.Event == "message",
            "LLM must not be called on invalid input; body: {0}", body);
    }

    [Fact(DisplayName = "TC-5b Validation: SkillId missing (negative) → event:error {code:ValidationError} (in-band)")]
    public async Task TC5b_Validation_SkillIdNegative_EmitsValidationError()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // SkillId=-1 also violates GreaterThan(0)
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { SkillId = -1, QuestionId = (int?)null }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200; validation error is in-band; body: {0}", body);

        var frames = ParseSseFrames(body);
        var errorFrame = frames.FirstOrDefault(f => f.Event == "error");
        errorFrame.Should().NotBe(default,
            "SkillId=-1 must surface as event:error; body: {0}", body);

        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue("body: {0}", body);
        codeProp.GetString().Should().Be("ValidationError",
            "SkillId=-1 must produce code=ValidationError; data: {0}", errorFrame.Data);

        frames.Should().NotContain(f => f.Event == "message", "body: {0}", body);
    }

    // =========================================================================
    // TC-6a — Auth: no bearer → 401
    // =========================================================================

    [Fact(DisplayName = "TC-6a Auth: no bearer token → 401")]
    public async Task TC6a_NoBearerToken_Returns401()
    {
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
        };
        // No Authorization header

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no bearer → 401 (endpoint is [Authorize(Roles=\"Student\")]); body: {0}", body);
    }

    // =========================================================================
    // TC-6b — Auth: Parent-role JWT (non-Student) → 403
    // =========================================================================

    [Fact(DisplayName = "TC-6b Auth: Parent-role JWT (non-Student) → 403")]
    public async Task TC6b_ParentJwt_Returns403()
    {
        var parentToken = await CreateParentJwtAsync();

        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent-role token on Student-only endpoint → 403; body: {0}", body);
    }

    // =========================================================================
    // TC-7a — Wire format (happy path): strict assertion of event grammar
    //          (P3-12 FE contract check).
    //          SimilarExample has NO hint-level preamble (unlike Hint / P3-05).
    // =========================================================================

    [Fact(DisplayName = "TC-7a WireFormat(happy): Content-Type=text/event-stream; message {content}; [DONE]; NO hint-level preamble")]
    public async Task TC7a_WireFormat_HappyPath_ExactFrameGrammar_NoPreamble()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        const string approvedContent = "مثال: 2/3 تعني جزءين من ثلاثة أجزاء.";

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = approvedContent };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody(skillId: 42)), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // --- STRICT WIRE CONTRACT ASSERTIONS ---

        // 1. Content-Type must be text/event-stream
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must send Content-Type: text/event-stream; body: {0}", rawBody);

        var frames = ParseSseFrames(rawBody);
        frames.Should().HaveCountGreaterOrEqualTo(2,
            "happy path must emit at least [message, done]; body: {0}", rawBody);

        // 2. Exactly ONE event:message (no hint-level preamble — this is the P3-06 vs P3-05 delta)
        var msgFrames = frames.Where(f => f.Event == "message").ToList();
        msgFrames.Should().HaveCount(1,
            "SimilarExample must emit exactly 1 event:message (content only — NO hint-level preamble); body: {0}", rawBody);

        // 3. event:message data shape: {"content":"…"} — no hintLevel/nextHintLevel keys
        var msgFrame = msgFrames[0];
        var msgObj   = JsonDocument.Parse(msgFrame.Data).RootElement;
        msgObj.ValueKind.Should().Be(JsonValueKind.Object,
            "event:message data must be a JSON object; data: {0}", msgFrame.Data);
        TryProp(msgObj, "content", out var cProp).Should().BeTrue(
            "event:message must have 'content' property; data: {0}", msgFrame.Data);
        cProp.ValueKind.Should().Be(JsonValueKind.String,
            "event:message content must be a string; data: {0}", msgFrame.Data);
        cProp.GetString().Should().Be(approvedContent,
            "content must match the safety-stub approved text; body: {0}", rawBody);

        // 4. NO hint-level preamble fields in ANY event:message frame
        TryProp(msgObj, "hintLevel", out _).Should().BeFalse(
            "SimilarExample event:message must NOT have 'hintLevel' — this is NOT a Hint endpoint (wire contract delta vs P3-05); data: {0}", msgFrame.Data);
        TryProp(msgObj, "nextHintLevel", out _).Should().BeFalse(
            "SimilarExample event:message must NOT have 'nextHintLevel'; data: {0}", msgFrame.Data);

        // 5. event:done — data is exactly "[DONE]" (not JSON-wrapped)
        var doneFrame = frames.Last(f => f.Event == "done");
        doneFrame.Data.Should().Be("[DONE]",
            "event:done data must be the literal string '[DONE]' (not JSON-wrapped); body: {0}", rawBody);

        // 6. No unexpected event types in happy path
        frames.Should().NotContain(f => f.Event == "redirect",
            "no redirect frame in happy path; body: {0}", rawBody);
        frames.Should().NotContain(f => f.Event == "error",
            "no error frame in happy path; body: {0}", rawBody);
    }

    // =========================================================================
    // TC-7b — Wire format (redirect): exact redirect shape {type,targetId} + done.
    // =========================================================================

    [Fact(DisplayName = "TC-7b WireFormat(redirect): exact shape {type:lesson,targetId} + event:done; NO event:message")]
    public async Task TC7b_WireFormat_Redirect_ExactFrameGrammar()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act — SkillId=99 is the anchor; empty context triggers redirect; targetId must be "99"
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody(skillId: 99)), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        var frames = ParseSseFrames(rawBody);

        // event:redirect shape: {"type":"lesson","targetId":"99"}
        var rdFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        rdFrame.Should().NotBe(default,
            "redirect frame must be present; body: {0}", rawBody);
        var rdObj = JsonDocument.Parse(rdFrame.Data).RootElement;
        rdObj.ValueKind.Should().Be(JsonValueKind.Object,
            "redirect data must be a JSON object; data: {0}", rdFrame.Data);
        TryProp(rdObj, "type", out var tProp).Should().BeTrue(
            "redirect must have 'type'; data: {0}", rdFrame.Data);
        tProp.GetString().Should().Be("lesson",
            "type must be 'lesson' per pinned wire contract; data: {0}", rdFrame.Data);
        TryProp(rdObj, "targetId", out var tidProp).Should().BeTrue(
            "redirect must have 'targetId'; data: {0}", rdFrame.Data);
        tidProp.ValueKind.Should().Be(JsonValueKind.String,
            "targetId must be a string per pinned wire contract; data: {0}", rdFrame.Data);
        // targetId = SkillId.ToString() — controller uses redirect.TargetSkillId?.ToString()
        tidProp.GetString().Should().Be("99",
            "targetId must equal the SkillId from the request (99); data: {0}", rdFrame.Data);

        // event:done follows redirect
        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue(
            "event:done [DONE] must follow event:redirect; body: {0}", rawBody);

        // No content
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame on redirect (LLM not called); body: {0}", rawBody);
    }

    // =========================================================================
    // TC-7c — Wire format (error): exact error shape {code,message}; no event:done.
    // =========================================================================

    [Fact(DisplayName = "TC-7c WireFormat(error): exact shape {code:string,message:string}; NO event:done after error")]
    public async Task TC7c_WireFormat_Error_ExactFrameGrammar_NoDone()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        var frames = ParseSseFrames(rawBody);

        // event:error shape: {"code":"…","message":"…"}
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "event:error must be present on safety-block; body: {0}", rawBody);
        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        errObj.ValueKind.Should().Be(JsonValueKind.Object,
            "error data must be a JSON object; data: {0}", errFrame.Data);
        TryProp(errObj, "code", out var cProp).Should().BeTrue(
            "error must have 'code'; data: {0}", errFrame.Data);
        TryProp(errObj, "message", out var mProp).Should().BeTrue(
            "error must have 'message'; data: {0}", errFrame.Data);
        cProp.ValueKind.Should().Be(JsonValueKind.String,
            "code must be a string; data: {0}", errFrame.Data);
        mProp.ValueKind.Should().Be(JsonValueKind.String,
            "message must be a string; data: {0}", errFrame.Data);
        cProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", rawBody);
        mProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", rawBody);

        // NO event:done on error (wire contract: event:done not emitted after error)
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after error (wire contract violation if present); body: {0}", rawBody);

        // NO event:message (no content on safety-block)
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame on safety-block; body: {0}", rawBody);
    }

    // =========================================================================
    // TC-7d — Wire format: strict NO hint-level preamble assertion
    //          (critical P3-06 vs P3-05 contract delta — P3-12 FE reads this).
    //          SimilarExample differs from Hint: it NEVER emits {hintLevel,nextHintLevel}.
    // =========================================================================

    [Fact(DisplayName = "TC-7d WireFormat(NoHintLevelPreamble): ALL event:message frames lack hintLevel/nextHintLevel (P3-06 vs P3-05 wire contract delta)")]
    public async Task TC7d_WireFormat_NoHintLevelPreamble_InAnyMessageFrame()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "مثال مشابه للمفهوم." };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidBody(skillId: 55, questionId: 10)),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        var frames = ParseSseFrames(rawBody);

        // All event:message frames must NOT contain hintLevel or nextHintLevel
        var msgFrames = frames.Where(f => f.Event == "message").ToList();
        msgFrames.Should().NotBeEmpty(
            "at least one event:message frame must exist in happy path; body: {0}", rawBody);

        foreach (var mf in msgFrames)
        {
            var mfObj = JsonDocument.Parse(mf.Data).RootElement;
            TryProp(mfObj, "hintLevel", out _).Should().BeFalse(
                "SimilarExample must NEVER emit 'hintLevel' in any event:message frame " +
                "(wire contract delta vs P3-05 Hint — P3-12 FE relies on this distinction); " +
                $"data: {mf.Data}");
            TryProp(mfObj, "nextHintLevel", out _).Should().BeFalse(
                "SimilarExample must NEVER emit 'nextHintLevel' in any event:message frame; " +
                $"data: {mf.Data}");
        }

        // Sanity: event:done present
        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue(
            "event:done [DONE] must be present; body: {0}", rawBody);
    }

    // =========================================================================
    // TC-8 — Rate limit: exceed per-student window → in-band event:error.
    //         AiTutorRateLimiter allows 10 requests per 1-minute window per student.
    //         The 11th request must be rejected with event:error (rate-limited).
    // =========================================================================

    [Fact(DisplayName = "TC-8 RateLimit: 11th request from same student within 1-minute window → event:error (rate-limited; in-band)")]
    public async Task TC8_RateLimit_ExceedWindow_ReturnsRateLimitedError()
    {
        // Arrange: single student JWT for all 11 requests.
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildFactory(safety, context);
        var client = factory.CreateClient();

        // MaxRequestsPerWindow = 10 (from AiTutorRateLimiter source).
        // Requests 1-10: must succeed (event:message + event:done).
        // Request 11:    must be rate-limited (event:error, no event:message).

        string? lastBody   = null;
        List<(string Event, string Data)>? lastFrames = null;

        for (int i = 1; i <= 11; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, SimilarExampleUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(ValidBody()), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

            var resp = await client.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (i == 11)
            {
                lastBody   = body;
                lastFrames = ParseSseFrames(body);
            }
        }

        // The 11th request must be rate-limited.
        lastFrames.Should().NotBeNull(
            "11th request must have produced a response; body: {0}", lastBody);
        lastFrames!.Should().NotContain(f => f.Event == "message",
            "11th request must be rate-limited (no content frame); body: {0}", lastBody);

        // The rate limiter returns SimilarExampleResult.Error → event:error (in-band).
        var hasError = lastFrames.Any(f => f.Event == "error");
        hasError.Should().BeTrue(
            "rate-limited 11th request must produce event:error (in-band SSE — not HTTP 429); body: {0}", lastBody);

        var errFrame = lastFrames.First(f => f.Event == "error");
        var errJson  = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue(
            "rate-limit error must have 'code'; data: {0}", errFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue(
            "rate-limit error must have 'message'; data: {0}", errFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace(
            "rate-limit error code must be non-empty; body: {0}", lastBody);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace(
            "rate-limit error message must be non-empty; body: {0}", lastBody);

        // No stack trace in rate-limit error
        lastBody.Should().NotContain("StackTrace", "body: {0}", lastBody);
    }
}

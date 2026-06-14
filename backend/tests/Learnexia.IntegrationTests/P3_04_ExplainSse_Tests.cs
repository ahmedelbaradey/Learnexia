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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Learnexia.IntegrationTests;

// ============================================================================
// P3-04 — SSE Explain endpoint integration tests
//
// POST /api/AiTutor/Explain  [Authorize(Roles="Student")]
//
// Wire contract (pinned — do NOT deviate; P3-12 FE consumes this):
//   event: message  data: {"content":"<text>"}          — approved content chunk
//   event: redirect data: {"type":"lesson","targetId":"…"} — no-context refuse-and-redirect
//   event: error    data: {"code":"…","message":"…"}    — safety/gateway failure
//   event: done     data: [DONE]                         — stream terminator (not on error)
//
// Test-host stubs:
//   ISafetyLayer           — StubSafetyLayer:   configurable Allowed/Blocked/throw
//   ILearningContextProvider — StubContextProvider: configurable non-empty/empty Chunks
//
// Each test case derives a fresh factory so stub state is clean and independent
// of the shared LearnexiaWebAppFactory instance (which owns DB migrations).
// DB migrations run once via the shared factory (collection fixture); the SSE
// tests then build per-test factories that inherit the same Postgres container
// connection string via appsettings override but swap in the stubs.
// ============================================================================

/// <summary>
/// Specialized WebApplicationFactory that replaces ISafetyLayer and ILearningContextProvider
/// with test stubs. Each test creates its own instance so stub state is fully isolated.
/// </summary>
public sealed class SseTestFactory : WebApplicationFactory<Program>
{
    private readonly StubSafetyLayer _safetyStub;
    private readonly StubContextProvider _contextStub;
    private readonly string _postgresConnectionString;

    public SseTestFactory(
        string postgresConnectionString,
        StubSafetyLayer safetyStub,
        StubContextProvider contextStub)
    {
        _postgresConnectionString = postgresConnectionString;
        _safetyStub = safetyStub;
        _contextStub = contextStub;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace ISafetyLayer with the configurable stub.
            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ => _safetyStub);

            // Replace ILearningContextProvider with the configurable stub.
            services.RemoveAll<ILearningContextProvider>();
            services.AddTransient<ILearningContextProvider>(_ => _contextStub);

            // Replace ILessonContextContract with a stub that returns Subject.Math (1) for any lesson id.
            // This ensures the PromptBuilder finds a valid template (subjectId=0 → UnsupportedSubject).
            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            // Reset the rate limiter to ensure each test starts with a clean window.
            // Remove the singleton and re-register so the counter dictionary is empty.
            services.RemoveAll<AiTutorRateLimiter>();
            services.AddSingleton<AiTutorRateLimiter>();

            // Disable IP rate limiter (same pattern as LearnexiaWebAppFactory).
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
}

// ============================================================================
// Stubs
// ============================================================================

/// <summary>
/// Configurable safety layer stub. Three modes:
///   Allowed     → returns SafeAiResult(Allowed=true, Content=configured text)
///   Blocked     → returns SafeAiResult(Allowed=false, Verdict=Blocked)
///   ThrowOnCall → throws InvalidOperationException to simulate provider error
/// </summary>
public sealed class StubSafetyLayer : ISafetyLayer
{
    public enum Mode { Allowed, Blocked, ThrowOnCall }

    public Mode Behavior { get; set; } = Mode.Allowed;
    public string AllowedContent { get; set; } = "الكسر هو جزء من الكل.";

    public Task<SafeAiResult> GenerateSafeAsync(AiRequest request, CancellationToken ct = default)
    {
        if (Behavior == Mode.ThrowOnCall)
            throw new InvalidOperationException("Simulated provider failure");

        if (Behavior == Mode.Blocked)
        {
            return Task.FromResult(new SafeAiResult(
                Allowed: false,
                Content: "عذرًا، لا أستطيع الإجابة على هذا السؤال.",
                Verdict: SafetyVerdict.Blocked,
                Results: Array.Empty<CheckResult>()));
        }

        // Allowed
        return Task.FromResult(new SafeAiResult(
            Allowed: true,
            Content: AllowedContent,
            Verdict: SafetyVerdict.Allowed,
            Results: Array.Empty<CheckResult>(),
            Confidence: 0.95m));
    }
}

/// <summary>
/// Configurable learning context stub.
///   NonEmpty  → returns a LearningContext with one curriculum chunk.
///   Empty     → returns a LearningContext with empty Chunks (triggers refuse-and-redirect).
/// </summary>
public sealed class StubContextProvider : ILearningContextProvider
{
    public enum Mode { NonEmpty, Empty }

    public Mode Behavior { get; set; } = Mode.NonEmpty;

    public Task<LearningContext> GetContextAsync(
        int studentId,
        int skillId,
        int? questionId,
        string? wrongAnswer,
        CancellationToken ct = default)
    {
        IReadOnlyList<ChunkDto> chunks = Behavior == Mode.NonEmpty
            ? new[] { new ChunkDto("chunk-1", "الكسر العادي يمثل جزءًا من الكل.") }
            : Array.Empty<ChunkDto>();

        return Task.FromResult(new LearningContext(
            Chunks: chunks,
            QuestionText: "ما هو الكسر؟",
            WrongAnswer: null,
            SkillId: skillId == 0 ? 42 : skillId,
            QuestionId: questionId,
            GradeId: 3,
            SubjectId: 1,
            Language: TutorLanguage.Ar));
    }
}

/// <summary>
/// Stub ILessonContextContract that returns a fixed LessonContextDto for any lesson id > 0.
/// SubjectId=1 (Math) ensures the PromptBuilder finds a valid template.
/// Returns null for lessonId=0 (no lesson enrichment — handler falls back to subjectId=0
/// which is an UnsupportedSubject; but we never pass lessonId=0 in happy-path tests).
/// </summary>
public sealed class StubLessonContextContract : ILessonContextContract
{
    public Task<LessonContextDto?> GetLessonContextAsync(int lessonId, CancellationToken ct = default)
    {
        if (lessonId <= 0)
            return Task.FromResult<LessonContextDto?>(null);

        return Task.FromResult<LessonContextDto?>(new LessonContextDto(
            LessonId: lessonId,
            Title: "الكسور العادية",
            SubjectId: 1,   // Subject.Math = 1 — registered in TemplateSelector
            GradeId: 3));
    }
}

// ============================================================================
// Test class
// ============================================================================

/// <summary>
/// P3-04 integration tests for POST /api/AiTutor/Explain (SSE endpoint).
///
/// Each test:
///   1. Spins up a fresh SseTestFactory with appropriate stub configuration.
///   2. Obtains a Student JWT via the parent → add-child → sign-in flow
///      (the shared DB from the collection fixture already has migrations applied).
///   3. POSTs to the SSE endpoint with a valid command body.
///   4. Reads the raw SSE response body and parses frames.
///   5. Asserts frame grammar and JSON payload shapes.
///
/// SSE buffering note:
///   ASP.NET Core's TestServer buffers the response stream in-memory by default.
///   WebApplicationFactory.CreateClient() returns an HttpClient that reads the
///   complete body after the handler completes, so all frames are available in
///   the response string. No streaming workaround is needed — the controller
///   writes all frames synchronously before returning, making the full body
///   available as a single string after await ReadAsStringAsync().
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_04_ExplainSse_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string ExplainUrl          = "api/AiTutor/Explain";
    private const string RegisterParentUrl   = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl           = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl         = "api/Parent/Add-Child";
    private const string ValidChildPassword  = "Child@Pass1";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _sharedFactory;
    private readonly HttpClient _setupClient; // for parent/child provisioning only

    public P3_04_ExplainSse_Tests(LearnexiaWebAppFactory factory)
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
        => $"p304_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
    /// Registers a parent, adds a child (Student-role), signs in as the child.
    /// Returns the Student JWT. Uses the setup client that points at the shared DB.
    /// The token's claims include Grade=3, Language=ar from the Add-Child defaults.
    /// </summary>
    private async Task<string> CreateStudentJwtAsync(string languageOverride = "ar")
    {
        var parentEmail = UniqueEmail("parent");
        var (prResp, prRoot, prBody) = await SendAsync(_setupClient, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", prBody);
        TryProp(prRoot, "data", out var prData).Should().BeTrue("body: {0}", prBody);
        TryProp(prData, "accessToken", out var parentTok).Should().BeTrue("body: {0}", prBody);
        var parentToken = parentTok.GetString()!;

        var childEmail = UniqueEmail("child");
        var (addResp, _, addBody) = await SendAsync(_setupClient, HttpMethod.Post, AddChildUrl,
            new
            {
                FullName = "Test Student",
                Email    = childEmail,
                Password = ValidChildPassword,
                Grade    = 3,
                Language = languageOverride,
                Country  = "EG",
                LearningLanguage = languageOverride,
            },
            parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 },
            $"Add-Child must succeed; body: {addBody}");

        return await SignInAndGetTokenAsync(_setupClient, childEmail, ValidChildPassword);
    }

    /// <summary>
    /// Retrieves the Postgres connection string from the shared factory so SseTestFactory
    /// can point at the same container (no separate DB spin-up).
    /// </summary>
    private string GetSharedConnectionString()
    {
        using var scope = _sharedFactory.Services.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        return cfg["ConnectionStrings:Default"]!;
    }

    /// <summary>
    /// Builds a fresh SseTestFactory using the shared Postgres container.
    /// </summary>
    private SseTestFactory BuildSseFactory(StubSafetyLayer safety, StubContextProvider context)
        => new SseTestFactory(GetSharedConnectionString(), safety, context);

    /// <summary>
    /// Parses the raw SSE body into a list of (eventName, data) pairs.
    /// SSE frame format used by ExplainController:
    ///   "event: {name}\ndata: {json}\n\n"
    /// </summary>
    private static List<(string Event, string Data)> ParseSseFrames(string rawBody)
    {
        var frames = new List<(string, string)>();
        // Split on the double newline that separates frames.
        var blocks = rawBody.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? eventName = null;
            string? data = null;
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
    /// Minimal valid command: SkillId=42 + LessonId=1 satisfies the validator's "at least one anchor" rule.
    /// LessonId=1 causes the handler to call ILessonContextContract (stubbed to return SubjectId=1=Math),
    /// which is required so the PromptBuilder finds a valid template (subjectId=0 → UnsupportedSubject).
    /// </summary>
    private static object ValidExplainBody(int skillId = 42, int lessonId = 1)
        => new { SkillId = skillId, LessonId = lessonId, ConceptId = (int?)null, Question = (string?)null };

    // =========================================================================
    // TC-1 — Happy path (Arabic student, safety allows)
    // =========================================================================

    [Fact(DisplayName = "TC-1 HappyPath(ar): Student JWT + non-empty context + safety-allows → event:message + event:done; no stack trace")]
    public async Task TC1_HappyPath_Arabic_StreamsMessageAndDone()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync("ar");
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the SSE endpoint returns 200 for an authorized student; body: {0}", body);

        // Assert SSE frames
        var frames = ParseSseFrames(body);
        frames.Should().HaveCountGreaterOrEqualTo(2,
            "happy path must emit at least event:message and event:done; body: {0}", body);

        var messageFrame = frames.FirstOrDefault(f => f.Event == "message");
        messageFrame.Should().NotBe(default,
            "a 'message' event frame must be present in the SSE stream; body: {0}", body);

        // event:message data must be JSON with a non-empty "content" key
        var msgJson = JsonDocument.Parse(messageFrame.Data).RootElement;
        TryProp(msgJson, "content", out var contentProp).Should().BeTrue(
            "event:message data must contain 'content' key; data: {0}", messageFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace(
            "event:message content must be non-empty text; body: {0}", body);

        // event:done must be present and carry the [DONE] terminator
        var doneFrame = frames.FirstOrDefault(f => f.Event == "done");
        doneFrame.Should().NotBe(default,
            "a 'done' event frame must terminate the happy-path stream; body: {0}", body);
        doneFrame.Data.Should().Be("[DONE]",
            "event:done data must be exactly '[DONE]'; body: {0}", body);

        // No stack trace must appear in any frame
        body.Should().NotContain("StackTrace",
            "stack traces must never appear in SSE responses to students; body: {0}", body);
        body.Should().NotContain("at Learnexia",
            "stack trace lines must not appear in the response; body: {0}", body);
    }

    // =========================================================================
    // TC-2 — Happy path (English student)
    // =========================================================================

    [Fact(DisplayName = "TC-2 HappyPath(en): English-language student JWT → same frame grammar (message + done)")]
    public async Task TC2_HappyPath_English_StreamsMessageAndDone()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync("en");
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "A fraction is a part of a whole." };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var frames = ParseSseFrames(body);
        frames.Any(f => f.Event == "message").Should().BeTrue(
            "English student must also receive event:message frame; body: {0}", body);
        frames.Any(f => f.Event == "done").Should().BeTrue(
            "English student must receive event:done terminator; body: {0}", body);

        var msgFrame = frames.First(f => f.Event == "message");
        var msgJson  = JsonDocument.Parse(msgFrame.Data).RootElement;
        TryProp(msgJson, "content", out var contentProp).Should().BeTrue("data: {0}", msgFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace("content must be non-empty; body: {0}", body);

        var doneFrame = frames.First(f => f.Event == "done");
        doneFrame.Data.Should().Be("[DONE]", "body: {0}", body);
    }

    // =========================================================================
    // TC-3 — Safety block: no content leaks, event:error emitted
    // =========================================================================

    [Fact(DisplayName = "TC-3 SafetyBlock: blocked response → event:error with code+message; NO event:message with blocked text")]
    public async Task TC3_SafetyBlock_EmitsError_NoContentLeaks()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — must be HTTP 200 (the SSE protocol itself succeeded; the error is in-band)
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
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("code must be non-empty; body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("message must be non-empty; body: {0}", body);

        // No event:message frame must carry the blocked content
        frames.Should().NotContain(f => f.Event == "message",
            "blocked content must NOT be streamed as event:message; body: {0}", body);

        // No event:done on error (per wire contract)
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after a safety-block error; body: {0}", body);

        // No stack trace
        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-4 — Empty retrieval (refuse-and-redirect)
    // =========================================================================

    [Fact(DisplayName = "TC-4 RefuseAndRedirect: empty context → event:redirect with type+targetId; NO LLM/message frame")]
    public async Task TC4_EmptyContext_EmitsRedirect_NoMessageFrame()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed }; // should NOT be called
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act — SkillId=7 provides the context anchor (validator passes); no LessonId needed for redirect path
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { SkillId = 7, LessonId = (int?)null, ConceptId = (int?)null, Question = (string?)null }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:redirect must be present
        var redirectFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        redirectFrame.Should().NotBe(default,
            "empty context must produce event:redirect; body: {0}", body);

        // Redirect data must be JSON with type="lesson" and a targetId key
        var rdJson = JsonDocument.Parse(redirectFrame.Data).RootElement;
        TryProp(rdJson, "type", out var typeProp).Should().BeTrue(
            "redirect data must contain 'type'; data: {0}", redirectFrame.Data);
        typeProp.GetString().Should().Be("lesson",
            "redirect type must be 'lesson' per wire contract; data: {0}", redirectFrame.Data);
        TryProp(rdJson, "targetId", out var targetIdProp).Should().BeTrue(
            "redirect data must contain 'targetId'; data: {0}", redirectFrame.Data);
        // targetId is a string (skillId.ToString() from the controller)
        targetIdProp.ValueKind.Should().Be(JsonValueKind.String,
            "targetId must be a string per pinned wire contract; data: {0}", redirectFrame.Data);

        // NO event:message (safety/LLM was not called)
        frames.Should().NotContain(f => f.Event == "message",
            "LLM must NOT be called when context is empty — refuse-and-redirect only; body: {0}", body);

        // event:done MUST be present after redirect (controller emits done on redirect)
        frames.Any(f => f.Event == "done").Should().BeTrue(
            "event:done must follow event:redirect per controller implementation; body: {0}", body);

        // No stack trace
        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-5 — Provider error (safety stub throws)
    // =========================================================================

    [Fact(DisplayName = "TC-5 ProviderError: ISafetyLayer throws → event:error with typed payload; no raw stack trace")]
    public async Task TC5_ProviderError_EmitsTypedError_NoStackTrace()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.ThrowOnCall };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
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
            "provider exception must yield event:error; body: {0}", body);

        // event:error data must have code and message
        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue(
            "event:error data must have 'code'; data: {0}", errorFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue(
            "event:error data must have 'message'; data: {0}", errorFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("code must be non-empty; body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("message must be non-empty; body: {0}", body);

        // No raw stack trace in any frame (AC-6)
        body.Should().NotContain("StackTrace",
            "raw stack traces must not reach the SSE stream (AC-6); body: {0}", body);
        body.Should().NotContain("at Learnexia",
            "stack trace lines must not appear in student responses; body: {0}", body);

        // No event:message (no content streamed on error)
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame should be emitted on provider error; body: {0}", body);
    }

    // =========================================================================
    // TC-6a — Auth: no bearer → 401
    // =========================================================================

    [Fact(DisplayName = "TC-6a Auth: no bearer token → 401")]
    public async Task TC6a_NoBearerToken_Returns401()
    {
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        // No Authorization header

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no bearer → 401; body: {0}", body);
    }

    // =========================================================================
    // TC-6b — Auth: Parent role (non-Student) → 403
    // =========================================================================

    [Fact(DisplayName = "TC-6b Auth: Parent-role JWT (non-Student) → 403")]
    public async Task TC6b_ParentJwt_Returns403()
    {
        // Register a Parent — their token has role=Parent, not Student
        var parentEmail = UniqueEmail("parent403");
        var (prResp, prRoot, prBody) = await SendAsync(_setupClient, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", prBody);
        TryProp(prRoot, "data", out var prData).Should().BeTrue("body: {0}", prBody);
        TryProp(prData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", prBody);
        var parentToken = parentTokProp.GetString()!;

        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent-role token on Student-only endpoint → 403; body: {0}", body);
    }

    // =========================================================================
    // TC-7 — Wire format: exact frame grammar (strict P3-12 contract check)
    // =========================================================================

    [Fact(DisplayName = "TC-7 WireFormat(happy): exact event names, data JSON shapes, and [DONE] terminator")]
    public async Task TC7_WireFormat_HappyPath_ExactFrameGrammar()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "الكسر جزء من كل." };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // --- STRICT WIRE CONTRACT ASSERTIONS ---

        // 1. Content-Type must be text/event-stream
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must send Content-Type: text/event-stream; body: {0}", rawBody);

        // 2. Frames
        var frames = ParseSseFrames(rawBody);
        frames.Should().HaveCountGreaterOrEqualTo(2,
            "happy path must emit at least [message, done]; body: {0}", rawBody);

        // 3. event: message — data shape {"content":"..."}
        var msgFrame = frames.First(f => f.Event == "message");
        var msgObj = JsonDocument.Parse(msgFrame.Data).RootElement;
        msgObj.ValueKind.Should().Be(JsonValueKind.Object,
            "event:message data must be a JSON object; data: {0}", msgFrame.Data);
        TryProp(msgObj, "content", out var cProp).Should().BeTrue(
            "event:message must have 'content' property; data: {0}", msgFrame.Data);
        cProp.ValueKind.Should().Be(JsonValueKind.String,
            "event:message content must be a string; data: {0}", msgFrame.Data);
        cProp.GetString().Should().Be("الكسر جزء من كل.",
            "content must match the safety-stub approved text; body: {0}", rawBody);

        // 4. event: done — data is exactly "[DONE]" (not JSON, not quoted)
        var doneFrame = frames.Last(f => f.Event == "done");
        doneFrame.Data.Should().Be("[DONE]",
            "event:done data must be the literal string '[DONE]' (not JSON-wrapped); body: {0}", rawBody);

        // 5. No event:redirect or event:error in happy path
        frames.Should().NotContain(f => f.Event == "redirect",
            "no redirect frame in happy path; body: {0}", rawBody);
        frames.Should().NotContain(f => f.Event == "error",
            "no error frame in happy path; body: {0}", rawBody);
    }

    [Fact(DisplayName = "TC-7 WireFormat(redirect): exact redirect frame shape {type,targetId} + done terminator")]
    public async Task TC7_WireFormat_Redirect_ExactFrameGrammar()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act — use SkillId=99, no LessonId (redirect path doesn't reach PromptBuilder)
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { SkillId = 99, LessonId = (int?)null, ConceptId = (int?)null, Question = (string?)null }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // --- STRICT WIRE CONTRACT ASSERTIONS ---
        var frames = ParseSseFrames(rawBody);

        // event:redirect shape: {"type":"lesson","targetId":"<string>"}
        var rdFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        rdFrame.Should().NotBe(default, "redirect frame must be present; body: {0}", rawBody);
        var rdObj = JsonDocument.Parse(rdFrame.Data).RootElement;
        rdObj.ValueKind.Should().Be(JsonValueKind.Object, "redirect data must be a JSON object; data: {0}", rdFrame.Data);
        TryProp(rdObj, "type", out var tProp).Should().BeTrue("redirect must have 'type'; data: {0}", rdFrame.Data);
        tProp.GetString().Should().Be("lesson", "type must be 'lesson'; data: {0}", rdFrame.Data);
        TryProp(rdObj, "targetId", out var tidProp).Should().BeTrue("redirect must have 'targetId'; data: {0}", rdFrame.Data);
        tidProp.ValueKind.Should().Be(JsonValueKind.String, "targetId must be a string; data: {0}", rdFrame.Data);
        // targetId = SkillId.ToString() — we sent skillId=99
        tidProp.GetString().Should().Be("99", "targetId must equal the SkillId from the request; data: {0}", rdFrame.Data);

        // event:done follows redirect
        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue(
            "event:done + [DONE] must follow event:redirect; body: {0}", rawBody);
    }

    [Fact(DisplayName = "TC-7 WireFormat(error): exact error frame shape {code,message}; no event:done")]
    public async Task TC7_WireFormat_Error_ExactFrameGrammar()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody = await response.Content.ReadAsStringAsync();

        // --- STRICT WIRE CONTRACT ASSERTIONS ---
        var frames = ParseSseFrames(rawBody);

        // event:error shape: {"code":"...","message":"..."}
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "event:error must be present; body: {0}", rawBody);
        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        errObj.ValueKind.Should().Be(JsonValueKind.Object, "error data must be a JSON object; data: {0}", errFrame.Data);
        TryProp(errObj, "code", out var cProp).Should().BeTrue("error must have 'code'; data: {0}", errFrame.Data);
        TryProp(errObj, "message", out var mProp).Should().BeTrue("error must have 'message'; data: {0}", errFrame.Data);
        cProp.ValueKind.Should().Be(JsonValueKind.String, "code must be a string; data: {0}", errFrame.Data);
        mProp.ValueKind.Should().Be(JsonValueKind.String, "message must be a string; data: {0}", errFrame.Data);

        // No event:done on error (wire contract: "not emitted on error")
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after error (wire contract violation if present); body: {0}", rawBody);

        // No event:message
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame on safety-block; body: {0}", rawBody);
    }

    // =========================================================================
    // TC-8 — Rate limit: exceed per-student window → rate-limited response
    // =========================================================================

    [Fact(DisplayName = "TC-8 RateLimit: 11th request from same student within 1-minute window → event:error (rate-limited)")]
    public async Task TC8_RateLimit_ExceedWindow_ReturnsRateLimitedError()
    {
        // Arrange: use a single student JWT for all 11 requests.
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // The rate limiter is per-student (keyed by userId from JWT).
        // MaxRequestsPerWindow = 10 (from AiTutorRateLimiter source).
        // Requests 1-10: should succeed (event:message + event:done).
        // Request 11: should be rate-limited (event:error).

        string? lastBody = null;
        List<(string Event, string Data)>? lastFrames = null;

        for (int i = 1; i <= 11; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(ValidExplainBody()), Encoding.UTF8, "application/json")
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
        // The rate limiter returns ExplainResult.Error → event:error frame.
        lastFrames.Should().NotBeNull("11th request must have produced a response; body: {0}", lastBody);
        lastFrames!.Should().NotContain(f => f.Event == "message",
            "11th request must be rate-limited, not allowed; body: {0}", lastBody);

        // Either an event:error frame OR a 429 HTTP status — the implementation uses
        // ExplainResult.Error which maps to an in-band event:error frame (not HTTP 429).
        var hasError = lastFrames.Any(f => f.Event == "error");
        hasError.Should().BeTrue(
            "rate-limited request must produce event:error (in-band, since SSE protocol is used); body: {0}", lastBody);

        var errFrame = lastFrames.First(f => f.Event == "error");
        var errJson  = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue(
            "rate-limit error must have 'code'; data: {0}", errFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue(
            "rate-limit error must have 'message'; data: {0}", errFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", lastBody);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", lastBody);
    }

    // =========================================================================
    // TC-9 — Validation: missing context anchor → in-band event:error
    //
    // The SSE controller sets Content-Type:text/event-stream before calling
    // _mediator.Send. ValidationBehavior throws ValidationException, which is
    // caught by the dedicated catch(FluentValidation.ValidationException) block
    // and emitted as event:error {code:ValidationError} — P3-12 FE can
    // distinguish this from a real unhandled error (code:UnhandledError).
    // =========================================================================

    [Fact(DisplayName = "TC-9 Validation: body with no LessonId/ConceptId/SkillId → event:error with code=ValidationError (in-band for SSE)")]
    public async Task TC9_Validation_MissingContextAnchor_EmitsErrorFrame()
    {
        // ValidationBehavior throws → caught by catch(ValidationException) → event:error {code:ValidationError}.
        // HTTP status is still 200 (SSE protocol; error is in-band).
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        // All nulls — violates "at least one context anchor" rule
        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { LessonId = (int?)null, ConceptId = (int?)null, SkillId = (int?)null, Question = (string?)null }),
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
            "validation failure must surface as event:error for SSE endpoint; body: {0}", body);

        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue("error frame must have 'code'; data: {0}", errorFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue("error frame must have 'message'; data: {0}", errorFrame.Data);
        // Must be ValidationError — not UnhandledError — so P3-12 FE can distinguish
        codeProp.GetString().Should().Be("ValidationError",
            "validation exceptions must surface as code=ValidationError, not UnhandledError; data: {0}", errorFrame.Data);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // No event:message (LLM not called)
        frames.Should().NotContain(f => f.Event == "message",
            "LLM must not be called on invalid input; body: {0}", body);
    }

    // =========================================================================
    // TC-10 — Validation: Question too long → in-band event:error
    // =========================================================================

    [Fact(DisplayName = "TC-10 Validation: Question > 500 chars → event:error with code=ValidationError (in-band for SSE)")]
    public async Task TC10_Validation_QuestionTooLong_EmitsErrorFrame()
    {
        var studentToken = await CreateStudentJwtAsync();
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();

        using var factory = BuildSseFactory(safety, context);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    SkillId  = 42,
                    Question = new string('x', 501), // 501 chars — exceeds 500 cap
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE returns 200; error is in-band; body: {0}", body);

        var frames = ParseSseFrames(body);
        var errorFrame = frames.FirstOrDefault(f => f.Event == "error");
        errorFrame.Should().NotBe(default,
            "Question > 500 chars must produce event:error; body: {0}", body);

        var errJson = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue("error must have 'code'; data: {0}", errorFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue("error must have 'message'; data: {0}", errorFrame.Data);
        // Must be ValidationError — not UnhandledError — so P3-12 FE can distinguish
        codeProp.GetString().Should().Be("ValidationError",
            "validation exceptions must surface as code=ValidationError, not UnhandledError; data: {0}", errorFrame.Data);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        frames.Should().NotContain(f => f.Event == "message", "body: {0}", body);
    }
}

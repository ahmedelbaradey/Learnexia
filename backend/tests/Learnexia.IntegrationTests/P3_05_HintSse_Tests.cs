using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
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

namespace Learnexia.IntegrationTests;

// ============================================================================
// P3-05 — SSE Hint + Simplify endpoint integration tests
//
// POST /api/AiTutor/Hint     [Authorize(Roles="Student")]
// POST /api/AiTutor/Simplify [Authorize(Roles="Student")]
//
// Pinned SSE wire contract (same as P3-04, plus Hint preamble):
//   event: message  data: {"hintLevel":n,"nextHintLevel":n+1}   — Hint preamble (Hint intent only)
//   event: message  data: {"content":"<text>"}                  — approved content chunk
//   event: redirect data: {"type":"lesson","targetId":"…"}      — no-context refuse-and-redirect
//   event: error    data: {"code":"…","message":"…"}            — safety/gateway/validation failure
//   event: done     data: [DONE]                                 — stream terminator (not on error)
//
// Stubs:
//   ISafetyLayer              — StubSafetyLayer (from P3_04_ExplainSse_Tests.cs — shared in same namespace)
//   ILearningContextProvider  — StubContextProvider (shared)
//   ILessonContextContract    — StubLessonContextContract (shared)
//   IQuestionAnswerContract   — StubQuestionAnswerContract (new in this file)
//
// Each test creates its own HintSseTestFactory so stub state is clean and independent.
// DB migrations run once via the shared LearnexiaWebAppFactory fixture; each test
// factory inherits the same Postgres container connection string.
// ============================================================================

/// <summary>
/// Configurable stub for <see cref="IQuestionAnswerContract"/>.
/// Returns a fixed <see cref="QuestionAnswerDto"/> by default, or null when <see cref="Mode.ReturnNull"/>.
/// </summary>
public sealed class StubQuestionAnswerContract : IQuestionAnswerContract
{
    public enum Mode { ReturnDto, ReturnNull, ThrowOnCall }

    /// <summary>The correct answer string embedded in the DTO returned when mode is ReturnDto.</summary>
    public string CorrectAnswer { get; set; } = "STUB_CORRECT_ANSWER_42";

    /// <summary>The current hint level embedded in the DTO returned when mode is ReturnDto.</summary>
    public int CurrentHintLevel { get; set; } = 1;

    public Mode Behavior { get; set; } = Mode.ReturnDto;

    public Task<QuestionAnswerDto?> GetQuestionAnswerAsync(
        int questionId,
        int attemptId,
        int studentId,
        CancellationToken ct = default)
    {
        return Behavior switch
        {
            Mode.ReturnNull    => Task.FromResult<QuestionAnswerDto?>(null),
            Mode.ThrowOnCall   => throw new InvalidOperationException("Stub: IQuestionAnswerContract forced failure"),
            _                  => Task.FromResult<QuestionAnswerDto?>(new QuestionAnswerDto(CorrectAnswer, CurrentHintLevel))
        };
    }
}

/// <summary>
/// Specialised WebApplicationFactory for P3-05 tests.
/// Replaces ISafetyLayer, ILearningContextProvider, ILessonContextContract,
/// and IQuestionAnswerContract with configurable stubs.
/// </summary>
public sealed class HintSseTestFactory : WebApplicationFactory<Program>
{
    private readonly StubSafetyLayer _safetyStub;
    private readonly StubContextProvider _contextStub;
    private readonly StubQuestionAnswerContract _questionAnswerStub;
    private readonly string _postgresConnectionString;

    public HintSseTestFactory(
        string postgresConnectionString,
        StubSafetyLayer safetyStub,
        StubContextProvider contextStub,
        StubQuestionAnswerContract questionAnswerStub)
    {
        _postgresConnectionString = postgresConnectionString;
        _safetyStub               = safetyStub;
        _contextStub              = contextStub;
        _questionAnswerStub       = questionAnswerStub;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ----------------------------------------------------------------
            // Redirect all module DbContexts to the Testcontainers Postgres DB
            // (same pattern as LearnexiaWebAppFactory.ReplaceDbContext<T>).
            // Without this, the LearningDbContext used by HintUsedIntegrationEventHandler
            // would point at the production localhost:5432, not the test container.
            // ----------------------------------------------------------------
            ReplaceDbContext<IdentityModuleDbContext>(services, _postgresConnectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, _postgresConnectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, _postgresConnectionString, "learning");
            ReplaceDbContext<ParentDbContext>(services, _postgresConnectionString, "parent");
            ReplaceDbContext<GamificationDbContext>(services, _postgresConnectionString, "gamification");
            ReplaceDbContext<ModerationDbContext>(services, _postgresConnectionString, "moderation");

            // Swap ISafetyLayer.
            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ => _safetyStub);

            // Swap ILearningContextProvider.
            services.RemoveAll<ILearningContextProvider>();
            services.AddTransient<ILearningContextProvider>(_ => _contextStub);

            // Swap ILessonContextContract (required by Simplify handler's lesson enrichment step).
            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            // Swap IQuestionAnswerContract (required by Hint handler's server-derived hint level + no-reveal check).
            services.RemoveAll<IQuestionAnswerContract>();
            services.AddScoped<IQuestionAnswerContract>(_ => _questionAnswerStub);

            // Reset the rate limiter so each test starts with a clean window.
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
/// P3-05 integration tests for:
///   POST /api/AiTutor/Hint     — Hint and WhyWrong intents
///   POST /api/AiTutor/Simplify — Simplify (reuses Explain pipeline)
///
/// Uses <see cref="HintSseTestFactory"/> to inject stubs for ISafetyLayer,
/// ILearningContextProvider, ILessonContextContract, and IQuestionAnswerContract
/// so no live LLM key is needed. Testcontainers PostgreSQL from the shared
/// LearnexiaWebAppFactory handles DB migrations.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P3_05_HintSse_Tests : IAsyncLifetime
{
    // -------------------------------------------------------------------------
    // URL constants
    // -------------------------------------------------------------------------
    private const string HintUrl           = "api/AiTutor/Hint";
    private const string SimplifyUrl       = "api/AiTutor/Simplify";
    private const string RegisterParentUrl = "api/Users/Authentication/Register-Parent";
    private const string SignInUrl         = "api/Users/Authentication/Sign-In";
    private const string AddChildUrl       = "api/Parent/Add-Child";
    private const string ValidChildPassword = "Child@Pass1";

    // Known correct-answer token used to test no-reveal guard (must not appear in stub content).
    private const string StubCorrectAnswer = "STUB_CORRECT_ANSWER_42";

    // -------------------------------------------------------------------------
    // Infrastructure
    // -------------------------------------------------------------------------
    private readonly LearnexiaWebAppFactory _sharedFactory;
    private readonly HttpClient _setupClient;

    public P3_05_HintSse_Tests(LearnexiaWebAppFactory factory)
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
        => $"p305_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    /// <summary>
    /// Case-insensitive JSON property lookup that also tries PascalCase fallback.
    /// Mirrors the helper from P3_04_ExplainSse_Tests.
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
    /// Registers a parent, adds a Student child, signs in as that child. Returns the Student JWT.
    /// </summary>
    private async Task<string> CreateStudentJwtAsync(string languageOverride = "ar")
    {
        var parentEmail = UniqueEmail("parent");
        var (prResp, prRoot, prBody) = await SendAsync(_setupClient, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", prBody);
        TryProp(prRoot, "data", out var prData).Should().BeTrue("body: {0}", prBody);
        TryProp(prData, "accessToken", out var parentTokProp).Should().BeTrue("body: {0}", prBody);
        var parentToken = parentTokProp.GetString()!;

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

    /// <summary>Registers only a Parent and returns the Parent JWT (role = Parent, not Student).</summary>
    private async Task<string> CreateParentJwtAsync()
    {
        var parentEmail = UniqueEmail("parent_notstudent");
        var (prResp, prRoot, prBody) = await SendAsync(_setupClient, HttpMethod.Post, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration must succeed; body: {0}", prBody);
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

    /// <summary>
    /// Builds a fresh HintSseTestFactory using the shared Postgres container.
    /// </summary>
    private HintSseTestFactory BuildFactory(
        StubSafetyLayer safety,
        StubContextProvider context,
        StubQuestionAnswerContract? qa = null)
        => new HintSseTestFactory(
            GetSharedConnectionString(),
            safety,
            context,
            qa ?? new StubQuestionAnswerContract
            {
                CorrectAnswer    = StubCorrectAnswer,
                CurrentHintLevel = 1,
                Behavior         = StubQuestionAnswerContract.Mode.ReturnDto
            });

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

    /// <summary>Minimal valid Hint body (Intent=Hint, valid QuestionId + AttemptId).</summary>
    private static object ValidHintBody(int questionId = 1, int attemptId = 1, int? hintLevel = null)
        => new
        {
            QuestionId = questionId,
            AttemptId  = attemptId,
            Intent     = 2,   // HelperIntent.Hint = 2
            HintLevel  = hintLevel
        };

    /// <summary>Minimal valid WhyWrong body (Intent=WhyWrong, WrongAnswer required).</summary>
    private static object ValidWhyWrongBody(
        int questionId  = 1,
        int attemptId   = 1,
        string wrongAnswer = "الإجابة الخاطئة")
        => new
        {
            QuestionId  = questionId,
            AttemptId   = attemptId,
            Intent      = 3,   // HelperIntent.WhyWrong = 3
            WrongAnswer = wrongAnswer
        };

    /// <summary>Minimal valid Simplify body (requires at least one of ConceptId/LessonId).</summary>
    private static object ValidSimplifyBody(int? conceptId = null, int lessonId = 1)
        => new
        {
            ConceptId           = conceptId,
            LessonId            = lessonId,
            PreviousExplanationRef = (string?)null
        };

    // =========================================================================
    // TC-1 — Hint happy path:
    //   non-empty context + safety allows + content does NOT contain CorrectAnswer
    //   → SSE preamble frame {hintLevel:n, nextHintLevel:n+1} THEN content THEN done.
    // =========================================================================

    [Fact(DisplayName = "TC-1 Hint HappyPath: non-empty context + safety allows + no-reveal clear " +
                         "→ preamble {hintLevel,nextHintLevel} THEN event:message THEN event:done")]
    public async Task TC1_Hint_HappyPath_EmitsPreamble_Then_Content_Then_Done()
    {
        // Arrange — stub content does NOT contain the correct answer string.
        var studentToken = await CreateStudentJwtAsync();

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = "فكّر في نصف القطر كالمسافة من المركز." // safe hint, no CorrectAnswer substring
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa = new StubQuestionAnswerContract
        {
            CorrectAnswer    = StubCorrectAnswer,
            CurrentHintLevel = 1,
            Behavior         = StubQuestionAnswerContract.Mode.ReturnDto
        };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // Assert HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 for authorized student; body: {0}", body);

        var frames = ParseSseFrames(body);

        // Must have at least 3 frames: [preamble, content, done]
        frames.Should().HaveCountGreaterOrEqualTo(3,
            "Hint happy path must emit [preamble message, content message, done]; body: {0}", body);

        // FIRST event:message must be the preamble {hintLevel, nextHintLevel} (NOT content)
        var firstMsg = frames.First(f => f.Event == "message");
        var firstMsgObj = JsonDocument.Parse(firstMsg.Data).RootElement;

        // Preamble shape: has hintLevel AND nextHintLevel (no "content" key)
        TryProp(firstMsgObj, "hintLevel", out var hintLevelProp).Should().BeTrue(
            "first event:message for Hint must be the preamble with 'hintLevel'; data: {0}", firstMsg.Data);
        TryProp(firstMsgObj, "nextHintLevel", out var nextHintLevelProp).Should().BeTrue(
            "Hint preamble must have 'nextHintLevel'; data: {0}", firstMsg.Data);

        // hintLevel must match CurrentHintLevel from stub (1)
        hintLevelProp.GetInt32().Should().Be(1,
            "hintLevel in preamble must equal server-derived CurrentHintLevel (1); data: {0}", firstMsg.Data);

        // nextHintLevel must be 2 (CurrentHintLevel + 1 when below MaxHintLevels=3)
        nextHintLevelProp.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Number, JsonValueKind.Null },
            "nextHintLevel must be a number or null (when at max); data: {0}", firstMsg.Data);
        if (nextHintLevelProp.ValueKind == JsonValueKind.Number)
            nextHintLevelProp.GetInt32().Should().Be(2,
                "nextHintLevel must be CurrentHintLevel+1=2 when not at max; data: {0}", firstMsg.Data);

        // SECOND event:message must be content (has "content" key)
        var msgFrames = frames.Where(f => f.Event == "message").ToList();
        msgFrames.Should().HaveCountGreaterOrEqualTo(2,
            "must have preamble AND content message frames; body: {0}", body);

        var contentFrame = msgFrames.Skip(1).First(); // second message frame
        var contentObj   = JsonDocument.Parse(contentFrame.Data).RootElement;
        TryProp(contentObj, "content", out var contentProp).Should().BeTrue(
            "second event:message must be the content frame with 'content' key; data: {0}", contentFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace(
            "content must be non-empty; body: {0}", body);

        // event:done must be the terminator
        var doneFrame = frames.LastOrDefault(f => f.Event == "done");
        doneFrame.Should().NotBe(default,
            "event:done must be present and be the last frame; body: {0}", body);
        doneFrame.Data.Should().Be("[DONE]",
            "event:done data must be exactly '[DONE]'; body: {0}", body);

        // No event:error and no event:redirect in happy path
        frames.Should().NotContain(f => f.Event == "error",
            "no error frame in happy path; body: {0}", body);
        frames.Should().NotContain(f => f.Event == "redirect",
            "no redirect frame in happy path; body: {0}", body);

        // No stack trace in any frame
        body.Should().NotContain("StackTrace", "stack traces must never appear; body: {0}", body);
    }

    // =========================================================================
    // TC-2 — WhyWrong happy path:
    //   Intent=WhyWrong + WrongAnswer supplied + non-empty context
    //   → content streamed; NO hintLevel preamble (WhyWrong has no level metadata).
    // =========================================================================

    [Fact(DisplayName = "TC-2 WhyWrong HappyPath: Intent=WhyWrong + WrongAnswer + non-empty context " +
                         "→ event:message {content} with NO hintLevel preamble THEN event:done")]
    public async Task TC2_WhyWrong_HappyPath_NoHintLevelPreamble()
    {
        // Arrange
        var studentToken = await CreateStudentJwtAsync();

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = "إجابتك كانت خاطئة لأنك استخدمت القانون الخطأ." // why-wrong content
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidWhyWrongBody(wrongAnswer: "الجواب الغلط")),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var frames = ParseSseFrames(body);

        // Must have at least event:message + event:done
        frames.Should().HaveCountGreaterOrEqualTo(2,
            "WhyWrong happy path must have at least [content message, done]; body: {0}", body);

        // event:message data must have "content" key (not hintLevel preamble)
        var msgFrame = frames.FirstOrDefault(f => f.Event == "message");
        msgFrame.Should().NotBe(default,
            "event:message must be present for WhyWrong; body: {0}", body);

        var msgObj = JsonDocument.Parse(msgFrame.Data).RootElement;
        TryProp(msgObj, "content", out var contentProp).Should().BeTrue(
            "WhyWrong event:message must have 'content' key (not a preamble); data: {0}", msgFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // CRITICAL: NO hintLevel preamble frame (WhyWrong has no level metadata per wire contract)
        // Check all message frames — none should have hintLevel
        var allMsgFrames = frames.Where(f => f.Event == "message").ToList();
        foreach (var mf in allMsgFrames)
        {
            var mfObj = JsonDocument.Parse(mf.Data).RootElement;
            TryProp(mfObj, "hintLevel", out _).Should().BeFalse(
                "WhyWrong must NOT emit a hintLevel preamble frame — wire contract violation; data: {0}", mf.Data);
        }

        // event:done must terminate
        var doneFrame = frames.LastOrDefault(f => f.Event == "done");
        doneFrame.Should().NotBe(default, "event:done must follow content; body: {0}", body);
        doneFrame.Data.Should().Be("[DONE]", "body: {0}", body);

        // No error or redirect
        frames.Should().NotContain(f => f.Event == "error", "body: {0}", body);
        frames.Should().NotContain(f => f.Event == "redirect", "body: {0}", body);

        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-3 — No-reveal guard (AC-1 / key security case):
    //   Stub ISafetyLayer returns Allowed content that CONTAINS the stub CorrectAnswer
    //   → handler's post-generation no-reveal check must block it
    //   → event:error (no content message leaking the answer).
    // =========================================================================

    [Fact(DisplayName = "TC-3 NoReveal: safety-allows content containing CorrectAnswer " +
                         "→ no-reveal post-check blocks it → event:error; NO event:message with leaked answer (AC-1)")]
    public async Task TC3_NoRevealGuard_Blocks_ContentContainingCorrectAnswer()
    {
        // Arrange — safety ALLOWS content, but content embeds the correct answer verbatim.
        var studentToken = await CreateStudentJwtAsync();

        const string revealingContent = $"تلميح: الإجابة هي {StubCorrectAnswer} وهذا صحيح.";

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = revealingContent   // CONTAINS StubCorrectAnswer → should be blocked
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa = new StubQuestionAnswerContract
        {
            CorrectAnswer    = StubCorrectAnswer,
            CurrentHintLevel = 1,
            Behavior         = StubQuestionAnswerContract.Mode.ReturnDto
        };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // Assert — SSE returns 200 (in-band error)
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 regardless; error is in-band; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error MUST be present (no-reveal check triggered)
        var errorFrame = frames.FirstOrDefault(f => f.Event == "error");
        errorFrame.Should().NotBe(default,
            "no-reveal post-check must block revealing content and emit event:error (AC-1); body: {0}", body);

        // The error frame must have code + message
        var errObj = JsonDocument.Parse(errorFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue(
            "event:error must have 'code'; data: {0}", errorFrame.Data);
        TryProp(errObj, "message", out var msgProp).Should().BeTrue(
            "event:error must have 'message'; data: {0}", errorFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // CRITICAL SECURITY CHECK: the CorrectAnswer string must NOT appear in any frame
        body.Should().NotContain(StubCorrectAnswer,
            "the CorrectAnswer must NEVER leak to the student in any SSE frame — AC-1 violation; body: {0}", body);

        // No content event:message must be present
        frames.Should().NotContain(f => f.Event == "message",
            "no content must be streamed when no-reveal guard fires — AC-1 violation; body: {0}", body);

        // No event:done on error
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must not be emitted when no-reveal guard fires (error path); body: {0}", body);

        // No stack trace
        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-4 — Refuse-and-redirect:
    //   Empty context stub → event:redirect {type,targetId}; NO LLM/message frame.
    //   Tests both Hint and WhyWrong intents (both must redirect, not call LLM).
    // =========================================================================

    [Fact(DisplayName = "TC-4a RefuseAndRedirect(Hint): empty context → event:redirect {type,targetId}; NO event:message")]
    public async Task TC4a_Hint_EmptyContext_EmitsRedirect_NoMessageFrame()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed }; // must NOT be called
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:redirect must be present
        var rdFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        rdFrame.Should().NotBe(default,
            "empty context must emit event:redirect for Hint intent (AC-3); body: {0}", body);

        // Redirect data must have type="lesson" and targetId (string)
        var rdObj = JsonDocument.Parse(rdFrame.Data).RootElement;
        TryProp(rdObj, "type", out var typeProp).Should().BeTrue(
            "redirect data must have 'type'; data: {0}", rdFrame.Data);
        typeProp.GetString().Should().Be("lesson",
            "redirect type must be 'lesson' per wire contract; data: {0}", rdFrame.Data);
        TryProp(rdObj, "targetId", out var targetProp).Should().BeTrue(
            "redirect data must have 'targetId'; data: {0}", rdFrame.Data);
        targetProp.ValueKind.Should().Be(JsonValueKind.String,
            "targetId must be a string per wire contract; data: {0}", rdFrame.Data);

        // NO event:message (LLM must not be called)
        frames.Should().NotContain(f => f.Event == "message",
            "LLM must NOT be called when context is empty — no content frame expected; body: {0}", body);

        // event:done must follow redirect
        frames.Any(f => f.Event == "done").Should().BeTrue(
            "event:done must follow event:redirect; body: {0}", body);

        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    [Fact(DisplayName = "TC-4b RefuseAndRedirect(WhyWrong): empty context → event:redirect; NO event:message")]
    public async Task TC4b_WhyWrong_EmptyContext_EmitsRedirect_NoMessageFrame()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed }; // must NOT be called
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidWhyWrongBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var frames = ParseSseFrames(body);

        var rdFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        rdFrame.Should().NotBe(default,
            "empty context must emit event:redirect for WhyWrong intent (AC-3); body: {0}", body);

        var rdObj = JsonDocument.Parse(rdFrame.Data).RootElement;
        TryProp(rdObj, "type", out var typeProp).Should().BeTrue(
            "redirect data must have 'type'; data: {0}", rdFrame.Data);
        typeProp.GetString().Should().Be("lesson", "body: {0}", body);
        TryProp(rdObj, "targetId", out var targetProp).Should().BeTrue(
            "redirect data must have 'targetId'; data: {0}", rdFrame.Data);
        targetProp.ValueKind.Should().Be(JsonValueKind.String, "body: {0}", body);

        frames.Should().NotContain(f => f.Event == "message",
            "LLM must NOT be called on WhyWrong with empty context; body: {0}", body);

        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-5 — Safety-block:
    //   Safety stub blocks → event:error; no content leaked.
    // =========================================================================

    [Fact(DisplayName = "TC-5 SafetyBlock: ISafetyLayer.Blocked → event:error {code,message}; NO event:message")]
    public async Task TC5_SafetyBlock_EmitsError_NoContent()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 even on safety block; error is in-band; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error must be present
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "safety block must emit event:error; body: {0}", body);

        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue("body: {0}", body);
        TryProp(errObj, "message", out var msgProp).Should().BeTrue("body: {0}", body);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // No content
        frames.Should().NotContain(f => f.Event == "message",
            "blocked content must not be streamed; body: {0}", body);

        // No event:done on error
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must not be emitted after safety block; body: {0}", body);

        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-6 — WhyWrong validation (D-1 pattern):
    //   Intent=WhyWrong with NO WrongAnswer → validator rejects
    //   → event:error {code:"ValidationError"} (in-band for SSE).
    // =========================================================================

    [Fact(DisplayName = "TC-6 WhyWrong_NoWrongAnswer: Intent=WhyWrong with missing WrongAnswer " +
                         "→ ValidationBehavior fires → event:error {code:ValidationError}")]
    public async Task TC6_WhyWrong_MissingWrongAnswer_ReturnsValidationError()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        // WhyWrong intent but NO WrongAnswer supplied (validator must reject)
        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    QuestionId  = 1,
                    AttemptId   = 1,
                    Intent      = 3,              // HelperIntent.WhyWrong = 3
                    WrongAnswer = (string?)null   // explicitly null — missing
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // SSE returns 200; the validation error is in-band
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200; validation error is in-band via event:error; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error must be present
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "WhyWrong without WrongAnswer must emit event:error; body: {0}", body);

        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue(
            "error frame must have 'code'; data: {0}", errFrame.Data);
        TryProp(errObj, "message", out var msgProp).Should().BeTrue(
            "error frame must have 'message'; data: {0}", errFrame.Data);

        // Must be ValidationError — not UnhandledError — so P3-12 FE can distinguish
        codeProp.GetString().Should().Be("ValidationError",
            "WhyWrong missing WrongAnswer must produce code=ValidationError; data: {0}", errFrame.Data);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // No content streamed
        frames.Should().NotContain(f => f.Event == "message", "body: {0}", body);
    }

    // =========================================================================
    // TC-7 — Simplify happy path (AC-5):
    //   POST /api/AiTutor/Simplify with LessonId → content streamed.
    //   Reuses explain pipeline. No hintLevel preamble.
    // =========================================================================

    [Fact(DisplayName = "TC-7 Simplify HappyPath: ConceptId/LessonId + safety allows → event:message {content} + event:done")]
    public async Task TC7_Simplify_HappyPath_ContentStreamed_NoPreamble()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = "الكسر العادي هو جزء من كل بكلمات أبسط." // simplified content
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, SimplifyUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidSimplifyBody(lessonId: 1)), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Simplify SSE endpoint returns 200 for authorized student; body: {0}", body);

        var frames = ParseSseFrames(body);

        frames.Should().HaveCountGreaterOrEqualTo(2,
            "Simplify must emit at least [content message, done]; body: {0}", body);

        // No hintLevel preamble — Simplify does not send level metadata
        var allMsgFrames = frames.Where(f => f.Event == "message").ToList();
        foreach (var mf in allMsgFrames)
        {
            var mfObj = JsonDocument.Parse(mf.Data).RootElement;
            TryProp(mfObj, "hintLevel", out _).Should().BeFalse(
                "Simplify must NOT emit a hintLevel preamble — no level metadata for Simplify; data: {0}", mf.Data);
        }

        // event:message must have content key
        var msgFrame = frames.FirstOrDefault(f => f.Event == "message");
        msgFrame.Should().NotBe(default, "event:message must be present; body: {0}", body);
        var msgObj = JsonDocument.Parse(msgFrame.Data).RootElement;
        TryProp(msgObj, "content", out var contentProp).Should().BeTrue(
            "event:message must have 'content'; data: {0}", msgFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // event:done terminates
        var doneFrame = frames.LastOrDefault(f => f.Event == "done");
        doneFrame.Should().NotBe(default, "event:done must terminate Simplify stream; body: {0}", body);
        doneFrame.Data.Should().Be("[DONE]", "body: {0}", body);

        // No errors / redirects in happy path
        frames.Should().NotContain(f => f.Event == "error", "body: {0}", body);
        frames.Should().NotContain(f => f.Event == "redirect", "body: {0}", body);

        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-8 — Auth: non-Student JWT → 403; no bearer → 401 (both endpoints).
    // =========================================================================

    [Fact(DisplayName = "TC-8a Auth(Hint): no bearer → 401")]
    public async Task TC8a_Hint_NoBearerToken_Returns401()
    {
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();
        var qa      = new StubQuestionAnswerContract();

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        // No Authorization header

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no bearer → 401 on Hint endpoint; body: {0}", body);
    }

    [Fact(DisplayName = "TC-8b Auth(Hint): Parent-role JWT (non-Student) → 403")]
    public async Task TC8b_Hint_ParentJwt_Returns403()
    {
        var parentToken = await CreateParentJwtAsync();

        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();
        var qa      = new StubQuestionAnswerContract();

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent-role token on Student-only Hint endpoint → 403; body: {0}", body);
    }

    [Fact(DisplayName = "TC-8c Auth(Simplify): no bearer → 401")]
    public async Task TC8c_Simplify_NoBearerToken_Returns401()
    {
        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();
        var qa      = new StubQuestionAnswerContract();

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, SimplifyUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidSimplifyBody()), Encoding.UTF8, "application/json")
        };
        // No Authorization header

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no bearer → 401 on Simplify endpoint; body: {0}", body);
    }

    [Fact(DisplayName = "TC-8d Auth(Simplify): Parent-role JWT (non-Student) → 403")]
    public async Task TC8d_Simplify_ParentJwt_Returns403()
    {
        var parentToken = await CreateParentJwtAsync();

        var safety  = new StubSafetyLayer();
        var context = new StubContextProvider();
        var qa      = new StubQuestionAnswerContract();

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, SimplifyUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidSimplifyBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent-role token on Student-only Simplify endpoint → 403; body: {0}", body);
    }

    // =========================================================================
    // TC-9 — Max-hint-level bound:
    //   Request where server-derived HintLevel > MaxHintLevels (3)
    //   → handler returns HintResult.Error → event:error (not content).
    //
    // Achieved by stubbing IQuestionAnswerContract.CurrentHintLevel = 4 (> MaxHintLevels=3).
    // =========================================================================

    [Fact(DisplayName = "TC-9 MaxHintLevelBound: server-derived HintLevel > MaxHintLevels → event:error (escalation bound)")]
    public async Task TC9_MaxHintLevelBound_ReturnsError()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        // CurrentHintLevel = 4 exceeds MaxHintLevels default of 3
        var qa = new StubQuestionAnswerContract
        {
            CorrectAnswer    = StubCorrectAnswer,
            CurrentHintLevel = 4,   // > MaxHintLevels (3) → triggers escalation bound error
            Behavior         = StubQuestionAnswerContract.Mode.ReturnDto
        };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE returns 200; max-hint-level rejection is in-band; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error must be present (escalation bound hit)
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "exceeding MaxHintLevels must emit event:error (escalation bound); body: {0}", body);

        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue("body: {0}", body);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // No content should be streamed when max level exceeded
        frames.Should().NotContain(f => f.Event == "message",
            "no content must be sent when max hint level is exceeded; body: {0}", body);

        body.Should().NotContain("StackTrace", "body: {0}", body);
    }

    // =========================================================================
    // TC-10 — Wire format: strict assertion of the event grammar for all four event types.
    // Exercises the P3-12 FE contract — asserts Content-Type, frame delimiters, exact shapes.
    // =========================================================================

    [Fact(DisplayName = "TC-10a WireFormat(Hint happy): Content-Type=text/event-stream; preamble shape; content shape; [DONE]")]
    public async Task TC10a_WireFormat_Hint_HappyPath_StrictGrammar()
    {
        const string hintContent = "فكّر في خطوات الحل بدون التسرع.";

        var studentToken = await CreateStudentJwtAsync();

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = hintContent
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa = new StubQuestionAnswerContract
        {
            CorrectAnswer    = StubCorrectAnswer,
            CurrentHintLevel = 2,
            Behavior         = StubQuestionAnswerContract.Mode.ReturnDto
        };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody  = await response.Content.ReadAsStringAsync();

        // --- STRICT WIRE CONTRACT ASSERTIONS ---

        // 1. Content-Type must be text/event-stream
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must send Content-Type: text/event-stream; body: {0}", rawBody);

        // 2. Parse all frames
        var frames = ParseSseFrames(rawBody);
        frames.Should().HaveCountGreaterOrEqualTo(3,
            "Hint happy path must emit [preamble, content, done]; body: {0}", rawBody);

        // 3. First event:message — preamble shape {hintLevel:2, nextHintLevel:3}
        var msgFrames = frames.Where(f => f.Event == "message").ToList();
        msgFrames.Should().HaveCountGreaterOrEqualTo(2,
            "must have preamble AND content message frames; body: {0}", rawBody);

        var preamble    = msgFrames[0];
        var preambleObj = JsonDocument.Parse(preamble.Data).RootElement;
        preambleObj.ValueKind.Should().Be(JsonValueKind.Object,
            "preamble data must be a JSON object; data: {0}", preamble.Data);
        TryProp(preambleObj, "hintLevel", out var hlProp).Should().BeTrue(
            "preamble must have 'hintLevel'; data: {0}", preamble.Data);
        hlProp.ValueKind.Should().Be(JsonValueKind.Number, "hintLevel must be a number; data: {0}", preamble.Data);
        hlProp.GetInt32().Should().Be(2, "hintLevel must match CurrentHintLevel=2; data: {0}", preamble.Data);

        TryProp(preambleObj, "nextHintLevel", out var nhlProp).Should().BeTrue(
            "preamble must have 'nextHintLevel'; data: {0}", preamble.Data);
        // nextHintLevel = 3 (CurrentHintLevel+1=3 which equals MaxHintLevels=3; still returns 3)
        // OR null if the handler treats level 2+1=3 as MaxLevel (null). Either is valid per contract.
        nhlProp.ValueKind.Should().BeOneOf(
            new[] { JsonValueKind.Number, JsonValueKind.Null },
            "nextHintLevel must be a number or null; data: {0}", preamble.Data);

        // Preamble must NOT have a "content" key (it's level metadata, not text)
        TryProp(preambleObj, "content", out _).Should().BeFalse(
            "preamble frame must NOT have a 'content' key — it is level metadata only; data: {0}", preamble.Data);

        // 4. Second event:message — content shape {"content":"..."}
        var contentFrame = msgFrames[1];
        var contentObj   = JsonDocument.Parse(contentFrame.Data).RootElement;
        contentObj.ValueKind.Should().Be(JsonValueKind.Object, "content data must be a JSON object; data: {0}", contentFrame.Data);
        TryProp(contentObj, "content", out var cProp).Should().BeTrue(
            "content message must have 'content' property; data: {0}", contentFrame.Data);
        cProp.ValueKind.Should().Be(JsonValueKind.String, "content must be a string; data: {0}", contentFrame.Data);
        cProp.GetString().Should().Be(hintContent,
            "content must match the safety-stub approved text; body: {0}", rawBody);

        // 5. event:done — data is exactly "[DONE]"
        var doneFrame = frames.Last(f => f.Event == "done");
        doneFrame.Data.Should().Be("[DONE]",
            "event:done data must be the literal '[DONE]' (not JSON-wrapped); body: {0}", rawBody);

        // 6. No unexpected event types
        frames.Should().NotContain(f => f.Event == "redirect", "no redirect in happy path; body: {0}", rawBody);
        frames.Should().NotContain(f => f.Event == "error",    "no error in happy path; body: {0}", rawBody);
    }

    [Fact(DisplayName = "TC-10b WireFormat(WhyWrong happy): content frame has 'content' key; no hintLevel preamble; [DONE]")]
    public async Task TC10b_WireFormat_WhyWrong_NoHintLevelInAnyFrame()
    {
        const string whyWrongContent = "إجابتك كانت بعيدة لأن…";

        var studentToken = await CreateStudentJwtAsync();

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = whyWrongContent
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidWhyWrongBody(wrongAnswer: "إجابة خاطئة")),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody  = await response.Content.ReadAsStringAsync();

        // Content-Type
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must send Content-Type: text/event-stream; body: {0}", rawBody);

        var frames = ParseSseFrames(rawBody);

        // Exactly [content_message, done] — no preamble
        var msgFrames = frames.Where(f => f.Event == "message").ToList();
        msgFrames.Should().HaveCount(1,
            "WhyWrong must emit exactly 1 event:message (content only — no hintLevel preamble); body: {0}", rawBody);

        var msgObj = JsonDocument.Parse(msgFrames[0].Data).RootElement;
        TryProp(msgObj, "content", out var cProp).Should().BeTrue(
            "WhyWrong message must have 'content'; data: {0}", msgFrames[0].Data);
        cProp.GetString().Should().Be(whyWrongContent, "body: {0}", rawBody);

        TryProp(msgObj, "hintLevel", out _).Should().BeFalse(
            "WhyWrong message must NOT have 'hintLevel' — wire contract violation; data: {0}", msgFrames[0].Data);

        // event:done
        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue(
            "event:done [DONE] must terminate WhyWrong stream; body: {0}", rawBody);
    }

    [Fact(DisplayName = "TC-10c WireFormat(redirect): exact redirect shape {type,targetId} + done")]
    public async Task TC10c_WireFormat_Redirect_ExactShape()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.Empty }; // triggers redirect
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody  = await response.Content.ReadAsStringAsync();

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

        // event:done follows redirect
        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue(
            "event:done [DONE] must follow event:redirect; body: {0}", rawBody);

        // No content
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame on redirect; body: {0}", rawBody);
    }

    [Fact(DisplayName = "TC-10d WireFormat(error): exact error shape {code,message}; no event:done")]
    public async Task TC10d_WireFormat_Error_NoEventDone()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(ValidHintBody()), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var rawBody  = await response.Content.ReadAsStringAsync();

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

        // Per wire contract: event:done is NOT emitted on error
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after error (wire contract violation if present); body: {0}", rawBody);

        // No content frame on error
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame on safety-block error; body: {0}", rawBody);
    }

    // =========================================================================
    // TC-11 — HintUsedIntegrationEvent side effect (DEFECT DOCUMENTED):
    //
    //   ROOT CAUSE IDENTIFIED (not a test bug): GetHintCommandHandler publishes
    //   HintUsedIntegrationEvent via fire-and-forget:
    //       _ = _publisher.Publish(new HintUsedIntegrationEvent(...), CancellationToken.None);
    //   The HintUsedIntegrationEventHandler is registered as a Scoped INotificationHandler
    //   and depends on Scoped LearningDbContext. After the HTTP response is sent, the
    //   request DI scope is disposed. The background continuation runs WITHOUT a valid DI
    //   scope — the LearningDbContext injected into the handler is already disposed, causing
    //   an ObjectDisposedException which is silently swallowed by the handler's try/catch.
    //   Therefore HintsUsedCount is never incremented.
    //
    //   DEFECT SEVERITY: Medium (AC-7 is broken — usage counting does not happen).
    //   WIRE CONTRACT IMPACT: None (SSE frames are correct). Only usage analytics/
    //   gamification are affected (Attempt.HintsUsedCount stays at 0).
    //   RECOMMENDED FIX: Wrap fire-and-forget Publish in IServiceScopeFactory.CreateScope()
    //   inside GetHintCommandHandler, or switch to a Singleton/Transient event-bus that
    //   creates its own scope (the standard ASP.NET Core background-work pattern).
    //
    //   This test documents the defect and is marked Skip so the 19 other cases
    //   remain green. The test code itself is correct — it is the feature code that fails.
    // =========================================================================

    [Fact(DisplayName = "TC-11 HintUsedIntegrationEvent [DEFECT]: fire-and-forget publish with Scoped handler " +
                        "causes ObjectDisposedException → HintsUsedCount NOT incremented (AC-7 broken)")]
    [Trait("Category", "SideEffectObservability")]
    public async Task TC11_HintDelivered_HintsUsedCount_Incremented_DEFECT()
    {
        // DEFECT: this test correctly documents that AC-7 is not satisfied.
        // The assertion deliberately fails to surface the defect to the reviewer.
        // Do NOT skip or xunit.Skip — the failure is intentional documentation.

        // Arrange: create a Student + a real Attempt row in the DB so the handler can find it.
        var studentToken = await CreateStudentJwtAsync();
        var studentId    = GetStudentIdFromToken(studentToken);

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = "فكّر في الخطوات بتمعّن."
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa = new StubQuestionAnswerContract
        {
            CorrectAnswer    = StubCorrectAnswer,
            CurrentHintLevel = 1,
            Behavior         = StubQuestionAnswerContract.Mode.ReturnDto
        };

        using var factory = BuildFactory(safety, context, qa);

        int attemptId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var attempt = new Learnexia.Modules.Learning.Domain.Entities.Attempt
            {
                StudentId      = studentId,
                LessonId       = 99,
                Status         = Learnexia.Modules.Learning.Domain.Enums.AttemptStatus.InProgress,
                HintsUsedCount = 0,
                StartedAt      = DateTime.UtcNow
            };
            db.Add(attempt);
            await db.SaveChangesAsync(studentId);
            attemptId = attempt.Id;
        }

        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    QuestionId = 1,
                    AttemptId  = attemptId,
                    Intent     = 2,
                    HintLevel  = (int?)null
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // The SSE stream itself is healthy (preamble + content + done).
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Hint delivery itself must succeed — the defect is in the side effect; body: {0}", body);
        var frames = ParseSseFrames(body);
        frames.Should().Contain(f => f.Event == "done",
            "Hint delivery completes successfully; body: {0}", body);

        // Poll for the side effect — allow up to 5 seconds.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        int hintsUsedCount = 0;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
            var row = await db.Set<Learnexia.Modules.Learning.Domain.Entities.Attempt>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attemptId);
            hintsUsedCount = row?.HintsUsedCount ?? 0;
            if (hintsUsedCount > 0) break;
            await Task.Delay(250);
        }

        // INTENTIONAL DEFECT ASSERTION — this fails to document AC-7 breakage.
        // Fix required in GetHintCommandHandler: wrap fire-and-forget Publish in a
        // fresh IServiceScopeFactory.CreateScope() so the Scoped LearningDbContext
        // in HintUsedIntegrationEventHandler is not disposed before the handler runs.
        hintsUsedCount.Should().BeGreaterThan(0,
            "DEFECT (AC-7): HintUsedIntegrationEvent fire-and-forget publish races against " +
            "request scope disposal — HintUsedIntegrationEventHandler's Scoped LearningDbContext " +
            "is disposed before the background continuation runs. " +
            "Fix: use IServiceScopeFactory.CreateScope() in GetHintCommandHandler when publishing. " +
            "See P3-05 TC-11 test notes.");
    }

    // =========================================================================
    // Helpers for TC-11
    // =========================================================================

    /// <summary>
    /// Decodes the student integer UserId from the JWT token payload.
    /// The Learnexia identity module stores the numeric user id in a custom claim "Id".
    /// Standard claims ("sub", "nameid") carry the email address string, not the integer id.
    /// </summary>
    private static int GetStudentIdFromToken(string jwtToken)
    {
        // JWT is base64url-encoded: header.payload.signature
        var parts   = jwtToken.Split('.');
        var payload = parts[1];

        // Pad to a multiple of 4 for Base64 decoding
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json   = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var doc    = JsonDocument.Parse(json);

        // The Learnexia identity module emits the integer user id as the custom claim "Id".
        // Try that first, then fall back to common claim names used in other token schemes.
        foreach (var claimName in new[]
        {
            "Id",       // Learnexia custom claim (first priority)
            "sub",
            "nameid",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
        })
        {
            if (doc.RootElement.TryGetProperty(claimName, out var val)
                && val.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            {
                if (val.ValueKind == JsonValueKind.Number)
                    return val.GetInt32();
                if (int.TryParse(val.GetString(), out var id))
                    return id;
            }
        }

        throw new InvalidOperationException(
            $"Could not extract integer user id from JWT payload: {json}");
    }

    // =========================================================================
    // TC-12 — Simplify validation:
    //   Body with neither ConceptId nor LessonId → event:error {code:ValidationError}.
    // =========================================================================

    [Fact(DisplayName = "TC-12 Simplify Validation: body with no ConceptId/LessonId → event:error {code:ValidationError}")]
    public async Task TC12_Simplify_MissingContextAnchor_EmitsValidationError()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, SimplifyUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    ConceptId              = (int?)null,
                    LessonId               = (int?)null,  // violates "at least one required" rule
                    PreviousExplanationRef = (string?)null
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // SSE returns 200; validation error is in-band
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE returns 200; validation error is in-band via event:error; body: {0}", body);

        var frames = ParseSseFrames(body);
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "missing ConceptId/LessonId on Simplify must emit event:error; body: {0}", body);

        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue("body: {0}", body);
        codeProp.GetString().Should().Be("ValidationError",
            "must be code=ValidationError; data: {0}", errFrame.Data);
        TryProp(errObj, "message", out var msgProp).Should().BeTrue("body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        frames.Should().NotContain(f => f.Event == "message", "body: {0}", body);
    }

    // =========================================================================
    // TC-13 — Hint validation: QuestionId = 0 → event:error {code:ValidationError}
    // =========================================================================

    [Fact(DisplayName = "TC-13 Hint Validation: QuestionId=0 (not positive) → event:error {code:ValidationError}")]
    public async Task TC13_Hint_QuestionIdZero_EmitsValidationError()
    {
        var studentToken = await CreateStudentJwtAsync();

        var safety  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qa      = new StubQuestionAnswerContract { Behavior = StubQuestionAnswerContract.Mode.ReturnDto };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    QuestionId = 0,   // must be > 0
                    AttemptId  = 1,
                    Intent     = 2
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);

        var frames = ParseSseFrames(body);
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "QuestionId=0 must emit event:error via ValidationBehavior; body: {0}", body);

        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue("body: {0}", body);
        codeProp.GetString().Should().Be("ValidationError",
            "must be code=ValidationError (not UnhandledError); data: {0}", errFrame.Data);

        frames.Should().NotContain(f => f.Event == "message", "body: {0}", body);
    }

    // =========================================================================
    // TC-IDOR — IDOR ownership guard (security fix, HIGH severity):
    //   Student B requests a Hint using Student A's AttemptId.
    //   The StubQuestionAnswerContract.Mode.ReturnNull simulates the adapter's
    //   ownership check returning null (attempt found but not owned by the caller).
    //   → handler must emit event:error (refused); NO content frame, no hint generated.
    //
    // Why stub ReturnNull:
    //   The integration harness cannot easily create two separate students and cross-wire
    //   their attempt ids through the full DB pipeline in a single test. Instead, we set
    //   the QA stub to ReturnNull — which is exactly the signal the real adapter returns
    //   when a.StudentId != studentId (ownership mismatch).  This tests the handler's
    //   null→refuse path end-to-end, which is the critical IDOR closure point.
    //   The adapter's ownership scoping (a.Id == attemptId && a.StudentId == studentId)
    //   is separately covered by the adapter implementation and the contract change.
    // =========================================================================

    [Fact(DisplayName = "TC-IDOR IDOR Guard: Student passes another student's AttemptId " +
                        "→ contract returns null (not owned) → event:error (refused); NO content frame, no hint generated")]
    public async Task TC_IDOR_CrossStudentAttemptId_RefusedWithError_NoContentStreamed()
    {
        // Arrange — Student B is authenticated; the QA stub returns null to simulate
        // the adapter rejecting an attempt that does not belong to this student.
        var studentBToken = await CreateStudentJwtAsync();

        var safety = new StubSafetyLayer
        {
            Behavior       = StubSafetyLayer.Mode.Allowed,
            AllowedContent = "This should never reach the student."
        };
        var context = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };

        // ReturnNull simulates: adapter found the attempt row BUT a.StudentId != authenticatedStudentId
        // → returns null → handler must refuse before any LLM call.
        var qa = new StubQuestionAnswerContract
        {
            Behavior = StubQuestionAnswerContract.Mode.ReturnNull
        };

        using var factory = BuildFactory(safety, context, qa);
        var client = factory.CreateClient();

        // Student B submits AttemptId=9999 (conceptually belonging to Student A).
        var request = new HttpRequestMessage(HttpMethod.Post, HintUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    QuestionId = 1,
                    AttemptId  = 9999,  // belongs to a different student — QA stub returns null
                    Intent     = 2,     // HelperIntent.Hint = 2
                    HintLevel  = (int?)null
                }),
                Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentBToken);
        var response = await client.SendAsync(request);
        var body     = await response.Content.ReadAsStringAsync();

        // Assert — SSE returns 200 (in-band error per wire contract).
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "SSE endpoint returns 200 for all in-band errors; body: {0}", body);

        var frames = ParseSseFrames(body);

        // event:error MUST be present (IDOR attempt refused).
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default,
            "IDOR: cross-student AttemptId must emit event:error (handler refused); body: {0}", body);

        // Error frame must have code + message.
        var errObj = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errObj, "code", out var codeProp).Should().BeTrue(
            "event:error must have 'code'; data: {0}", errFrame.Data);
        TryProp(errObj, "message", out var msgProp).Should().BeTrue(
            "event:error must have 'message'; data: {0}", errFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("body: {0}", body);

        // CRITICAL SECURITY: NO content frame must be emitted — the LLM was never called.
        frames.Should().NotContain(f => f.Event == "message",
            "IDOR: no content must be streamed when the attempt ownership check fails (LLM must not be called); body: {0}", body);

        // No event:done on error (wire contract).
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must not be emitted on IDOR refusal; body: {0}", body);

        // No stack trace in any frame.
        body.Should().NotContain("StackTrace",
            "stack traces must never appear in SSE frames; body: {0}", body);

        // The error code must be generic — must NOT reveal "IDOR", "attempt", "ownership", etc.
        // (anti-enumeration: client cannot distinguish "not found" from "not owned")
        codeProp.GetString().Should().NotContain("Idor",
            "error code must not expose IDOR wording — anti-enumeration; body: {0}", body);
        codeProp.GetString().Should().NotContain("Owned",
            "error code must not expose ownership wording — anti-enumeration; body: {0}", body);
    }
}

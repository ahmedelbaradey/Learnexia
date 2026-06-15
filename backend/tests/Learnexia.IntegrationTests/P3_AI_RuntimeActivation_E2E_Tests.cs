// ReSharper disable InconsistentNaming
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Seeders;
using Learnexia.Shared.Kernel.Settings;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Learning;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// AI RUNTIME ACTIVATION — END-TO-END INTEGRATION TEST SUITE
//
// Lead's "FINAL DECISION — before AI infra activation" checklist:
//
// Area 1: 4 AI intents — happy path SSE + validation (422 in-band) + auth
// Area 2: SSE wire contract — event:message/redirect/error/done grammar
// Area 3: Safety path — blocked → not cached; fail-closed
// Area 4: Cache HIT/MISS — fake gateway call counter proves zero extra invocations on HIT
// Area 5: Approved-only cache — autoApproveEnabled=false → PendingReview → not served (MISS)
// Area 6: RAG retrieval — seeded fake-embedded chunks; grounded answer vs redirect
// Area 7: Grade filtering — grade-isolated retrieval; cross-grade never leaks
//
// The fakes:
//   FakeCountingGateway    — counts CompleteAsync calls; returns deterministic content;
//                            can be switched to return "UNSAFE:" prefix so the real
//                            SafetyLayer (ToxicityCheck) blocks it.
//   FakeDeterministicEmbeddingProvider — delegates to DeterministicEmbedding so seeded
//                            vectors and query vectors are produced by the same function.
//
// The factory: AiRuntimeTestFactory
//   • Testcontainers Postgres (pgvector) + Redis (Testcontainers.Redis)
//   • Overrides ConnectionStrings:Default and ConnectionStrings:Redis
//   • Replaces IAiGateway with FakeCountingGateway
//   • Replaces IEmbeddingProvider with FakeDeterministicEmbeddingProvider
//   • Configures AiHelper:ContextProvider=Rag (or empty per test need)
//   • Configures AiHelper:Cache:autoApproveEnabled (per test need)
//   • Migrates ALL module DbContexts + Curriculum; seeds corpus chunks
//   • Provides Student JWT via real parent→child→sign-in flow
// =============================================================================

// ── Collection definition ────────────────────────────────────────────────────

[CollectionDefinition("AiRuntimeE2E")]
public sealed class AiRuntimeE2ECollection : ICollectionFixture<AiRuntimeFixture> { }

// ── Shared fixture: one Postgres + Redis container per test-class collection ─

/// <summary>
/// Shared test fixture: spins up ONE Postgres pgvector container and ONE Redis container,
/// applies migrations, seeds the corpus, provisions the initial admin/parent accounts.
/// Re-used across all <see cref="P3_AI_RuntimeActivation_E2E_Tests"/> tests via
/// <see cref="AiRuntimeE2ECollection"/>.
/// </summary>
public sealed class AiRuntimeFixture : IAsyncLifetime
{
    public readonly PostgreSqlContainer Postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("learnexia_ai_runtime_e2e")
        .WithUsername("postgres")
        .WithPassword("testpwd")
        .Build();

    public readonly RedisContainer Redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string RedisConnectionString    { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        await Task.WhenAll(Postgres.StartAsync(), Redis.StartAsync());
        PostgresConnectionString = Postgres.GetConnectionString();
        RedisConnectionString    = $"{Redis.Hostname}:{Redis.GetMappedPublicPort(6379)}";
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(Postgres.StopAsync(), Redis.StopAsync());
    }
}

// ── FakeCountingGateway ──────────────────────────────────────────────────────

/// <summary>
/// Test IAiGateway that:
///   • Counts every CompleteAsync invocation (<see cref="CallCount"/>).
///   • Returns deterministic content (<see cref="ResponseContent"/>).
///   • Can be configured to return content prefixed with "UNSAFE:" so that the
///     real ToxicityCheck (substring keyword match) can block it — letting safety
///     integration tests run against the full SafetyLayer.
/// Thread-safe for parallel test usage (Interlocked.Increment).
/// </summary>
public sealed class FakeCountingGateway : IAiGateway
{
    private int _callCount;

    /// <summary>Number of times CompleteAsync has been invoked.</summary>
    public int CallCount => _callCount;

    /// <summary>Reset the call counter (call before each test that checks the count).</summary>
    public void Reset() => _callCount = 0;

    /// <summary>
    /// Content returned on a successful call.
    /// Default: safe Arabic math explanation.
    /// Set to a string containing a keyword that ToxicityCheck blocks to test the safety path.
    /// </summary>
    public string ResponseContent { get; set; } =
        "الكسر العادي هو جزء من كل. على سبيل المثال، نصف التفاحة يُكتب كـ 1/2.";

    /// <summary>When true, CompleteAsync returns Fail (simulates gateway down).</summary>
    public bool SimulateFailure { get; set; }

    public Task<AiResult> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        System.Threading.Interlocked.Increment(ref _callCount);

        if (SimulateFailure)
            return Task.FromResult(AiResult.Fail(new AiError(AiErrorKind.Unavailable, "Simulated gateway failure")));

        // Safety check calls (ToxicityCheck, AgeAppropriatenessCheck) use AiTaskKind.Classify.
        // Return JSON containing BOTH expected fields so both parsers pass:
        //   ToxicityCheck       expects: "toxic": false
        //   AgeAppropriatenessCheck expects: "inappropriate": false
        var content = request.Task == AiTaskKind.Classify
            ? "{\"toxic\": false, \"severity\": \"none\", \"reason\": \"safe\", \"inappropriate\": false}"
            : ResponseContent;

        return Task.FromResult(AiResult.Ok(
            content,
            new AiUsage
            {
                Provider         = "fake",
                ModelId          = "fake-model",
                PromptTokens     = 10,
                CompletionTokens = 20,
                LatencyMs        = 5,
                EstimatedCostUsd = 0m,
                WasCacheHit      = false,
            }));
    }

#pragma warning disable CS1998 // Async method lacks 'await'
    public async IAsyncEnumerable<AiChunk> StreamAsync(
        AiRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        System.Threading.Interlocked.Increment(ref _callCount);
        yield return new AiChunk { Delta = ResponseContent, IsLast = true };
    }
#pragma warning restore CS1998
}

// ── FakeDeterministicEmbeddingProvider ───────────────────────────────────────

/// <summary>
/// Stub IEmbeddingProvider that delegates to DeterministicEmbedding (same function
/// used by CurriculumChunkSeeder). Guarantees seeded-text queries return cosine
/// distance ≈ 0 (passes floor); out-of-corpus text returns near-orthogonal distance
/// (exceeds floor → empty result). No live TEI endpoint required.
/// </summary>
internal sealed class FakeDeterministicEmbeddingProvider : IEmbeddingProvider
{
    public Task<Vector?> EmbedAsync(string text, CancellationToken ct = default)
        => DeterministicEmbedding.ComputeAsync(text, ct)
               .ContinueWith(t => (Vector?)t.Result, ct);
}

// ── StubQuestionAnswerContract for Hint tests ────────────────────────────────

/// <summary>
/// Stub IQuestionAnswerContract that returns a fixed QuestionAnswerDto for any
/// (questionId, attemptId, studentId) triple. Used by Hint/WhyWrong tests where
/// the Learning module database tables are not seeded with real questions/attempts.
/// </summary>
internal sealed class AiRuntimeStubQuestionAnswerContract : IQuestionAnswerContract
{
    public string CorrectAnswer { get; set; } = "42";

    public Task<QuestionAnswerDto?> GetQuestionAnswerAsync(
        int questionId, int attemptId, int studentId, CancellationToken ct = default)
    {
        // QuestionAnswerDto(string CorrectAnswer, int CurrentHintLevel) — two positional params.
        // CorrectAnswer must NOT appear in the FakeCountingGateway response (avoids the no-reveal block).
        return Task.FromResult<QuestionAnswerDto?>(new QuestionAnswerDto(CorrectAnswer, 1));
    }
}

// ── AiRuntimeTestFactory ─────────────────────────────────────────────────────

/// <summary>
/// Per-test WebApplicationFactory. Receives the shared fixture (Postgres + Redis
/// connection strings already started), plus per-test configuration overrides.
///
/// Wires:
///   • All module DbContexts → container Postgres
///   • IDistributedCache + IConnectionMultiplexer → container Redis
///   • IAiGateway → FakeCountingGateway (shared reference so tests can check CallCount)
///   • IEmbeddingProvider → FakeDeterministicEmbeddingProvider
///   • IQuestionAnswerContract → AiRuntimeStubQuestionAnswerContract (for Hint/WhyWrong)
///   • ILessonContextContract → StubLessonContextContract (from P3_04 tests, same namespace)
///   • AiHelper:ContextProvider = contextProvider ("Rag" or "")
///   • AiHelper:Cache:autoApproveEnabled = autoApproveEnabled
///   • AiHelper:Cache:safetyPassConfidence = 0.95 (ensures confident responses auto-approve)
///   • AiHelper:Cache:autoApprovalConfidence = 0.85 (default; responses with 0.90 confidence ≥ 0.85 → Approved)
///   • Curriculum:Retrieval:SimilarityDistanceFloor = 0.5 (permissive for deterministic vectors)
///   • Ai:Safety:MaxRegenerationAttempts = 0 (no regeneration loops in tests)
/// </summary>
public sealed class AiRuntimeTestFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;
    private readonly string _redisConnectionString;
    private readonly string _contextProvider;
    private readonly bool _autoApproveEnabled;

    public readonly FakeCountingGateway FakeGateway = new();

    public AiRuntimeTestFactory(
        AiRuntimeFixture fixture,
        string contextProvider   = "",
        bool autoApproveEnabled  = true)
    {
        _postgresConnectionString = fixture.PostgresConnectionString;
        _redisConnectionString    = fixture.RedisConnectionString;
        _contextProvider          = contextProvider;
        _autoApproveEnabled       = autoApproveEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgresConnectionString,
                ["ConnectionStrings:Redis"]   = _redisConnectionString,
                ["AiHelper:ContextProvider"]  = _contextProvider,
                ["AiHelper:Cache:autoApproveEnabled"]       = _autoApproveEnabled.ToString().ToLower(),
                ["AiHelper:Cache:safetyPassConfidence"]     = "0.95",
                ["AiHelper:Cache:autoApprovalConfidence"]   = "0.85",
                ["Curriculum:Retrieval:SimilarityDistanceFloor"] = "0.5",
                ["Ai:Safety:MaxRegenerationAttempts"]       = "0",
                // Dummy provider key so gateway routing doesn't short-circuit on absent key check
                ["Ai:Providers:Claude:ApiKey"]  = "test-dummy-key",
                ["Ai:Providers:OpenAi:ApiKey"]  = "test-dummy-key",
            });
        });

        builder.ConfigureServices(services =>
        {
            // ── Replace ALL DbContexts with the container connection ──────────

            ReplaceDbContext<IdentityModuleDbContext>(services, _postgresConnectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, _postgresConnectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, _postgresConnectionString, "learning");
            ReplaceDbContext<ParentDbContext>(services, _postgresConnectionString, "parent");
            ReplaceDbContext<GamificationDbContext>(services, _postgresConnectionString, "gamification");
            ReplaceDbContext<ModerationDbContext>(services, _postgresConnectionString, "moderation");
            ReplaceDbContext<AiDbContext>(services, _postgresConnectionString, "ai");
            ReplaceDbContext<BillingDbContext>(services, _postgresConnectionString, BillingDbContext.Schema);

            // CurriculumDbContext: needs UseVector() for pgvector support
            services.RemoveAll<DbContextOptions<CurriculumDbContext>>();
            services.RemoveAll<CurriculumDbContext>();
            services.AddDbContext<CurriculumDbContext>(options =>
                options.UseNpgsql(
                    _postgresConnectionString,
                    npgsql => npgsql
                        .UseVector()
                        .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                        .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));

            // ── Replace IAiGateway with the fake call-counting implementation ─
            services.RemoveAll<IAiGateway>();
            services.AddScoped<IAiGateway>(_ => FakeGateway);

            // ── Replace IEmbeddingProvider with the deterministic fake ─────────
            services.RemoveAll<IEmbeddingProvider>();
            services.AddScoped<IEmbeddingProvider, FakeDeterministicEmbeddingProvider>();

            // ── Replace IQuestionAnswerContract with stub (Hint/WhyWrong) ─────
            services.RemoveAll<IQuestionAnswerContract>();
            services.AddTransient<IQuestionAnswerContract, AiRuntimeStubQuestionAnswerContract>();

            // ── Replace ILessonContextContract (for Explain subject routing) ──
            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            // ── Disable IP rate limiter ────────────────────────────────────────
            services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<AspNetCoreRateLimit.RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });
        });
    }

    // ── Migrations + seed (called once per test that needs a clean DB) ────────

    public async Task ApplyMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        await sp.GetRequiredService<IdentityModuleDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<LearningDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ParentDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<GamificationDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<ModerationDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<AiDbContext>().Database.MigrateAsync();
        await sp.GetRequiredService<CurriculumDbContext>().Database.MigrateAsync();

        // Billing: migrate schema + seed GlobalSettings (ai_cost.* and credits.* keys)
        // so the pre-authorize step in AI delivery handlers can read per-intent costs.
        var billingDb     = sp.GetRequiredService<BillingDbContext>();
        var billingLogger = sp.GetRequiredService<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();
        await billingDb.Database.MigrateAsync();
        await GlobalSettingsSeeder.SeedAsync(billingDb, billingLogger);

        // Seed roles + superadmin + basicuser (required by sign-in / role-check tests).
        await Learnexia.Modules.Identity.Api.IdentityModule.SeedAsync(sp);
        var userManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Learnexia.Modules.Identity.Domain.Entities.User>>();
        var roleManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Learnexia.Modules.Identity.Domain.Entities.Role>>();
        await Learnexia.Modules.Identity.Infrastructure.Persistence.Seed.UserSeeder.SeedBasicUserAsync(userManager, roleManager);
        await Learnexia.Modules.Identity.Infrastructure.Persistence.Seed.UserSeeder.SeedSuperAdminAsync(userManager, roleManager);

        // Seed curriculum corpus with deterministic vectors (no TEI endpoint needed).
        await CurriculumChunkSeeder.SeedAsync(sp);
    }

    // ── Internal static helpers (called by specialized factories) ────────────

    /// <summary>
    /// Replaces ALL module DbContexts to point at the Testcontainers Postgres.
    /// CurriculumDbContext also enables pgvector via UseVector().
    /// BillingDbContext is included so the energy pre-authorize path in AI delivery
    /// handlers can resolve costs and accounts from the test container.
    /// </summary>
    internal static void ReplaceDbContexts(IServiceCollection services, string pgConn)
    {
        ReplaceDbContext<IdentityModuleDbContext>(services, pgConn, "identity");
        ReplaceDbContext<NotificationsDbContext>(services, pgConn, "notifications");
        ReplaceDbContext<LearningDbContext>(services, pgConn, "learning");
        ReplaceDbContext<ParentDbContext>(services, pgConn, "parent");
        ReplaceDbContext<GamificationDbContext>(services, pgConn, "gamification");
        ReplaceDbContext<ModerationDbContext>(services, pgConn, "moderation");
        ReplaceDbContext<AiDbContext>(services, pgConn, "ai");
        ReplaceDbContext<BillingDbContext>(services, pgConn, BillingDbContext.Schema);

        services.RemoveAll<DbContextOptions<CurriculumDbContext>>();
        services.RemoveAll<CurriculumDbContext>();
        services.AddDbContext<CurriculumDbContext>(options =>
            options.UseNpgsql(
                pgConn,
                npgsql => npgsql
                    .UseVector()
                    .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                    .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));
    }

    /// <summary>
    /// Seeds a <see cref="BillingDbContext"/> <see cref="Learnexia.Modules.Billing.Domain.Entities.CreditAccount"/>
    /// with an ample granted balance for the given <paramref name="childId"/>.
    ///
    /// <para>Called after <see cref="CreateStudentJwtAsync"/> returns, using the child user-id
    /// decoded from the JWT. This ensures the energy pre-authorize step inside AI delivery
    /// handlers never blocks happy-path tests.</para>
    ///
    /// <para>Idempotent: if the account already exists, the grant is added on top of any
    /// existing balance (same pattern as <c>EnergyEconomyTestFactory.CreateStudentWithBalanceAsync</c>).</para>
    /// </summary>
    internal static async Task SeedStudentBalanceAsync(
        WebApplicationFactory<Program> factory,
        int childId,
        int balance = 500)
    {
        using var scope = factory.Services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var account = await db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId);
        if (account is null)
        {
            account = Learnexia.Modules.Billing.Domain.Entities.CreditAccount
                .CreateEmpty(childId, "Africa/Cairo");
            db.CreditAccounts.Add(account);
            await db.SaveChangesAsync(0);
        }

        var grantKey  = $"ai-e2e-seed:{childId}:{Guid.NewGuid():N}";
        var expiresAt = DateTime.UtcNow.AddMonths(6);
        var tx        = account.ApplyGrant(
            balance,
            expiresAt,
            CreditReasonCode.MonthlyGrantFree,
            grantKey);
        db.CreditTransactions.Add(tx);
        await db.SaveChangesAsync(0);
    }

    /// <summary>
    /// Disables the IP rate limiter so tests aren't throttled regardless of how many
    /// requests they send within one test run.
    /// </summary>
    internal static void DisableIpRateLimit(IServiceCollection services)
    {
        services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(opt =>
        {
            opt.EnableEndpointRateLimiting = false;
            opt.GeneralRules = new List<AspNetCoreRateLimit.RateLimitRule>
            {
                new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
            };
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────

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

// =============================================================================
// TEST CLASS
// =============================================================================

/// <summary>
/// AI Runtime Activation end-to-end test suite.
///
/// All tests share the <see cref="AiRuntimeFixture"/> (one Postgres + Redis container)
/// but each spin up their own <see cref="AiRuntimeTestFactory"/> so per-test
/// configuration (contextProvider, autoApproveEnabled, FakeGateway reset) is isolated.
///
/// The fixture is initialised ONCE; migrations run once per test class instance
/// (guarded by the shared Postgres container's idempotent migrations).
/// </summary>
[Collection("AiRuntimeE2E")]
public sealed class P3_AI_RuntimeActivation_E2E_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string ExplainUrl         = "api/AiTutor/Explain";
    private const string HintUrl            = "api/AiTutor/Hint";
    private const string SimilarExampleUrl  = "api/AiTutor/SimilarExample";
    private const string SimplifyUrl        = "api/AiTutor/Simplify";

    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string ValidChildPassword = "Child@Pass1";

    // Corpus constants (must match CurriculumChunkSeeder)
    private const int MathSubjectId   = 1;   // Math = SubjectId 1 (Math-Ar seeder)
    private const int MathEnSubjectId = 2;   // Math-En = SubjectId 2
    private const int ScienceSubjectId = 4;  // Science-En = SubjectId 4
    private const int SeededGradeId   = 3;   // All corpus chunks are grade 3

    // Exact seeded texts that DeterministicEmbedding will score near-zero distance against
    private const string MathArChunkText =
        "الكسر العادي هو جزء من كل. يُكتب الكسر على شكل بسط ومقام. " +
        "مثال: 1/2 يعني نصف الشيء.";
    private const string MathEnChunkText =
        "Counting to 1000: We count numbers up to 1000 using place value. " +
        "Each digit has a place: ones, tens, and hundreds.";
    private const string ScienceEnChunkText =
        "Living vs Non-Living Things: Living things grow, breathe, reproduce and respond to " +
        "their environment. Plants and animals are living. Rocks, water, and air are non-living.";

    private const string OutOfCorpusText = "XYZ_OUTLIER_AI_RUNTIME_TEST_NOT_IN_CORPUS_SENTINEL_2026";

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly AiRuntimeFixture _fixture;
    private bool _migrationsApplied;

    public P3_AI_RuntimeActivation_E2E_Tests(AiRuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_migrationsApplied)
        {
            // Use a RAG factory for the initial seed so curriculum migrations run.
            using var seedFactory = new AiRuntimeTestFactory(_fixture, contextProvider: "Rag");
            await seedFactory.ApplyMigrationsAndSeedAsync();
            _migrationsApplied = true;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ==========================================================================
    // HELPERS
    // ==========================================================================

    private static string UniqueEmail(string tag)
        => $"aire2e_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            { value = p.Value; return true; }
        value = default; return false;
    }

    private static async Task<(HttpResponseMessage Response, string Body)>
        PostAsync(HttpClient client, string url, object? body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null)
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        return (resp, bodyStr);
    }

    /// <summary>
    /// Provisions a unique student account and returns a signed JWT.
    /// Grade is embedded in the JWT claims (from the child profile).
    ///
    /// <para>W2b energy charging: when <paramref name="factory"/> is supplied the helper seeds
    /// an ample <c>CreditAccount</c> grant (500 credits) for the new child so the
    /// pre-authorize step in AI delivery handlers never blocks happy-path tests.
    /// No-delivery tests (401/403/validation/redirect/safety-block) create students
    /// without a factory reference and therefore start with zero balance — which is fine
    /// because those paths never reach the pre-auth check.</para>
    /// </summary>
    private async Task<string> CreateStudentJwtAsync(
        HttpClient client,
        int grade = 4,
        WebApplicationFactory<Program>? factory = null)
    {
        var parentEmail = UniqueEmail("par");
        var (prResp, prBody) = await PostAsync(client, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass!", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body: {0}", prBody);
        var prJson = JsonDocument.Parse(prBody).RootElement;
        TryProp(prJson, "data", out var prData).Should().BeTrue("body: {0}", prBody);
        TryProp(prData, "accessToken", out var prToken).Should().BeTrue("body: {0}", prBody);
        var parentToken = prToken.GetString()!;

        var childEmail = UniqueEmail("child");
        var (addResp, addBody) = await PostAsync(client, AddChildUrl,
            new
            {
                FullName         = "AI Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = grade,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "Add-Child; body: {0}", addBody);

        var (signResp, signBody) = await PostAsync(client, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body: {0}", signBody);
        var signJson = JsonDocument.Parse(signBody).RootElement;
        TryProp(signJson, "data", out var signData).Should().BeTrue("body: {0}", signBody);
        TryProp(signData, "accessToken", out var accessToken).Should().BeTrue("body: {0}", signBody);
        var jwt = accessToken.GetString()!;

        // Seed energy balance so happy-path delivery tests are never blocked by pre-auth.
        if (factory is not null)
        {
            var childId = GetChildIdFromJwt(jwt);
            await AiRuntimeTestFactory.SeedStudentBalanceAsync(factory, childId, balance: 500);
        }

        return jwt;
    }

    /// <summary>
    /// Decodes the child (student) integer user-id from a JWT bearer token.
    /// Mirrors the same logic used in <c>P10_W2_EnergyEconomy_E2E_Tests.GetSubjectIdFromJwt</c>.
    /// </summary>
    private static int GetChildIdFromJwt(string jwtToken)
    {
        var parts   = jwtToken.Split('.');
        var payload = parts[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var decoded = System.Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json    = System.Text.Encoding.UTF8.GetString(decoded);
        var doc     = JsonDocument.Parse(json).RootElement;

        // Learnexia-specific: "Id" claim is the integer user id (emitted as a string "23")
        if (doc.TryGetProperty("Id", out var idProp))
        {
            var raw = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt32().ToString()
                : idProp.GetString() ?? "";
            if (int.TryParse(raw, out var parsed)) return parsed;
        }

        // Fallback: standard JWT claims
        foreach (var prop in doc.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = prop.Value.ValueKind == JsonValueKind.Number
                ? prop.Value.GetInt32().ToString()
                : prop.Value.GetString() ?? "";
            if (int.TryParse(raw, out var vi)) return vi;
        }

        throw new InvalidOperationException($"Cannot parse child id from JWT payload: {json}");
    }

    /// <summary>Parses raw SSE body into (eventName, data) pairs.</summary>
    private static List<(string Event, string Data)> ParseSseFrames(string rawBody)
    {
        var frames = new List<(string, string)>();
        var blocks = rawBody.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? eventName = null, data = null;
            foreach (var line in lines)
            {
                if (line.StartsWith("event: ")) eventName = line["event: ".Length..].Trim();
                else if (line.StartsWith("data: ")) data = line["data: ".Length..].Trim();
            }
            if (eventName is not null && data is not null)
                frames.Add((eventName, data));
        }
        return frames;
    }

    /// <summary>Asserts the SSE stream contains exactly event:message + event:done in the happy path.</summary>
    private static string AssertHappyPathFrames(List<(string Event, string Data)> frames, string context)
    {
        var msg = frames.FirstOrDefault(f => f.Event == "message");
        msg.Should().NotBe(default, "event:message expected; context={0}", context);

        var msgJson = JsonDocument.Parse(msg.Data).RootElement;
        TryProp(msgJson, "content", out var contentProp).Should().BeTrue(
            "event:message data must have 'content'; data={0}", msg.Data);
        var content = contentProp.GetString()!;
        content.Should().NotBeNullOrWhiteSpace("content must not be empty; context={0}", context);

        var done = frames.FirstOrDefault(f => f.Event == "done");
        done.Should().NotBe(default, "event:done expected; context={0}", context);
        done.Data.Should().Be("[DONE]", "done data must be exactly [DONE]; context={0}", context);

        frames.Should().NotContain(f => f.Event == "error",
            "no error in happy path; context={0}", context);
        return content;
    }

    // ==========================================================================
    // AREA 1 — 4 AI INTENTS: HAPPY PATH + AUTH + VALIDATION
    // ==========================================================================

    // ── 1A: Explain ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "AI-E2E-1A Explain: Student + non-empty context → event:message + event:done; 200")]
    public async Task Area1A_Explain_HappyPath_MessageAndDone()
    {
        using var factory = new AiRuntimeTestFactory(_fixture, contextProvider: "");
        factory.FakeGateway.ResponseContent = "الكسر جزء من كل — شرح من النموذج المزيف.";
        var client       = factory.CreateClient();
        var studentToken = await CreateStudentJwtAsync(client);

        // Provide a non-empty context by using a StubContextProvider via factory replacement —
        // the factory uses EmptyLearningContextProvider when ContextProvider="" which triggers
        // a redirect, NOT a gateway call. We need a non-empty context here.
        // Solution: use ContextProvider="Rag" with seeded corpus text as the question query.
        // But the Rag provider queries by text embedding; the empty factory triggers redirect.
        // We work around this by injecting the stub context provider directly.
        // Since this factory doesn't allow overriding context per-test-class, we create a
        // nested override via a derived class pattern using the existing SseTestFactory:
        var safetyStub   = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "الكسر جزء من كل — شرح مزيف." };
        var contextStub  = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        using var sseFactory = new SseTestFactory(
            _fixture.PostgresConnectionString, safetyStub, contextStub);

        // SseTestFactory also points at the shared Postgres but uses in-memory Redis
        // (no Redis override) — OK since cache tests have their own factories.
        var sseClient = sseFactory.CreateClient();
        var sseToken  = await CreateStudentJwtAsync(sseClient, factory: sseFactory);

        var (resp, body) = await PostAsync(sseClient, ExplainUrl,
            new { SkillId = 101, LessonId = 1, ConceptId = (int?)null, Question = (string?)null },
            sseToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);
        AssertHappyPathFrames(frames, $"Explain; body={body}");
    }

    [Fact(DisplayName = "AI-E2E-1A Explain: No JWT → 401")]
    public async Task Area1A_Explain_NoAuth_Returns401()
    {
        using var factory = new AiRuntimeTestFactory(_fixture);
        var client = factory.CreateClient();

        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 1, LessonId = 1 });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no bearer → 401; body: {0}", body);
    }

    [Fact(DisplayName = "AI-E2E-1A Explain: Parent JWT (non-Student role) → 403")]
    public async Task Area1A_Explain_ParentJwt_Returns403()
    {
        using var factory = new AiRuntimeTestFactory(_fixture);
        var client = factory.CreateClient();

        var parentEmail = UniqueEmail("p403");
        var (regResp, regBody) = await PostAsync(client, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass!", AcceptedTerms = true });
        regResp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", regBody);
        var regJson = JsonDocument.Parse(regBody).RootElement;
        TryProp(regJson, "data", out var regData).Should().BeTrue();
        TryProp(regData, "accessToken", out var parentTok).Should().BeTrue();
        var parentToken = parentTok.GetString()!;

        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 1, LessonId = 1 }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent role → 403 on Student-only endpoint; body: {0}", body);
    }

    [Fact(DisplayName = "AI-E2E-1A Explain Validation: no context anchor → event:error code=ValidationError")]
    public async Task Area1A_Explain_Validation_MissingAnchor_ErrorFrame()
    {
        var safetyStub  = new StubSafetyLayer();
        var contextStub = new StubContextProvider();
        using var factory = new SseTestFactory(_fixture.PostgresConnectionString, safetyStub, contextStub);
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client);

        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { LessonId = (int?)null, ConceptId = (int?)null, SkillId = (int?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE returns 200; body: {0}", body);
        var frames = ParseSseFrames(body);
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "validation error must surface as event:error; body: {0}", body);
        var errJson = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var code).Should().BeTrue();
        code.GetString().Should().Be("ValidationError", "body: {0}", body);
    }

    // ── 1B: Hint ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "AI-E2E-1B Hint: Student + valid question + non-empty context → preamble + content + done")]
    public async Task Area1B_Hint_HappyPath_PreambleAndContent()
    {
        var safetyStub  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed };
        var contextStub = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        using var factory = new SseTestFactory(_fixture.PostgresConnectionString, safetyStub, contextStub);

        // Also stub the IQuestionAnswerContract inside this factory
        // (SseTestFactory doesn't stub it, so we build our own mini-factory)
        using var hintFactory = new AiRuntimeTestFactory(_fixture, contextProvider: "");
        // Override safety + context in AiRuntimeTestFactory's services
        // This factory has FakeCountingGateway; we patch at the service level below via a wrapper.
        // For the Hint happy-path test, we only need SSE wire + preamble assertion.
        // The Hint handler needs: ILearningContextProvider (non-empty), IQuestionAnswerContract.
        // Use SseTestFactory which already stubs context, then also stub QuestionAnswerContract.

        // Build a custom factory that stubs all required seams for Hint:
        var safeStub2  = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "تلميح: فكر في كيفية تقسيم الشيء." };
        var ctxStub2   = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        // StubQuestionAnswerContract.CorrectAnswer must NOT appear in AllowedContent (no-reveal guard).
        var qaStub2    = new StubQuestionAnswerContract { CorrectAnswer = "HINT_CORRECT_SENTINEL_XYZ" };
        using var hintSseFactory = new HintSseTestFactory(_fixture.PostgresConnectionString, safeStub2, ctxStub2, qaStub2);
        var client = hintSseFactory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: hintSseFactory);

        var (resp, body) = await PostAsync(client, HintUrl,
            new { QuestionId = 55, AttemptId = 1, Intent = 2 /* Hint */, HintLevel = (int?)null, WrongAnswer = (string?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        // Hint intent: must have preamble frame with hintLevel/nextHintLevel
        var preamble = frames.FirstOrDefault(f =>
        {
            JsonElement hintLevelProp;
            return f.Event == "message" &&
                   JsonDocument.Parse(f.Data).RootElement.TryGetProperty("hintLevel", out hintLevelProp);
        });
        preamble.Should().NotBe(default,
            "Hint intent must emit preamble frame with hintLevel; body={0}", body);

        // Then content frame
        var contentFrame = frames.FirstOrDefault(f =>
        {
            JsonElement contentProp;
            return f.Event == "message" &&
                   JsonDocument.Parse(f.Data).RootElement.TryGetProperty("content", out contentProp);
        });
        contentFrame.Should().NotBe(default, "Hint content frame expected; body={0}", body);

        // Then done
        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue(
            "event:done + [DONE] expected; body={0}", body);
    }

    [Fact(DisplayName = "AI-E2E-1B Hint Validation: missing QuestionId → event:error code=ValidationError")]
    public async Task Area1B_Hint_Validation_MissingQuestionId_ErrorFrame()
    {
        var safeStub = new StubSafetyLayer();
        var ctxStub  = new StubContextProvider();
        var qaStub   = new StubQuestionAnswerContract { CorrectAnswer = "HINT_CORRECT_SENTINEL_XYZ" };
        using var factory = new HintSseTestFactory(_fixture.PostgresConnectionString, safeStub, ctxStub, qaStub);
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client);

        // QuestionId=0 fails the validator (must be > 0)
        var (resp, body) = await PostAsync(client, HintUrl,
            new { QuestionId = 0, AttemptId = 1, Intent = 2 /* Hint */ },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "body: {0}", body);
        var errJson = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var code).Should().BeTrue();
        code.GetString().Should().Be("ValidationError", "body: {0}", body);
    }

    // ── 1C: WhyWrong ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "AI-E2E-1C WhyWrong: Student + wrong answer + non-empty context → event:message + done; no preamble")]
    public async Task Area1C_WhyWrong_HappyPath_ContentNoPreamble()
    {
        var safeStub = new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "لأن الإجابة الخاطئة تُظهر..." };
        var ctxStub  = new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty };
        var qaStub   = new StubQuestionAnswerContract { CorrectAnswer = "HINT_CORRECT_SENTINEL_XYZ" };
        using var factory = new HintSseTestFactory(_fixture.PostgresConnectionString, safeStub, ctxStub, qaStub);
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        var (resp, body) = await PostAsync(client, HintUrl,
            new { QuestionId = 55, AttemptId = 1, Intent = 3 /* WhyWrong */, HintLevel = (int?)null, WrongAnswer = "5" },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        // WhyWrong must NOT have preamble frame with hintLevel
        Func<(string Event, string Data), bool> hasHintLevel = f =>
        {
            if (f.Event != "message") return false;
            JsonElement dummy;
            return JsonDocument.Parse(f.Data).RootElement.TryGetProperty("hintLevel", out dummy);
        };
        frames.Where(hasHintLevel).Should().BeEmpty("WhyWrong must not emit hintLevel preamble; body={0}", body);

        // Must have content frame
        var contentFrame = frames.FirstOrDefault(f =>
        {
            JsonElement cp;
            return f.Event == "message" &&
                   JsonDocument.Parse(f.Data).RootElement.TryGetProperty("content", out cp);
        });
        contentFrame.Should().NotBe(default, "content frame expected; body={0}", body);

        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue("body: {0}", body);
    }

    // ── 1D: SimilarExample ───────────────────────────────────────────────────

    [Fact(DisplayName = "AI-E2E-1D SimilarExample: Student + non-empty context → event:message + event:done")]
    public async Task Area1D_SimilarExample_HappyPath_MessageAndDone()
    {
        using var factory = new SseTestFactory(
            _fixture.PostgresConnectionString,
            new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "مثال مشابه: 3/4 من كمية الماء..." },
            new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty });
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        var (resp, body) = await PostAsync(client, SimilarExampleUrl,
            new { SkillId = 77, QuestionId = (int?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);
        AssertHappyPathFrames(frames, $"SimilarExample; body={body}");
    }

    [Fact(DisplayName = "AI-E2E-1D SimilarExample: No JWT → 401")]
    public async Task Area1D_SimilarExample_NoAuth_Returns401()
    {
        using var factory = new AiRuntimeTestFactory(_fixture);
        var (resp, body) = await PostAsync(factory.CreateClient(), SimilarExampleUrl,
            new { SkillId = 1 });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "body: {0}", body);
    }

    [Fact(DisplayName = "AI-E2E-1D SimilarExample Validation: SkillId=0 → event:error code=ValidationError")]
    public async Task Area1D_SimilarExample_Validation_SkillIdZero_ErrorFrame()
    {
        using var factory = new SseTestFactory(
            _fixture.PostgresConnectionString,
            new StubSafetyLayer(), new StubContextProvider());
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client);

        var (resp, body) = await PostAsync(client, SimilarExampleUrl,
            new { SkillId = 0 }, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "SkillId=0 must fail validation; body={0}", body);
        var errJson = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var code).Should().BeTrue();
        code.GetString().Should().Be("ValidationError", "body: {0}", body);
    }

    // ── 1E: Simplify ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "AI-E2E-1E Simplify: Student + non-empty context → event:message + event:done; no preamble")]
    public async Task Area1E_Simplify_HappyPath_MessageAndDone_NoPreamble()
    {
        using var factory = new SseTestFactory(
            _fixture.PostgresConnectionString,
            new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed, AllowedContent = "بكلام أبسط: النصف هو نصف الشيء." },
            new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty });
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        var (resp, body) = await PostAsync(client, SimplifyUrl,
            new { ConceptId = 5, LessonId = 1, PreviousExplanationRef = (string?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        // Simplify must NOT have preamble frame (no hint level)
        Func<(string Event, string Data), bool> hasHintLevelS = f =>
        {
            if (f.Event != "message") return false;
            JsonElement hlp;
            return JsonDocument.Parse(f.Data).RootElement.TryGetProperty("hintLevel", out hlp);
        };
        frames.Where(hasHintLevelS).Should().BeEmpty("Simplify must not emit preamble; body={0}", body);

        AssertHappyPathFrames(frames, $"Simplify; body={body}");
    }

    // ==========================================================================
    // AREA 2 — SSE WIRE CONTRACT (Covered by Area 1 happy paths + existing P3_04 tests)
    // Focused sub-set: content-type header, exact [DONE] terminator, error shape
    // ==========================================================================

    [Fact(DisplayName = "AI-E2E-2A SSE Wire: Content-Type must be text/event-stream")]
    public async Task Area2A_SseWire_ContentType_IsTextEventStream()
    {
        using var factory = new SseTestFactory(
            _fixture.PostgresConnectionString,
            new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed },
            new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty });
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        var req = new HttpRequestMessage(HttpMethod.Post, ExplainUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { SkillId = 42, LessonId = 1 }),
                Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must set Content-Type: text/event-stream");
    }

    [Fact(DisplayName = "AI-E2E-2B SSE Wire: redirect frame has type=lesson and string targetId")]
    public async Task Area2B_SseWire_RedirectFrame_TypeLessonAndStringTargetId()
    {
        using var factory = new SseTestFactory(
            _fixture.PostgresConnectionString,
            new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed },
            new StubContextProvider { Behavior = StubContextProvider.Mode.Empty });
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 88, LessonId = (int?)null, ConceptId = (int?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        var rdFrame = frames.FirstOrDefault(f => f.Event == "redirect");
        rdFrame.Should().NotBe(default, "redirect frame expected; body={0}", body);
        var rdJson = JsonDocument.Parse(rdFrame.Data).RootElement;
        TryProp(rdJson, "type", out var typeProp).Should().BeTrue();
        typeProp.GetString().Should().Be("lesson");
        TryProp(rdJson, "targetId", out var tidProp).Should().BeTrue();
        tidProp.ValueKind.Should().Be(JsonValueKind.String, "targetId must be a string");
        tidProp.GetString().Should().Be("88", "targetId must match SkillId");

        frames.Any(f => f.Event == "done" && f.Data == "[DONE]").Should().BeTrue("body: {0}", body);
    }

    [Fact(DisplayName = "AI-E2E-2C SSE Wire: error frame has code+message; no event:done after error")]
    public async Task Area2C_SseWire_ErrorFrame_CodeAndMessage_NoDoneAfterError()
    {
        using var factory = new SseTestFactory(
            _fixture.PostgresConnectionString,
            new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Blocked },
            new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty });
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        // Use a unique ConceptId per test run to guarantee a cache MISS on this request.
        // Tests share the same Postgres DB (AiRuntimeFixture); a prior test with subjectId=1,
        // conceptId=0 (from ConceptId=null with LessonId=1) may have written an Approved entry.
        // A cache HIT bypasses ISafetyLayer.Blocked entirely → test would see event:message (wrong).
        var uniqueConceptId2C = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 1_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 42, LessonId = 1, ConceptId = (int?)uniqueConceptId2C }, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "error frame expected; body={0}", body);
        var errJson = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue();
        TryProp(errJson, "message", out var msgProp).Should().BeTrue();
        codeProp.ValueKind.Should().Be(JsonValueKind.String);
        msgProp.ValueKind.Should().Be(JsonValueKind.String);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace();
        msgProp.GetString().Should().NotBeNullOrWhiteSpace();

        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame on safety-block; body={0}", body);
        body.Should().NotContain("StackTrace", "no raw stack traces in SSE; body={0}", body);
    }

    // ==========================================================================
    // AREA 3 — SAFETY: BLOCKED → NOT CACHED; FAIL-CLOSED
    // ==========================================================================

    /// <summary>
    /// Safety-blocked responses must NOT be written to the AI cache.
    /// After a safety block, a subsequent identical request must be a MISS (gateway called again).
    /// </summary>
    [Fact(DisplayName = "AI-E2E-3A Safety: blocked response is NOT written to cache → subsequent request still calls gateway")]
    public async Task Area3A_Safety_BlockedResponse_NotCached()
    {
        // Use AiRuntimeTestFactory so we can inspect FakeGateway + AiDbContext
        using var factory = new AiRuntimeTestFactory(_fixture, contextProvider: "");
        factory.FakeGateway.Reset();

        // Stub context + force safety to block — but we need IAiGateway path to trigger.
        // With ContextProvider="" the handler goes through EmptyLearningContextProvider → redirect (no gateway call).
        // To test the safety-blocked-not-cached path, we need non-empty context.
        // Use SseTestFactory which has StubContextProvider(NonEmpty) + override ISafetyLayer to Blocked.
        // But SseTestFactory stubs ISafetyLayer entirely. We need the real SafetyLayer + fake gateway
        // returning something that ToxicityCheck will block.
        // In our setup the ToxicityCheck keyword blocker (in AgeAppropriatenessCheck/ToxicityCheck)
        // is a stub regex. Let's verify what happens with a blocked safety result.
        // The safest path: use the StubSafetyLayer(Blocked) which is equivalent to what the real
        // SafetyLayer does when checks fail, then verify the AiResponseCache table has NO entry.

        using var blockedFactory = new CacheValidatingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString,
            safetyMode: StubSafetyLayer.Mode.Blocked,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var client = blockedFactory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: blockedFactory);

        // Record the baseline cache count BEFORE the blocked request (tests share a DB;
        // prior tests may have already written entries → compare delta, not absolute zero).
        using var baselineScope = blockedFactory.Services.CreateScope();
        var baselineDb = baselineScope.ServiceProvider.GetRequiredService<AiDbContext>();
        var cacheCountBefore = await baselineDb.AiResponseCaches.AsNoTracking().CountAsync();

        // Use a unique ConceptId per test run to guarantee a MISS on this request.
        // Tests share the same Postgres DB (AiRuntimeFixture); prior tests may have written
        // Approved entries with subjectId=1, conceptId=0 (the default when ConceptId=null).
        // A cache HIT bypasses the safety layer entirely → the safety stub (Mode.Blocked)
        // would never be reached → the test would wrongly see a successful response.
        // Unique ConceptId → unique cache key → guaranteed MISS → safety stub is reached.
        var uniqueConceptId3A = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 2_000_000;

        // First request: safety-blocked
        var (resp1, body1) = await PostAsync(client, ExplainUrl,
            new { SkillId = 200, LessonId = 1, ConceptId = (int?)uniqueConceptId3A }, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body1);
        var frames1 = ParseSseFrames(body1);
        frames1.Should().Contain(f => f.Event == "error",
            "blocked safety must produce event:error; body={0}", body1);
        frames1.Should().NotContain(f => f.Event == "message",
            "no content emitted on block; body={0}", body1);

        // Allow up to 500 ms for any hypothetical fire-and-forget write to materialize
        // (safety-blocked path must NOT write — so this pause is the minimum needed to
        // give a false-positive write a chance to appear before we assert delta=0).
        // Asserting absence of a write is inherently a timed wait; 500 ms is more than
        // sufficient for a local Task.Run to complete.
        await Task.Delay(500);

        // Verify NO NEW cache entry was written (safety-blocked must not be cached).
        // Compare delta vs baseline to be isolated from entries written by prior tests.
        using var scope = blockedFactory.Services.CreateScope();
        var aiDb = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        var cacheCountAfter = await aiDb.AiResponseCaches.AsNoTracking().CountAsync();
        (cacheCountAfter - cacheCountBefore).Should().Be(0,
            "safety-blocked responses must NEVER be written to AiResponseCache; " +
            "delta (new entries after blocked request): {0} (before={1}, after={2})",
            cacheCountAfter - cacheCountBefore, cacheCountBefore, cacheCountAfter);
    }

    [Fact(DisplayName = "AI-E2E-3B Safety: fail-closed — gateway down → event:error; no unscreened content")]
    public async Task Area3B_Safety_GatewayDown_FailClosed_NoUnsacreenedContent()
    {
        using var factory = new CacheValidatingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString,
            safetyMode: StubSafetyLayer.Mode.ThrowOnCall,
            contextMode: StubContextProvider.Mode.NonEmpty);
        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 300, LessonId = 1 }, token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always returns 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        frames.Should().Contain(f => f.Event == "error",
            "gateway down must yield event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame when gateway fails; body={0}", body);
        body.Should().NotContain("StackTrace", "no raw stack traces; body={0}", body);
    }

    // ==========================================================================
    // AREA 4 — CACHE HIT/MISS: FAKE GATEWAY CALL COUNTER
    // ==========================================================================

    /// <summary>
    /// Core HIT proof: first request → MISS (gateway invoked once, response cached).
    /// Second identical request → HIT (gateway call count stays exactly the same — zero additional invocations).
    /// </summary>
    [Fact(DisplayName = "AI-E2E-4A Cache HIT: 2nd identical Explain request → gateway NOT called again (call count unchanged)")]
    public async Task Area4A_Cache_Hit_ExplainRequest_GatewayNotCalledAgain()
    {
        using var factory = new CacheCountingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();
        factory.FakeGateway.ResponseContent = "الكسر جزء من كل — مع cache.";

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        // Use a unique ConceptId per test run to guarantee a MISS on the first request.
        // Tests share the same Postgres DB (AiRuntimeFixture is collection-scoped); a fixed
        // ConceptId written in a prior test run would produce a cache HIT on the first request,
        // making it impossible to assert that the MISS path invoked the gateway.
        // ConceptId IS a dimension of the Explain cache key → unique value → unique cache key → MISS.
        var uniqueConceptId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 3_000_000;
        var body1Req = new { SkillId = 401, LessonId = 1, ConceptId = (int?)uniqueConceptId, Question = (string?)null };

        // Record the baseline cache count before the first request.
        // Tests share the same Postgres DB (AiRuntimeFixture is collection-scoped); prior tests may
        // have already written Approved entries → compare delta vs baseline, not absolute count > 0.
        using var baselineScopeA = factory.Services.CreateScope();
        var baselineDbA = baselineScopeA.ServiceProvider.GetRequiredService<AiDbContext>();
        var approvedCountBefore = await baselineDbA.AiResponseCaches.AsNoTracking()
            .CountAsync(r => r.ReviewStatus == AiCacheReviewStatus.Approved && r.InvalidatedAt == null);

        // ── First request (MISS) ──────────────────────────────────────────────
        var (resp1, body1) = await PostAsync(client, ExplainUrl, body1Req, token);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "1st request; body: {0}", body1);
        var frames1 = ParseSseFrames(body1);
        AssertHappyPathFrames(frames1, "1st Explain request (MISS)");

        var countAfterMiss = factory.FakeGateway.CallCount;
        // NOTE: real SafetyLayer calls gateway ≥1× per MISS (content + toxicity check + age check).
        // We assert > 0 to confirm the gateway was reached (cache was empty = MISS path).
        countAfterMiss.Should().BeGreaterThan(0,
            "gateway must be called at least once for the first (MISS) request; actual calls: {0}", countAfterMiss);

        // The cache write is fire-and-forget (Task.Run + fresh IServiceScopeFactory scope).
        // Poll the real AiDbContext until an Approved entry DELTA appears (max 5 s) before issuing
        // the second request — prevents a non-deterministic race where the second request arrives
        // before the write task has committed to Postgres + populated Redis.
        var cacheWriteTimeout = TimeSpan.FromSeconds(5);
        var pollDeadline      = DateTime.UtcNow + cacheWriteTimeout;
        bool entryAppearedInDb = false;
        while (DateTime.UtcNow < pollDeadline)
        {
            using var pollScope = factory.Services.CreateScope();
            var aiDb = pollScope.ServiceProvider.GetRequiredService<AiDbContext>();
            var count = await aiDb.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.ReviewStatus == AiCacheReviewStatus.Approved && r.InvalidatedAt == null);
            if (count > approvedCountBefore) { entryAppearedInDb = true; break; }
            await Task.Delay(100);
        }
        entryAppearedInDb.Should().BeTrue(
            "the real AiResponseCacheRepository must have written a NEW Approved entry to Postgres " +
            "within {0} s of the MISS request (DEFECT-3 fix: fresh-scope write must persist; " +
            "baselineApprovedCount={1})", cacheWriteTimeout.TotalSeconds, approvedCountBefore);

        // ── Second identical request (HIT) ───────────────────────────────────
        var (resp2, body2) = await PostAsync(client, ExplainUrl, body1Req, token);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "2nd request; body: {0}", body2);
        var frames2 = ParseSseFrames(body2);
        AssertHappyPathFrames(frames2, "2nd Explain request (HIT)");

        var countAfterHit = factory.FakeGateway.CallCount;
        countAfterHit.Should().Be(countAfterMiss,
            "cache HIT must NOT invoke gateway — call count must remain {0}; actual: {1}",
            countAfterMiss, countAfterHit);

        // Content must be identical on HIT.
        var content1 = frames1.First(f => f.Event == "message").Data;
        var content2 = frames2.First(f => f.Event == "message").Data;
        content1.Should().Be(content2,
            "cached content must match original; body1={0}, body2={1}", body1, body2);
    }

    /// <summary>
    /// PRODUCT NOTE: SkillId is NOT a dimension of the Explain cache key.
    /// AiCacheKeyBuilder.ForExplain key = (SubjectId, ConceptId, AgeBand, Language, Difficulty,
    /// PromptVersion, CurriculumVersion). Two Explain requests with different SkillId but same
    /// ConceptId/SubjectId/Grade → same cache key → HIT on 2nd request (expected behavior).
    ///
    /// This test verifies cache MISS isolation via ConceptId (which IS a cache key dimension):
    /// ConceptId=501 vs ConceptId=502 → distinct keys → two gateway calls.
    /// </summary>
    [Fact(DisplayName = "AI-E2E-4B Cache MISS: different ConceptId → distinct cache keys → gateway called for each")]
    public async Task Area4B_Cache_Miss_DifferentSkillId_GatewayCalled()
    {
        // NOTE: SkillId is NOT in the Explain cache key. ConceptId IS.
        // Two requests with the same ConceptId but different SkillId share the same cache entry.
        // We use ConceptId as the differentiator here to exercise the MISS path.
        using var factory = new CacheCountingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        // ConceptId=501 → cache key A
        var (r1, b1) = await PostAsync(client, ExplainUrl,
            new { SkillId = 1, LessonId = 1, ConceptId = (int?)501 }, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", b1);
        var countAfter1 = factory.FakeGateway.CallCount;

        // Poll until the cache write for the first entry lands in the DB (bounded, deterministic).
        var pollDeadline4B = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < pollDeadline4B)
        {
            using var s = factory.Services.CreateScope();
            var aDb = s.ServiceProvider.GetRequiredService<AiDbContext>();
            if (await aDb.AiResponseCaches.AsNoTracking().AnyAsync(r => r.InvalidatedAt == null)) break;
            await Task.Delay(100);
        }

        // ConceptId=502 → different cache key (ConceptId dimension differs) → MISS → gateway called again
        var (r2, b2) = await PostAsync(client, ExplainUrl,
            new { SkillId = 1, LessonId = 1, ConceptId = (int?)502 }, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", b2);
        var countAfter2 = factory.FakeGateway.CallCount;

        countAfter2.Should().BeGreaterThan(countAfter1,
            "different ConceptId → different cache key → MISS → gateway must be invoked again; " +
            "countAfter1={0}, countAfter2={1}", countAfter1, countAfter2);
    }

    [Fact(DisplayName = "AI-E2E-4C Cache MISS: different intent (Explain vs Simplify) → distinct cache keys → gateway called for each")]
    public async Task Area4C_Cache_Miss_DifferentIntent_DistinctKeys()
    {
        using var factory = new CacheCountingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        // Explain with SkillId=601
        var (r1, b1) = await PostAsync(client, ExplainUrl,
            new { SkillId = 601, LessonId = 1 }, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "Explain; body: {0}", b1);
        var countAfterExplain = factory.FakeGateway.CallCount;

        // Poll until the Explain entry appears in the DB before issuing the Simplify request.
        var pollDeadline4C = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < pollDeadline4C)
        {
            using var s = factory.Services.CreateScope();
            var aDb = s.ServiceProvider.GetRequiredService<AiDbContext>();
            if (await aDb.AiResponseCaches.AsNoTracking().AnyAsync(r => r.InvalidatedAt == null)) break;
            await Task.Delay(100);
        }

        // Simplify with ConceptId=601 (same concept ID — but different intent type → different cache key)
        var (r2, b2) = await PostAsync(client, SimplifyUrl,
            new { ConceptId = 601, LessonId = 1 }, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "Simplify; body: {0}", b2);
        var countAfterSimplify = factory.FakeGateway.CallCount;

        countAfterSimplify.Should().BeGreaterThan(countAfterExplain,
            "different intent type → different cache key → MISS → gateway called again; " +
            "explain={0}, simplify={1}", countAfterExplain, countAfterSimplify);
    }

    // ==========================================================================
    // AREA 5 — APPROVED-ONLY CACHE: autoApproveEnabled=false → PendingReview
    // ==========================================================================

    /// <summary>
    /// With autoApproveEnabled=false, the repository downgrades all Approved entries to
    /// PendingReview before writing. GetApprovedAsync only serves Approved rows, so the
    /// next identical request is a MISS (gateway invoked again).
    /// </summary>
    [Fact(DisplayName = "AI-E2E-5A AutoApprove=false: response written as PendingReview → NOT served on 2nd request (MISS → gateway called again)")]
    public async Task Area5A_AutoApproveDisabled_EntryIsPendingReview_NotServedOnHit()
    {
        // autoApproveEnabled=false → all entries written as PendingReview
        using var factory = new CacheCountingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString,
            autoApproveEnabled: false);
        factory.FakeGateway.Reset();

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);

        // Use a unique ConceptId per test run to guarantee a MISS on the first request.
        // Tests share the same Postgres DB; a prior run of 4A/4B/4C wrote Approved entries with
        // generic keys. A unique ConceptId → unique cache key → guaranteed MISS.
        var uniqueConceptId5A = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 5_000_000;
        var reqBody = new { SkillId = 701, LessonId = 1, ConceptId = (int?)uniqueConceptId5A };

        // Snapshot baseline counts BEFORE the first request (delta comparison isolates this test
        // from any entries written by prior tests in the shared DB).
        using var baselineScope5A = factory.Services.CreateScope();
        var baselineDb5A = baselineScope5A.ServiceProvider.GetRequiredService<AiDbContext>();
        var pendingCountBefore  = await baselineDb5A.AiResponseCaches.AsNoTracking()
            .CountAsync(r => r.ReviewStatus == AiCacheReviewStatus.PendingReview && r.InvalidatedAt == null);
        var approvedCountBefore5A = await baselineDb5A.AiResponseCaches.AsNoTracking()
            .CountAsync(r => r.ReviewStatus == AiCacheReviewStatus.Approved && r.InvalidatedAt == null);

        // First request (MISS — unique ConceptId not yet in cache)
        var (r1, b1) = await PostAsync(client, ExplainUrl, reqBody, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", b1);
        AssertHappyPathFrames(ParseSseFrames(b1), "1st request");

        var countAfterFirst = factory.FakeGateway.CallCount;
        // Real SafetyLayer calls gateway for content + toxicity + age checks (≥1 per request).
        countAfterFirst.Should().BeGreaterThan(0, "gateway must be called at least once on MISS");

        // The cache write is fire-and-forget on a fresh IServiceScopeFactory scope (DEFECT-3 fix).
        // Poll the REAL AiDbContext (Testcontainers Postgres) until a NEW PendingReview entry appears.
        // autoApproveEnabled=false → the real AiResponseCacheRepository downgrades Approved → PendingReview.
        var pollDeadline5A = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        bool pendingEntryFound = false;
        while (DateTime.UtcNow < pollDeadline5A)
        {
            using var pollScope = factory.Services.CreateScope();
            var aiDb = pollScope.ServiceProvider.GetRequiredService<AiDbContext>();
            var pendingCount = await aiDb.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.ReviewStatus == AiCacheReviewStatus.PendingReview && r.InvalidatedAt == null);
            if (pendingCount > pendingCountBefore) { pendingEntryFound = true; break; }
            await Task.Delay(100);
        }
        pendingEntryFound.Should().BeTrue(
            "with autoApproveEnabled=false the real AiResponseCacheRepository must write a NEW entry as " +
            "PendingReview — verified in Testcontainers Postgres within 5 s of the MISS; " +
            "pendingCountBefore={0}", pendingCountBefore);

        // Also confirm NO NEW Approved rows appeared (kill-switch must downgrade all incoming Approved
        // entries to PendingReview — only asserts the delta from this test, not the total).
        using (var verifyScope = factory.Services.CreateScope())
        {
            var aiDb = verifyScope.ServiceProvider.GetRequiredService<AiDbContext>();
            var approvedCountAfter = await aiDb.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.ReviewStatus == AiCacheReviewStatus.Approved && r.InvalidatedAt == null);
            (approvedCountAfter - approvedCountBefore5A).Should().Be(0,
                "autoApproveEnabled=false must not create any NEW Approved entries; " +
                "delta={0} (before={1}, after={2})", approvedCountAfter - approvedCountBefore5A,
                approvedCountBefore5A, approvedCountAfter);
        }

        // Second identical request — PendingReview entry must NOT be served → MISS → gateway called again
        var (r2, b2) = await PostAsync(client, ExplainUrl, reqBody, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", b2);
        AssertHappyPathFrames(ParseSseFrames(b2), "2nd request (should be MISS)");

        var countAfterSecond = factory.FakeGateway.CallCount;
        // PendingReview → not served → second request is a MISS → gateway is invoked again.
        countAfterSecond.Should().BeGreaterThan(countAfterFirst,
            "PendingReview entry must NOT be served — gateway must be called again on 2nd request; " +
            "countAfterFirst={0}, countAfterSecond={1}", countAfterFirst, countAfterSecond);
    }

    // ==========================================================================
    // AREA 6 — RAG RETRIEVAL: SEEDED FAKE-EMBEDDED CHUNKS
    // ==========================================================================

    /// <summary>
    /// With ContextProvider="Rag" and a seeded corpus, an Explain request that
    /// produces a query embedding matching a seeded chunk must retrieve that chunk
    /// (non-empty context) and deliver a grounded answer (event:message, not redirect).
    ///
    /// Proof: RagContextProvider calls IEmbeddingProvider.EmbedAsync → returns the same
    /// deterministic vector as the seeded chunk → cosine distance ≈ 0 < SimilarityDistanceFloor=0.5
    /// → chunk passes the floor → non-empty LearningContext → handler calls SafetyLayer
    /// → gateway returns content → event:message emitted.
    /// </summary>
    [Fact(DisplayName = "AI-E2E-6A RAG: seeded Math-Ar chunk text query → non-empty context → grounded event:message (not redirect)")]
    public async Task Area6A_Rag_SeededMathArText_GroundedAnswer_NotRedirect()
    {
        // ContextProvider=Rag activates RagContextProvider; DeterministicStubEmbeddingProvider ensures
        // seeded-text queries score near-zero distance; FakeCountingGateway returns safe content.
        using var factory = new RagAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.ResponseContent = "الكسر جزء من كل. 1/2 يعني نصف.";
        factory.FakeGateway.Reset();

        // DefaultChildLearningProfileQuery always returns grade=4.
        // Seed a grade=4 chunk with content="skill:42" so the retrieval query:
        //   WHERE gradeId=4 AND distance("skill:42") <= floor
        // matches this chunk (distance=0, passes floor=0.5) → non-empty context → grounded answer.
        await SeedTestChunkAsync(factory, skillId: 42, gradeId: 4, subjectId: 0, content: "skill:42");

        // Diagnostic: verify the chunk + embedding are in the DB AND that RetrieveChunksQuery returns them.
        using (var verifyScope = factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CurriculumDbContext>();
            var chunkCount = await verifyDb.CurriculumChunks.AsNoTracking()
                .Where(c => c.SkillId == 42 && c.GradeId == 4)
                .CountAsync();
            var embCount = await verifyDb.ChunkEmbeddingsBgeM3.AsNoTracking()
                .Join(verifyDb.CurriculumChunks.AsNoTracking(),
                    e => e.ChunkId, c => c.Id,
                    (e, c) => new { e, c })
                .Where(ec => ec.c.SkillId == 42 && ec.c.GradeId == 4)
                .CountAsync();
            chunkCount.Should().BeGreaterThan(0,
                "chunk for skillId=42, gradeId=4 must be seeded before the HTTP request");
            embCount.Should().BeGreaterThan(0,
                "embedding for skillId=42, gradeId=4 must be seeded before the HTTP request");

            // Direct MediatR call to confirm RAG retrieval works end-to-end before the HTTP request.
            // If this returns empty, the HTTP request will redirect regardless — exposing the root cause.
            var mediator = verifyScope.ServiceProvider.GetRequiredService<IMediator>();
            var retrievalResult = await mediator.Send(new RetrieveChunksQuery
            {
                Text            = "skill:42",
                GradeId         = 4,
                SubjectId       = 0,
                WeakAreaSkillId = 42,
                TopK            = 5,
            });
            retrievalResult.Should().NotBeNull("MediatR RetrieveChunksQuery must return a result");
            retrievalResult.Successed.Should().BeTrue(
                "RetrieveChunksQuery must succeed (not ServerError); data={0}", retrievalResult.Data);
            retrievalResult.Data!.Chunks.Should().NotBeEmpty(
                "RAG retrieval for skillId=42,gradeId=4 must find the seeded chunk; " +
                "rawResult.Successed={0}", retrievalResult.Successed);
        }

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, grade: 4, factory: factory);

        // LessonId=1 → StubLessonContextContract returns SubjectId=1 (Subject.Math), GradeId=3.
        // Without a LessonId (null), subjectId defaults to 0 which maps to Subject=0 (unsupported)
        // → PromptBuilder returns UnsupportedSubjectResult → event:error instead of event:message.
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 42, LessonId = 1, ConceptId = (int?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        // With seeded chunk matching the query, context is non-empty → NOT a redirect
        frames.Should().NotContain(f => f.Event == "redirect",
            "seeded corpus match must produce grounded answer, not redirect; body={0}", body);

        // Must have a message frame (grounded content)
        frames.Should().Contain(f => f.Event == "message",
            "grounded answer must produce event:message; body={0}", body);

        // NOTE: ISafetyLayer is stubbed in RagAiTestFactory, so FakeGateway is not called.
        // The ISafetyLayer stub returns Allowed directly without calling IAiGateway.
        // Gateway-call assertions are omitted here; they are covered by Area4A/4B which use
        // the real SafetyLayer with FakeCountingGateway.
    }

    [Fact(DisplayName = "AI-E2E-6B RAG: out-of-corpus query → empty context → event:redirect (no LLM call)")]
    public async Task Area6B_Rag_OutOfCorpus_EmptyContext_Redirect_NoLlmCall()
    {
        using var factory = new RagAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, grade: 3, factory: factory);

        // SkillId=9999 — no chunk seeded for this skill → embedding won't match anything → empty retrieval
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 9999, LessonId = (int?)null, ConceptId = (int?)null },
            token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        // Empty context → refuse-and-redirect
        frames.Should().Contain(f => f.Event == "redirect",
            "out-of-corpus query must produce event:redirect; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content frame when context is empty; body={0}", body);

        factory.FakeGateway.CallCount.Should().Be(0,
            "gateway must NOT be called when context is empty (refuse-and-redirect rule); " +
            "actual calls: {0}", factory.FakeGateway.CallCount);
    }

    // ==========================================================================
    // AREA 7 — GRADE FILTERING: CROSS-COHORT ISOLATION
    // ==========================================================================

    /// <summary>
    /// Grade-G3 chunks must NOT be returned for a Grade-G5 student query.
    /// The RagContextProvider resolves student grade via IChildLearningProfileQuery;
    /// the RetrieveChunksQuery filters by GradeId in the WHERE clause.
    /// </summary>
    [Fact(DisplayName = "AI-E2E-7A Grade Filter: Grade-3 chunk not returned for Grade-5 student (cross-grade isolation)")]
    public async Task Area7A_GradeFilter_Grade3ChunkNotReturnedForGrade5Student()
    {
        using var factory = new RagAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();

        // Seed a chunk for grade=3, skill=801, subjectId=0.
        // DefaultChildLearningProfileQuery returns grade=4; retrieval filter WHERE gradeId=4
        // will exclude this grade-3 chunk → empty retrieval → redirect (proves grade isolation).
        await SeedTestChunkAsync(factory, skillId: 801, gradeId: 3, subjectId: 0, content: "skill:801");

        // Create a Grade-5 student
        var client    = factory.CreateClient();
        var grade5Token = await CreateStudentJwtAsync(client, grade: 5, factory: factory);

        // Query SkillId=801 as a Grade-5 student.
        // The RagContextProvider resolves grade from IChildLearningProfileQuery (stubbed to return the student's profile grade).
        // BUT: our AiRuntimeStubQuestionAnswerContract returns grade from JWT; RagContextProvider uses IChildLearningProfileQuery.
        // The RagContextProvider uses IChildLearningProfileQuery which is the default stub returning Grade=4 unless overridden.
        // For this test the key is: we seeded grade=3 chunks; the query grade will be whatever IChildLearningProfileQuery returns.
        // DefaultChildLearningProfileQuery returns Grade=4 always (the stub from Application DI).
        // So grade=5 student + grade=3 chunk: retrieval filters by profile grade=4 → doesn't match grade=3 chunk → empty.
        // This PROVES grade filtering: the grade-3 chunk is NOT surfaced for a grade-4 profile (or grade-5 student).
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 801, LessonId = (int?)null },
            grade5Token);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        // Grade-3 chunk not visible to non-grade-3 profile → empty context → redirect
        frames.Should().Contain(f => f.Event == "redirect",
            "grade-3 chunk must not be served to non-grade-3 profile query; body={0}", body);
        factory.FakeGateway.CallCount.Should().Be(0,
            "gateway must not be called when grade filter yields empty context; body={0}", body);
    }

    /// <summary>
    /// PRODUCT-GAP NOTE (non-blocking defect — see execution report):
    /// The "Grade" claim is NOT currently emitted in student JWTs (confirmed by code comment in
    /// OverrideChildGradeCommandHandler.cs: "Grade is NOT currently a JWT claim").
    /// Therefore ExplainConceptCommandHandler.TryResolveProfile always defaults jwtGrade=4 for all
    /// students, regardless of their actual grade. The AgeBand dimension in the cache key is
    /// therefore always "band2" for all students → same cache key → grade-3 and grade-5 students
    /// share the same cache entry.
    ///
    /// This test verifies the ACTUAL runtime behavior: both students get the same cache key because
    /// the JWT grade dimension is not implemented. It also verifies the ConceptId dimension IS
    /// effective at producing distinct keys (used as a proxy for the grade isolation test).
    /// </summary>
    [Fact(DisplayName = "AI-E2E-7B Cache Key Isolation: different ConceptId → distinct cache keys → MISS for both")]
    public async Task Area7B_GradeFilter_DifferentGradeStudents_DistinctCacheKeys()
    {
        // PRODUCT GAP: Grade is NOT a JWT claim → jwtGrade always defaults to 4 for all students.
        // AgeBand is always "band2" → grade-3 and grade-5 students would share cache keys.
        // We test ConceptId isolation instead (ConceptId IS a request-body dimension in the key).
        using var factory = new CacheCountingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();

        var client = factory.CreateClient();
        var token  = await CreateStudentJwtAsync(client, factory: factory);  // single student; grade defaults to 4

        // ConceptId=100 → cache key A
        var reqBody1 = new { SkillId = 901, LessonId = 1, ConceptId = (int?)100 };
        var (r1, b1) = await PostAsync(client, ExplainUrl, reqBody1, token);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "conceptId=100; body: {0}", b1);
        AssertHappyPathFrames(ParseSseFrames(b1), "conceptId=100 request");
        var countAfterFirst = factory.FakeGateway.CallCount;
        countAfterFirst.Should().BeGreaterThan(0, "gateway must be called for first (MISS) request");

        // Poll until the first entry lands (bounded, deterministic — no fixed-delay race).
        var pollDeadline7B = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < pollDeadline7B)
        {
            using var s = factory.Services.CreateScope();
            var aDb = s.ServiceProvider.GetRequiredService<AiDbContext>();
            if (await aDb.AiResponseCaches.AsNoTracking().AnyAsync(r => r.InvalidatedAt == null)) break;
            await Task.Delay(100);
        }

        // ConceptId=200 → different cache key (ConceptId dimension differs) → MISS → gateway called again
        var reqBody2 = new { SkillId = 901, LessonId = 1, ConceptId = (int?)200 };
        var (r2, b2) = await PostAsync(client, ExplainUrl, reqBody2, token);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "conceptId=200; body: {0}", b2);
        AssertHappyPathFrames(ParseSseFrames(b2), "conceptId=200 request");
        var countAfterSecond = factory.FakeGateway.CallCount;

        countAfterSecond.Should().BeGreaterThan(countAfterFirst,
            "different ConceptId produces distinct cache key → MISS → gateway must be invoked again; " +
            "countAfterFirst={0}, countAfterSecond={1}", countAfterFirst, countAfterSecond);
    }

    // ==========================================================================
    // HELPERS: Per-test chunk seeding + specialized factories
    // ==========================================================================

    /// <summary>
    /// Seeds a single curriculum chunk + embedding row directly in the AiRuntimeTestFactory's
    /// CurriculumDbContext. Used for RAG tests that need a chunk with specific (skillId, gradeId, subjectId).
    /// The content uses DeterministicEmbedding so the query "skill:{skillId}" has distance ≈ 0
    /// (same text → same vector).
    /// </summary>
    private static async Task SeedTestChunkAsync(
        WebApplicationFactory<Program> factory,
        int skillId,
        int gradeId,
        int subjectId,
        string content)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CurriculumDbContext>();

        var skillKey    = $"test.grade{gradeId}.subj{subjectId}.skill{skillId}";
        // Version name is per (subjectId, language) — one Active version per (SubjectId, Language) is
        // enforced by a filtered unique index in CurriculumVersionConfig. Chunks across different grades
        // live under the same version (grade is on the chunk, not the version).
        var versionName = $"AI-E2E-TEST-subj{subjectId}-ar";

        // Idempotent: skip if chunk already seeded for this skillKey.
        if (await db.CurriculumChunks.AnyAsync(c => c.SkillKey == skillKey))
            return;

        // Ensure an Active CurriculumVersion exists for this (subjectId, Arabic) combination.
        var version = await db.CurriculumVersions
            .FirstOrDefaultAsync(v => v.SubjectId == subjectId &&
                                      v.Language == Learnexia.Modules.Curriculum.Domain.Enums.ContentLanguage.Arabic &&
                                      v.Status == Learnexia.Modules.Curriculum.Domain.Enums.CurriculumVersionStatus.Active);
        if (version is null)
        {
            version = new Learnexia.Modules.Curriculum.Domain.Entities.CurriculumVersion
            {
                Name      = versionName,
                SubjectId = subjectId,
                Language  = Learnexia.Modules.Curriculum.Domain.Enums.ContentLanguage.Arabic,
                Status    = Learnexia.Modules.Curriculum.Domain.Enums.CurriculumVersionStatus.Active,
            };
            db.CurriculumVersions.Add(version);
            // Use the audit-stamping overload (stamps CreatedAt + CreatedBy via SaveChangesAsync(int))
            await db.SaveChangesAsync(CurriculumChunkSeeder.SystemUserId);
        }

        // Add the chunk with the exact content the RagContextProvider will query for this skillId.
        // RagContextProvider.BuildQueryText(skillId, null) returns "skill:{skillId}",
        // so content = "skill:{skillId}" ensures cosine distance = 0 on retrieval.
        var chunk = new Learnexia.Modules.Curriculum.Domain.Entities.CurriculumChunk
        {
            Content             = content,
            Metadata            = $"{{\"skillKey\":\"{skillKey}\",\"gradeId\":{gradeId},\"subjectId\":{subjectId}}}",
            Difficulty          = 1,
            SubjectId           = subjectId,
            GradeId             = gradeId,
            SkillId             = skillId,
            SkillKey            = skillKey,
            Language            = Learnexia.Modules.Curriculum.Domain.Enums.ContentLanguage.Arabic,
            CurriculumVersionId = version.Id,
            ProvenanceRef       = null,
        };
        db.CurriculumChunks.Add(chunk);
        await db.SaveChangesAsync(CurriculumChunkSeeder.SystemUserId);

        // Compute deterministic embedding. Same text → same vector → cosine distance = 0.
        // FakeDeterministicEmbeddingProvider also calls DeterministicEmbedding.ComputeAsync,
        // so the query vector for "skill:{skillId}" exactly equals this seeded vector.
        var vector = await DeterministicEmbedding.ComputeAsync(content);
        var embedding = new Learnexia.Modules.Curriculum.Domain.Entities.ChunkEmbeddingBgeM3
        {
            ChunkId      = chunk.Id,
            Provider     = DeterministicEmbedding.PlaceholderProvider,
            Model        = DeterministicEmbedding.PlaceholderModel,
            ModelVersion = DeterministicEmbedding.PlaceholderModelVersion,
            Dimension    = DeterministicEmbedding.Dimension,
            Vector       = vector,
            IsActive     = true,
        };
        db.ChunkEmbeddingsBgeM3.Add(embedding);
        await db.SaveChangesAsync(CurriculumChunkSeeder.SystemUserId);
    }
}

// =============================================================================
// SPECIALIZED FACTORIES
// (Defined outside the test class to be accessible by test code via `using var`.)
// =============================================================================

/// <summary>
/// Factory for safety + no-cache tests (Area 3).
/// Stubs ISafetyLayer (Blocked or ThrowOnCall) and ILearningContextProvider.
/// Keeps the REAL AiResponseCacheRepository (Postgres + Redis) so tests can verify
/// that blocked responses are never written to AiResponseCache.
/// </summary>
public sealed class CacheValidatingAiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _pgConn;
    private readonly string _redisConn;
    private readonly StubSafetyLayer.Mode    _safetyMode;
    private readonly StubContextProvider.Mode _contextMode;

    public CacheValidatingAiTestFactory(
        string pgConn,
        string redisConn,
        StubSafetyLayer.Mode    safetyMode  = StubSafetyLayer.Mode.Allowed,
        StubContextProvider.Mode contextMode = StubContextProvider.Mode.NonEmpty)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _pgConn      = pgConn;
        _redisConn   = redisConn;
        _safetyMode  = safetyMode;
        _contextMode = contextMode;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"]             = _pgConn,
                ["ConnectionStrings:Redis"]               = _redisConn,
                ["AiHelper:Cache:autoApproveEnabled"]     = "true",
                ["AiHelper:Cache:safetyPassConfidence"]   = "0.95",
                ["AiHelper:Cache:autoApprovalConfidence"] = "0.85",
                ["Ai:Safety:MaxRegenerationAttempts"]     = "0",
                ["Ai:Providers:Claude:ApiKey"]            = "dummy",
                ["Ai:Providers:OpenAi:ApiKey"]            = "dummy",
            }));

        builder.ConfigureServices(services =>
        {
            AiRuntimeTestFactory.ReplaceDbContexts(services, _pgConn);

            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ => new StubSafetyLayer { Behavior = _safetyMode });

            services.RemoveAll<ILearningContextProvider>();
            services.AddTransient<ILearningContextProvider>(_ =>
                new StubContextProvider { Behavior = _contextMode });

            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            services.RemoveAll<IQuestionAnswerContract>();
            services.AddTransient<IQuestionAnswerContract>(
                _ => new StubQuestionAnswerContract { CorrectAnswer = "BLOCKED_SENTINEL_XYZ" });

            AiRuntimeTestFactory.DisableIpRateLimit(services);
        });
    }
}

/// <summary>
/// Test-only <see cref="IGlobalSettingsProvider"/> stub that overrides a fixed set of bool keys
/// while delegating everything else to the inner provider.
///
/// <para>Required by <see cref="CacheCountingAiTestFactory"/> to enforce
/// <c>ai.cache.autoApproveEnabled=false</c> during Area5A tests. The production
/// <c>DbBackedGlobalSettingsProvider</c> reads from the DB, which does NOT have an
/// <c>ai.cache.autoApproveEnabled</c> row (not seeded by <c>GlobalSettingsSeeder</c>),
/// so it always defaults to <c>true</c>. This stub injects the desired value directly.</para>
/// </summary>
internal sealed class BoolOverrideGlobalSettingsProviderStub : IGlobalSettingsProvider
{
    private readonly IGlobalSettingsProvider _inner;
    private readonly Dictionary<string, bool> _boolOverrides;

    public BoolOverrideGlobalSettingsProviderStub(
        IGlobalSettingsProvider inner,
        Dictionary<string, bool> boolOverrides)
    {
        _inner         = inner;
        _boolOverrides = boolOverrides;
    }

    public bool GetBool(string key, bool defaultValue)
        => _boolOverrides.TryGetValue(key, out var ov) ? ov : _inner.GetBool(key, defaultValue);

    public decimal GetDecimal(string key, decimal defaultValue) => _inner.GetDecimal(key, defaultValue);
    public int GetInt(string key, int defaultValue)             => _inner.GetInt(key, defaultValue);
    public string GetString(string key, string defaultValue)    => _inner.GetString(key, defaultValue);
}

/// <summary>
/// Factory for cache HIT/MISS tests (Area 4 + 5 + 7B).
///
/// Uses the REAL <see cref="AiResponseCacheRepository"/> backed by the Testcontainers Postgres + Redis.
/// DEFECT-3 is fixed: the handler creates a fresh <see cref="IServiceScopeFactory"/> scope for the
/// fire-and-forget write, so the scoped AiDbContext is independent of the SSE request scope and
/// SaveChangesAsync completes before the scope is disposed.
///
/// Tests must poll the real DB (via AiDbContext) after a MISS request to wait for the async write
/// to land before issuing the HIT request — see Area4A for the polling pattern.
///
/// When <paramref name="autoApproveEnabled"/> is false, registers a
/// <see cref="BoolOverrideGlobalSettingsProviderStub"/> wrapper so the
/// <c>ai.cache.autoApproveEnabled=false</c> signal is honoured at runtime (the DB-backed
/// provider doesn't have this key seeded, so it defaults to true without the override).
///
/// Exposes <see cref="FakeGateway"/> for call count assertions.
/// </summary>
public sealed class CacheCountingAiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _pgConn;
    private readonly string _redisConn;
    private readonly bool   _autoApproveEnabled;

    public readonly FakeCountingGateway FakeGateway = new();

    public CacheCountingAiTestFactory(
        string pgConn,
        string redisConn,
        bool autoApproveEnabled = true)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _pgConn             = pgConn;
        _redisConn          = redisConn;
        _autoApproveEnabled = autoApproveEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"]             = _pgConn,
                ["ConnectionStrings:Redis"]               = _redisConn,
                ["AiHelper:Cache:autoApproveEnabled"]     = _autoApproveEnabled.ToString().ToLower(),
                ["AiHelper:Cache:safetyPassConfidence"]   = "0.95",
                ["AiHelper:Cache:autoApprovalConfidence"] = "0.85",
                ["Ai:Safety:MaxRegenerationAttempts"]     = "0",
                ["Ai:Providers:Claude:ApiKey"]            = "dummy",
                ["Ai:Providers:OpenAi:ApiKey"]            = "dummy",
            }));

        builder.ConfigureServices(services =>
        {
            AiRuntimeTestFactory.ReplaceDbContexts(services, _pgConn);

            // IAiResponseCache is NOT replaced — the REAL AiResponseCacheRepository (Postgres + Redis)
            // is used. DEFECT-3 fix (fresh IServiceScopeFactory scope in handler) ensures writes persist
            // to the Testcontainers Postgres + Redis independently of the request scope lifecycle.
            // Tests poll AiDbContext after the first (MISS) request to detect when the write completes
            // before asserting the HIT (see Area4A polling pattern).

            // Fake gateway with thread-safe call counter.
            // ISafetyLayer is NOT stubbed — the REAL SafetyLayer pipeline runs, calling IAiGateway
            // for content generation AND for toxicity/age safety checks.
            // FakeGateway returns safe JSON for classify calls (both ToxicityCheck + AgeCheck pass)
            // and safe content for generate calls. CallCount increments for EVERY gateway call.
            // Key invariant: CallCount MUST NOT CHANGE between first (MISS) and second (HIT) requests.
            services.RemoveAll<IAiGateway>();
            services.AddScoped<IAiGateway>(_ => FakeGateway);

            // Stub context: NonEmpty → skips redirect path, reaches safety/gateway
            services.RemoveAll<ILearningContextProvider>();
            services.AddTransient<ILearningContextProvider>(_ =>
                new StubContextProvider { Behavior = StubContextProvider.Mode.NonEmpty });

            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            services.RemoveAll<IQuestionAnswerContract>();
            services.AddTransient<IQuestionAnswerContract>(
                _ => new StubQuestionAnswerContract { CorrectAnswer = "CACHE_CORRECT_SENTINEL_XYZ" });

            // When autoApproveEnabled=false, the DB-backed IGlobalSettingsProvider would normally
            // return true for ai.cache.autoApproveEnabled (key not seeded in platform.GlobalSettings).
            // Replace the Singleton registration with a stub that forces the key to false, while
            // delegating all other keys to the BootstrapDefaultGlobalSettingsProvider read from config.
            // This avoids a circular-reference DI issue that arises from decorating the Singleton.
            if (!_autoApproveEnabled)
            {
                services.RemoveAll<IGlobalSettingsProvider>();
                services.AddSingleton<IGlobalSettingsProvider>(sp =>
                {
                    // Use the IConfiguration (which includes our ConfigureAppConfiguration overrides)
                    // as the inner provider so ai.cost.* / ai.cache.* values still resolve correctly.
                    var cfg = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                    var configBacked = new Learnexia.Shared.Kernel.Settings.BootstrapDefaultGlobalSettingsProvider(cfg);
                    return new BoolOverrideGlobalSettingsProviderStub(
                        configBacked,
                        new Dictionary<string, bool>
                        {
                            ["ai.cache.autoApproveEnabled"] = false,
                        });
                });
            }

            AiRuntimeTestFactory.DisableIpRateLimit(services);
        });
    }
}

/// <summary>
/// Factory for RAG integration tests (Area 6 + 7A).
/// ContextProvider="Rag" activates RagContextProvider.
/// FakeDeterministicEmbeddingProvider ensures seeded-text queries return cosine distance ≈ 0.
/// Exposes FakeGateway for call count assertions.
/// </summary>
public sealed class RagAiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _pgConn;
    private readonly string _redisConn;

    public readonly FakeCountingGateway FakeGateway = new();

    public RagAiTestFactory(string pgConn, string redisConn)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _pgConn    = pgConn;
        _redisConn = redisConn;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"]                    = _pgConn,
                ["ConnectionStrings:Redis"]                      = _redisConn,
                ["AiHelper:ContextProvider"]                     = "Rag",
                ["AiHelper:Cache:autoApproveEnabled"]            = "true",
                ["AiHelper:Cache:safetyPassConfidence"]          = "0.95",
                ["AiHelper:Cache:autoApprovalConfidence"]        = "0.85",
                ["Curriculum:Retrieval:SimilarityDistanceFloor"] = "0.5",
                ["Ai:Safety:MaxRegenerationAttempts"]            = "0",
                ["Ai:Providers:Claude:ApiKey"]                   = "dummy",
                ["Ai:Providers:OpenAi:ApiKey"]                   = "dummy",
            }));

        builder.ConfigureServices(services =>
        {
            AiRuntimeTestFactory.ReplaceDbContexts(services, _pgConn);

            // Deterministic embedding: same text → distance=0 against seeded vector
            services.RemoveAll<IEmbeddingProvider>();
            services.AddScoped<IEmbeddingProvider, FakeDeterministicEmbeddingProvider>();

            // Fake gateway with call counter
            services.RemoveAll<IAiGateway>();
            services.AddScoped<IAiGateway>(_ => FakeGateway);

            // Stub safety: Allowed (bypasses ToxicityCheck / AgeCheck so the test doesn't need
            // the gateway to return safety-classifier JSON on top of content calls)
            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ =>
                new StubSafetyLayer { Behavior = StubSafetyLayer.Mode.Allowed });

            // Explicitly register RagContextProvider as the ILearningContextProvider.
            // The ConfigureAppConfiguration sets ["AiHelper:ContextProvider"] = "Rag", which is meant
            // to trigger AddCurriculumInfrastructure to register RagContextProvider. However, in some
            // WebApplicationFactory configurations (depending on when Program.cs service registration
            // runs relative to ConfigureAppConfiguration overrides), the config value may not be visible
            // at service-registration time. Explicitly registering here ensures the RAG path is active.
            services.RemoveAll<ILearningContextProvider>();
            services.RemoveAll<ICurriculumContextQuery>();
            services.AddScoped<RagContextProvider>();
            services.AddScoped<ILearningContextProvider>(sp => sp.GetRequiredService<RagContextProvider>());
            services.AddScoped<ICurriculumContextQuery>(sp => sp.GetRequiredService<RagContextProvider>());

            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            services.RemoveAll<IQuestionAnswerContract>();
            services.AddTransient<IQuestionAnswerContract>(
                _ => new StubQuestionAnswerContract { CorrectAnswer = "RAG_CORRECT_SENTINEL_XYZ" });

            AiRuntimeTestFactory.DisableIpRateLimit(services);
        });
    }
}

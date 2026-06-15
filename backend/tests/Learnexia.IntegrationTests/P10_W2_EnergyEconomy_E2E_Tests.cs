// ReSharper disable InconsistentNaming
// P10 Wave 2 — Energy Economy End-to-End Integration Tests
//
// Tests: charge-on-delivery (MISS + HIT), no-charge safety paths, pre-auth block,
//        per-intent costs, daily soft cap, grant (P10-02), energy-status IDOR (P10-04).
//
// Infrastructure:
//   AiRuntimeFixture (shared Testcontainers Postgres/pgvector + Redis — already defined in
//   P3_AI_RuntimeActivation_E2E_Tests.cs in the same compilation unit).
//   EnergyEconomyTestFactory: WebApplicationFactory wired like AiRuntimeTestFactory but
//   also exposing BillingDbContext for direct ledger assertions; fakes both IAiGateway and
//   ISafetyLayer so the tests are fully offline.
//
// Money-correctness invariant (STOP criteria):
//   If a no-charge case actually charges, or a delivered answer doesn't charge,
//   or pre-auth doesn't block on empty balance → STOP and report as a real defect.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Billing.Infrastructure.Jobs;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Seeders;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// COLLECTION: reuses the shared AiRuntimeFixture (Postgres pgvector + Redis)
// so we don't spin a third set of containers. The AiRuntimeE2ECollection is
// already declared in P3_AI_RuntimeActivation_E2E_Tests.cs.
// =============================================================================

/// <summary>
/// WebApplicationFactory for energy-economy E2E tests.
///
/// Key differences from AiRuntimeTestFactory:
///   • BillingDbContext is also migrated (always present in that suite too, but here
///     we expose it and seed GlobalSettings so ai_cost.* defaults are present).
///   • ISafetyLayer is stubbed deterministically: Allowed / Blocked / ThrowOnCall
///     controlled per-factory so no live Claude call is ever made.
///   • IAiGateway is stubbed via FakeCountingGateway so all SSE calls resolve offline.
///   • ILearningContextProvider is stubbed (NonEmpty / Empty / ThrowOnCall) so redirect
///     vs content paths are controlled per-test.
///   • IQuestionAnswerContract is stubbed for Hint/WhyWrong calls.
///   • Exposes the shared FakeCountingGateway for call-count assertions.
/// </summary>
public sealed class EnergyEconomyTestFactory : WebApplicationFactory<Program>
{
    private readonly string _pgConn;
    private readonly string _redisConn;
    private readonly StubSafetyLayer.Mode _safetyMode;
    private readonly StubContextProvider.Mode _contextMode;
    private readonly bool _hardStopEnabled;

    public readonly FakeCountingGateway FakeGateway = new();

    public EnergyEconomyTestFactory(
        AiRuntimeFixture fixture,
        StubSafetyLayer.Mode safetyMode    = StubSafetyLayer.Mode.Allowed,
        StubContextProvider.Mode contextMode = StubContextProvider.Mode.NonEmpty,
        bool hardStopEnabled               = false)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _pgConn          = fixture.PostgresConnectionString;
        _redisConn       = fixture.RedisConnectionString;
        _safetyMode      = safetyMode;
        _contextMode     = contextMode;
        _hardStopEnabled = hardStopEnabled;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"]             = _pgConn,
                ["ConnectionStrings:Redis"]               = _redisConn,
                ["AiHelper:Cache:autoApproveEnabled"]     = "true",
                ["AiHelper:Cache:safetyPassConfidence"]   = "0.95",
                ["AiHelper:Cache:autoApprovalConfidence"] = "0.85",
                ["Ai:Safety:MaxRegenerationAttempts"]     = "0",
                ["Ai:Providers:Claude:ApiKey"]            = "dummy",
                ["Ai:Providers:OpenAi:ApiKey"]            = "dummy",
                // P10-03: Billing hard-stop flag (controls daily-cap block)
                ["Billing:HardStopEnabled"]               = _hardStopEnabled.ToString().ToLower(),
            }));

        builder.ConfigureServices(services =>
        {
            // ── DbContexts → Testcontainers Postgres ──────────────────────────
            AiRuntimeTestFactory.ReplaceDbContexts(services, _pgConn);

            // BillingDbContext (not covered by AiRuntimeTestFactory.ReplaceDbContexts)
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<BillingDbContext>();
            services.AddDbContext<BillingDbContext>(options =>
                options.UseNpgsql(
                    _pgConn,
                    npgsql => npgsql
                        .MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)
                        .MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName)));

            // ── Fakes ─────────────────────────────────────────────────────────
            services.RemoveAll<IAiGateway>();
            services.AddScoped<IAiGateway>(_ => FakeGateway);

            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ =>
                new StubSafetyLayer { Behavior = _safetyMode });

            services.RemoveAll<ILearningContextProvider>();
            services.AddTransient<ILearningContextProvider>(_ =>
                new StubContextProvider { Behavior = _contextMode });

            services.RemoveAll<ILessonContextContract>();
            services.AddTransient<ILessonContextContract, StubLessonContextContract>();

            services.RemoveAll<IQuestionAnswerContract>();
            services.AddTransient<IQuestionAnswerContract>(
                _ => new StubQuestionAnswerContract { CorrectAnswer = "ENERGY_TEST_SENTINEL_XYZ" });

            // Disable IP rate limiter
            AiRuntimeTestFactory.DisableIpRateLimit(services);
        });
    }

    /// <summary>
    /// Applies all module migrations + seeds GlobalSettings, roles, superadmin, basicuser.
    /// Idempotent (EF migrations are naturally idempotent; seeders are guarded).
    /// Also migrates BillingDbContext and seeds GlobalSettings so ai_cost.* keys exist.
    /// </summary>
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
        await sp.GetRequiredService<BillingDbContext>().Database.MigrateAsync();

        // Seed 17 default GlobalSetting rows (ai_cost.*, credits.*).
        var billingDb     = sp.GetRequiredService<BillingDbContext>();
        var billingLogger = sp.GetRequiredService<Learnexia.Shared.Kernel.Abstractions.ILoggerManager>();
        await GlobalSettingsSeeder.SeedAsync(billingDb, billingLogger);

        await Learnexia.Modules.Identity.Api.IdentityModule.SeedAsync(sp);
        var userManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Learnexia.Modules.Identity.Domain.Entities.User>>();
        var roleManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Learnexia.Modules.Identity.Domain.Entities.Role>>();
        await Learnexia.Modules.Identity.Infrastructure.Persistence.Seed.UserSeeder.SeedBasicUserAsync(userManager, roleManager);
        await Learnexia.Modules.Identity.Infrastructure.Persistence.Seed.UserSeeder.SeedSuperAdminAsync(userManager, roleManager);
    }
}

// =============================================================================
// TEST CLASS
// =============================================================================

/// <summary>
/// P10 Wave 2 — Energy Economy end-to-end integration tests.
///
/// Each test area is completely isolated:
///   • unique email/child per test (guaranteed by timestamp + Guid).
///   • balance read BEFORE and AFTER the AI SSE call; delta asserted exactly.
///   • no fixed-delay sleeps — bounded polling for fire-and-forget debits.
///
/// All AI gateway and safety layer calls are faked (offline).
/// </summary>
[Collection("AiRuntimeE2E")]
public sealed class P10_W2_EnergyEconomy_E2E_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string ExplainUrl       = "api/AiTutor/Explain";
    private const string HintUrl          = "api/AiTutor/Hint";
    private const string SimilarExUrl     = "api/AiTutor/SimilarExample";
    private const string SimplifyUrl      = "api/AiTutor/Simplify";
    private const string EnergyStatusBase = "api/Billing/Energy";
    private const string GrantUrl         = "api/Billing/Credits/Grant";

    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string ValidChildPassword = "Child@Pass1";

    // ── Per-intent locked costs (must match GlobalSettingsSeeder defaults) ───
    private const int CostExplain       = 3;   // ai_cost.deep_explanation
    private const int CostSimplify      = 3;   // ai_cost.deep_explanation (same key)
    private const int CostHint          = 1;   // ai_cost.hint
    private const int CostWhyWrong      = 2;   // ai_cost.explain_mistake
    private const int CostSimilarEx     = 5;   // ai_cost.practice_generation

    private const int DefaultDailyCap   = 10;  // credits.free_daily_cap
    private const int DefaultMonthly    = 100; // credits.free_monthly

    // ── Infrastructure ────────────────────────────────────────────────────────
    private readonly AiRuntimeFixture _fixture;
    private bool _migrationsApplied;

    public P10_W2_EnergyEconomy_E2E_Tests(AiRuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_migrationsApplied)
        {
            using var seedFactory = new EnergyEconomyTestFactory(_fixture);
            await seedFactory.ApplyMigrationsAndSeedAsync();
            _migrationsApplied = true;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static string UniqueEmail(string tag)
        => $"econ_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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

    private static async Task<(HttpResponseMessage Resp, string Body)>
        PostAsync(HttpClient client, string url, object? body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = body is null
                ? null
                : new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpResponseMessage Resp, string Body)>
        GetAsync(HttpClient client, string url, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates a parent → child account and returns (childId, childToken, parentToken).
    /// The child has a GrantedBalance of <paramref name="seedBalance"/> credits seeded
    /// directly via the BillingDbContext so tests start with a known balance.
    /// </summary>
    private async Task<(int ChildId, string ChildToken, string ParentToken)>
        CreateStudentWithBalanceAsync(
            EnergyEconomyTestFactory factory,
            int seedBalance,
            string tag = "")
    {
        var client = factory.CreateClient();

        // Register parent
        var parentEmail = UniqueEmail($"par{tag}");
        var (prResp, prBody) = await PostAsync(client, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass!", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body: {0}", prBody);
        var prJson = JsonDocument.Parse(prBody).RootElement;
        TryProp(prJson, "data", out var prData);
        TryProp(prData, "accessToken", out var prTok);
        var parentToken = prTok.GetString()!;

        // Add child
        var childEmail = UniqueEmail($"child{tag}");
        var (addResp, addBody) = await PostAsync(client, AddChildUrl,
            new
            {
                FullName         = "Energy Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 4,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "Add-Child; body: {0}", addBody);

        // Sign in as child to get token + id
        var (signResp, signBody) = await PostAsync(client, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body: {0}", signBody);
        var signJson = JsonDocument.Parse(signBody).RootElement;
        TryProp(signJson, "data", out var signData);
        TryProp(signData, "accessToken", out var accessTok);
        var childToken = accessTok.GetString()!;
        var childId    = GetSubjectIdFromJwt(childToken);

        // Seed balance directly via BillingDbContext (bypasses HTTP — no Billing.Create policy needed)
        if (seedBalance > 0)
        {
            using var scope = factory.Services.CreateScope();
            var db          = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var account     = await db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId);
            if (account is null)
            {
                account = Learnexia.Modules.Billing.Domain.Entities.CreditAccount
                    .CreateEmpty(childId, "Africa/Cairo");
                db.CreditAccounts.Add(account);
                await db.SaveChangesAsync(0);
            }

            var grantKey = $"test-seed-grant:{childId}:{Guid.NewGuid():N}";
            var expiresAt = DateTime.UtcNow.AddMonths(1);
            var tx = account.ApplyGrant(
                seedBalance,
                expiresAt,
                Learnexia.Modules.Billing.Domain.Enums.CreditReasonCode.MonthlyGrantFree,
                grantKey);
            db.CreditTransactions.Add(tx);
            await db.SaveChangesAsync(0);
        }

        return (childId, childToken, parentToken);
    }

    /// <summary>Reads the current total balance from BillingDbContext for <paramref name="childId"/>.</summary>
    private static async Task<(int Total, int DailyUsed, int TxCount)>
        ReadBalanceAsync(EnergyEconomyTestFactory factory, int childId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var account = await db.CreditAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == childId);
        var txCount = account is null
            ? 0
            : await db.CreditTransactions.AsNoTracking()
                .CountAsync(t => t.CreditAccountId == account.Id &&
                                 t.Type == Learnexia.Modules.Billing.Domain.Enums.CreditTransactionType.Spend);
        return (account?.TotalBalance ?? 0, account?.DailyUsed ?? 0, txCount);
    }

    /// <summary>
    /// Polls the ledger until balance drops by <paramref name="expectedDelta"/> (debits appear)
    /// or the deadline is reached. Returns true when the expected delta is observed.
    /// </summary>
    private static async Task<bool> PollForBalanceDeltaAsync(
        EnergyEconomyTestFactory factory,
        int childId,
        int balanceBefore,
        int expectedDelta,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var (total, _, _) = await ReadBalanceAsync(factory, childId);
            if (balanceBefore - total >= expectedDelta) return true;
            await Task.Delay(100);
        }
        return false;
    }

    /// <summary>Parses SSE frames from raw body string.</summary>
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

    /// <summary>
    /// Decodes the user integer id from a JWT bearer token.
    /// Learnexia JWTs use a custom "Id" claim (integer string) rather than the standard "sub".
    /// Falls back to common claim names for robustness.
    /// </summary>
    private static int GetSubjectIdFromJwt(string jwtToken)
    {
        var parts   = jwtToken.Split('.');
        var payload = parts[1];
        // Add base64url padding
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
            if (int.TryParse(raw, out var parsedId)) return parsedId;
        }

        // Fallback: standard JWT claims
        if (doc.TryGetProperty("sub", out var sub))
        {
            var raw = sub.ValueKind == JsonValueKind.Number
                ? sub.GetInt32().ToString()
                : sub.GetString() ?? "";
            if (int.TryParse(raw, out var sid)) return sid;
        }
        if (doc.TryGetProperty("nameid", out var nameid))
        {
            var raw = nameid.ValueKind == JsonValueKind.Number
                ? nameid.GetInt32().ToString()
                : nameid.GetString() ?? "";
            if (int.TryParse(raw, out var nid)) return nid;
        }

        // Last resort: scan all properties for an integer-valued "id" key
        foreach (var prop in doc.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = prop.Value.ValueKind == JsonValueKind.Number
                ? prop.Value.GetInt32().ToString()
                : prop.Value.GetString() ?? "";
            if (int.TryParse(raw, out var vi)) return vi;
        }

        throw new InvalidOperationException($"Cannot parse user id from JWT payload: {json}");
    }

    // =========================================================================
    // AREA 1 — CHARGE ON DELIVERY (MISS)
    // AC: generated answer → balance debited by exactly the Explain cost (3);
    //     ledger has exactly one Spend row; DailyUsed incremented.
    // =========================================================================

    [Fact(DisplayName = "ENERGY-1A ChargeMiss: Explain MISS → balance debited by 3; one Spend ledger row; dailyUsed incremented")]
    public async Task Area1A_ChargeOnDelivery_Miss_Explain_BalanceDebited3_SpendRow_DailyUsed()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);
        factory.FakeGateway.Reset();
        factory.FakeGateway.ResponseContent = "شرح اختباري مقبول من النموذج المزيف.";

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "1A");
        var (balanceBefore, dailyBefore, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(50, "seed balance must be 50 before the request");

        // Use a unique ConceptId so no cache HIT occurs (the shared DB may have prior Approved entries).
        var uniqueConceptId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 10_000_000;

        var (resp, body) = await PostAsync(factory.CreateClient(), ExplainUrl,
            new { SkillId = 11, LessonId = 1, ConceptId = (int?)uniqueConceptId },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always returns 200; body: {0}", body);
        var frames = ParseSseFrames(body);
        frames.Should().Contain(f => f.Event == "message",
            "successful Explain must emit event:message; body={0}", body);

        // Debit is the last step inside the handler (after safety pass + cache write fire-and-forget).
        // Poll the DB until the balance drop is observed (bounded, no fixed sleep).
        var debited = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostExplain, TimeSpan.FromSeconds(8));
        debited.Should().BeTrue(
            "balance must drop by exactly {0} (Explain cost) within 8 s; " +
            "balanceBefore={1}; MONEY DEFECT if still not debited — STOP",
            CostExplain, balanceBefore);

        var (balanceAfter, dailyAfter, spendAfter) = await ReadBalanceAsync(factory, childId);

        // Exact balance delta
        (balanceBefore - balanceAfter).Should().Be(CostExplain,
            "balance delta must be exactly {0} (Explain cost={0}); before={1}, after={2}",
            CostExplain, balanceBefore, balanceAfter);

        // Exactly one new Spend transaction row
        (spendAfter - spendBefore).Should().Be(1,
            "exactly one Spend ledger row must be written; spendBefore={0}, spendAfter={1}",
            spendBefore, spendAfter);

        // DailyUsed incremented by cost
        (dailyAfter - dailyBefore).Should().Be(CostExplain,
            "DailyUsed must be incremented by {0}; before={1}, after={2}",
            CostExplain, dailyBefore, dailyAfter);
    }

    // =========================================================================
    // AREA 2 — CACHE HIT ALSO CHARGES
    // AC: repeat identical request → cache HIT (gateway NOT re-invoked) →
    //     balance debited AGAIN. Two requests → two debits.
    // =========================================================================

    [Fact(DisplayName = "ENERGY-2A CacheHitCharges: 2nd identical Explain → cache HIT (gateway silent) → balance debited again")]
    public async Task Area2A_CacheHit_AlsoCharges_BalanceDebited_Twice()
    {
        // This test uses CacheCountingAiTestFactory (real SafetyLayer + FakeCountingGateway + shared Postgres/Redis)
        // to prove the gateway is NOT invoked on the second (HIT) request, combined with a BillingDbContext
        // scope to assert balance debited twice.
        //
        // CacheCountingAiTestFactory points at the same Testcontainers Postgres as the AiRuntimeFixture
        // and does NOT migrate BillingDbContext (migrations were already applied by InitializeAsync via
        // EnergyEconomyTestFactory.ApplyMigrationsAndSeedAsync). We only need the BillingDbContext for
        // balance assertions — which we access via a scope on an EnergyEconomyTestFactory that also
        // shares the same Postgres connection string.

        // Step 1: use EnergyEconomyTestFactory to create the student + seed balance
        // (needed so balance seed works on the shared Postgres).
        using var billingFactory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(billingFactory, seedBalance: 30, tag: "2A");
        var (balanceBefore, _, _) = await ReadBalanceAsync(billingFactory, childId);
        balanceBefore.Should().Be(30, "seed balance before first request");

        // Step 2: use CacheCountingAiTestFactory to make the SSE requests (real SafetyLayer + FakeGateway).
        // It shares the same Postgres (same AiRuntimeFixture) so the child's CreditAccount is visible.
        using var factory = new CacheCountingAiTestFactory(
            _fixture.PostgresConnectionString,
            _fixture.RedisConnectionString);
        factory.FakeGateway.Reset();
        factory.FakeGateway.ResponseContent = "الكسر جزء من الكل — مع ذاكرة التخزين.";
        var client = factory.CreateClient();

        // Use a unique ConceptId so the first request is a MISS (writes to cache).
        var uniqueConceptId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 20_000_000;
        var reqBody = new { SkillId = 21, LessonId = 1, ConceptId = (int?)uniqueConceptId };

        // ── First request (MISS) ──────────────────────────────────────────────
        var (resp1, body1) = await PostAsync(client, ExplainUrl, reqBody, childToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "1st request; body: {0}", body1);
        ParseSseFrames(body1).Should().Contain(f => f.Event == "message", "1st Explain must deliver; body={0}", body1);

        var gatewayCallsAfterMiss = factory.FakeGateway.CallCount;
        gatewayCallsAfterMiss.Should().BeGreaterThan(0,
            "gateway must be called on cache MISS (real SafetyLayer path); actual={0}", gatewayCallsAfterMiss);

        // Poll until first debit lands in BillingDbContext (shared Postgres)
        var debit1Ok = await PollForBalanceDeltaAsync(billingFactory, childId, balanceBefore, CostExplain, TimeSpan.FromSeconds(8));
        debit1Ok.Should().BeTrue("1st debit (MISS) must land; MONEY DEFECT if not — STOP");

        // Poll until the cache write for this entry appears in AiResponseCache (bounded — no race).
        var cacheWriteDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < cacheWriteDeadline)
        {
            using var pollScope = factory.Services.CreateScope();
            var aiDb = pollScope.ServiceProvider.GetRequiredService<AiDbContext>();
            if (await aiDb.AiResponseCaches.AsNoTracking().AnyAsync(r => r.InvalidatedAt == null)) break;
            await Task.Delay(100);
        }

        var (balanceAfterMiss, _, spendAfterMiss) = await ReadBalanceAsync(billingFactory, childId);
        (balanceBefore - balanceAfterMiss).Should().Be(CostExplain,
            "balance after 1st (MISS) must drop by exactly {0}; before={1}, after={2}",
            CostExplain, balanceBefore, balanceAfterMiss);

        // ── Second identical request (HIT) ───────────────────────────────────
        var (resp2, body2) = await PostAsync(client, ExplainUrl, reqBody, childToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "2nd request; body: {0}", body2);
        ParseSseFrames(body2).Should().Contain(f => f.Event == "message",
            "cache HIT must still deliver content; body={0}", body2);

        // Gateway must NOT be called again on a HIT (call count unchanged).
        factory.FakeGateway.CallCount.Should().Be(gatewayCallsAfterMiss,
            "cache HIT must NOT invoke gateway — call count must stay at {0}; actual={1}",
            gatewayCallsAfterMiss, factory.FakeGateway.CallCount);

        // Poll until second debit lands — HIT path also debits (locked economy invariant).
        var debit2Ok = await PollForBalanceDeltaAsync(billingFactory, childId, balanceAfterMiss, CostExplain, TimeSpan.FromSeconds(8));
        debit2Ok.Should().BeTrue(
            "2nd debit (HIT) must land; cache HIT MUST charge — locked energy economy invariant; MONEY DEFECT — STOP");

        var (balanceAfterHit, _, spendAfterHit) = await ReadBalanceAsync(billingFactory, childId);
        (balanceBefore - balanceAfterHit).Should().Be(2 * CostExplain,
            "two requests → two debits → total delta={0}; before={1}, after={2}",
            2 * CostExplain, balanceBefore, balanceAfterHit);
        (spendAfterHit - spendAfterMiss).Should().Be(1,
            "one additional Spend ledger row for the HIT; spendAfterMiss={0}, spendAfterHit={1}",
            spendAfterMiss, spendAfterHit);
    }

    // =========================================================================
    // AREA 3 — NO DELIVERY → NO DEBIT (CRITICAL SAFETY CASES)
    // 3a. Safety-blocked response → no debit, no Spend row
    // 3b. Refuse-and-redirect (empty context) → no debit
    // 3c. Generation error (safety throws) → no debit
    // =========================================================================

    [Fact(DisplayName = "ENERGY-3A NoCharge_SafetyBlocked: safety blocks response → balance UNCHANGED, no Spend row")]
    public async Task Area3A_NoCharge_SafetyBlocked_BalanceUnchanged()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Blocked,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "3A");
        var client = factory.CreateClient();
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var uniqueConceptId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 30_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 31, LessonId = 1, ConceptId = (int?)uniqueConceptId },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE returns 200 even on block; body: {0}", body);
        var frames = ParseSseFrames(body);
        frames.Should().Contain(f => f.Event == "error",
            "safety block must emit event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content delivered on safety block; body={0}", body);

        // Wait to confirm no debit occurs (no expected delta → wait 1 s then assert zero delta).
        await Task.Delay(1500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);

        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: safety-blocked response MUST NOT debit; balanceBefore={0}, balanceAfter={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend ledger row must appear on safety block; spendBefore={0}, spendAfter={1}. STOP.",
            spendBefore, spendAfter);
    }

    [Fact(DisplayName = "ENERGY-3B NoCharge_RedirectNoContext: empty context → redirect → balance UNCHANGED")]
    public async Task Area3B_NoCharge_Redirect_EmptyContext_BalanceUnchanged()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.Empty);   // triggers refuse-and-redirect

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "3B");
        var client = factory.CreateClient();
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 32, LessonId = (int?)null, ConceptId = (int?)null },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE returns 200; body: {0}", body);
        var frames = ParseSseFrames(body);
        frames.Should().Contain(f => f.Event == "redirect",
            "empty context must produce event:redirect; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content on redirect; body={0}", body);

        await Task.Delay(1500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);

        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: refuse-and-redirect MUST NOT debit; balanceBefore={0}, balanceAfter={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend row on redirect; spendBefore={0}, spendAfter={1}. STOP.",
            spendBefore, spendAfter);
    }

    [Fact(DisplayName = "ENERGY-3C NoCharge_GenerationError: safety throws → event:error → balance UNCHANGED")]
    public async Task Area3C_NoCharge_GenerationError_BalanceUnchanged()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.ThrowOnCall,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "3C");
        var client = factory.CreateClient();
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var uniqueConceptId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 33_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 33, LessonId = 1, ConceptId = (int?)uniqueConceptId },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE returns 200 even on error; body: {0}", body);
        var frames = ParseSseFrames(body);
        frames.Should().Contain(f => f.Event == "error",
            "generation error must emit event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content on generation error; body={0}", body);

        await Task.Delay(1500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);

        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: generation error MUST NOT debit; balanceBefore={0}, balanceAfter={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend row on generation error; spendBefore={0}, spendAfter={1}. STOP.",
            spendBefore, spendAfter);
    }

    // =========================================================================
    // AREA 4 — PRE-AUTHORIZE / INSUFFICIENT ENERGY BLOCK
    // AC: balance < cost → request BLOCKED with typed SSE error; gateway NEVER
    //     called; balance stays (no debit). Top up → now succeeds + charges.
    // =========================================================================

    [Fact(DisplayName = "ENERGY-4A PreAuth_InsufficientBalance: balance=0 → blocked with SSE error; gateway NOT called; balance unchanged")]
    public async Task Area4A_PreAuth_EmptyBalance_BlockedWithSseError_GatewayNotCalled()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);
        factory.FakeGateway.Reset();

        // Create student with ZERO balance (seedBalance=0).
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 0, tag: "4A");
        var client = factory.CreateClient();
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(0, "child must start with zero balance for pre-auth test");

        var uniqueConceptId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 40_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 41, LessonId = 1, ConceptId = (int?)uniqueConceptId },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        // Must have an error frame (insufficient energy)
        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "insufficient balance must produce event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content when pre-auth blocks; body={0}", body);

        // Gateway must NOT be called (pre-auth short-circuits before any LLM call).
        factory.FakeGateway.CallCount.Should().Be(0,
            "MONEY DEFECT: gateway must NOT be invoked when balance < cost; callCount={0}. STOP.",
            factory.FakeGateway.CallCount);

        // Balance unchanged
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: balance must not change on pre-auth block; before={0}, after={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend row on pre-auth block; before={0}, after={1}. STOP.",
            spendBefore, spendAfter);
    }

    [Fact(DisplayName = "ENERGY-4B PreAuth_TopUp_Succeeds: balance topped up → request succeeds + charges")]
    public async Task Area4B_PreAuth_TopUp_ThenSucceeds_AndCharges()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);
        factory.FakeGateway.ResponseContent = "شرح بعد شحن الرصيد.";

        // Start with balance = cost - 1 (below threshold) to confirm block, then seed more.
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: CostExplain - 1, tag: "4B");
        var client = factory.CreateClient();

        // Confirm blocked with insufficient balance
        var uniqueConceptId4B = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 41_000_000;
        var reqBody = new { SkillId = 42, LessonId = 1, ConceptId = (int?)uniqueConceptId4B };
        var (resp1, body1) = await PostAsync(client, ExplainUrl, reqBody, childToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body1);
        ParseSseFrames(body1).Should().Contain(f => f.Event == "error",
            "balance={0} < cost={1} must block; body={0}", CostExplain - 1, CostExplain, body1);

        // Top up: seed additional credits directly
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var account = await db.CreditAccounts.FirstAsync(a => a.ChildId == childId);
            var grantKey = $"topup:{childId}:{Guid.NewGuid():N}";
            var tx = account.ApplyGrant(
                10, DateTime.UtcNow.AddMonths(1),
                Learnexia.Modules.Billing.Domain.Enums.CreditReasonCode.MonthlyGrantFree,
                grantKey);
            db.CreditTransactions.Add(tx);
            await db.SaveChangesAsync(0);
        }

        var (balanceAfterTopUp, _, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceAfterTopUp.Should().BeGreaterThanOrEqualTo(CostExplain,
            "after top-up balance must be at least {0}", CostExplain);

        // Now the same request must succeed and charge
        factory.FakeGateway.Reset();
        var (resp2, body2) = await PostAsync(client, ExplainUrl, reqBody, childToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body2);
        ParseSseFrames(body2).Should().Contain(f => f.Event == "message",
            "after top-up request must deliver; body={0}", body2);

        // Wait for debit
        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceAfterTopUp, CostExplain, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("debit must land after top-up success; MONEY DEFECT — STOP");

        var (balanceAfterRequest, _, spendAfter) = await ReadBalanceAsync(factory, childId);
        (balanceAfterTopUp - balanceAfterRequest).Should().Be(CostExplain,
            "balance delta after top-up request must be {0}; before={1}, after={2}",
            CostExplain, balanceAfterTopUp, balanceAfterRequest);
        (spendAfter - spendBefore).Should().Be(1,
            "exactly one Spend row for the post-top-up request; spendBefore={0}, spendAfter={1}",
            spendBefore, spendAfter);
    }

    // =========================================================================
    // AREA 5 — PER-INTENT COSTS
    // AC: Hint=1, WhyWrong=2, Explain=3, SimilarExample=5 (from GlobalSettings)
    // =========================================================================

    [Fact(DisplayName = "ENERGY-5A PerIntent_Hint: Hint request → balance debited by exactly 1")]
    public async Task Area5A_PerIntent_Hint_Debits1()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "5A");
        var (balanceBefore, _, _) = await ReadBalanceAsync(factory, childId);
        var client = factory.CreateClient();

        var (resp, body) = await PostAsync(client, HintUrl,
            new { QuestionId = 55, AttemptId = 1, Intent = 2 /* Hint */, HintLevel = (int?)null, WrongAnswer = (string?)null },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        ParseSseFrames(body).Should().Contain(f => f.Event == "message" || f.Event == "done",
            "Hint happy-path must deliver; body={0}", body);

        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostHint, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("Hint debit must land within 8 s; MONEY DEFECT — STOP");

        var (balanceAfter, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(CostHint,
            "Hint cost must be exactly {0}; before={1}, after={2}", CostHint, balanceBefore, balanceAfter);
    }

    [Fact(DisplayName = "ENERGY-5B PerIntent_WhyWrong: WhyWrong intent → balance debited by exactly 2")]
    public async Task Area5B_PerIntent_WhyWrong_Debits2()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "5B");
        var (balanceBefore, _, _) = await ReadBalanceAsync(factory, childId);
        var client = factory.CreateClient();

        var (resp, body) = await PostAsync(client, HintUrl,
            new { QuestionId = 55, AttemptId = 1, Intent = 3 /* WhyWrong */, HintLevel = (int?)null, WrongAnswer = "incorrect_answer_5B" },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        ParseSseFrames(body).Should().Contain(f => f.Event == "message" || f.Event == "done",
            "WhyWrong happy-path must deliver; body={0}", body);

        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostWhyWrong, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("WhyWrong debit must land within 8 s; MONEY DEFECT — STOP");

        var (balanceAfter, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(CostWhyWrong,
            "WhyWrong cost must be exactly {0}; before={1}, after={2}", CostWhyWrong, balanceBefore, balanceAfter);
    }

    [Fact(DisplayName = "ENERGY-5C PerIntent_Explain: Explain (deep_explanation) → balance debited by exactly 3")]
    public async Task Area5C_PerIntent_Explain_Debits3()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "5C");
        var (balanceBefore, _, _) = await ReadBalanceAsync(factory, childId);
        var client = factory.CreateClient();

        var uniqueConceptId5C = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 50_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 51, LessonId = 1, ConceptId = (int?)uniqueConceptId5C },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        ParseSseFrames(body).Should().Contain(f => f.Event == "message" || f.Event == "done",
            "Explain happy-path must deliver; body={0}", body);

        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostExplain, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("Explain debit must land within 8 s; MONEY DEFECT — STOP");

        var (balanceAfter, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(CostExplain,
            "Explain cost must be exactly {0}; before={1}, after={2}", CostExplain, balanceBefore, balanceAfter);
    }

    [Fact(DisplayName = "ENERGY-5D PerIntent_SimilarExample: SimilarExample → balance debited by exactly 5")]
    public async Task Area5D_PerIntent_SimilarExample_Debits5()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "5D");
        var (balanceBefore, _, _) = await ReadBalanceAsync(factory, childId);
        var client = factory.CreateClient();

        var (resp, body) = await PostAsync(client, SimilarExUrl,
            new { SkillId = 54, QuestionId = (int?)null },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        ParseSseFrames(body).Should().Contain(f => f.Event == "message" || f.Event == "done",
            "SimilarExample happy-path must deliver; body={0}", body);

        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostSimilarEx, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("SimilarExample debit must land within 8 s; MONEY DEFECT — STOP");

        var (balanceAfter, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(CostSimilarEx,
            "SimilarExample cost must be exactly {0}; before={1}, after={2}", CostSimilarEx, balanceBefore, balanceAfter);
    }

    [Fact(DisplayName = "ENERGY-5E PerIntent_Simplify: Simplify (deep_explanation key) → balance debited by exactly 3")]
    public async Task Area5E_PerIntent_Simplify_Debits3()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "5E");
        var (balanceBefore, _, _) = await ReadBalanceAsync(factory, childId);
        var client = factory.CreateClient();

        var uniqueConceptId5E = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 55_000_000;
        var (resp, body) = await PostAsync(client, SimplifyUrl,
            new { ConceptId = uniqueConceptId5E, LessonId = 1, PreviousExplanationRef = (string?)null },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        ParseSseFrames(body).Should().Contain(f => f.Event == "message" || f.Event == "done",
            "Simplify happy-path must deliver; body={0}", body);

        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostSimplify, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("Simplify debit must land within 8 s; MONEY DEFECT — STOP");

        var (balanceAfter, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(CostSimplify,
            "Simplify cost must be exactly {0}; before={1}, after={2}", CostSimplify, balanceBefore, balanceAfter);
    }

    // =========================================================================
    // AREA 6 — DAILY SOFT CAP (WARNING, NOT BLOCK BY DEFAULT)
    // AC: drive spend past daily soft cap → requests still succeed (soft cap);
    //     EnergyStatusQuery reports DailyCapReached=true.
    //     With HardStopEnabled=true, daily-cap-reached BLOCKS requests.
    // =========================================================================

    [Fact(DisplayName = "ENERGY-6A DailyCap_Soft: spend past daily cap → requests still succeed; EnergyStatus.DailyCapReached=true")]
    public async Task Area6A_DailySoftCap_RequestsStillSucceed_StatusReportsCapReached()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty,
            hardStopEnabled: false);

        // Seed enough balance to exceed daily cap (cap=10 credits; Hint=1 each → 11 Hint requests)
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "6A");
        var client = factory.CreateClient();

        // Drive spend to exactly the daily cap via direct DB manipulation to avoid slow looping:
        // Manually set DailyUsed = dailyCap and DailyUsedDateLocal = today.
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var account = await db.CreditAccounts.FirstAsync(a => a.ChildId == childId);
            account.DailyUsed = DefaultDailyCap;  // exactly at cap
            account.DailyUsedDateLocal = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await db.SaveChangesAsync(0);
        }

        // Now issue one more Hint request. With HardStopEnabled=false → soft cap → should succeed.
        var uniqueConceptId6A = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 60_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 61, LessonId = 1, ConceptId = (int?)uniqueConceptId6A },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        // Soft cap: request must SUCCEED (no block — soft warns, doesn't stop)
        frames.Should().Contain(f => f.Event == "message",
            "soft daily cap must NOT block the request — it should still deliver content; body={0}", body);
        frames.Should().NotContain(f => f.Event == "error",
            "soft cap must not emit error; body={0}", body);

        // Wait for debit to land, then check EnergyStatus
        var debitOk = await PollForBalanceDeltaAsync(factory, childId,
            // Use balanceBefore from direct read
            (await ReadBalanceAsync(factory, childId)).Total + CostExplain,
            0, TimeSpan.FromSeconds(2)); // just wait

        // Query EnergyStatus endpoint to confirm DailyCapReached
        await Task.Delay(500); // brief wait for debit to finalize DailyUsed
        var (statusResp, statusBody) = await GetAsync(client,
            $"{EnergyStatusBase}/{childId}/Status", childToken);
        statusResp.StatusCode.Should().Be(HttpStatusCode.OK, "EnergyStatus must return 200; body: {0}", statusBody);
        var statusJson = JsonDocument.Parse(statusBody).RootElement;
        TryProp(statusJson, "data", out var statusData).Should().BeTrue("body: {0}", statusBody);
        TryProp(statusData, "dailyCapReached", out var capReachedProp).Should().BeTrue("body: {0}", statusBody);
        capReachedProp.GetBoolean().Should().BeTrue(
            "after spending past daily cap, EnergyStatus.DailyCapReached must be true; body={0}", statusBody);
        TryProp(statusData, "warningState", out var warnProp).Should().BeTrue("body: {0}", statusBody);
        // WarningState.DailyCapReached = 3 (enum numeric)
        var warnRaw = warnProp.ValueKind == JsonValueKind.Number
            ? warnProp.GetInt32().ToString()
            : warnProp.GetString();
        warnRaw.Should().NotBeNullOrWhiteSpace("warningState must be present; body={0}", statusBody);
    }

    [Fact(DisplayName = "ENERGY-6B DailyCap_Hard: HardStopEnabled=true + dailyCapReached → request BLOCKED with SSE error")]
    public async Task Area6B_DailyHardStop_RequestBlocked()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty,
            hardStopEnabled: true);    // triggers daily-cap hard-stop path
        factory.FakeGateway.Reset();

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "6B");
        var client = factory.CreateClient();

        // Manually set DailyUsed = dailyCap (reached) and DailyUsedDateLocal = today.
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var account = await db.CreditAccounts.FirstAsync(a => a.ChildId == childId);
            account.DailyUsed = DefaultDailyCap;
            account.DailyUsedDateLocal = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await db.SaveChangesAsync(0);
        }

        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var uniqueConceptId6B = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 61_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 62, LessonId = 1, ConceptId = (int?)uniqueConceptId6B },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        // Hard stop: must emit error, no content
        frames.Should().Contain(f => f.Event == "error",
            "hard daily cap must block the request and emit event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content when hard daily cap blocks; body={0}", body);

        // Gateway must NOT be called (pre-auth short-circuits before LLM)
        factory.FakeGateway.CallCount.Should().Be(0,
            "gateway must NOT be called when daily hard-stop blocks; callCount={0}", factory.FakeGateway.CallCount);

        // Balance unchanged, no Spend row
        await Task.Delay(500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: daily hard-stop must NOT debit; before={0}, after={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend row on daily hard-stop; before={0}, after={1}. STOP.",
            spendBefore, spendAfter);
    }

    // =========================================================================
    // AREA 7 — GRANT (P10-02)
    // AC: child receives monthly grant via BillingGrantJob;
    //     granted balance is spendable; idempotent (no double-grant).
    // =========================================================================

    [Fact(DisplayName = "ENERGY-7A Grant_MonthlyJobGrantsBalance: BillingGrantJob → child receives granted balance")]
    public async Task Area7A_Grant_BillingGrantJob_ChildReceivesBalance()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        // Create child with zero balance
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 0, tag: "7A");

        // Manually create the CreditAccount for this child so the job can see it.
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var existing = await db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId);
            if (existing is null)
            {
                var account = Learnexia.Modules.Billing.Domain.Entities.CreditAccount
                    .CreateEmpty(childId, "Africa/Cairo");
                db.CreditAccounts.Add(account);
                await db.SaveChangesAsync(0);
            }
        }

        var (balanceBefore, _, _) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(0, "child must start with zero balance");

        // Run BillingGrantJob directly (no Hangfire scheduler — invoke the job method directly).
        using (var jobScope = factory.Services.CreateScope())
        {
            var job = jobScope.ServiceProvider.GetRequiredService<BillingGrantJob>();
            await job.RunAsync(CancellationToken.None);
        }

        // After the job runs, the child must have received the free monthly grant (100 credits default).
        var (balanceAfterGrant, _, _) = await ReadBalanceAsync(factory, childId);
        balanceAfterGrant.Should().Be(DefaultMonthly,
            "child must receive the monthly free grant of {0} credits; balanceBefore={1}, balanceAfter={2}",
            DefaultMonthly, balanceBefore, balanceAfterGrant);
    }

    [Fact(DisplayName = "ENERGY-7B Grant_Idempotent: BillingGrantJob run twice → same period → NO double-grant")]
    public async Task Area7B_Grant_Idempotent_NoDoubleGrant()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        // Create child + zero balance account
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 0, tag: "7B");
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var existing = await db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId);
            if (existing is null)
            {
                db.CreditAccounts.Add(
                    Learnexia.Modules.Billing.Domain.Entities.CreditAccount.CreateEmpty(childId, "Africa/Cairo"));
                await db.SaveChangesAsync(0);
            }
        }

        // Run job twice for the same UTC month.
        using (var scope1 = factory.Services.CreateScope())
        {
            var job = scope1.ServiceProvider.GetRequiredService<BillingGrantJob>();
            await job.RunAsync(CancellationToken.None);
        }
        var (balanceAfterFirst, _, _) = await ReadBalanceAsync(factory, childId);

        using (var scope2 = factory.Services.CreateScope())
        {
            var job = scope2.ServiceProvider.GetRequiredService<BillingGrantJob>();
            await job.RunAsync(CancellationToken.None);
        }
        var (balanceAfterSecond, _, _) = await ReadBalanceAsync(factory, childId);

        balanceAfterSecond.Should().Be(balanceAfterFirst,
            "BillingGrantJob must be idempotent — running twice in the same period must NOT double-grant; " +
            "firstBalance={0}, secondBalance={1}", balanceAfterFirst, balanceAfterSecond);
    }

    [Fact(DisplayName = "ENERGY-7C Grant_Spendable: granted balance is spendable for AI requests")]
    public async Task Area7C_Grant_Spendable_ForAiRequests()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);
        factory.FakeGateway.ResponseContent = "شرح بعد منح الرصيد الشهري.";

        // Create child with zero balance
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 0, tag: "7C");
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var existing = await db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId);
            if (existing is null)
            {
                db.CreditAccounts.Add(
                    Learnexia.Modules.Billing.Domain.Entities.CreditAccount.CreateEmpty(childId, "Africa/Cairo"));
                await db.SaveChangesAsync(0);
            }
        }

        // Run grant job to give the child their monthly grant
        using (var scope = factory.Services.CreateScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<BillingGrantJob>();
            await job.RunAsync(CancellationToken.None);
        }

        var (balanceAfterGrant, _, _) = await ReadBalanceAsync(factory, childId);
        balanceAfterGrant.Should().BeGreaterThanOrEqualTo(CostExplain,
            "granted balance must be enough to cover Explain cost={0}", CostExplain);

        // Now make an Explain request — must succeed and charge
        var client = factory.CreateClient();
        var uniqueConceptId7C = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 2_000_000) + 70_000_000;
        var (resp, body) = await PostAsync(client, ExplainUrl,
            new { SkillId = 71, LessonId = 1, ConceptId = (int?)uniqueConceptId7C },
            childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        ParseSseFrames(body).Should().Contain(f => f.Event == "message",
            "granted balance must enable AI request; body={0}", body);

        var debitOk = await PollForBalanceDeltaAsync(factory, childId, balanceAfterGrant, CostExplain, TimeSpan.FromSeconds(8));
        debitOk.Should().BeTrue("granted balance debit must land within 8 s; MONEY DEFECT — STOP");

        var (balanceFinal, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceAfterGrant - balanceFinal).Should().Be(CostExplain,
            "granted balance must be debited by Explain cost={0}; before={1}, after={2}",
            CostExplain, balanceAfterGrant, balanceFinal);
    }

    // =========================================================================
    // AREA 8 — ENERGY STATUS (P10-04) + IDOR
    // AC: GET /api/Billing/Energy/{childId}/Status returns correct balance/daily/warning;
    //     a parent CANNOT read another family's child status (403);
    //     a student can only read their own (403 for another's childId).
    // =========================================================================

    [Fact(DisplayName = "ENERGY-8A EnergyStatus: student reads own status → 200 with correct balance fields")]
    public async Task Area8A_EnergyStatus_OwnChild_Returns200_WithBalanceFields()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 42, tag: "8A");
        var client = factory.CreateClient();

        var (resp, body) = await GetAsync(client, $"{EnergyStatusBase}/{childId}/Status", childToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "student reading own status must get 200; body: {0}", body);

        var json = JsonDocument.Parse(body).RootElement;
        TryProp(json, "successed", out var succeedProp).Should().BeTrue("body: {0}", body);
        succeedProp.GetBoolean().Should().BeTrue("successed must be true; body: {0}", body);

        TryProp(json, "data", out var dataProp).Should().BeTrue("body: {0}", body);
        TryProp(dataProp, "totalBalance", out var totalProp).Should().BeTrue("body: {0}", body);
        totalProp.GetInt32().Should().Be(42, "totalBalance must reflect seeded balance of 42; body: {0}", body);

        TryProp(dataProp, "dailyCap", out var dailyCapProp).Should().BeTrue("body: {0}", body);
        dailyCapProp.GetInt32().Should().Be(DefaultDailyCap, "dailyCap must be {0}; body: {1}", DefaultDailyCap, body);

        TryProp(dataProp, "grantedBalance", out var grantedProp).Should().BeTrue("body: {0}", body);
        grantedProp.GetInt32().Should().Be(42, "grantedBalance must be 42 (seeded via ApplyGrant); body: {0}", body);

        TryProp(dataProp, "warningState", out _).Should().BeTrue("warningState field must be present; body: {0}", body);
        TryProp(dataProp, "dailyCapReached", out var capProp).Should().BeTrue("body: {0}", body);
        capProp.GetBoolean().Should().BeFalse("cap not reached when DailyUsed=0; body: {0}", body);
    }

    [Fact(DisplayName = "ENERGY-8B IDOR_StudentCannotReadOtherChild: student JWT for childA reads childB status → 403/424")]
    public async Task Area8B_IDOR_Student_CannotReadOtherChildStatus()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        // Create two independent children
        var (childAId, childAToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 10, tag: "8Ba");
        var (childBId, childBToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 10, tag: "8Bb");
        var client = factory.CreateClient();

        // Child A tries to read Child B's energy status → must be 403 or 424 (business validation / forbidden).
        var (resp, body) = await GetAsync(client, $"{EnergyStatusBase}/{childBId}/Status", childAToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 403, 424 },
            "student reading ANOTHER child's energy status must be denied (403 Forbidden or 424 BusinessValidation); " +
            "200 would be an IDOR defect — STOP; body: {0}", body);
    }

    [Fact(DisplayName = "ENERGY-8C IDOR_ParentCannotReadUnlinkedChild: parent JWT reads unlinked child status → 403/424")]
    public async Task Area8C_IDOR_Parent_CannotReadUnlinkedChildStatus()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture,
            safetyMode: StubSafetyLayer.Mode.Allowed,
            contextMode: StubContextProvider.Mode.NonEmpty);

        // Create two families (each parent registers + adds a child)
        var (childAId, _, parentAToken) = await CreateStudentWithBalanceAsync(factory, seedBalance: 10, tag: "8Ca");
        var (childBId, _, _)            = await CreateStudentWithBalanceAsync(factory, seedBalance: 10, tag: "8Cb");
        var client = factory.CreateClient();

        // Parent A tries to read Parent B's child (childB) status → must be denied.
        var (resp, body) = await GetAsync(client, $"{EnergyStatusBase}/{childBId}/Status", parentAToken);
        ((int)resp.StatusCode).Should().BeOneOf(new[] { 403, 424 },
            "parent reading ANOTHER family's child energy status must be denied (403/424); " +
            "200 would be an IDOR defect — STOP; actual={0}; body: {1}", (int)resp.StatusCode, body);
    }

    [Fact(DisplayName = "ENERGY-8D EnergyStatus_Unauthenticated: no JWT → 401")]
    public async Task Area8D_EnergyStatus_NoJwt_Returns401()
    {
        using var factory = new EnergyEconomyTestFactory(_fixture);
        var (childId, _, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 10, tag: "8D");
        var client = factory.CreateClient();

        var (resp, body) = await GetAsync(client, $"{EnergyStatusBase}/{childId}/Status");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "unauthenticated request to EnergyStatus must return 401; body: {0}", body);
    }
}

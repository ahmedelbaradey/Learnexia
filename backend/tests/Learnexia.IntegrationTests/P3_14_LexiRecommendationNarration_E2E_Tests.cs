// ReSharper disable InconsistentNaming
// P3-14a — Lexi-framing enrichment: CurrentLevel in cache key (appended to this file)
// P3-14 — Lexi Recommendation Narration SSE endpoint integration tests
//
// POST /api/AiTutor/Recommendation/Narrate   [Authorize(Roles="Student")]
//
// SSE wire contract (pinned — do NOT deviate):
//   event: message   data: {"content":"<approved narration>"}    — delivery, charged
//   event: declined  data: {"reason":"NoData"}                   — no recs, NO charge
//   event: error     data: {"code":"…","message":"…"}            — safety/gate/auth err, NO charge
//   event: done      data: [DONE]                                — terminator (not on error)
//
// Money-correctness invariant (STOP criteria):
//   If any no-charge case actually charges, or delivery does NOT charge → STOP and report defect.
//   Cost = ai_cost.recommendation = 5 (seeded by GlobalSettingsSeeder).
//   Reason code = CreditReasonCode.AiRecommendation = "AiRecommendation".
//
// Infrastructure:
//   Reuses AiRuntimeFixture (shared Testcontainers Postgres/pgvector + Redis from
//   P3_AI_RuntimeActivation_E2E_Tests.cs in the same compilation unit).
//   Reuses EnergyEconomyTestFactory (from P10_W2_EnergyEconomy_E2E_Tests.cs — same compilation unit).
//   StudentRecommendation rows are seeded directly via LearningDbContext.
//   IStudentRecommendationsQuery is the REAL adapter (read from LearningDbContext) — no stub needed.
//   ISafetyLayer is stubbed per-test (StubSafetyLayer from P3_04_ExplainSse_Tests.cs).
//
// Running:
//   dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj
//             --filter "FullyQualifiedName~P3_14"

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Ai.Application.Cache;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// FACTORY
// A thin specialisation of EnergyEconomyTestFactory that:
//   - Additionally stubs ISafetyLayer (so the test controls Allowed/Blocked/Throw).
//   - Exposes the FakeCountingGateway for gateway-call assertions.
//   - Does NOT stub IStudentRecommendationsQuery — the real Learning adapter runs
//     against the test Postgres, so we can seed real recommendation rows and have
//     the handler pick them up.
//   - Uses the same shared AiRuntimeFixture (one Postgres + Redis for the whole
//     AiRuntimeE2E collection) so no extra containers are spun up.
// =============================================================================

/// <summary>
/// WebApplicationFactory for P3-14 Lexi recommendation narration integration tests.
///
/// Key wires:
///   • IAiGateway  → FakeCountingGateway (offline, counted)
///   • ISafetyLayer → StubSafetyLayer (Allowed / Blocked / ThrowOnCall per test)
///   • IStudentRecommendationsQuery → REAL adapter (reads from test Postgres)
///   • All DbContexts → Testcontainers Postgres via AiRuntimeTestFactory.ReplaceDbContexts
///   • BillingDbContext additionally replaced (for wallet read/assert)
///   • Redis → Testcontainers Redis
/// </summary>
public sealed class LexiNarrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _pgConn;
    private readonly string _redisConn;
    private readonly StubSafetyLayer.Mode _safetyMode;

    public readonly FakeCountingGateway FakeGateway = new();

    public LexiNarrationTestFactory(
        AiRuntimeFixture fixture,
        StubSafetyLayer.Mode safetyMode = StubSafetyLayer.Mode.Allowed)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _pgConn     = fixture.PostgresConnectionString;
        _redisConn  = fixture.RedisConnectionString;
        _safetyMode = safetyMode;
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
                ["Billing:HardStopEnabled"]               = "false",
            }));

        builder.ConfigureServices(services =>
        {
            // ── All module DbContexts → Testcontainers Postgres ─────────────────
            AiRuntimeTestFactory.ReplaceDbContexts(services, _pgConn);

            // ── BillingDbContext (not covered by ReplaceDbContexts above) ───────
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<BillingDbContext>();
            services.AddDbContext<BillingDbContext>(options =>
                options.UseNpgsql(
                    _pgConn,
                    npgsql => npgsql
                        .MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)
                        .MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName)));

            // ── IAiGateway → FakeCountingGateway (offline) ──────────────────────
            services.RemoveAll<IAiGateway>();
            services.AddScoped<IAiGateway>(_ => FakeGateway);

            // ── ISafetyLayer → per-test stub ────────────────────────────────────
            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ =>
                new StubSafetyLayer { Behavior = _safetyMode });

            // ── Disable IP rate limiter ──────────────────────────────────────────
            AiRuntimeTestFactory.DisableIpRateLimit(services);
        });
    }
}

// =============================================================================
// TEST CLASS
// =============================================================================

/// <summary>
/// P3-14 end-to-end integration tests for POST /api/AiTutor/Recommendation/Narrate.
///
/// Test areas:
///   TC-1  Happy + charge: recs present → event:message delivered + balance debited by 5 + Spend row with AiRecommendation
///   TC-2  Empty recs → declined, NO debit
///   TC-3  Insufficient energy → blocked (event:error), NO debit
///   TC-4  Safety-blocked → event:error, NO debit
///   TC-5  Auth: anonymous → 401; Parent JWT → 403
///   TC-6  Cache-hit charges: second identical-day call hits cache → STILL debits 5
/// </summary>
[Collection("AiRuntimeE2E")]
public sealed class P3_14_LexiRecommendationNarration_E2E_Tests : IAsyncLifetime
{
    // ── URL constants ──────────────────────────────────────────────────────────
    private const string NarrateUrl         = "api/AiTutor/Recommendation/Narrate";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string ValidChildPassword = "Child@Pass1";

    // P3-14 locked cost (GlobalSettingsSeeder: ai_cost.recommendation = 5)
    private const int CostRecommendation = 5;

    // ── Infrastructure ─────────────────────────────────────────────────────────
    private readonly AiRuntimeFixture _fixture;
    private bool _migrationsApplied;

    public P3_14_LexiRecommendationNarration_E2E_Tests(AiRuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_migrationsApplied)
        {
            // Migrate + seed GlobalSettings (ai_cost.recommendation seeded here).
            // Re-use EnergyEconomyTestFactory.ApplyMigrationsAndSeedAsync which migrates
            // all modules including BillingDbContext and seeds GlobalSettings.
            using var seedFactory = new EnergyEconomyTestFactory(_fixture);
            await seedFactory.ApplyMigrationsAndSeedAsync();
            _migrationsApplied = true;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ==========================================================================
    // HELPERS
    // ==========================================================================

    private static string UniqueEmail(string tag)
        => $"p314_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
        PostAsync(HttpClient client, string url, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            // P3-14 command has an empty body (childId comes from JWT)
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
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
    /// Creates a parent → child account and returns (childId, childToken, parentToken).
    /// Seeds balance directly via BillingDbContext using the FamilyEnergyAccount/ChildEnergyAllocation
    /// wallet model (P10-13). Uses the same pattern as EnergyEconomyTestFactory.CreateStudentWithBalanceAsync.
    /// </summary>
    private async Task<(int ChildId, string ChildToken, string ParentToken)>
        CreateStudentWithBalanceAsync(
            LexiNarrationTestFactory factory,
            int seedBalance,
            string tag = "")
    {
        var client = factory.CreateClient();

        var parentEmail = UniqueEmail($"par{tag}");
        var (prResp, prBody) = await PostParentAsync(client, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass!", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body: {0}", prBody);
        var prJson = JsonDocument.Parse(prBody).RootElement;
        TryProp(prJson, "data", out var prData);
        TryProp(prData, "accessToken", out var prTok);
        var parentToken = prTok.GetString()!;

        var childEmail = UniqueEmail($"child{tag}");
        var (addResp, addBody) = await PostParentAsync(client, AddChildUrl,
            new
            {
                FullName         = "Lexi Narration Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 4,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "Add-Child; body: {0}", addBody);

        var (signResp, signBody) = await PostParentAsync(client, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body: {0}", signBody);
        var signJson = JsonDocument.Parse(signBody).RootElement;
        TryProp(signJson, "data", out var signData);
        TryProp(signData, "accessToken", out var accessTok);
        var childToken = accessTok.GetString()!;
        var childId    = GetSubjectIdFromJwt(childToken);

        if (seedBalance > 0)
        {
            using var scope = factory.Services.CreateScope();
            var db          = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            var syntheticParentId = -(childId);
            var wallet = await db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.ParentUserId == syntheticParentId);
            if (wallet is null)
            {
                wallet = Learnexia.Modules.Billing.Domain.Entities.FamilyEnergyAccount.CreateEmpty(syntheticParentId);
                db.FamilyEnergyAccounts.Add(wallet);
                await db.SaveChangesAsync(0);
            }

            var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
            var alloc = await db.ChildEnergyAllocations.FirstOrDefaultAsync(
                a => a.FamilyEnergyAccountId == wallet.Id && a.ChildId == childId && a.CycleStartUtc == cycleStart);
            if (alloc is null)
            {
                alloc = new Learnexia.Modules.Billing.Domain.Entities.ChildEnergyAllocation
                {
                    FamilyEnergyAccountId = wallet.Id,
                    ChildId               = childId,
                    CycleStartUtc         = cycleStart,
                    CycleEndUtc           = cycleEnd,
                    AllocatedAmount       = seedBalance,
                    SpentAmount           = 0,
                };
                db.ChildEnergyAllocations.Add(alloc);
            }
            else
            {
                alloc.AllocatedAmount += seedBalance;
            }
            wallet.SubscriptionBalance += seedBalance;
            await db.SaveChangesAsync(0);
        }

        return (childId, childToken, parentToken);
    }

    /// <summary>
    /// Reads current total balance + daily-used + Spend-tx-count for a child from BillingDbContext.
    /// Same logic as EnergyEconomyTestFactory.ReadBalanceAsync.
    /// </summary>
    private static async Task<(int Total, int DailyUsed, int SpendTxCount)>
        ReadBalanceAsync(LexiNarrationTestFactory factory, int childId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var allocation = await db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync();

        int purchasedBalance = 0;
        if (allocation is not null)
        {
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == allocation.FamilyEnergyAccountId);
            purchasedBalance = wallet?.PurchasedBalance ?? 0;
        }
        var grantedRemaining = allocation?.Remaining ?? 0;
        var totalBalance     = grantedRemaining + purchasedBalance;

        var dailyUsage = await db.ChildDailyUsages.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ChildId == childId);
        var dailyUsed = dailyUsage?.DailyUsed ?? 0;

        var allocIds  = await db.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.ChildId == childId).Select(a => (int?)a.Id).ToListAsync();
        var walletIds = await db.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.ChildId == childId).Select(a => (int?)a.FamilyEnergyAccountId).ToListAsync();
        var txCount = await db.CreditTransactions.AsNoTracking()
            .CountAsync(t => t.Type == Learnexia.Modules.Billing.Domain.Enums.CreditTransactionType.Spend
                             && (allocIds.Contains(t.ChildEnergyAllocationId)
                                 || walletIds.Contains(t.FamilyEnergyAccountId)));

        return (totalBalance, dailyUsed, txCount);
    }

    /// <summary>
    /// Asserts the Spend ledger row has the expected reason code (AiRecommendation = 14).
    /// Reads CreditTransactions linked to the child and verifies at least one new row
    /// with ReasonCode = AiRecommendation appeared since <paramref name="txCountBefore"/>.
    /// </summary>
    private static async Task<bool> AssertAiRecommendationSpendRowAsync(
        LexiNarrationTestFactory factory,
        int childId,
        int txCountBefore,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            var allocIds  = await db.ChildEnergyAllocations.AsNoTracking()
                .Where(a => a.ChildId == childId).Select(a => (int?)a.Id).ToListAsync();
            var walletIds = await db.ChildEnergyAllocations.AsNoTracking()
                .Where(a => a.ChildId == childId).Select(a => (int?)a.FamilyEnergyAccountId).ToListAsync();

            var newReasonRows = await db.CreditTransactions.AsNoTracking()
                .CountAsync(t =>
                    t.Type       == Learnexia.Modules.Billing.Domain.Enums.CreditTransactionType.Spend &&
                    t.ReasonCode == Learnexia.Modules.Billing.Domain.Enums.CreditReasonCode.AiRecommendation &&
                    (allocIds.Contains(t.ChildEnergyAllocationId) || walletIds.Contains(t.FamilyEnergyAccountId)));

            if (newReasonRows > txCountBefore) return true;
            await Task.Delay(100);
        }
        return false;
    }

    /// <summary>
    /// Polls the ledger until balance drops by <paramref name="expectedDelta"/> or deadline.
    /// </summary>
    private static async Task<bool> PollForBalanceDeltaAsync(
        LexiNarrationTestFactory factory,
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

    /// <summary>
    /// Seeds a <see cref="StudentRecommendation"/> row directly in LearningDbContext
    /// for the given <paramref name="childId"/>, dated today (UTC).
    /// Uses a minimal set of <see cref="RecommendationItem"/> entries.
    /// </summary>
    private static async Task SeedRecommendationAsync(
        LexiNarrationTestFactory factory,
        int childId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Idempotent: if a recommendation for today already exists for this child, skip.
        var existing = await db.StudentRecommendations
            .FirstOrDefaultAsync(r => r.StudentId == childId && r.RecommendationDate == today);
        if (existing is not null) return;

        var items = new[]
        {
            new RecommendationItem(
                SkillId:          42,
                SubjectCode:      0,  // Math
                TitleKey:         "Rec_PracticeSkill_Title",
                BodyKey:          "Rec_PracticeSkill_Body",
                CtaKey:           "Rec_Practice_Cta",
                Severity:         2,  // Medium
                ActionType:       RecommendationActionType.Practice,
                TargetDifficulty: 2),
            new RecommendationItem(
                SkillId:          17,
                SubjectCode:      1,  // Science
                TitleKey:         "Rec_ReviewConcept_Title",
                BodyKey:          "Rec_ReviewConcept_Body",
                CtaKey:           "Rec_Review_Cta",
                Severity:         3,  // High
                ActionType:       RecommendationActionType.Review,
                TargetDifficulty: 1),
        };
        var itemsJson = System.Text.Json.JsonSerializer.Serialize(items,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        var row = new StudentRecommendation
        {
            StudentId          = childId,
            RecommendationDate = today,
            ItemsJson          = itemsJson,
            GeneratedAtUtc     = DateTime.UtcNow,
        };
        // FullAuditedEntity audit fields (CreatedAt / CreatedBy etc.) — SaveChangesAsync(userId)
        // stamps them automatically. Use 0 as system user id.
        db.StudentRecommendations.Add(row);
        await db.SaveChangesAsync(0);
    }

    private static async Task<(HttpResponseMessage Resp, string Body)>
        PostParentAsync(HttpClient client, string url, object body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    private static int GetSubjectIdFromJwt(string jwtToken)
    {
        var parts   = jwtToken.Split('.');
        var payload = parts[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var decoded = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json    = Encoding.UTF8.GetString(decoded);
        var doc     = JsonDocument.Parse(json).RootElement;

        if (doc.TryGetProperty("Id", out var idProp))
        {
            var raw = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt32().ToString()
                : idProp.GetString() ?? "";
            if (int.TryParse(raw, out var parsed)) return parsed;
        }
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

    // ==========================================================================
    // TC-1: HAPPY PATH + CHARGE
    // AC: Student with energy + persisted recommendation set
    //     → 200 SSE event:message delivered
    //     → balance debited by exactly 5
    //     → exactly one new Spend ledger row with CreditReasonCode.AiRecommendation
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-1 HappyCharge: recs present + sufficient energy → event:message + balance -5 + AiRecommendation Spend row")]
    public async Task TC1_HappyPath_RecsPresentSufficientEnergy_MessageDelivered_BalanceDebited5_SpendRowAiRecommendation()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.ResponseContent = "توصياتنا اليوم: تدرّب على الكسور وراجع العلوم!";

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc1");
        await SeedRecommendationAsync(factory, childId);

        var (balanceBefore, dailyBefore, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(50, "seed balance must be 50 before call");

        // Also count AiRecommendation spend rows before the call
        using var preSc = factory.Services.CreateScope();
        var preBillingDb = preSc.ServiceProvider.GetRequiredService<BillingDbContext>();
        var allocIds0 = await preBillingDb.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.ChildId == childId).Select(a => (int?)a.Id).ToListAsync();
        var walletIds0 = await preBillingDb.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.ChildId == childId).Select(a => (int?)a.FamilyEnergyAccountId).ToListAsync();
        var aiRecSpendBefore = await preBillingDb.CreditTransactions.AsNoTracking()
            .CountAsync(t =>
                t.Type       == Learnexia.Modules.Billing.Domain.Enums.CreditTransactionType.Spend &&
                t.ReasonCode == Learnexia.Modules.Billing.Domain.Enums.CreditReasonCode.AiRecommendation &&
                (allocIds0.Contains(t.ChildEnergyAllocationId) || walletIds0.Contains(t.FamilyEnergyAccountId)));

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        // ── HTTP status ──────────────────────────────────────────────────────────
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);

        // ── SSE wire ─────────────────────────────────────────────────────────────
        var frames = ParseSseFrames(body);
        frames.Should().Contain(f => f.Event == "message",
            "happy-path with recs must emit event:message; body={0}", body);
        frames.Should().Contain(f => f.Event == "done",
            "happy-path must emit event:done; body={0}", body);
        var doneFrame = frames.First(f => f.Event == "done");
        doneFrame.Data.Should().Be("[DONE]", "done data must be exactly [DONE]; body={0}", body);
        frames.Should().NotContain(f => f.Event == "error",
            "no error in happy-path; body={0}", body);
        frames.Should().NotContain(f => f.Event == "declined",
            "no declined in happy-path with recs; body={0}", body);

        // ── event:message content shape ───────────────────────────────────────────
        var msgFrame = frames.First(f => f.Event == "message");
        var msgJson  = JsonDocument.Parse(msgFrame.Data).RootElement;
        TryProp(msgJson, "content", out var contentProp).Should().BeTrue(
            "event:message data must have 'content' key; data={0}", msgFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace(
            "content must not be empty; data={0}", msgFrame.Data);

        // ── Energy debit: balance must drop by exactly 5 ─────────────────────────
        var debited = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostRecommendation, TimeSpan.FromSeconds(8));
        debited.Should().BeTrue(
            "MONEY DEFECT: balance MUST drop by exactly {0} (ai_cost.recommendation) within 8 s; " +
            "balanceBefore={1}. STOP — content was delivered without a debit.",
            CostRecommendation, balanceBefore);

        var (balanceAfter, dailyAfter, spendAfter) = await ReadBalanceAsync(factory, childId);

        (balanceBefore - balanceAfter).Should().Be(CostRecommendation,
            "MONEY DEFECT: balance delta must be exactly {0} (cost=recommendation=5); " +
            "before={1}, after={2}. STOP.",
            CostRecommendation, balanceBefore, balanceAfter);

        // ── Exactly one new Spend row ─────────────────────────────────────────────
        (spendAfter - spendBefore).Should().Be(1,
            "exactly one new Spend ledger row must appear; spendBefore={0}, spendAfter={1}",
            spendBefore, spendAfter);

        // ── Spend row has CreditReasonCode.AiRecommendation (= 14) ───────────────
        var aiRecRowFound = await AssertAiRecommendationSpendRowAsync(
            factory, childId, aiRecSpendBefore, TimeSpan.FromSeconds(5));
        aiRecRowFound.Should().BeTrue(
            "MONEY DEFECT: Spend row with CreditReasonCode.AiRecommendation (=14) must be written; " +
            "aiRecSpendBefore={0}. STOP.", aiRecSpendBefore);

        // ── DailyUsed incremented ────────────────────────────────────────────────
        (dailyAfter - dailyBefore).Should().Be(CostRecommendation,
            "DailyUsed must increment by {0}; before={1}, after={2}",
            CostRecommendation, dailyBefore, dailyAfter);
    }

    // ==========================================================================
    // TC-2: EMPTY RECOMMENDATIONS → DECLINED, NO DEBIT
    // AC: Student with sufficient energy but NO persisted recommendations
    //     → event:declined (reason=NoData)
    //     → balance UNCHANGED (no debit, no Spend row)
    //     → gateway NOT called (no LLM involved)
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-2 EmptyRecs_Declined_NoDebit: no recommendations → event:declined + balance unchanged")]
    public async Task TC2_EmptyRecs_Declined_BalanceUnchanged_GatewayNotCalled()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.Reset();

        // Student has energy but NO seeded recommendation row
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc2");
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        // Must emit event:declined
        frames.Should().Contain(f => f.Event == "declined",
            "no recs must produce event:declined; body={0}", body);

        // declined reason must be NoData
        var declinedFrame = frames.First(f => f.Event == "declined");
        var declinedJson  = JsonDocument.Parse(declinedFrame.Data).RootElement;
        TryProp(declinedJson, "reason", out var reasonProp).Should().BeTrue(
            "declined data must have 'reason'; data={0}", declinedFrame.Data);
        reasonProp.GetString().Should().Be("NoData", "reason must be 'NoData'; data={0}", declinedFrame.Data);

        // No content or error frames
        frames.Should().NotContain(f => f.Event == "message",
            "no content on declined; body={0}", body);
        frames.Should().NotContain(f => f.Event == "error",
            "no error frame on declined; body={0}", body);

        // Gateway must NOT be called (declined before LLM call)
        factory.FakeGateway.CallCount.Should().Be(0,
            "MONEY DEFECT: gateway must NOT be invoked when no recommendations exist; callCount={0}. STOP.",
            factory.FakeGateway.CallCount);

        // Wait to confirm no debit
        await Task.Delay(1500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);

        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: empty-recs decline MUST NOT debit; balanceBefore={0}, balanceAfter={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend ledger row on decline; spendBefore={0}, spendAfter={1}. STOP.",
            spendBefore, spendAfter);
    }

    // ==========================================================================
    // TC-3: INSUFFICIENT ENERGY → BLOCKED, NO DEBIT
    // AC: Student with 0 balance + persisted recs
    //     → event:error (insufficient energy)
    //     → balance UNCHANGED, no Spend row
    //     → gateway NOT called (pre-auth short-circuits)
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-3 InsufficientEnergy_Blocked_NoDebit: balance=0 → event:error + balance unchanged + gateway silent")]
    public async Task TC3_InsufficientEnergy_Blocked_NoDebit_GatewayNotCalled()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.Reset();

        // Student with ZERO balance
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 0, tag: "tc3");
        await SeedRecommendationAsync(factory, childId);

        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(0, "child must start with zero balance for pre-auth test");

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        // Must have event:error for insufficient balance
        frames.Should().Contain(f => f.Event == "error",
            "zero balance must produce event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content when pre-auth blocks; body={0}", body);
        frames.Should().NotContain(f => f.Event == "declined",
            "declined must NOT appear on pre-auth block (recs ARE present); body={0}", body);

        // Gateway NOT called (pre-auth short-circuits)
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

    // ==========================================================================
    // TC-4: SAFETY BLOCK → event:error, NO DEBIT
    // AC: Student with energy + recs, safety stub returns Blocked
    //     → event:error (safety blocked)
    //     → balance UNCHANGED
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-4 SafetyBlock_NoDebit: safety blocks narration → event:error + balance unchanged")]
    public async Task TC4_SafetyBlock_EventError_BalanceUnchanged()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Blocked);

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc4");
        await SeedRecommendationAsync(factory, childId);
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "SSE always 200; body: {0}", body);
        var frames = ParseSseFrames(body);

        frames.Should().Contain(f => f.Event == "error",
            "safety block must produce event:error; body={0}", body);
        frames.Should().NotContain(f => f.Event == "message",
            "no content on safety block; body={0}", body);

        // No event:done on error (per pinned wire contract)
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT be emitted after error (pinned wire contract); body={0}", body);

        // error frame must have code + message fields
        var errFrame = frames.First(f => f.Event == "error");
        var errJson  = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue("error data must have 'code'; data={0}", errFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue("error data must have 'message'; data={0}", errFrame.Data);
        codeProp.GetString().Should().NotBeNullOrWhiteSpace("code must not be empty; data={0}", errFrame.Data);
        msgProp.GetString().Should().NotBeNullOrWhiteSpace("message must not be empty; data={0}", errFrame.Data);
        body.Should().NotContain("StackTrace", "no raw stack traces in SSE; body={0}", body);

        // Wait to confirm no debit
        await Task.Delay(1500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: safety-blocked narration MUST NOT debit; balanceBefore={0}, balanceAfter={1}. STOP.",
            balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend row on safety block; spendBefore={0}, spendAfter={1}. STOP.",
            spendBefore, spendAfter);
    }

    // ==========================================================================
    // TC-5a: AUTH — Anonymous → 401
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-5a Auth_Anonymous_Returns401")]
    public async Task TC5a_Auth_Anonymous_Returns401()
    {
        using var factory = new LexiNarrationTestFactory(_fixture);
        var client = factory.CreateClient();

        // No bearer token
        var (resp, body) = await PostAsync(client, NarrateUrl, bearer: null);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "no bearer token → 401; body: {0}", body);
    }

    // ==========================================================================
    // TC-5b: AUTH — Parent JWT → 403 (Student-only endpoint)
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-5b Auth_ParentJwt_Returns403")]
    public async Task TC5b_Auth_ParentJwt_Returns403()
    {
        using var factory = new LexiNarrationTestFactory(_fixture);
        var client = factory.CreateClient();

        // Register a parent and get parent token
        var parentEmail = UniqueEmail("par5b");
        var (regResp, regBody) = await PostParentAsync(client, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass!", AcceptedTerms = true });
        regResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent reg; body: {0}", regBody);
        var regJson = JsonDocument.Parse(regBody).RootElement;
        TryProp(regJson, "data", out var regData);
        TryProp(regData, "accessToken", out var parentTok);
        var parentToken = parentTok.GetString()!;

        // Parent JWT → Student-only endpoint → 403
        var (resp, body) = await PostAsync(client, NarrateUrl, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Parent role → 403 on Student-only endpoint; body: {0}", body);
    }

    // ==========================================================================
    // TC-6: CACHE HIT STILL CHARGES
    // AC: Two identical Narrate calls on the same day (same recommendations → same cache key)
    //     → first call (MISS) → debits 5
    //     → second call (HIT) → gateway NOT called again → STILL debits 5
    //     → total debit = 10 after two calls
    //
    // Note: the recommendation cache key includes studentId + date + contentHash,
    //       so two requests by the same student with the same recommendations on the
    //       same UTC day → same cache key → HIT on the second request.
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-6 CacheHitCharges: 2nd identical narration (cache HIT) → gateway NOT called + STILL debits 5")]
    public async Task TC6_CacheHit_StillCharges_BalanceDebitedTwice()
    {
        // We need a factory where the REAL AiResponseCache (Postgres + Redis) is used so the
        // first request actually writes to cache.
        // LexiNarrationTestFactory stubs ISafetyLayer (Allowed, confidence=0.95) and uses the
        // REAL IAiResponseCache (not replaced — falls through to the real implementation).
        // autoApproveEnabled=true (configured) + confidence=0.95 > threshold=0.85 → Approved entry written.
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.ResponseContent = "توصياتنا اليوم: تدرّب على الكسور وراجع العلوم — cache test!";
        factory.FakeGateway.Reset();

        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "tc6");
        await SeedRecommendationAsync(factory, childId);
        var client = factory.CreateClient();

        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(30, "seed balance must be 30 before first call");

        // Snapshot the cache count BEFORE the first request so the polling delta is clean.
        int cacheCountBefore;
        using (var baseScope = factory.Services.CreateScope())
        {
            var aiDb0 = baseScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
            cacheCountBefore = await aiDb0.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.InvalidatedAt == null);
        }

        // ── First request (MISS) ─────────────────────────────────────────────────
        var (resp1, body1) = await PostAsync(client, NarrateUrl, childToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "1st request; body: {0}", body1);
        ParseSseFrames(body1).Should().Contain(f => f.Event == "message",
            "1st narration must deliver; body={0}", body1);

        // Wait for 1st debit to land
        var debit1Ok = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostRecommendation, TimeSpan.FromSeconds(8));
        debit1Ok.Should().BeTrue(
            "MONEY DEFECT: 1st debit (MISS) must land within 8 s; balanceBefore={0}. STOP.",
            balanceBefore);

        var (balanceAfterMiss, _, spendAfterMiss) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfterMiss).Should().Be(CostRecommendation,
            "balance delta after 1st (MISS) must be exactly {0}; before={1}, after={2}",
            CostRecommendation, balanceBefore, balanceAfterMiss);

        // Poll until the cache write for this entry appears in AiResponseCache (bounded).
        // The cache write is fire-and-forget on a fresh IServiceScopeFactory scope (DEFECT-3 fix pattern).
        // Each poll opens a fresh scope so EF does not return a stale snapshot.
        var cacheWriteDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        bool cacheEntryAppeared = false;
        while (DateTime.UtcNow < cacheWriteDeadline)
        {
            using var pollScope = factory.Services.CreateScope();
            var aiDb = pollScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
            var current = await aiDb.AiResponseCaches.AsNoTracking().CountAsync(r => r.InvalidatedAt == null);
            if (current > cacheCountBefore) { cacheEntryAppeared = true; break; }
            await Task.Delay(150);
        }
        cacheEntryAppeared.Should().BeTrue(
            "AiResponseCache must have a new entry within 8 s of the MISS request (DEFECT-3 pattern); cacheCountBefore={0}",
            cacheCountBefore);

        var gatewayCallsAfterMiss = factory.FakeGateway.CallCount;

        // ── Second identical request (HIT) ───────────────────────────────────────
        var (resp2, body2) = await PostAsync(client, NarrateUrl, childToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "2nd request; body: {0}", body2);
        ParseSseFrames(body2).Should().Contain(f => f.Event == "message",
            "cache HIT must still deliver content; body={0}", body2);

        // Gateway must NOT be called again on cache HIT
        factory.FakeGateway.CallCount.Should().Be(gatewayCallsAfterMiss,
            "MONEY DEFECT: cache HIT must NOT invoke gateway — call count must stay at {0}; actual={1}. STOP.",
            gatewayCallsAfterMiss, factory.FakeGateway.CallCount);

        // Poll until 2nd debit lands — HIT path MUST also charge (locked economy)
        var debit2Ok = await PollForBalanceDeltaAsync(factory, childId, balanceAfterMiss, CostRecommendation, TimeSpan.FromSeconds(8));
        debit2Ok.Should().BeTrue(
            "MONEY DEFECT: 2nd debit (HIT) must land — cache HIT MUST charge (locked energy economy invariant); STOP.");

        var (balanceAfterHit, _, spendAfterHit) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfterHit).Should().Be(2 * CostRecommendation,
            "two calls → two debits → total delta={0}; before={1}, after={2}",
            2 * CostRecommendation, balanceBefore, balanceAfterHit);
        (spendAfterHit - spendBefore).Should().Be(2,
            "two Spend ledger rows for two deliveries; spendBefore={0}, spendAfterHit={1}",
            spendBefore, spendAfterHit);
    }

    // ==========================================================================
    // TC-7: SSE WIRE CONTRACT — Content-Type + error shape
    // ==========================================================================

    [Fact(DisplayName = "P3-14-TC-7a SseWire_ContentType_TextEventStream")]
    public async Task TC7a_SseWire_ContentType_IsTextEventStream()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc7a");
        await SeedRecommendationAsync(factory, childId);

        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Post, NarrateUrl)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", childToken);
        var resp = await client.SendAsync(req);

        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must set Content-Type: text/event-stream");
    }

    [Fact(DisplayName = "P3-14-TC-7b SseWire_ErrorShape_CodeAndMessage_NoDoneAfterError")]
    public async Task TC7b_SseWire_ErrorShape_CodeAndMessage_NoDoneAfterError()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Blocked);
        var (childId, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc7b");
        await SeedRecommendationAsync(factory, childId);

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        var errFrame = frames.FirstOrDefault(f => f.Event == "error");
        errFrame.Should().NotBe(default, "safety block must produce event:error; body={0}", body);

        var errJson = JsonDocument.Parse(errFrame.Data).RootElement;
        TryProp(errJson, "code", out var codeProp).Should().BeTrue("error data must have 'code'; data={0}", errFrame.Data);
        TryProp(errJson, "message", out var msgProp).Should().BeTrue("error data must have 'message'; data={0}", errFrame.Data);
        codeProp.ValueKind.Should().Be(JsonValueKind.String);
        msgProp.ValueKind.Should().Be(JsonValueKind.String);

        // No event:done after error (pinned wire contract)
        frames.Should().NotContain(f => f.Event == "done",
            "event:done must NOT appear after error; body={0}", body);
        body.Should().NotContain("StackTrace",
            "no raw stack traces in SSE response; body={0}", body);
    }

    [Fact(DisplayName = "P3-14-TC-7c SseWire_DeclinedFrame_HasReasonField_ThenDone")]
    public async Task TC7c_SseWire_DeclinedFrame_HasReasonField_FollowedByDone()
    {
        using var factory = new LexiNarrationTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        // No recommendation seeded → should decline
        var (_, childToken, _) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc7c");

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        var declinedFrame = frames.FirstOrDefault(f => f.Event == "declined");
        declinedFrame.Should().NotBe(default, "no-recs path must emit event:declined; body={0}", body);

        var decJson = JsonDocument.Parse(declinedFrame.Data).RootElement;
        TryProp(decJson, "reason", out var reasonProp).Should().BeTrue("declined data must have 'reason'; data={0}", declinedFrame.Data);
        reasonProp.GetString().Should().Be("NoData", "reason must be 'NoData'; data={0}", declinedFrame.Data);

        // event:done MUST follow event:declined (per controller code)
        frames.Should().Contain(f => f.Event == "done",
            "event:done must follow event:declined; body={0}", body);
        var doneFrame = frames.First(f => f.Event == "done");
        doneFrame.Data.Should().Be("[DONE]", "done data must be exactly [DONE]; body={0}", body);
    }
}

// =============================================================================
// P3-14a — Lexi-framing enrichment: CurrentLevel in AiCacheKeyBuilder
// =============================================================================
//
// What changed in P3-14a:
//   The Recommendation narration cache key now includes CurrentLevel (and
//   encouragementStyleHint). AiCacheKeyBuilder.ForRecommendation gains `lv` and
//   `es` dimensions. A level-up must produce a cache MISS (fresh narration) and
//   a fresh debit. A same-level second call remains a HIT that still debits.
//
// Strategy for TC-P3-14a-1 (level-up → MISS):
//   Register a ControlledStudentXpQuery whose returned CurrentLevel is mutated
//   between the first and second Narrate calls. This bypasses the Redis TTL of
//   CachedStudentXpQuery and directly drives the handler's `currentLevel` variable.
//   Seam contract: IStudentXpQuery registered with TryAddScoped in the Ai module;
//   overriding it in tests is the documented extension point (DefaultStudentXpQuery
//   is the fallback stub). The LexiNarrationLevelTestFactory replaces the seam.
//
// Assertions that define "P3-14a headline":
//   Level-up → gateway CallCount increases by at least 1 on the second call.
//   Level-up → balance drops by another 5 (not 0 / stale-free-serve).
//   Same-level → gateway NOT called again on the second call (cache HIT).
//   Same-level → balance still drops by 5 (HIT charges — locked economy).
//
// Running:
//   dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj
//             --filter "FullyQualifiedName~P3_14a"

/// <summary>
/// Controllable <see cref="IStudentXpQuery"/> stub.
/// Returns <see cref="CurrentLevel"/> wrapped in a <see cref="StudentXpSnapshot"/>.
/// Mutable between calls so a single test can simulate a level-up between two Narrate requests.
/// </summary>
public sealed class ControlledStudentXpQuery : IStudentXpQuery
{
    /// <summary>The level returned by the next call to GetByStudentIdAsync.</summary>
    public int CurrentLevel { get; set; } = 1;

    public Task<StudentXpSnapshot?> GetByStudentIdAsync(int studentId, CancellationToken ct = default)
        => Task.FromResult<StudentXpSnapshot?>(new StudentXpSnapshot(studentId, TotalXp: 0, CurrentLevel: CurrentLevel));
}

/// <summary>
/// WebApplicationFactory for P3-14a level-enrichment tests.
///
/// Key difference from <see cref="LexiNarrationTestFactory"/>: registers a
/// <see cref="ControlledStudentXpQuery"/> as <see cref="IStudentXpQuery"/>
/// (replacing both the DefaultStudentXpQuery stub AND the real CachedStudentXpQuery)
/// so tests can mutate the returned level between requests.
/// </summary>
public sealed class LexiNarrationLevelTestFactory : WebApplicationFactory<Program>
{
    private readonly string _pgConn;
    private readonly string _redisConn;
    private readonly StubSafetyLayer.Mode _safetyMode;

    public readonly FakeCountingGateway FakeGateway = new();

    /// <summary>Mutate this between calls to simulate a level-up.</summary>
    public readonly ControlledStudentXpQuery LevelQuery = new();

    public LexiNarrationLevelTestFactory(
        AiRuntimeFixture fixture,
        StubSafetyLayer.Mode safetyMode = StubSafetyLayer.Mode.Allowed)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        _pgConn     = fixture.PostgresConnectionString;
        _redisConn  = fixture.RedisConnectionString;
        _safetyMode = safetyMode;
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
                ["Billing:HardStopEnabled"]               = "false",
            }));

        builder.ConfigureServices(services =>
        {
            // ── All module DbContexts → Testcontainers Postgres ─────────────────
            AiRuntimeTestFactory.ReplaceDbContexts(services, _pgConn);

            // ── BillingDbContext (not covered by ReplaceDbContexts above) ────────
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<BillingDbContext>();
            services.AddDbContext<BillingDbContext>(options =>
                options.UseNpgsql(
                    _pgConn,
                    npgsql => npgsql
                        .MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)
                        .MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName)));

            // ── IAiGateway → FakeCountingGateway (offline) ──────────────────────
            services.RemoveAll<IAiGateway>();
            services.AddScoped<IAiGateway>(_ => FakeGateway);

            // ── ISafetyLayer → per-test stub ────────────────────────────────────
            services.RemoveAll<ISafetyLayer>();
            services.AddScoped<ISafetyLayer>(_ =>
                new StubSafetyLayer { Behavior = _safetyMode });

            // ── IStudentXpQuery → ControlledStudentXpQuery (P3-14a seam) ────────
            // Replace both the TryAdd stub AND any real CachedStudentXpQuery
            // so the test fully controls CurrentLevel between requests.
            services.RemoveAll<IStudentXpQuery>();
            services.AddScoped<IStudentXpQuery>(_ => LevelQuery);

            // ── Disable IP rate limiter ──────────────────────────────────────────
            AiRuntimeTestFactory.DisableIpRateLimit(services);
        });
    }
}

/// <summary>
/// P3-14a integration tests — Lexi-framing enrichment: CurrentLevel in the narration cache key.
///
/// Test areas:
///   TC-P3-14a-1  Level-up → MISS + fresh charge (headline test)
///   TC-P3-14a-2  Same level → cache HIT still charges (regression of locked economy)
///   TC-P3-14a-3a Delivery still works with enriched prompt (SSE wire contract)
///   TC-P3-14a-3b No-delivery (empty recs) still does NOT debit
///   TC-P3-14a-4  Unit-level: AiCacheKeyBuilder.ForRecommendation key differs on level change
/// </summary>
[Collection("AiRuntimeE2E")]
public sealed class P3_14a_LexiNarrationLevelEnrichment_E2E_Tests : IAsyncLifetime
{
    private const string NarrateUrl         = "api/AiTutor/Recommendation/Narrate";
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string ValidChildPassword = "Child@Pass1";
    private const int CostRecommendation    = 5;

    private readonly AiRuntimeFixture _fixture;
    private bool _migrationsApplied;

    public P3_14a_LexiNarrationLevelEnrichment_E2E_Tests(AiRuntimeFixture fixture)
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

    // ── Private helpers (mirror the P3-14 base test helpers) ──────────────────

    private static string UniqueEmail(string tag)
        => $"p314a_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@test.local";

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
        PostAsync(HttpClient client, string url, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpResponseMessage Resp, string Body)>
        PostJsonAsync(HttpClient client, string url, object body, string? bearer = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        var resp = await client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

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

    private async Task<(int ChildId, string ChildToken)>
        CreateStudentWithBalanceAsync(LexiNarrationLevelTestFactory factory, int seedBalance, string tag = "")
    {
        var client = factory.CreateClient();
        var parentEmail = UniqueEmail($"par{tag}");
        var (prResp, prBody) = await PostJsonAsync(client, RegisterParentUrl,
            new { Email = parentEmail, Password = "Str0ng@Pass!", AcceptedTerms = true });
        prResp.StatusCode.Should().Be(HttpStatusCode.OK, "parent reg; body: {0}", prBody);
        var prJson = JsonDocument.Parse(prBody).RootElement;
        TryProp(prJson, "data", out var prData);
        TryProp(prData, "accessToken", out var prTok);
        var parentToken = prTok.GetString()!;

        var childEmail = UniqueEmail($"child{tag}");
        var (addResp, addBody) = await PostJsonAsync(client, AddChildUrl,
            new
            {
                FullName         = "P314a Level Test Student",
                Email            = childEmail,
                Password         = ValidChildPassword,
                Grade            = 4,
                Language         = "ar",
                Country          = "EG",
                LearningLanguage = "ar",
            }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf(new[] { 200, 201 }, "Add-Child; body: {0}", addBody);

        var (signResp, signBody) = await PostJsonAsync(client, SignInUrl,
            new { UserName = childEmail, Password = ValidChildPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body: {0}", signBody);
        var signJson  = JsonDocument.Parse(signBody).RootElement;
        TryProp(signJson, "data", out var signData);
        TryProp(signData, "accessToken", out var accessTok);
        var childToken = accessTok.GetString()!;
        var childId    = GetSubjectIdFromJwt(childToken);

        if (seedBalance > 0)
        {
            using var scope = factory.Services.CreateScope();
            var db          = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var syntheticParentId = -(childId);
            var wallet = await db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.ParentUserId == syntheticParentId);
            if (wallet is null)
            {
                wallet = Learnexia.Modules.Billing.Domain.Entities.FamilyEnergyAccount.CreateEmpty(syntheticParentId);
                db.FamilyEnergyAccounts.Add(wallet);
                await db.SaveChangesAsync(0);
            }
            var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
            var alloc = await db.ChildEnergyAllocations.FirstOrDefaultAsync(
                a => a.FamilyEnergyAccountId == wallet.Id && a.ChildId == childId && a.CycleStartUtc == cycleStart);
            if (alloc is null)
            {
                alloc = new Learnexia.Modules.Billing.Domain.Entities.ChildEnergyAllocation
                {
                    FamilyEnergyAccountId = wallet.Id,
                    ChildId               = childId,
                    CycleStartUtc         = cycleStart,
                    CycleEndUtc           = cycleEnd,
                    AllocatedAmount       = seedBalance,
                    SpentAmount           = 0,
                };
                db.ChildEnergyAllocations.Add(alloc);
            }
            else
            {
                alloc.AllocatedAmount += seedBalance;
            }
            wallet.SubscriptionBalance += seedBalance;
            await db.SaveChangesAsync(0);
        }
        return (childId, childToken);
    }

    private static async Task<(int Total, int DailyUsed, int SpendTxCount)>
        ReadBalanceAsync(LexiNarrationLevelTestFactory factory, int childId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var allocation = await db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync();
        int purchasedBalance = 0;
        if (allocation is not null)
        {
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == allocation.FamilyEnergyAccountId);
            purchasedBalance = wallet?.PurchasedBalance ?? 0;
        }
        var grantedRemaining = allocation?.Remaining ?? 0;
        var totalBalance     = grantedRemaining + purchasedBalance;
        var dailyUsage = await db.ChildDailyUsages.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ChildId == childId);
        var dailyUsed = dailyUsage?.DailyUsed ?? 0;
        var allocIds  = await db.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.ChildId == childId).Select(a => (int?)a.Id).ToListAsync();
        var walletIds = await db.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.ChildId == childId).Select(a => (int?)a.FamilyEnergyAccountId).ToListAsync();
        var txCount = await db.CreditTransactions.AsNoTracking()
            .CountAsync(t => t.Type == Learnexia.Modules.Billing.Domain.Enums.CreditTransactionType.Spend
                             && (allocIds.Contains(t.ChildEnergyAllocationId)
                                 || walletIds.Contains(t.FamilyEnergyAccountId)));
        return (totalBalance, dailyUsed, txCount);
    }

    private static async Task<bool> PollForBalanceDeltaAsync(
        LexiNarrationLevelTestFactory factory, int childId, int balanceBefore, int expectedDelta, TimeSpan timeout)
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

    private static async Task SeedRecommendationAsync(LexiNarrationLevelTestFactory factory, int childId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await db.StudentRecommendations
            .FirstOrDefaultAsync(r => r.StudentId == childId && r.RecommendationDate == today);
        if (existing is not null) return;

        var items = new[]
        {
            new RecommendationItem(
                SkillId:          42,
                SubjectCode:      0,
                TitleKey:         "Rec_PracticeSkill_Title",
                BodyKey:          "Rec_PracticeSkill_Body",
                CtaKey:           "Rec_Practice_Cta",
                Severity:         2,
                ActionType:       RecommendationActionType.Practice,
                TargetDifficulty: 2),
        };
        var itemsJson = System.Text.Json.JsonSerializer.Serialize(items,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var row = new StudentRecommendation
        {
            StudentId          = childId,
            RecommendationDate = today,
            ItemsJson          = itemsJson,
            GeneratedAtUtc     = DateTime.UtcNow,
        };
        db.StudentRecommendations.Add(row);
        await db.SaveChangesAsync(0);
    }

    private static int GetSubjectIdFromJwt(string jwtToken)
    {
        var parts   = jwtToken.Split('.');
        var payload = parts[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var decoded = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json    = Encoding.UTF8.GetString(decoded);
        var doc     = JsonDocument.Parse(json).RootElement;
        if (doc.TryGetProperty("Id", out var idProp))
        {
            var raw = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt32().ToString()
                : idProp.GetString() ?? "";
            if (int.TryParse(raw, out var parsed)) return parsed;
        }
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

    // ==========================================================================
    // TC-P3-14a-4  Unit-level: AiCacheKeyBuilder key changes when level changes
    //
    // This is a pure unit assertion — no HTTP call needed. It proves that the P3-14a
    // lv dimension is actually included in the key tuple so a level change produces a
    // different hash. This is the foundational correctness check for TC-P3-14a-1.
    // ==========================================================================

    [Fact(DisplayName = "P3-14a-TC-4 AiCacheKeyBuilder.ForRecommendation key differs on CurrentLevel change (unit)")]
    public void TC4_CacheKeyBuilder_LevelChangeProducesDifferentKey()
    {
        var date        = DateOnly.FromDateTime(DateTime.UtcNow);
        var contentHash = "abc123";
        var grade       = 4;
        var lang        = TutorLanguage.Ar;
        var pv          = "v1";
        var cv          = "mvp";
        var studentId   = 99999;

        var keyLevel3 = AiCacheKeyBuilder.ForRecommendation(
            studentId, date, contentHash, grade, lang, pv, cv, currentLevel: 3);

        var keyLevel5 = AiCacheKeyBuilder.ForRecommendation(
            studentId, date, contentHash, grade, lang, pv, cv, currentLevel: 5);

        keyLevel3.Should().NotBe(keyLevel5,
            "DEFECT (P3-14a OQ-7): CurrentLevel={0} and CurrentLevel={1} must produce DIFFERENT cache keys " +
            "so a level-up yields a fresh narration — stale level-{0} narration must never be served for free " +
            "to a level-{1} student. key_L3={2}, key_L5={3}",
            3, 5, keyLevel3, keyLevel5);

        // Sanity: same level produces the same key (determinism).
        var keyLevel3Again = AiCacheKeyBuilder.ForRecommendation(
            studentId, date, contentHash, grade, lang, pv, cv, currentLevel: 3);
        keyLevel3.Should().Be(keyLevel3Again,
            "same inputs must always produce the same cache key (determinism)");
    }

    // ==========================================================================
    // TC-P3-14a-1  HEADLINE — Level-up yields a fresh (MISS) narration + fresh debit
    //
    // Scenario:
    //   1. ControlledStudentXpQuery returns level 3.
    //   2. POST Narrate → MISS (no cache entry for level-3 key); cache written; debit -5.
    //   3. ControlledStudentXpQuery raised to level 5.
    //      (Level 5 is in the same age-band as level 3 (both band2, grade 4)
    //       but the `lv` field itself differs so AiCacheKeyBuilder produces a DIFFERENT key.)
    //   4. POST Narrate → MISS (no cache entry for level-5 key); second cache entry written;
    //      balance debited -5 again. Total debit = -10.
    //
    // MISS indicator: new AiResponseCache row appears (StubSafetyLayer bypasses the real
    // IAiGateway, so FakeGateway.CallCount is NOT a reliable MISS indicator — cache count is).
    //
    // STOP criteria: if balance drops by 0 on the second call (stale free serve on level-up),
    // that is a real P3-14a defect (OQ-7 regression) — report it, do NOT flag as infra.
    // ==========================================================================

    [Fact(DisplayName = "P3-14a-TC-1 HEADLINE: level-up → cache MISS → new cache entry written → balance -5 again (no stale free serve)")]
    public async Task TC1_LevelUp_ProducesMiss_FreshCacheEntry_FreshCharge_NotStaleFreeServe()
    {
        using var factory = new LexiNarrationLevelTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.ResponseContent = "استعد لمستوى جديد! توصياتنا: تدرّب على الكسور!";

        // ── Snapshot global cache count BEFORE the test (shared DB with all E2E tests) ──
        int cacheCountBase;
        using (var baseScope = factory.Services.CreateScope())
        {
            var aiDb0 = baseScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
            cacheCountBase = await aiDb0.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.InvalidatedAt == null);
        }

        // Set level 3 for the first narration.
        factory.LevelQuery.CurrentLevel = 3;

        var (childId, childToken) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc1a");
        await SeedRecommendationAsync(factory, childId);

        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(50, "seed balance must be 50 before first call");

        var client = factory.CreateClient();

        // ── First call: level 3 → MISS ───────────────────────────────────────────
        var (resp1, body1) = await PostAsync(client, NarrateUrl, childToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "1st request; body: {0}", body1);
        var frames1 = ParseSseFrames(body1);
        frames1.Should().Contain(f => f.Event == "message",
            "1st narration at level 3 must deliver; body={0}", body1);
        frames1.Should().NotContain(f => f.Event == "error",
            "no error on 1st narration; body={0}", body1);

        // Poll until debit lands.
        var debit1Ok = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostRecommendation, TimeSpan.FromSeconds(8));
        debit1Ok.Should().BeTrue(
            "MONEY DEFECT: 1st debit (level 3 MISS) must land within 8 s; balanceBefore={0}. STOP.", balanceBefore);

        var (balanceAfterFirstCall, _, spendAfterFirstCall) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfterFirstCall).Should().Be(CostRecommendation,
            "balance delta after 1st (level 3 MISS) must be exactly {0}; before={1}, after={2}",
            CostRecommendation, balanceBefore, balanceAfterFirstCall);

        // Poll until the AiResponseCache MISS write completes (fire-and-forget — DEFECT-3 fix).
        int cacheCountAfterLevel3Call = 0;
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            bool appeared = false;
            while (DateTime.UtcNow < deadline)
            {
                using var pollScope = factory.Services.CreateScope();
                var aiDb = pollScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
                var cur = await aiDb.AiResponseCaches.AsNoTracking().CountAsync(r => r.InvalidatedAt == null);
                if (cur > cacheCountBase) { appeared = true; cacheCountAfterLevel3Call = cur; break; }
                await Task.Delay(150);
            }
            appeared.Should().BeTrue(
                "AiResponseCache must have at least one new entry within 8 s of the level-3 MISS request; " +
                "cacheCountBase={0}. If missing, the MISS path did not write a cache row.", cacheCountBase);
        }

        // ── LEVEL-UP: raise to level 5 ──────────────────────────────────────────
        // Mutate the ControlledStudentXpQuery — the NEXT request scope will call
        // GetByStudentIdAsync and receive level 5.
        factory.LevelQuery.CurrentLevel = 5;

        // ── Second call: level 5 → MUST be a MISS (different `lv` in cache key) ─
        var (resp2, body2) = await PostAsync(client, NarrateUrl, childToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "2nd request after level-up; body: {0}", body2);
        var frames2 = ParseSseFrames(body2);
        frames2.Should().Contain(f => f.Event == "message",
            "2nd narration after level-up must deliver (fresh generation, not stale); body={0}", body2);
        frames2.Should().NotContain(f => f.Event == "error",
            "no error on 2nd narration after level-up; body={0}", body2);

        // Balance must drop by ANOTHER 5 (fresh delivery → fresh debit).
        // This is the headline invariant: stale free serve would leave balance unchanged.
        var debit2Ok = await PollForBalanceDeltaAsync(factory, childId, balanceAfterFirstCall, CostRecommendation, TimeSpan.FromSeconds(8));
        debit2Ok.Should().BeTrue(
            "MONEY DEFECT (P3-14a OQ-7 headline): level-up narration MUST debit again (fresh delivery = fresh charge). " +
            "If balance did NOT drop, the stale level-3 narration was served for FREE — that is a defect. " +
            "balanceAfterFirstCall={0}. STOP.",
            balanceAfterFirstCall);

        var (balanceAfterSecondCall, _, spendAfterSecondCall) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfterSecondCall).Should().Be(2 * CostRecommendation,
            "MONEY DEFECT: two separate MISS deliveries (level-3 + level-5) must debit 2x{0}={1}; " +
            "balanceBefore={2}, balanceAfterSecondCall={3}. STOP.",
            CostRecommendation, 2 * CostRecommendation, balanceBefore, balanceAfterSecondCall);

        (spendAfterSecondCall - spendBefore).Should().Be(2,
            "two separate MISS deliveries must produce 2 Spend ledger rows; spendBefore={0}, spendAfterSecondCall={1}",
            spendBefore, spendAfterSecondCall);

        // A second AiResponseCache entry MUST appear for the level-5 key (proves it was a MISS,
        // not a HIT on the level-3 key — which would have been a stale free serve).
        {
            var deadline2 = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            bool secondEntryAppeared = false;
            while (DateTime.UtcNow < deadline2)
            {
                using var pollScope = factory.Services.CreateScope();
                var aiDb = pollScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
                var cur = await aiDb.AiResponseCaches.AsNoTracking().CountAsync(r => r.InvalidatedAt == null);
                if (cur > cacheCountAfterLevel3Call) { secondEntryAppeared = true; break; }
                await Task.Delay(150);
            }
            secondEntryAppeared.Should().BeTrue(
                "DEFECT (P3-14a OQ-7): AiResponseCache must have a SECOND new entry within 8 s of the level-5 " +
                "MISS request — proves the level-5 key is different from the level-3 key. " +
                "cacheCountAfterLevel3Call={0}. If no second entry, the level-5 key collided with level-3 " +
                "and a STALE entry was served.", cacheCountAfterLevel3Call);
        }
    }

    // ==========================================================================
    // TC-P3-14a-2  REGRESSION — Same level → cache HIT still charges
    //
    // Verifies that the base P3-14 TC-6 locked-economy invariant is NOT broken by P3-14a:
    // two Narrate calls at the SAME level → second is a HIT → NO new cache row written
    // → BUT balance still drops by 5 (HIT charges — locked economy unchanged by P3-14a).
    //
    // Cache HIT indicator: AiResponseCache row count does NOT increase on the second call.
    // (StubSafetyLayer bypasses IAiGateway, so FakeGateway.CallCount is always 0 and is
    //  not a valid HIT/MISS discriminator in this integration harness.)
    // ==========================================================================

    [Fact(DisplayName = "P3-14a-TC-2 REGRESSION: same level → 2nd call cache HIT → no new cache row → STILL debits 5 (locked economy)")]
    public async Task TC2_SameLevel_SecondCall_CacheHit_StillCharges()
    {
        using var factory = new LexiNarrationLevelTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.ResponseContent = "التوصيات لليوم: كرّر الكسور والعلوم! cache-regression-test";

        // Fixed level — does not change between calls.
        factory.LevelQuery.CurrentLevel = 7;

        var (childId, childToken) = await CreateStudentWithBalanceAsync(factory, seedBalance: 30, tag: "tc2a");
        await SeedRecommendationAsync(factory, childId);
        var client = factory.CreateClient();

        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);
        balanceBefore.Should().Be(30, "seed balance must be 30 before first call");

        // Snapshot cache count before first request.
        int cacheCountBefore;
        using (var initScope = factory.Services.CreateScope())
        {
            var aiDb0 = initScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
            cacheCountBefore = await aiDb0.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.InvalidatedAt == null);
        }

        // ── First call (MISS) ────────────────────────────────────────────────────
        var (resp1, body1) = await PostAsync(client, NarrateUrl, childToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "1st request; body: {0}", body1);
        ParseSseFrames(body1).Should().Contain(f => f.Event == "message",
            "1st narration must deliver; body={0}", body1);

        var debit1Ok = await PollForBalanceDeltaAsync(factory, childId, balanceBefore, CostRecommendation, TimeSpan.FromSeconds(8));
        debit1Ok.Should().BeTrue(
            "MONEY DEFECT: 1st debit (same-level MISS) must land within 8 s. STOP.", 0);

        var (balanceAfterMiss, _, _) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfterMiss).Should().Be(CostRecommendation,
            "balance delta after MISS must be exactly {0}; before={1}, after={2}",
            CostRecommendation, balanceBefore, balanceAfterMiss);

        // Poll until the MISS cache entry appears (proves the 1st call went through the MISS path).
        int cacheCountAfterMiss = 0;
        {
            var cacheDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
            bool cacheEntryAppeared = false;
            while (DateTime.UtcNow < cacheDeadline)
            {
                using var pollScope = factory.Services.CreateScope();
                var aiDb = pollScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
                var cur = await aiDb.AiResponseCaches.AsNoTracking().CountAsync(r => r.InvalidatedAt == null);
                if (cur > cacheCountBefore) { cacheEntryAppeared = true; cacheCountAfterMiss = cur; break; }
                await Task.Delay(150);
            }
            cacheEntryAppeared.Should().BeTrue(
                "AiResponseCache must have a new entry within 8 s of the 1st (MISS) request; cacheCountBefore={0}",
                cacheCountBefore);
        }

        // ── Second call (HIT — same level, same rec set, same day) ───────────────
        // Level remains 7 (unchanged). Same cache key → HIT.
        var (resp2, body2) = await PostAsync(client, NarrateUrl, childToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "2nd request; body: {0}", body2);
        ParseSseFrames(body2).Should().Contain(f => f.Event == "message",
            "cache HIT must still deliver content; body={0}", body2);

        // HIT must STILL charge (locked economy invariant — P3-14a must not break this).
        var debit2Ok = await PollForBalanceDeltaAsync(factory, childId, balanceAfterMiss, CostRecommendation, TimeSpan.FromSeconds(8));
        debit2Ok.Should().BeTrue(
            "MONEY DEFECT (locked economy regression): cache HIT at same level MUST charge 5 (P3-14a must not " +
            "break the base TC-6 locked-economy invariant). STOP.");

        var (balanceAfterHit, _, spendAfterHit) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfterHit).Should().Be(2 * CostRecommendation,
            "two calls at same level → total debit = {0}; balanceBefore={1}, balanceAfterHit={2}",
            2 * CostRecommendation, balanceBefore, balanceAfterHit);
        (spendAfterHit - spendBefore).Should().Be(2,
            "two Spend ledger rows after two deliveries at same level; spendBefore={0}, spendAfterHit={1}",
            spendBefore, spendAfterHit);

        // HIT must NOT produce a new cache row (the same key's entry is reused).
        // Brief wait then snapshot — a HIT path never writes a new row.
        await Task.Delay(1000);
        int cacheCountAfterHit;
        using (var finalScope = factory.Services.CreateScope())
        {
            var aiDb = finalScope.ServiceProvider.GetRequiredService<Learnexia.Modules.Ai.Infrastructure.Persistence.AiDbContext>();
            cacheCountAfterHit = await aiDb.AiResponseCaches.AsNoTracking()
                .CountAsync(r => r.InvalidatedAt == null);
        }
        cacheCountAfterHit.Should().Be(cacheCountAfterMiss,
            "REGRESSION (P3-14a): same-level 2nd call must be a HIT — no new AiResponseCache row; " +
            "if count grew from {0} to {1}, the cache key changed on same inputs (P3-14a broke key stability).",
            cacheCountAfterMiss, cacheCountAfterHit);
    }

    // ==========================================================================
    // TC-P3-14a-3a  Delivery still works with enriched prompt (SSE wire contract)
    //
    // Spot-check: with P3-14a live, POST Narrate still returns the correct SSE
    // wire contract (event:message + event:done) when recs are present.
    // Ensures the prompt enrichment (framing + style fragments) did not break generation.
    // ==========================================================================

    [Fact(DisplayName = "P3-14a-TC-3a Delivery still works: enriched prompt emits event:message + event:done (SSE contract unchanged)")]
    public async Task TC3a_EnrichedPrompt_StillDeliversSseWireContract()
    {
        using var factory = new LexiNarrationLevelTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.ResponseContent = "مرحبًا يا بطل! هذه توصياتنا: تدرّب على الكسور!";
        factory.LevelQuery.CurrentLevel = 10;  // Some level — verifies framing doesn't break SSE

        var (childId, childToken) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc3a");
        await SeedRecommendationAsync(factory, childId);

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        // HTTP 200 (SSE always 200).
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream",
            "SSE endpoint must return Content-Type: text/event-stream");

        var frames = ParseSseFrames(body);

        // Must deliver event:message (content present).
        frames.Should().Contain(f => f.Event == "message",
            "enriched-prompt delivery must emit event:message; body={0}", body);

        // event:message data must have 'content' key.
        var msgFrame = frames.First(f => f.Event == "message");
        var msgJson  = JsonDocument.Parse(msgFrame.Data).RootElement;
        TryProp(msgJson, "content", out var contentProp).Should().BeTrue(
            "event:message data must have 'content' key; data={0}", msgFrame.Data);
        contentProp.GetString().Should().NotBeNullOrWhiteSpace(
            "content must not be empty; data={0}", msgFrame.Data);

        // Must emit event:done after event:message.
        frames.Should().Contain(f => f.Event == "done",
            "delivery must be followed by event:done; body={0}", body);
        frames.First(f => f.Event == "done").Data.Should().Be("[DONE]",
            "done data must be exactly [DONE]; body={0}", body);

        // No error or declined frames.
        frames.Should().NotContain(f => f.Event == "error",
            "no error in happy-path; body={0}", body);
        frames.Should().NotContain(f => f.Event == "declined",
            "no declined when recs present; body={0}", body);

        // No raw stack traces.
        body.Should().NotContain("StackTrace", "no stack traces in SSE; body={0}", body);
    }

    // ==========================================================================
    // TC-P3-14a-3b  No-delivery path still does NOT debit
    //
    // Spot-check: empty recommendations → event:declined → NO debit (unchanged from P3-14).
    // ==========================================================================

    [Fact(DisplayName = "P3-14a-TC-3b No-delivery path unchanged: empty recs → event:declined + NO debit")]
    public async Task TC3b_EmptyRecs_StillDeclined_NoDebit()
    {
        using var factory = new LexiNarrationLevelTestFactory(_fixture, StubSafetyLayer.Mode.Allowed);
        factory.FakeGateway.Reset();
        factory.LevelQuery.CurrentLevel = 2;  // Level set but no recs → must decline before LLM

        // Student with energy but NO recommendation row seeded.
        var (childId, childToken) = await CreateStudentWithBalanceAsync(factory, seedBalance: 50, tag: "tc3b");
        var (balanceBefore, _, spendBefore) = await ReadBalanceAsync(factory, childId);

        var (resp, body) = await PostAsync(factory.CreateClient(), NarrateUrl, childToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body: {0}", body);
        var frames = ParseSseFrames(body);

        frames.Should().Contain(f => f.Event == "declined",
            "no recs must produce event:declined even with P3-14a enrichment; body={0}", body);
        var declinedFrame = frames.First(f => f.Event == "declined");
        var decJson = JsonDocument.Parse(declinedFrame.Data).RootElement;
        TryProp(decJson, "reason", out var reasonProp).Should().BeTrue(
            "declined data must have 'reason'; data={0}", declinedFrame.Data);
        reasonProp.GetString().Should().Be("NoData",
            "reason must be 'NoData'; data={0}", declinedFrame.Data);

        frames.Should().NotContain(f => f.Event == "message",
            "no content on declined; body={0}", body);

        // Gateway NOT called (declined before LLM query).
        factory.FakeGateway.CallCount.Should().Be(0,
            "MONEY DEFECT: gateway must NOT be invoked on empty-recs decline; callCount={0}. STOP.",
            factory.FakeGateway.CallCount);

        // Wait and confirm no debit.
        await Task.Delay(1500);
        var (balanceAfter, _, spendAfter) = await ReadBalanceAsync(factory, childId);
        (balanceBefore - balanceAfter).Should().Be(0,
            "MONEY DEFECT: empty-recs decline MUST NOT debit even with P3-14a enrichment; " +
            "balanceBefore={0}, balanceAfter={1}. STOP.", balanceBefore, balanceAfter);
        (spendAfter - spendBefore).Should().Be(0,
            "MONEY DEFECT: no Spend ledger row on decline; spendBefore={0}, spendAfter={1}. STOP.",
            spendBefore, spendAfter);
    }
}

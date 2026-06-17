// ReSharper disable InconsistentNaming
// P10-13 Family Energy Wallet — Integration Tests
//
// Tests in this file exercise the running backend API (HTTP + service layer) for all
// P10-13 acceptance criteria using WebApplicationFactory<Program> + the shared
// Testcontainers Postgres container (LearnexiaWebAppFactory / [Collection("IntegrationTests")]).
//
// Coverage map:
//   BE-TC-WAL-01  Wallet overview – parent JWT required (401 without token)
//   BE-TC-WAL-02  Wallet overview – IDOR: another parent cannot see a different family's wallet
//   BE-TC-WAL-03  Wallet overview – 200 + BaseResponse envelope after wallet is provisioned
//   BE-TC-WAL-04  Wallet overview – 404 when no wallet exists yet for the parent
//   BE-TC-GRANT-01 Monthly grant: PlanEnergyPerSeat × ActivePaidSeats → SubscriptionBalance
//   BE-TC-GRANT-02 Equal split: allocated sum == grant exactly; deterministic remainder
//   BE-TC-GRANT-03 Per-child ChildEnergyAllocation rows created and Remaining = Allocated
//   BE-TC-GRANT-04 Idempotency: same (parentUserId, cycleStart) is a no-op (no double-grant)
//   BE-TC-GRANT-05 NoChildren: grant with zero active children → nothing allocated; no wallet created
//   BE-TC-SPEND-01 Spend debits child's ChildEnergyAllocation row FIRST
//   BE-TC-SPEND-02 Spend: child allocation covers full cost → shared PurchasedBalance UNTOUCHED
//   BE-TC-SPEND-03 Spend shortfall: allocation covers partial → shortfall drawn from PurchasedBalance
//   BE-TC-SPEND-04 Spend: never-negative — insufficient balance → no DB write (Charged=false)
//   BE-TC-SPEND-05 ChildDailyUsage row is created and incremented on each successful spend
//   BE-TC-SPEND-06 Daily soft cap: DailyCapReached flag set when DailyUsed >= cap; does not hard-block
//   BE-TC-PACK-01  Pack purchase lands on FamilyEnergyAccount.PurchasedBalance (BE-12 re-home)
//   BE-TC-PACK-02  Pack purchase writes a bucket-B Purchase ledger row (SourceBucket=Purchased)
//   BE-TC-PACK-03  Pack purchase does NOT touch CreditAccount or ChildEnergyAllocation rows
//   BE-TC-PACK-04  Pack purchase idempotent: duplicate webhook → no double-credit
//   BE-TC-CUTOVER-01 Migration: CreditAccount.GrantedBalance → ChildEnergyAllocation.AllocatedAmount
//   BE-TC-CUTOVER-02 Migration: CreditAccount.PurchasedBalance → FamilyEnergyAccount.PurchasedBalance
//   BE-TC-CUTOVER-03 Migration: idempotent (no duplicate wallet if already migrated)

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P10-13 Family Energy Wallet integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so that
/// the Testcontainers Postgres container is shared across all P10 test classes within the same run.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in <see cref="InitializeAsync"/>
/// for every test class.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_13_FamilyEnergyWallet_Tests : IAsyncLifetime
{
    // ── URL constants ─────────────────────────────────────────────────────────
    private const string RegisterParentUrl   = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl         = "api/Parent/Add-Child";
    private const string SignInUrl           = "api/Users/Authentication/Sign-In";
    private const string FamilyOverviewUrl   = "api/Billing/FamilyEnergy/Overview";
    private const string EnergyStatusUrl     = "api/Billing/Energy";
    private const string WebhookProviderUrl  = "api/Billing/Webhooks/Provider";
    private const string PacksCheckoutUrl    = "api/Billing/Packs/Checkout";

    private const string ValidChildPassword = "Child@Pass1";
    private const string AdminUserName      = "superadmin";
    private const string AdminPassword      = "123Pa$$word!";

    // Same signing secret wired in LearnexiaWebAppFactory
    private const string TestSigningSecret = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    // Default GlobalSettings values (from GlobalSettingsSeeder)
    private const int DefaultFreeMonthlyPerSeat = 100;
    private const int DefaultPackSize           = 1000;
    private const int DefaultDailyCap           = 10;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_13_FamilyEnergyWallet_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static string UniqueEmail(string tag = "")
        => $"wal_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@wallet.test";

    /// <summary>Case-insensitive property lookup (handles camelCase and PascalCase).</summary>
    private static bool TryProp(JsonElement el, string name, out JsonElement value)
    {
        if (el.TryGetProperty(name, out value)) return true;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (el.TryGetProperty(pascal, out value)) return true;
        foreach (var p in el.EnumerateObject())
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        value = default;
        return false;
    }

    private static JsonElement Prop(JsonElement el, string name)
    {
        TryProp(el, name, out var v);
        return v;
    }

    private async Task<(HttpResponseMessage Response, string Body, JsonElement Root)>
        SendAsync(HttpMethod method, string url, object? body = null, string? bearer = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (bearer is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var resp    = await _client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        try { root = JsonDocument.Parse(bodyStr).RootElement; }
        catch { /* non-JSON responses */ }
        return (resp, bodyStr, root);
    }

    /// <summary>
    /// Signs in as the seeded superadmin and returns an admin JWT token.
    /// </summary>
    private async Task<string> GetAdminTokenAsync()
    {
        var (resp, body, root) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = AdminUserName,
            Password = AdminPassword,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "admin sign-in must succeed; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    /// <summary>
    /// Registers a parent and returns their JWT access token.
    /// </summary>
    private async Task<(int ParentId, string ParentToken)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email        = email,
            Password     = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent registration; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        var id    = GetUserIdFromJwt(token);
        return (id, token);
    }

    /// <summary>
    /// Adds a child to a parent and returns the child's id and JWT token.
    /// </summary>
    private async Task<(int ChildId, string ChildToken)> AddChildAndSignInAsync(
        string parentToken,
        string tag = "")
    {
        var childEmail = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = $"Child {tag}",
            Email            = childEmail,
            Password         = ValidChildPassword,
            Grade            = 4,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201],
            "Add-Child; body={0}", addBody);

        var (signResp, signBody, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = childEmail,
            Password = ValidChildPassword,
        });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body={0}", signBody);
        TryProp(signRoot, "data", out var signData);
        TryProp(signData, "accessToken", out var tok);
        var childToken = tok.GetString()!;
        var childId    = GetUserIdFromJwt(childToken);
        return (childId, childToken);
    }

    /// <summary>
    /// Decodes the user integer id from a Learnexia JWT (reads the "Id" claim).
    /// </summary>
    private static int GetUserIdFromJwt(string jwt)
    {
        var parts   = jwt.Split('.');
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
            if (int.TryParse(raw, out var id)) return id;
        }
        foreach (var prop in doc.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = prop.Value.ValueKind == JsonValueKind.Number
                ? prop.Value.GetInt32().ToString()
                : prop.Value.GetString() ?? "";
            if (int.TryParse(raw, out var vi)) return vi;
        }
        throw new InvalidOperationException($"Cannot parse user id from JWT: {json}");
    }

    /// <summary>
    /// Builds a correctly-signed FakePaymentProvider webhook and sends it as POST to the webhook endpoint.
    /// </summary>
    private async Task<(HttpResponseMessage Response, string Body)> SendWebhookAsync(
        string eventId,
        string eventType,
        string paymentRef,
        decimal amount,
        string? wrongSecret = null)
    {
        var secret = wrongSecret ?? TestSigningSecret;
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, eventType, paymentRef, amount, secret);

        var req = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
        req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Hmac-Signature", sigHeader);

        var resp = await _client.SendAsync(req);
        return (resp, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates a Payment row directly in the DB (bypasses the checkout provider call for speed),
    /// returns the paymentId and providerPaymentRef.
    /// </summary>
    private async Task<(int PaymentId, string ProviderRef)> SeedPaymentRowAsync(
        int parentId,
        int childId,
        PaymentKind kind = PaymentKind.Pack)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var providerRef = $"fake-pack-{Guid.NewGuid():N}";
        var payment = new Payment
        {
            ParentUserId      = parentId,
            TargetChildId     = childId,
            Amount            = 50m,
            Currency          = PaymentCurrency.EGP,
            Status            = PaymentStatus.Initiated,
            Kind              = kind,
            IdempotencyKey    = $"ik-{Guid.NewGuid():N}",
            ProviderPaymentRef = providerRef,
            CreatedAt         = DateTime.UtcNow,
            CreatedBy         = parentId,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(0);
        return (payment.Id, providerRef);
    }

    /// <summary>
    /// Calls IFamilyEnergyAllocationService directly (service-layer) to allocate a grant for
    /// a family, bypassing the Hangfire job. Returns the AllocationResult.
    /// </summary>
    private async Task<AllocationResult> AllocateGrantAsync(
        int parentUserId,
        int grantAmount,
        DateTime cycleStart,
        DateTime cycleEnd,
        string idempotencyKey)
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFamilyEnergyAllocationService>();
        return await svc.AllocateSubscriptionGrantAsync(
            parentUserId,
            grantAmount,
            cycleStart,
            cycleEnd,
            idempotencyKey);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 1 — WALLET OVERVIEW ENDPOINT
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BE-TC-WAL-01: GET /api/Billing/FamilyEnergy/Overview without a JWT → 401.
    /// The endpoint is [Authorize] — anonymous requests must be rejected.
    /// </summary>
    [Fact(DisplayName = "WAL-01 Overview anon → 401 Unauthorized")]
    public async Task WAL01_Overview_Anonymous_Returns401()
    {
        var (resp, body, _) = await SendAsync(HttpMethod.Get, FamilyOverviewUrl);
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "FamilyEnergy/Overview is [Authorize] — anon must get 401; body={0}", body);
    }

    /// <summary>
    /// BE-TC-WAL-02: IDOR — Parent B uses their own valid JWT to call the overview endpoint.
    /// The handler resolves parentUserId from the JWT (not from a query param), so Parent B
    /// can only see their own wallet (which returns 404 if not yet provisioned), never Parent A's.
    /// This test verifies that the endpoint does NOT leak cross-family data.
    /// </summary>
    [Fact(DisplayName = "WAL-02 IDOR: Parent B cannot see Parent A's wallet — gets 404/empty own wallet")]
    public async Task WAL02_IDOR_ParentBCannotSeeParentAWallet()
    {
        // Parent A: register, add child, allocate grant (provisions wallet)
        var (parentAId, parentAToken) = await RegisterParentAsync("WAL02A");
        var (_, _) = await AddChildAndSignInAsync(parentAToken, "WAL02CA");

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentAId, 100, cycleStart, cycleEnd, $"idor-test:{parentAId}:{cycleStart:yyyyMM}");

        // Parent B: register (no children, no wallet)
        var (_, parentBToken) = await RegisterParentAsync("WAL02B");

        // Parent B calls Overview using THEIR OWN JWT
        // JWT is scoped to Parent B's userId, so handler returns 404 for Parent B's (non-existent) wallet.
        // It must NOT return Parent A's wallet data.
        var (resp, body, root) = await SendAsync(HttpMethod.Get, FamilyOverviewUrl, bearer: parentBToken);

        // Acceptable outcomes: 404 (no wallet for Parent B), or 200 with empty/zero data for Parent B.
        // What is NOT acceptable: 200 with Parent A's SubscriptionBalance > 0.
        if (resp.StatusCode == HttpStatusCode.OK)
        {
            TryProp(root, "data", out var data);
            TryProp(data, "subscriptionBalance", out var balEl);
            var bal = balEl.ValueKind == JsonValueKind.Number ? balEl.GetInt32() : 0;
            bal.Should().Be(0,
                "IDOR DEFECT: Parent B's Overview returned non-zero SubscriptionBalance — " +
                "this is Parent A's data leaking across families! body={0}", body);
        }
        else
        {
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
                "Parent B has no wallet — should be 404 or 200-with-zero; body={0}", body);
        }
    }

    /// <summary>
    /// BE-TC-WAL-03: After wallet is provisioned, GET Overview → 200 + BaseResponse envelope with
    /// required keys: statusCode, successed, message, data.
    /// data contains: subscriptionBalance, purchasedBalance, subscriptionExpiresAtUtc, children.
    /// </summary>
    [Fact(DisplayName = "WAL-03 Overview returns 200 + BaseResponse envelope after wallet provisioned")]
    public async Task WAL03_Overview_200_EnvelopeShape_WhenWalletExists()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WAL03");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "WAL03C");

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        var grant = await AllocateGrantAsync(parentId, 100, cycleStart, cycleEnd, $"wal03:{parentId}:{cycleStart:yyyyMM}");
        grant.Succeeded.Should().BeTrue("allocation must succeed for this test");

        var (resp, body, root) = await SendAsync(HttpMethod.Get, FamilyOverviewUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body={0}", body);

        // Envelope shape
        root.TryGetProperty("statusCode", out _).Should().BeTrue("envelope must have 'statusCode'; body={0}", body);
        TryProp(root, "successed", out var succEl).Should().BeTrue("envelope must have 'successed'; body={0}", body);
        succEl.GetBoolean().Should().BeTrue("successed must be true on success; body={0}", body);
        TryProp(root, "data", out var data).Should().BeTrue("envelope must have 'data'; body={0}", body);

        // Data fields
        TryProp(data, "subscriptionBalance", out _).Should().BeTrue("data.subscriptionBalance missing; body={0}", body);
        TryProp(data, "purchasedBalance", out _).Should().BeTrue("data.purchasedBalance missing; body={0}", body);
        TryProp(data, "children", out var childrenEl).Should().BeTrue("data.children missing; body={0}", body);
        childrenEl.GetArrayLength().Should().BeGreaterThan(0,
            "at least one child allocation row must be returned; body={0}", body);
    }

    /// <summary>
    /// BE-TC-WAL-04: Parent with no wallet yet → GET Overview → 404.
    /// </summary>
    [Fact(DisplayName = "WAL-04 Overview returns 404 when no wallet exists for parent")]
    public async Task WAL04_Overview_404_WhenNoWallet()
    {
        var (_, parentToken) = await RegisterParentAsync("WAL04");
        // No children added, no grant called → no FamilyEnergyAccount row
        var (resp, body, _) = await SendAsync(HttpMethod.Get, FamilyOverviewUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "no wallet yet → 404; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 2 — MONTHLY GRANT / ALLOCATION
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BE-TC-GRANT-01: Monthly grant deposited into FamilyEnergyAccount.SubscriptionBalance
    /// equals PlanEnergyPerSeat × ActivePaidSeats (with TemporarySeatQuery: 1 seat per child).
    /// </summary>
    [Fact(DisplayName = "GRANT-01 Grant = PlanEnergyPerSeat × ActivePaidSeats → wallet SubscriptionBalance")]
    public async Task GRANT01_Grant_EqualsPerSeatTimesSeats_WalletSubscriptionBalance()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR01");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId); // P10-14 seat gate: 2 children need ≥2 seats
        var (_, _) = await AddChildAndSignInAsync(parentToken, "GR01C1");
        var (_, _) = await AddChildAndSignInAsync(parentToken, "GR01C2");

        // With TemporarySeatQuery: 2 active children = 2 seats
        // grantAmount = 100 (free monthly per seat) × 2 = 200
        var perSeat    = DefaultFreeMonthlyPerSeat;
        var seats      = 2;
        var totalGrant = perSeat * seats; // 200

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        var result = await AllocateGrantAsync(parentId, totalGrant, cycleStart, cycleEnd,
            $"grant01:{parentId}:{cycleStart:yyyyMM}");

        result.Succeeded.Should().BeTrue("allocation must succeed");
        result.WasDuplicate.Should().BeFalse();

        // Assert wallet SubscriptionBalance == totalGrant
        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

        wallet.Should().NotBeNull("wallet must be created on first grant");
        wallet!.SubscriptionBalance.Should().Be(totalGrant,
            "SubscriptionBalance must equal PlanEnergyPerSeat({0}) × ActivePaidSeats({1}) = {2}",
            perSeat, seats, totalGrant);
    }

    /// <summary>
    /// BE-TC-GRANT-02: Equal split: allocated sum == grant exactly; deterministic remainder
    /// (with 3 children and grantAmount=100, split is 34/33/33 — sum=100).
    /// </summary>
    [Fact(DisplayName = "GRANT-02 Equal-split: allocated sum == grant; deterministic remainder")]
    public async Task GRANT02_EqualSplit_AllocatedSumEqualsGrant_DeterministicRemainder()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR02");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId); // P10-14 seat gate: 3 children need 3 seats (Premium)
        var (childId1, _) = await AddChildAndSignInAsync(parentToken, "GR02C1");
        var (childId2, _) = await AddChildAndSignInAsync(parentToken, "GR02C2");
        var (childId3, _) = await AddChildAndSignInAsync(parentToken, "GR02C3");

        // 3 children → grantAmount not divisible by 3 → remainder must be distributed
        const int grantAmount = 100; // 100 / 3 = 33 remainder 1 → [34, 33, 33]

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        var result = await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"grant02:{parentId}:{cycleStart:yyyyMM}");
        result.Succeeded.Should().BeTrue();
        result.TotalAllocated.Should().Be(grantAmount,
            "total allocated must equal grant exactly (no energy lost or invented); " +
            "grantAmount={0}, totalAllocated={1}", grantAmount, result.TotalAllocated);

        // Verify per-child allocations sum == grantAmount
        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking().FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        wallet.Should().NotBeNull();

        var allocs = await db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == wallet!.Id && a.CycleStartUtc == cycleStart)
            .ToListAsync();

        allocs.Count.Should().Be(3, "one allocation row per active child");
        var sumAllocated = allocs.Sum(a => a.AllocatedAmount);
        sumAllocated.Should().Be(grantAmount,
            "sum of all per-child AllocatedAmount must equal the grant exactly; " +
            "sum={0}, grant={1}", sumAllocated, grantAmount);

        // Verify deterministic remainder: first child gets the extra +1
        // (algorithm: first R=1 children get baseShare+1 = 34; rest get 33)
        var firstChildAlloc = allocs.Min(a => a.AllocatedAmount);
        var maxAlloc        = allocs.Max(a => a.AllocatedAmount);
        maxAlloc.Should().Be(34, "first child gets base+1=34 (remainder=1)");
        firstChildAlloc.Should().Be(33, "remaining children get base=33");
        allocs.Count(a => a.AllocatedAmount == 34).Should().Be(1, "exactly 1 child gets +1");
        allocs.Count(a => a.AllocatedAmount == 33).Should().Be(2, "exactly 2 children get base");
    }

    /// <summary>
    /// BE-TC-GRANT-03: Per-child ChildEnergyAllocation rows are created with correct cycle bounds
    /// and Remaining == Allocated (no spend yet).
    /// </summary>
    [Fact(DisplayName = "GRANT-03 ChildEnergyAllocation rows created; Remaining = AllocatedAmount")]
    public async Task GRANT03_ChildEnergyAllocationRows_Created_RemainingEqualsAllocated()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR03");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "GR03C");

        const int grantAmount = 100;
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"grant03:{parentId}:{cycleStart:yyyyMM}");

        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking().FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        wallet.Should().NotBeNull();

        var alloc = await db.ChildEnergyAllocations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.FamilyEnergyAccountId == wallet!.Id && a.ChildId == childId);

        alloc.Should().NotBeNull("ChildEnergyAllocation row must be created for the child");
        alloc!.AllocatedAmount.Should().Be(grantAmount,
            "AllocatedAmount must equal the full grant (1 child = all 100)");
        alloc.SpentAmount.Should().Be(0, "no spend yet — SpentAmount must be 0");
        alloc.Remaining.Should().Be(grantAmount, "Remaining = Allocated - Spent = {0}", grantAmount);
        // Npgsql legacy timestamp: DB may return the stored UTC value in local-time representation
        // depending on the EnableLegacyTimestampBehavior switch; use BeCloseTo(1 day) to tolerate
        // any timezone-offset artifact in the round-trip (the important invariant is the month).
        alloc.CycleStartUtc.ToUniversalTime().Should().BeCloseTo(cycleStart, TimeSpan.FromDays(1),
            "CycleStartUtc must be in the same month as cycleStart; stored={0}", alloc.CycleStartUtc);
        alloc.CycleEndUtc.ToUniversalTime().Should().BeCloseTo(cycleEnd, TimeSpan.FromDays(1),
            "CycleEndUtc must be in the same month as cycleEnd; stored={0}", alloc.CycleEndUtc);
    }

    /// <summary>
    /// BE-TC-GRANT-04: Idempotency — calling AllocateSubscriptionGrant twice with the same
    /// (parentUserId, cycleStart) returns WasDuplicate=true and does NOT double the balances.
    /// </summary>
    [Fact(DisplayName = "GRANT-04 Idempotency: same (parent, cycle) → WasDuplicate; no double-grant")]
    public async Task GRANT04_Idempotency_SameCycle_WasDuplicate_NoDoubleGrant()
    {
        var (parentId, parentToken) = await RegisterParentAsync("GR04");
        var (_, _) = await AddChildAndSignInAsync(parentToken, "GR04C");

        const int grantAmount = 100;
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        var key        = $"grant04:{parentId}:{cycleStart:yyyyMM}";

        var r1 = await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd, key);
        r1.Succeeded.Should().BeTrue("first allocation must succeed");
        r1.WasDuplicate.Should().BeFalse();

        var r2 = await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd, key);
        r2.WasDuplicate.Should().BeTrue("second call with same key must be idempotent");

        // Balance must NOT be doubled
        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking().FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        wallet.Should().NotBeNull();
        wallet!.SubscriptionBalance.Should().Be(grantAmount,
            "MONEY DEFECT: SubscriptionBalance must be {0} (not doubled to {1}); actual={2}",
            grantAmount, grantAmount * 2, wallet.SubscriptionBalance);
    }

    /// <summary>
    /// BE-TC-GRANT-05: When there are NO active children for a parent, the allocation service
    /// returns NoChildren result and no wallet is created.
    /// </summary>
    [Fact(DisplayName = "GRANT-05 NoChildren: zero active children → AllocationResult.NoChildren; no wallet created")]
    public async Task GRANT05_NoChildren_NoWalletCreated()
    {
        var (parentId, _) = await RegisterParentAsync("GR05");
        // No children added

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        var result = await AllocateGrantAsync(parentId, 100, cycleStart, cycleEnd,
            $"grant05:{parentId}:{cycleStart:yyyyMM}");

        result.ChildCount.Should().Be(0, "no active children → ChildCount=0");
        result.TotalAllocated.Should().Be(0, "no active children → TotalAllocated=0");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking().FirstOrDefaultAsync(w => w.ParentUserId == parentId);

        wallet.Should().BeNull("no wallet should be created when there are no active children");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 3 — SPEND: ALLOCATION-FIRST → PURCHASED-FALLBACK
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BE-TC-SPEND-01 + SPEND-02: Spend when allocation covers full cost.
    /// The child's ChildEnergyAllocation.SpentAmount increases; PurchasedBalance stays COLD.
    /// </summary>
    [Fact(DisplayName = "SPEND-01/02 Spend allocation-first: allocation covers cost → child row debited; PurchasedBalance untouched")]
    public async Task SPEND01_02_AllocationCoversFullCost_ChildRowDebited_PurchasedUntouched()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP01");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "SP01C");

        const int grantAmount = 100;
        const int spendAmount = 10;
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"spend01:{parentId}:{cycleStart:yyyyMM}");

        // Read initial wallet state
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<BillingDbContext>();
        var walletBefore = await dbBefore.FamilyEnergyAccounts
            .AsNoTracking().FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        var purchasedBefore = walletBefore!.PurchasedBalance;
        var allocBefore = await dbBefore.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletBefore.Id && a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc).FirstOrDefaultAsync();
        var remainingBefore = allocBefore!.Remaining;

        // Spend via ICreditSpendService
        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var debitResult = await spendSvc.TryDebitAsync(
            childId,
            spendAmount,
            "SpendAllocation",
            $"spend01-key:{childId}:{Guid.NewGuid():N}");

        debitResult.Charged.Should().BeTrue("spend must succeed when allocation covers cost");
        debitResult.Outcome.Should().Be(DebitOutcome.Charged);

        // Verify child allocation row debited
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();
        var allocAfter = await dbAfter.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletBefore.Id && a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc).FirstOrDefaultAsync();

        allocAfter!.SpentAmount.Should().Be(spendAmount,
            "child allocation SpentAmount must be {0} after spend; was 0 before", spendAmount);
        allocAfter.Remaining.Should().Be(remainingBefore - spendAmount,
            "Remaining must decrease by spend amount");

        // Purchased balance must be UNTOUCHED (cold-row rule)
        var walletAfter = await dbAfter.FamilyEnergyAccounts
            .AsNoTracking().FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        walletAfter!.PurchasedBalance.Should().Be(purchasedBefore,
            "MONEY DEFECT: PurchasedBalance must NOT be touched when child allocation covers the cost; " +
            "before={0}, after={1}. This violates the cold-row / hotspot-avoidance hard rule.",
            purchasedBefore, walletAfter.PurchasedBalance);
    }

    /// <summary>
    /// BE-TC-SPEND-03: Spend shortfall — allocation partially covers; remainder drawn from PurchasedBalance.
    /// </summary>
    [Fact(DisplayName = "SPEND-03 Shortfall: allocation covers partial → shortfall from PurchasedBalance")]
    public async Task SPEND03_Shortfall_DrawnFromPurchasedBalance()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP03");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "SP03C");

        const int grantAmount    = 5;   // small so shortfall is guaranteed
        const int purchasedSeed  = 100; // seed purchased balance
        const int spendAmount    = 8;   // > grantAmount; shortfall = 3

        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);

        await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"spend03:{parentId}:{cycleStart:yyyyMM}");

        // Seed purchased balance on the wallet
        {
            using var scope = _factory.Services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.FirstAsync(w => w.ParentUserId == parentId);
            wallet.PurchasedBalance += purchasedSeed;
            await db.SaveChangesAsync(0);
        }

        // Read balances before spend
        int walletIdCapture;
        {
            using var scope = _factory.Services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var w      = await db.FamilyEnergyAccounts.AsNoTracking().FirstAsync(x => x.ParentUserId == parentId);
            walletIdCapture = w.Id;
        }

        // Spend 8 (allocation=5 covers 5; shortfall=3 from purchased)
        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var debitResult = await spendSvc.TryDebitAsync(
            childId,
            spendAmount,
            "SpendAllocation",
            $"spend03-key:{childId}:{Guid.NewGuid():N}");

        debitResult.Charged.Should().BeTrue("spend must succeed (allocation+purchased cover cost)");

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();

        var allocAfter = await dbAfter.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletIdCapture && a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc).FirstOrDefaultAsync();

        // Allocation exhausted (SpentAmount = grantAmount)
        allocAfter!.SpentAmount.Should().Be(grantAmount,
            "child allocation must be fully spent (all {0} used)", grantAmount);
        allocAfter.Remaining.Should().Be(0, "Remaining must be 0 after full exhaustion");

        var walletAfter = await dbAfter.FamilyEnergyAccounts
            .AsNoTracking().FirstAsync(w => w.Id == walletIdCapture);

        var expectedPurchasedAfter = purchasedSeed - (spendAmount - grantAmount); // 100 - 3 = 97
        walletAfter.PurchasedBalance.Should().Be(expectedPurchasedAfter,
            "shortfall={0} must be drawn from PurchasedBalance; before={1}, expected after={2}, actual={3}",
            spendAmount - grantAmount, purchasedSeed, expectedPurchasedAfter, walletAfter.PurchasedBalance);
    }

    /// <summary>
    /// BE-TC-SPEND-04: Spend with INSUFFICIENT balance (allocation + purchased both zero) →
    /// Charged=false; InsufficientBalance outcome; NO ledger rows written.
    /// </summary>
    [Fact(DisplayName = "SPEND-04 Insufficient balance → Charged=false; no DB write; never negative")]
    public async Task SPEND04_InsufficientBalance_Charged_False_NoWrite()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP04");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "SP04C");

        // Allocate zero-amount grant (allocation Remaining = 0, PurchasedBalance = 0)
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentId, 0, cycleStart, cycleEnd,
            $"spend04:{parentId}:{cycleStart:yyyyMM}");

        // Count existing transactions before (baseline)
        int txCountBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            txCountBefore = await db.CreditTransactions.CountAsync();
        }

        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var result   = await spendSvc.TryDebitAsync(
            childId,
            amount: 1,
            reasonCode: "SpendAllocation",
            idempotencyKey: $"spend04-key:{childId}:{Guid.NewGuid():N}");

        result.Charged.Should().BeFalse(
            "MONEY DEFECT: TryDebitAsync must return Charged=false when balance is insufficient");
        result.Outcome.Should().Be(DebitOutcome.InsufficientBalance,
            "outcome must be InsufficientBalance");

        // No new ledger rows
        int txCountAfter;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            txCountAfter = await db.CreditTransactions.CountAsync();
        }
        txCountAfter.Should().Be(txCountBefore,
            "MONEY DEFECT: no CreditTransaction rows must be written on insufficient-balance; " +
            "before={0}, after={1}", txCountBefore, txCountAfter);
    }

    /// <summary>
    /// BE-TC-SPEND-05: ChildDailyUsage row is created (or updated) on successful spend.
    /// </summary>
    [Fact(DisplayName = "SPEND-05 ChildDailyUsage row created and incremented on successful spend")]
    public async Task SPEND05_ChildDailyUsage_CreatedAndIncremented()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP05");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "SP05C");

        const int grantAmount = 50;
        const int spendCost   = 3;
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"spend05:{parentId}:{cycleStart:yyyyMM}");

        // Verify no ChildDailyUsage before first spend
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var existingUsage = await db.ChildDailyUsages.AsNoTracking()
                .FirstOrDefaultAsync(d => d.ChildId == childId);
            // May or may not exist at this point (allocation doesn't create it)
            // We'll track DailyUsed before the spend
        }

        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var result   = await spendSvc.TryDebitAsync(
            childId, spendCost, "AiDeepExplanation",
            $"spend05-key:{childId}:{Guid.NewGuid():N}");

        result.Charged.Should().BeTrue("spend must succeed");

        // Check ChildDailyUsage
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();
        var usage   = await dbAfter.ChildDailyUsages
            .AsNoTracking().FirstOrDefaultAsync(d => d.ChildId == childId);

        usage.Should().NotBeNull("ChildDailyUsage row must be created on first successful spend");
        usage!.DailyUsed.Should().BeGreaterThanOrEqualTo(spendCost,
            "DailyUsed must be >= spendCost={0} after spend; actual={1}", spendCost, usage.DailyUsed);
        usage.DailyUsedDateLocal.Should().NotBeNullOrEmpty("DailyUsedDateLocal must be set");
    }

    /// <summary>
    /// BE-TC-SPEND-06: Daily soft cap — after spending dailyCap amount, GetBalanceAsync returns
    /// DailyCapReached=true. Does NOT hard-block (warning only by default).
    /// </summary>
    [Fact(DisplayName = "SPEND-06 Daily soft cap: DailyCapReached=true after spending cap amount")]
    public async Task SPEND06_DailySoftCap_DailyCapReachedFlag()
    {
        var (parentId, parentToken) = await RegisterParentAsync("SP06");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "SP06C");

        // Grant enough to exceed daily cap (cap=10 by default)
        const int grantAmount = 200;
        const int spendCost   = DefaultDailyCap; // 10 — exactly at the cap
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"spend06:{parentId}:{cycleStart:yyyyMM}");

        // Spend exactly the daily cap amount
        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var result   = await spendSvc.TryDebitAsync(
            childId, spendCost, "AiDeepExplanation",
            $"spend06-key:{childId}:{Guid.NewGuid():N}");
        result.Charged.Should().BeTrue("spending exactly cap must succeed (soft cap, not hard block)");

        // GetBalance should show DailyCapReached
        using var balScope = _factory.Services.CreateScope();
        var balSvc  = balScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var balance = await balSvc.GetBalanceAsync(childId);

        balance.DailyCapReached.Should().BeTrue(
            "DailyCapReached must be true when DailyUsed({0}) >= DailyCap({1})",
            balance.DailyUsed, balance.DailyCap);
        balance.DailyUsed.Should().BeGreaterThanOrEqualTo(DefaultDailyCap,
            "DailyUsed must be >= {0}", DefaultDailyCap);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 4 — PACK PURCHASE RE-HOME (BE-12)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BE-TC-PACK-01: A pack purchase via verified webhook → credits FamilyEnergyAccount.PurchasedBalance
    /// (not the per-child CreditAccount). PurchasedBalance increases by PackSize.
    /// </summary>
    [Fact(DisplayName = "PACK-01 Pack purchase → FamilyEnergyAccount.PurchasedBalance increases by PackSize")]
    public async Task PACK01_PackPurchase_CreditsSharedFamilyPurchasedBalance()
    {
        var (parentId, parentToken) = await RegisterParentAsync("PK01");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "PK01C");

        // Create a Payment row (simulate checkout without calling provider)
        var (paymentId, providerRef) = await SeedPaymentRowAsync(parentId, childId, PaymentKind.Pack);

        // Record PurchasedBalance before
        int purchasedBefore;
        int? walletId = null;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            purchasedBefore = wallet?.PurchasedBalance ?? 0;
            walletId = wallet?.Id;
        }

        // Send pack.succeeded webhook
        var eventId = Guid.NewGuid().ToString("N");
        var (resp, body) = await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 50m);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "valid pack webhook must return 200; body={0}", body);

        // Verify PurchasedBalance increased by PackSize (1000)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

            wallet.Should().NotBeNull("FamilyEnergyAccount must be created on first pack purchase");
            wallet!.PurchasedBalance.Should().Be(purchasedBefore + DefaultPackSize,
                "PurchasedBalance must increase by PackSize={0} after pack purchase; " +
                "before={1}, expected={2}, actual={3}",
                DefaultPackSize, purchasedBefore, purchasedBefore + DefaultPackSize, wallet.PurchasedBalance);
        }
    }

    /// <summary>
    /// BE-TC-PACK-02: Pack purchase writes a bucket-B Purchase ledger row with
    /// SourceBucket=Purchased, Type=Purchase, and RelatedPaymentId set.
    /// </summary>
    [Fact(DisplayName = "PACK-02 Pack purchase writes bucket-B Purchase ledger row (SourceBucket=Purchased)")]
    public async Task PACK02_PackPurchase_WritesBucketBLedgerRow()
    {
        var (parentId, parentToken) = await RegisterParentAsync("PK02");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "PK02C");
        var (paymentId, providerRef) = await SeedPaymentRowAsync(parentId, childId, PaymentKind.Pack);

        var eventId = Guid.NewGuid().ToString("N");
        await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 50m);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
        wallet.Should().NotBeNull();

        // Find the Purchase ledger row linked to this wallet
        var purchaseTx = await db.CreditTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.FamilyEnergyAccountId == wallet!.Id &&
                t.Type == CreditTransactionType.Purchase &&
                t.RelatedPaymentId == paymentId.ToString());

        purchaseTx.Should().NotBeNull(
            "a Purchase ledger row must be written for the pack purchase; walletId={0}, paymentId={1}",
            wallet!.Id, paymentId);
        purchaseTx!.SourceBucket.Should().Be(EnergyBucket.Purchased,
            "pack purchase must have SourceBucket=Purchased (bucket B)");
        purchaseTx.Type.Should().Be(CreditTransactionType.Purchase,
            "transaction type must be Purchase");
        purchaseTx.Amount.Should().Be(DefaultPackSize,
            "ledger amount must equal PackSize={0}", DefaultPackSize);
        purchaseTx.ReasonCode.Should().Be(CreditReasonCode.PackPurchase,
            "ReasonCode must be PackPurchase");
    }

    /// <summary>
    /// BE-TC-PACK-03: Pack purchase does NOT write to CreditAccount or ChildEnergyAllocation rows.
    /// The purchased balance belongs to the shared family wallet, not the per-child model.
    /// </summary>
    [Fact(DisplayName = "PACK-03 Pack purchase does NOT touch CreditAccount or ChildEnergyAllocation rows")]
    public async Task PACK03_PackPurchase_NoPerChildCreditAccountTouch()
    {
        var (parentId, parentToken) = await RegisterParentAsync("PK03");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "PK03C");
        var (paymentId, providerRef) = await SeedPaymentRowAsync(parentId, childId, PaymentKind.Pack);

        // Count per-child rows before
        int creditAccountTxBefore, allocTxBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            creditAccountTxBefore = await db.CreditTransactions
                .AsNoTracking()
                .CountAsync(t => t.CreditAccountId != null);
            allocTxBefore = await db.CreditTransactions
                .AsNoTracking()
                .CountAsync(t => t.ChildEnergyAllocationId != null);
        }

        var eventId = Guid.NewGuid().ToString("N");
        await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 50m);

        // Count per-child rows after
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var creditAccountTxAfter = await db.CreditTransactions
                .AsNoTracking()
                .CountAsync(t => t.CreditAccountId != null);
            var allocTxAfter = await db.CreditTransactions
                .AsNoTracking()
                .CountAsync(t => t.ChildEnergyAllocationId != null);

            creditAccountTxAfter.Should().Be(creditAccountTxBefore,
                "ARCHITECTURE DEFECT: pack purchase must NOT write to CreditAccount rows; " +
                "before={0}, after={1}", creditAccountTxBefore, creditAccountTxAfter);
            allocTxAfter.Should().Be(allocTxBefore,
                "ARCHITECTURE DEFECT: pack purchase must NOT write to ChildEnergyAllocation rows; " +
                "before={0}, after={1}", allocTxBefore, allocTxAfter);
        }
    }

    /// <summary>
    /// BE-TC-PACK-04: Duplicate pack webhook (same eventId) → idempotent no-op.
    /// PurchasedBalance must NOT be credited twice.
    /// </summary>
    [Fact(DisplayName = "PACK-04 Duplicate pack webhook → idempotent no-op; PurchasedBalance not doubled")]
    public async Task PACK04_DuplicatePackWebhook_Idempotent_NoDuplicateCredit()
    {
        var (parentId, parentToken) = await RegisterParentAsync("PK04");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "PK04C");
        var (paymentId, providerRef) = await SeedPaymentRowAsync(parentId, childId, PaymentKind.Pack);

        var eventId = Guid.NewGuid().ToString("N");

        // First webhook
        var (resp1, body1) = await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 50m);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first webhook; body={0}", body1);

        // Read balance after first
        int purchasedAfterFirst;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            purchasedAfterFirst = wallet.PurchasedBalance;
        }

        // Second webhook with same eventId (replay)
        var (resp2, body2) = await SendWebhookAsync(eventId, "payment.succeeded", providerRef, 50m);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "idempotent replay must return 200 (not error); body={0}", body2);

        // Balance must NOT be doubled
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            wallet.PurchasedBalance.Should().Be(purchasedAfterFirst,
                "MONEY DEFECT: PurchasedBalance must NOT be doubled on duplicate webhook; " +
                "after first={0}, after second={1}", purchasedAfterFirst, wallet.PurchasedBalance);
        }

        // Only one Purchase ledger row for this payment
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            var purchaseTxCount = await db.CreditTransactions
                .AsNoTracking()
                .CountAsync(t =>
                    t.FamilyEnergyAccountId == wallet.Id &&
                    t.Type == CreditTransactionType.Purchase &&
                    t.RelatedPaymentId == paymentId.ToString());
            purchaseTxCount.Should().Be(1,
                "MONEY DEFECT: exactly one Purchase ledger row; found={0}", purchaseTxCount);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 5 — CLEAN CUTOVER (MIGRATION)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// BE-TC-CUTOVER-01: Migration converts CreditAccount.GrantedBalance → per-child
    /// ChildEnergyAllocation.AllocatedAmount for the current cycle.
    /// </summary>
    // ══════════════════════════════════════════════════════════════════════════
    // CUTOVER AREA NOTE (P10-13 post-migration):
    // The billing.CreditAccounts table was DROPPED by the DropLegacyCreditAccounts migration.
    // The CUTOVER-01/02/03 tests originally seeded data via raw SQL INSERT into that table —
    // which fails with PostgresException 42P01 "relation does not exist" since P10-13.
    //
    // These tests are redesigned to verify the POST-CUTOVER equivalent behavior:
    //   CUTOVER-01: a wallet + allocation is provisioned with the correct AllocatedAmount
    //   CUTOVER-02: a second child's purchased balance rolls up into the shared family wallet
    //   CUTOVER-03: MigrateAsync() is a no-op (returns 0/0/0) on a fully-migrated database
    //               (i.e. billing.CreditAccounts is gone → service returns immediately)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CUTOVER-01 (post-cutover): Admin grant → FamilyEnergyAccount.PurchasedBalance matches granted amount")]
    public async Task CUTOVER01_Migration_GrantedBalance_ToChildEnergyAllocation()
    {
        // P10-13 CUTOVER: billing.CreditAccounts table has been dropped; can no longer seed via raw SQL.
        // Redesigned: the admin Grant endpoint (POST /Credits/Grant) now deposits into FamilyEnergyAccount.PurchasedBalance
        // (permanent admin comp). ChildEnergyAllocation rows are created by BillingGrantJob (monthly subscription grant),
        // NOT by the admin grant endpoint. This test verifies the admin grant path:
        //   POST /Credits/Grant → FamilyEnergyAccount.PurchasedBalance == grantedAmount.
        var (parentId, parentToken) = await RegisterParentAsync("CUT01");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "CUT01C");

        const int grantedAmount = 75;
        var adminToken = await GetAdminTokenAsync();
        var grantKey   = $"cut01:{parentId}:{Guid.NewGuid():N}";

        // Admin grant via HTTP → creates FamilyEnergyAccount and sets PurchasedBalance = grantedAmount.
        var (gResp, gBody, _) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantedAmount,
            IdempotencyKey = grantKey,
        }, adminToken);
        gResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin grant must succeed for CUTOVER-01; body={0}", gBody);

        // Verify FamilyEnergyAccount was created with PurchasedBalance == grantedAmount
        {
            using var scope = _factory.Services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            wallet.Should().NotBeNull("FamilyEnergyAccount must be created by admin grant");

            // Admin grant deposits into PurchasedBalance (permanent comp, not monthly subscription).
            wallet!.PurchasedBalance.Should().Be(grantedAmount,
                "PurchasedBalance must equal the granted amount={0}; actual={1}",
                grantedAmount, wallet.PurchasedBalance);
        }
    }

    /// <summary>
    /// BE-TC-CUTOVER-02 (redesigned post P10-13): Pack purchase accumulates into shared FamilyEnergyAccount.PurchasedBalance.
    /// </summary>
    [Fact(DisplayName = "CUTOVER-02 (post-cutover): Pack purchase accumulates into shared FamilyEnergyAccount.PurchasedBalance")]
    public async Task CUTOVER02_Migration_PurchasedBalance_ToSharedFamilyWallet()
    {
        // P10-13 CUTOVER: billing.CreditAccounts table dropped; redesigned to verify
        // that pack purchases accumulate into the shared FamilyEnergyAccount.PurchasedBalance.
        var (parentId, parentToken) = await RegisterParentAsync("CUT02");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId); // P10-14 seat gate: 2 children need ≥2 seats
        var (child1Id, _) = await AddChildAndSignInAsync(parentToken, "CUT02C1");
        var (child2Id, _) = await AddChildAndSignInAsync(parentToken, "CUT02C2");

        const int purchased1 = 200;
        const int purchased2 = 300;
        const int totalPurchased = purchased1 + purchased2;

        // Create the family wallet directly via DB (empty wallet — PurchasedBalance starts at 0).
        // We do NOT use the admin grant endpoint here to avoid an initial seed PurchasedBalance
        // that would make the final assertion ambiguous.
        {
            using var scope = _factory.Services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = FamilyEnergyAccount.CreateEmpty(parentId);
            db.FamilyEnergyAccounts.Add(wallet);
            await db.SaveChangesAsync(0);

            // Simulate two pack purchases by calling ApplyPurchase (mirrors EnergyPackService after webhook).
            var key1 = $"cut02-pack1:{parentId}:{Guid.NewGuid():N}";
            var key2 = $"cut02-pack2:{parentId}:{Guid.NewGuid():N}";

            var tx1 = wallet!.ApplyPurchase(purchased1, key1);
            tx1.FamilyEnergyAccountId = wallet.Id;
            db.CreditTransactions.Add(tx1);

            var tx2 = wallet.ApplyPurchase(purchased2, key2);
            tx2.FamilyEnergyAccountId = wallet.Id;
            db.CreditTransactions.Add(tx2);

            await db.SaveChangesAsync(0);
        }

        // Verify shared FamilyEnergyAccount has accumulated PurchasedBalance correctly.
        {
            using var scope = _factory.Services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
            wallet.Should().NotBeNull("FamilyEnergyAccount must exist");

            // AC13-8: pack purchases from two separate top-ups roll up into the SHARED family purchased balance.
            // purchased1(200) + purchased2(300) = 500 — no other deposits were made.
            wallet!.PurchasedBalance.Should().Be(totalPurchased,
                "shared PurchasedBalance must equal sum of both pack purchases " +
                "({0}+{1}={2}); actual={3}",
                purchased1, purchased2, totalPurchased, wallet.PurchasedBalance);
        }
    }

    /// <summary>
    /// BE-TC-CUTOVER-03 (redesigned post P10-13): Admin grant is idempotent — duplicate key → single wallet, no double balance.
    /// Also verifies MigrateAsync() is a no-op on a fully-migrated database (returns 0/0/0).
    /// </summary>
    [Fact(DisplayName = "CUTOVER-03 (post-cutover): Duplicate admin grant key → single wallet, no doubled balance; MigrateAsync is no-op")]
    public async Task CUTOVER03_Migration_Idempotent_NoDoubleWallet()
    {
        // P10-13 CUTOVER: billing.CreditAccounts table dropped; redesigned to verify:
        // 1. Admin grant with duplicate IdempotencyKey → only one wallet + allocation (idempotent)
        // 2. MigrateAsync() returns 0/0/0 on a fully-migrated database (table gone = no-op)
        var (parentId, parentToken) = await RegisterParentAsync("CUT03");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "CUT03C");

        const int grantedBalance = 50;
        var adminToken  = await GetAdminTokenAsync();
        var idempotencyKey = $"cut03:{parentId}:{Guid.NewGuid():N}";

        // First grant — creates wallet + allocation
        var (r1, b1, _) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantedBalance,
            IdempotencyKey = idempotencyKey,
        }, adminToken);
        r1.StatusCode.Should().Be(HttpStatusCode.OK, "first grant must succeed; body={0}", b1);

        // Second grant with same key — idempotent replay
        var (r2, b2, _) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantedBalance,
            IdempotencyKey = idempotencyKey,
        }, adminToken);
        r2.StatusCode.Should().Be(HttpStatusCode.OK, "duplicate grant must return 200; body={0}", b2);

        // Verify exactly ONE wallet for this parent (no double-wallet)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var walletCount = await db.FamilyEnergyAccounts
                .AsNoTracking()
                .CountAsync(w => w.ParentUserId == parentId);
            walletCount.Should().Be(1,
                "idempotency defect: must have exactly 1 FamilyEnergyAccount for parentId={0}; found={1}",
                parentId, walletCount);

            // PurchasedBalance must not be doubled (admin grant deposits into PurchasedBalance)
            var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.ParentUserId == parentId);
            wallet.PurchasedBalance.Should().Be(grantedBalance,
                "MONEY DEFECT: PurchasedBalance must not be doubled on duplicate grant key; " +
                "expected={0}; actual={1}", grantedBalance, wallet.PurchasedBalance);
        }

        // Verify MigrateAsync is a no-op on a fully-migrated database
        // (billing.CreditAccounts is gone → ReadAllCreditAccountsRawAsync catches 42P01 → returns 0/0/0)
        using var migrateScope = _factory.Services.CreateScope();
        var migrationSvc = migrateScope.ServiceProvider.GetRequiredService<ICreditAccountMigrationService>();
        var migResult    = await migrationSvc.MigrateAsync();
        migResult.Migrated.Should().Be(0,
            "MigrateAsync must return 0 migrated on a fully-migrated database (table gone)");
        migResult.Failed.Should().Be(0,
            "MigrateAsync must not report any failures on a fresh database");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 6 — OVERVIEW ENDPOINT DATA ACCURACY (POST-GRANT)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// After grant + partial spend, the Overview endpoint returns accurate
    /// SubscriptionBalance, per-child Allocated / Spent / Remaining values.
    /// </summary>
    [Fact(DisplayName = "WAL-05 Overview data accuracy: SubscriptionBalance and per-child Allocated/Spent/Remaining")]
    public async Task WAL05_Overview_DataAccuracy_AfterGrantAndSpend()
    {
        var (parentId, parentToken) = await RegisterParentAsync("WAL05");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "WAL05C");

        const int grantAmount = 100;
        const int spendAmount = 15;
        var cycleStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd   = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentId, grantAmount, cycleStart, cycleEnd,
            $"wal05:{parentId}:{cycleStart:yyyyMM}");

        // Spend some energy
        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        await spendSvc.TryDebitAsync(
            childId, spendAmount, "AiDeepExplanation",
            $"wal05-spend:{childId}:{Guid.NewGuid():N}");

        // GET Overview
        var (resp, body, root) = await SendAsync(HttpMethod.Get, FamilyOverviewUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body={0}", body);

        TryProp(root, "data", out var data);
        TryProp(data, "subscriptionBalance", out var subBalEl);
        var subscriptionBalance = subBalEl.GetInt32();
        subscriptionBalance.Should().Be(grantAmount,
            "SubscriptionBalance in wallet (deposit side) should still reflect grant amount; " +
            "actual={0}", subscriptionBalance);

        TryProp(data, "children", out var childrenEl);
        childrenEl.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        // Find this child
        ChildAllocationAssert? childData = null;
        foreach (var child in childrenEl.EnumerateArray())
        {
            TryProp(child, "childId", out var cidEl);
            if (cidEl.GetInt32() == childId)
            {
                TryProp(child, "allocated", out var allocEl);
                TryProp(child, "spent", out var spentEl);
                TryProp(child, "remaining", out var remEl);
                childData = new ChildAllocationAssert(
                    allocEl.GetInt32(), spentEl.GetInt32(), remEl.GetInt32());
                break;
            }
        }

        childData.Should().NotBeNull("child {0} must appear in Overview.children; body={1}", childId, body);
        childData!.Allocated.Should().Be(grantAmount,
            "child Allocated must equal grant={0}; actual={1}", grantAmount, childData.Allocated);
        childData.Spent.Should().Be(spendAmount,
            "child Spent must equal spendAmount={0}; actual={1}", spendAmount, childData.Spent);
        childData.Remaining.Should().Be(grantAmount - spendAmount,
            "child Remaining must be {0}; actual={1}", grantAmount - spendAmount, childData.Remaining);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 7 — ADMIN FAMILY-WALLET GRANT (re-homed from per-child CreditAccount)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ADMIN-GRANT-01: POST /Credits/Grant with a valid admin JWT deposits into the family wallet
    /// PurchasedBalance (permanent admin comp) and writes an immutable ledger row.
    /// Does NOT require a pre-existing FamilyEnergyAccount (creates one if absent).
    /// </summary>
    [Fact(DisplayName = "ADMIN-GRANT-01 Family-wallet admin comp: deposits into PurchasedBalance + ledger row written")]
    public async Task ADMIN_GRANT01_FamilyWalletGrant_DepositsToPurchasedBalance()
    {
        var (parentId, _) = await RegisterParentAsync("AG01");

        // Get admin token
        var (_, _, adminRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = AdminUserName,
            Password = AdminPassword,
        });
        TryProp(adminRoot, "data", out var adminData);
        TryProp(adminData, "accessToken", out var adminTokEl);
        var adminToken = adminTokEl.GetString()!;

        var idempotencyKey = $"admin-grant:{parentId}:{Guid.NewGuid():N}";
        const int grantAmount = 250;

        var (resp, body, root) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantAmount,
            IdempotencyKey = idempotencyKey,
        }, adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin grant must return 200; body={0}", body);

        TryProp(root, "successed", out var succEl);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);

        // Verify PurchasedBalance on the family wallet
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

        wallet.Should().NotBeNull("FamilyEnergyAccount must be created by admin grant when absent");
        wallet!.PurchasedBalance.Should().Be(grantAmount,
            "admin comp must land in PurchasedBalance; expected={0}, actual={1}",
            grantAmount, wallet.PurchasedBalance);

        // Verify ledger row written
        var ledgerTx = await db.CreditTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.FamilyEnergyAccountId == wallet.Id &&
                t.IdempotencyKey == idempotencyKey);

        ledgerTx.Should().NotBeNull("immutable ledger row must be written for admin grant");
        ledgerTx!.Amount.Should().Be(grantAmount, "ledger row amount must equal grant amount");
        ledgerTx.ReasonCode.Should().Be(CreditReasonCode.AdminCredit, "reason code must be AdminCredit");
        ledgerTx.Pool.Should().Be(CreditPool.Purchased, "admin comp lands in Purchased pool");
    }

    /// <summary>
    /// ADMIN-GRANT-02: Same idempotencyKey → DuplicateIdempotent; PurchasedBalance NOT doubled.
    /// </summary>
    [Fact(DisplayName = "ADMIN-GRANT-02 Family-wallet admin comp idempotent: same key → no double-credit")]
    public async Task ADMIN_GRANT02_FamilyWalletGrant_Idempotent_NoDuplicateCredit()
    {
        var (parentId, _) = await RegisterParentAsync("AG02");

        var (_, _, adminRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = AdminUserName,
            Password = AdminPassword,
        });
        TryProp(adminRoot, "data", out var adminData);
        TryProp(adminData, "accessToken", out var adminTokEl);
        var adminToken = adminTokEl.GetString()!;

        var idempotencyKey = $"admin-grant:{parentId}:{Guid.NewGuid():N}";
        const int grantAmount = 100;

        // First grant
        var (resp1, body1, _) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantAmount,
            IdempotencyKey = idempotencyKey,
        }, adminToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first grant; body={0}", body1);

        // Second grant with same key
        var (resp2, body2, root2) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantAmount,
            IdempotencyKey = idempotencyKey,
        }, adminToken);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "idempotent replay must return 200; body={0}", body2);

        TryProp(root2, "data", out var data2);
        TryProp(data2, "outcome", out var outcomeEl);
        // Outcome must be DuplicateIdempotent (3) — DebitOutcome.DuplicateIdempotent = 3
        outcomeEl.GetInt32().Should().Be(3, "outcome must be DuplicateIdempotent=3; body={0}", body2);

        // PurchasedBalance must NOT be doubled
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId);

        wallet!.PurchasedBalance.Should().Be(grantAmount,
            "MONEY DEFECT: PurchasedBalance must not be doubled on duplicate key; " +
            "expected={0}, actual={1}", grantAmount, wallet.PurchasedBalance);
    }

    /// <summary>
    /// ADMIN-GRANT-03: POST /Credits/Grant without auth → 401 Unauthorized.
    /// </summary>
    [Fact(DisplayName = "ADMIN-GRANT-03 Family-wallet grant without auth → 401")]
    public async Task ADMIN_GRANT03_FamilyWalletGrant_Anonymous_Returns401()
    {
        var (resp, body, _) = await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = 1,
            Amount         = 100,
            IdempotencyKey = $"anon:{Guid.NewGuid():N}",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Grant is [Authorize(Billing.Create)] — anon must get 401; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 8 — FAMILY WALLET RECONCILE (re-homed from per-child CreditAccount)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// RECONCILE-01: GET /Credits/Reconcile/{parentId} with admin JWT → 200 + ReconciliationResultDto
    /// that shows no drift immediately after a clean grant.
    /// </summary>
    [Fact(DisplayName = "RECONCILE-01 Family-wallet reconcile → 200 + no drift after clean grant")]
    public async Task RECONCILE01_FamilyWalletReconcile_NoDriftAfterGrant()
    {
        var (parentId, _) = await RegisterParentAsync("RC01");

        var (_, _, adminRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = AdminUserName,
            Password = AdminPassword,
        });
        TryProp(adminRoot, "data", out var adminData);
        TryProp(adminData, "accessToken", out var adminTokEl);
        var adminToken = adminTokEl.GetString()!;

        const int grantAmount = 150;
        var grantKey = $"admin-grant-rc01:{parentId}:{Guid.NewGuid():N}";

        // Admin comp grant
        await SendAsync(HttpMethod.Post, "api/Billing/Credits/Grant", new
        {
            ParentId       = parentId,
            Amount         = grantAmount,
            IdempotencyKey = grantKey,
        }, adminToken);

        // Reconcile
        var (resp, body, root) = await SendAsync(
            HttpMethod.Get, $"api/Billing/Credits/Reconcile/{parentId}", bearer: adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "reconcile must return 200; body={0}", body);

        TryProp(root, "successed", out var succEl);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);
        TryProp(root, "data", out var data);

        TryProp(data, "parentId", out var pidEl);
        pidEl.GetInt32().Should().Be(parentId, "parentId must match; body={0}", body);

        TryProp(data, "hasDrift", out var driftEl);
        driftEl.GetBoolean().Should().BeFalse("no drift expected immediately after clean grant; body={0}", body);

        TryProp(data, "storedPurchasedBalance", out var storedEl);
        storedEl.GetInt32().Should().Be(grantAmount,
            "storedPurchasedBalance must equal grantAmount={0}; actual={1}", grantAmount, storedEl.GetInt32());

        TryProp(data, "ledgerPurchasedBalance", out var ledgerEl);
        ledgerEl.GetInt32().Should().Be(grantAmount,
            "ledgerPurchasedBalance must equal grantAmount={0}; actual={1}", grantAmount, ledgerEl.GetInt32());
    }

    /// <summary>
    /// RECONCILE-02: GET /Credits/Reconcile/{parentId} for a non-existent wallet → 404.
    /// </summary>
    [Fact(DisplayName = "RECONCILE-02 Family-wallet reconcile → 404 when no wallet exists")]
    public async Task RECONCILE02_FamilyWalletReconcile_404_WhenNoWallet()
    {
        var (parentId, _) = await RegisterParentAsync("RC02");

        var (_, _, adminRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = AdminUserName,
            Password = AdminPassword,
        });
        TryProp(adminRoot, "data", out var adminData);
        TryProp(adminData, "accessToken", out var adminTokEl);
        var adminToken = adminTokEl.GetString()!;

        // No wallet provisioned for this parent
        var (resp, body, _) = await SendAsync(
            HttpMethod.Get, $"api/Billing/Credits/Reconcile/{parentId}", bearer: adminToken);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "reconcile for non-existent wallet must return 404; body={0}", body);
    }

    // Small record to hold assertion data
    private sealed record ChildAllocationAssert(int Allocated, int Spent, int Remaining);
}

// ReSharper disable InconsistentNaming
// P10-16 Family Energy Redistribution — Integration Tests
//
// Tests in this file exercise the running backend API (HTTP + service layer) for all
// P10-16 acceptance criteria using WebApplicationFactory<Program> + the shared
// Testcontainers Postgres container (LearnexiaWebAppFactory / [Collection("IntegrationTests")]).
//
// Coverage map (keyed to spec cases in the task brief):
//
//   ALLOC-GET-01   GetAllocation: 200 + envelope + per-child snapshot (allocated/spent/remaining)
//   ALLOC-GET-02   GetAllocation: mid-cycle child (no ChildEnergyAllocation row) → isMidCycleNoGrant=true / 0s
//   ALLOC-GET-03   GetAllocation: family-scoped — Parent B cannot see Parent A's children
//   ALLOC-GET-04   GetAllocation: child JWT → 401/403; anonymous → 401
//   ALLOC-GET-05   GetAllocation: wallet not yet provisioned → 404
//
//   TRANSFER-01    Transfer happy path: source.remaining decreases; dest.remaining increases; zero-sum
//   TRANSFER-02    Transfer happy path: paired ledger entries (TransferOut on source + TransferIn on dest) with same CorrelationId
//   TRANSFER-03    Transfer: bucket B (PurchasedBalance) is UNCHANGED after transfer
//   TRANSFER-04    Transfer IDOR: toChildId from another family → 403, no mutation
//   TRANSFER-05    Transfer IDOR: fromChildId from another family → 403, no mutation
//   TRANSFER-06    Transfer: amount > source.remaining → rejected (422)
//   TRANSFER-07    Transfer: amount = 0 → rejected by validator (422)
//   TRANSFER-08    Transfer: amount < 0 → rejected by validator (422)
//   TRANSFER-09    Transfer: fromChildId == toChildId → rejected by validator (422)
//   TRANSFER-10    Transfer: already-spent energy is not movable; only remaining is
//   TRANSFER-11    Transfer to mid-cycle destination (no allocation row) → row INSERTed; dest.remaining = amount
//   TRANSFER-12    Idempotency: same idempotencyKey twice → balances unchanged from first call
//   TRANSFER-13    Transfer: NoSeatLocked source → rejected (422)
//   TRANSFER-14    Transfer: NoSeatLocked destination → rejected (422)

using System.Net;
using System.Net.Http.Headers;
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
/// P10-16 Family Energy Redistribution integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] so
/// the Testcontainers Postgres container is shared across all P10 test classes within the same run.
/// <c>ApplyMigrationsAndSeedAsync</c> is idempotent — safe to call in <see cref="InitializeAsync"/>.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_16_FamilyAllocation_IntegrationTests : IAsyncLifetime
{
    // ── URL constants ────────────────────────────────────────────────────────────
    private const string RegisterParentUrl       = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl             = "api/Parent/Add-Child";
    private const string SignInUrl               = "api/Users/Authentication/Sign-In";
    private const string AllocationUrl           = "api/Billing/FamilyEnergy/Allocation";
    private const string TransferUrl             = "api/Billing/FamilyEnergy/Transfer";
    private const string SubscriptionUpgradeUrl  = "api/Billing/Subscription/Upgrade";
    private const string SubscriptionCheckoutUrl = "api/Billing/Subscription/Checkout";
    private const string WebhookProviderUrl      = "api/Billing/Webhooks/Provider";

    private const string ValidChildPassword  = "Child@Pass1";
    private const string TestSigningSecret   = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    // Default GlobalSettings values (from GlobalSettingsSeeder)
    private const int DefaultPremiumMonthlyPerSeat = 5000;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_16_FamilyAllocation_IntegrationTests(LearnexiaWebAppFactory factory)
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
        => $"p16_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@alloc.test";

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

    /// <summary>Registers a new parent and returns (parentId, parentToken).</summary>
    private async Task<(int ParentId, string ParentToken)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email         = email,
            Password      = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "parent registration must succeed; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        return (GetUserIdFromJwt(token), token);
    }

    /// <summary>
    /// Adds a child under <paramref name="parentToken"/> and returns (childId, childToken).
    /// Calls SeatTestSupport.GrantSeatsAsync first when <paramref name="ensureSeats"/> is true.
    /// </summary>
    private async Task<(int ChildId, string ChildToken)> AddChildAndSignInAsync(
        string parentToken,
        string tag,
        bool ensureSeats = false)
    {
        if (ensureSeats)
            await SeatTestSupport.GrantSeatsAsync(_factory, parentToken);

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
            "Add-Child must succeed; body={0}", addBody);

        var (signResp, signBody, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl, new
        {
            UserName = childEmail,
            Password = ValidChildPassword,
        });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "child sign-in must succeed; body={0}", signBody);
        TryProp(signRoot, "data", out var signData);
        TryProp(signData, "accessToken", out var tok);
        var childToken = tok.GetString()!;
        return (GetUserIdFromJwt(childToken), childToken);
    }

    /// <summary>
    /// Upgrades parent to Premium via Upgrade → Checkout → webhook flow.
    /// Returns the subscriptionId.
    /// </summary>
    private async Task<int> UpgradeParentToPremiumAsync(int parentId, string parentToken)
    {
        var (upResp, upBody, _) = await SendAsync(HttpMethod.Post, SubscriptionUpgradeUrl,
            new { BillingPeriod = 0 /* Monthly */ }, parentToken);
        upResp.IsSuccessStatusCode.Should().BeTrue("upgrade must succeed; body={0}", upBody);

        var (chResp, chBody, chRoot) = await SendAsync(HttpMethod.Post, SubscriptionCheckoutUrl,
            null, parentToken);
        chResp.IsSuccessStatusCode.Should().BeTrue("checkout must succeed; body={0}", chBody);
        TryProp(chRoot, "data", out var chData);
        TryProp(chData, "providerPaymentRef", out var provRefEl);
        var providerRef = provRefEl.GetString()!;

        var (rawBody, sig) = FakePaymentProvider.BuildSignedWebhookPayload(
            Guid.NewGuid().ToString("N"), "payment.succeeded", providerRef, 199m, TestSigningSecret);

        var whReq = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
        whReq.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        whReq.Headers.Add("X-Hmac-Signature", sig);
        var whResp = await _client.SendAsync(whReq);
        whResp.IsSuccessStatusCode.Should().BeTrue("subscription webhook must succeed");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var sub = await db.Subscriptions.AsNoTracking()
            .FirstAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);
        return sub.Id;
    }

    /// <summary>
    /// Grants a subscription allocation to <paramref name="parentId"/> via the service layer
    /// (bypasses the Hangfire job). Returns the AllocationResult.
    /// </summary>
    private async Task<AllocationResult> AllocateGrantAsync(
        int parentId, int grantAmount, DateTime cycleStart, DateTime cycleEnd, string idempotencyKey)
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IFamilyEnergyAllocationService>();
        return await svc.AllocateSubscriptionGrantAsync(
            parentId, grantAmount, cycleStart, cycleEnd, idempotencyKey);
    }

    /// <summary>Decodes the integer user id from a Learnexia JWT (the "Id" claim).</summary>
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
    /// Sets up a parent with one (or two) children who each have a non-zero allocation.
    /// Returns the (parentId, parentToken, child1Id, child2Id, walletId, cycleStart, grantAmount).
    /// </summary>
    private async Task<(int ParentId, string ParentToken, int Child1Id, int Child2Id,
        int WalletId, DateTime CycleStart, int GrantAmount)>
        SetupTwoChildFamilyWithAllocationsAsync(string tag, int grantPerChild = 100)
    {
        var (parentId, parentToken) = await RegisterParentAsync(tag);
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId, extraSeats: 0);
        var (child1Id, _) = await AddChildAndSignInAsync(parentToken, $"{tag}C1");
        var (child2Id, _) = await AddChildAndSignInAsync(parentToken, $"{tag}C2");

        var totalGrant = grantPerChild * 2;
        var cycleStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd = cycleStart.AddMonths(1).AddSeconds(-1);
        var result = await AllocateGrantAsync(
            parentId, totalGrant, cycleStart, cycleEnd, $"setup:{tag}:{parentId}:{cycleStart:yyyyMM}");
        result.Succeeded.Should().BeTrue("setup: allocation must succeed");

        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts.AsNoTracking()
            .FirstAsync(w => w.ParentUserId == parentId);

        return (parentId, parentToken, child1Id, child2Id, wallet.Id, cycleStart, totalGrant);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 1 — GET ALLOCATION ENDPOINT (ALLOC-GET-01 .. ALLOC-GET-05)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ALLOC-GET-01: GET /api/Billing/FamilyEnergy/Allocation with a valid parent JWT after a grant
    /// has been issued → 200 + BaseResponse envelope + children array with allocated/spent/remaining.
    /// </summary>
    [Fact(DisplayName = "ALLOC-GET-01 GetAllocation returns 200 + envelope + per-child snapshot")]
    public async Task ALLOC_GET_01_GetAllocation_Returns200_WithPerChildSnapshot()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("AG01");

        var (resp, body, root) = await SendAsync(HttpMethod.Get, AllocationUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ALLOC-GET-01: authenticated parent must get 200; body={0}", body);

        // Envelope shape
        root.TryGetProperty("statusCode", out _).Should().BeTrue(
            "envelope must have statusCode; body={0}", body);
        TryProp(root, "successed", out var succEl).Should().BeTrue(
            "envelope must have successed; body={0}", body);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);
        TryProp(root, "data", out var data).Should().BeTrue(
            "envelope must have data; body={0}", body);

        // data.children array
        TryProp(data, "children", out var childrenEl).Should().BeTrue(
            "ALLOC-GET-01 DEFECT: data must have children array; body={0}", body);
        childrenEl.ValueKind.Should().Be(JsonValueKind.Array,
            "children must be an array; body={0}", body);
        childrenEl.GetArrayLength().Should().Be(2,
            "ALLOC-GET-01: two active-seat children must be returned; body={0}", body);

        // Per-child fields: childId, allocated, spent, remaining, isMidCycleNoGrant
        foreach (var child in childrenEl.EnumerateArray())
        {
            TryProp(child, "childId", out _).Should().BeTrue(
                "ALLOC-GET-01 DEFECT: child item must have childId; body={0}", body);
            TryProp(child, "allocated", out var allocEl).Should().BeTrue(
                "ALLOC-GET-01 DEFECT: child item must have allocated; body={0}", body);
            TryProp(child, "spent", out _).Should().BeTrue(
                "ALLOC-GET-01 DEFECT: child item must have spent; body={0}", body);
            TryProp(child, "remaining", out var remEl).Should().BeTrue(
                "ALLOC-GET-01 DEFECT: child item must have remaining; body={0}", body);
            TryProp(child, "isMidCycleNoGrant", out var midCycleEl).Should().BeTrue(
                "ALLOC-GET-01 DEFECT: child item must have isMidCycleNoGrant; body={0}", body);

            allocEl.GetInt32().Should().BeGreaterThan(0,
                "ALLOC-GET-01: each child must have allocated > 0 after a grant; body={0}", body);
            remEl.GetInt32().Should().BeGreaterThan(0,
                "ALLOC-GET-01: remaining must be > 0 before any spend; body={0}", body);
            midCycleEl.GetBoolean().Should().BeFalse(
                "ALLOC-GET-01: isMidCycleNoGrant must be false for grant-bearing children; body={0}", body);
        }
    }

    /// <summary>
    /// ALLOC-GET-02: A mid-cycle-added child (active seat but NO ChildEnergyAllocation row for the
    /// current cycle) appears in the result with allocated=0 / spent=0 / remaining=0 /
    /// isMidCycleNoGrant=true.
    /// </summary>
    [Fact(DisplayName = "ALLOC-GET-02 GetAllocation: mid-cycle child appears with isMidCycleNoGrant=true")]
    public async Task ALLOC_GET_02_GetAllocation_MidCycleChild_HasNoGrantFlag()
    {
        // Parent setup: grant allocation to child1 only (child2 will be mid-cycle with no grant row)
        var (parentId, parentToken) = await RegisterParentAsync("AG02");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId, extraSeats: 0);
        var (child1Id, _) = await AddChildAndSignInAsync(parentToken, "AG02C1");

        // Issue a grant BEFORE adding child2 — so child2 has no allocation row for this cycle
        var cycleStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd = cycleStart.AddMonths(1).AddSeconds(-1);
        var grant = await AllocateGrantAsync(
            parentId, 100, cycleStart, cycleEnd, $"ag02:{parentId}:{cycleStart:yyyyMM}");
        grant.Succeeded.Should().BeTrue("grant must succeed for child1 setup; body=N/A");

        // Now add child2 AFTER the grant (mid-cycle)
        var (child2Id, _) = await AddChildAndSignInAsync(parentToken, "AG02C2");

        // GET Allocation — child2 must appear with isMidCycleNoGrant=true
        var (resp, body, root) = await SendAsync(HttpMethod.Get, AllocationUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ALLOC-GET-02: must return 200; body={0}", body);

        TryProp(root, "data", out var data);
        TryProp(data, "children", out var childrenEl);
        childrenEl.ValueKind.Should().Be(JsonValueKind.Array,
            "children must be array; body={0}", body);

        // Find child2's item
        JsonElement? child2Item = null;
        foreach (var child in childrenEl.EnumerateArray())
        {
            TryProp(child, "childId", out var cidEl);
            if (cidEl.ValueKind == JsonValueKind.Number && cidEl.GetInt32() == child2Id)
            {
                child2Item = child;
                break;
            }
        }

        child2Item.Should().NotBeNull(
            "ALLOC-GET-02 DEFECT: mid-cycle child2 must appear in the GetAllocation response; " +
            "child2Id={0}; body={1}", child2Id, body);

        TryProp(child2Item!.Value, "allocated", out var allocEl);
        TryProp(child2Item.Value, "spent", out var spentEl);
        TryProp(child2Item.Value, "remaining", out var remEl);
        TryProp(child2Item.Value, "isMidCycleNoGrant", out var midCycleEl);

        allocEl.GetInt32().Should().Be(0,
            "ALLOC-GET-02 DEFECT: mid-cycle child must have allocated=0; body={0}", body);
        spentEl.GetInt32().Should().Be(0,
            "ALLOC-GET-02 DEFECT: mid-cycle child must have spent=0; body={0}", body);
        remEl.GetInt32().Should().Be(0,
            "ALLOC-GET-02 DEFECT: mid-cycle child must have remaining=0; body={0}", body);
        midCycleEl.GetBoolean().Should().BeTrue(
            "ALLOC-GET-02 DEFECT: mid-cycle child must have isMidCycleNoGrant=true; body={0}", body);
    }

    /// <summary>
    /// ALLOC-GET-03: GetAllocation is family-scoped. Parent B's JWT only returns their own children —
    /// they cannot see Parent A's children (no IDOR).
    /// </summary>
    [Fact(DisplayName = "ALLOC-GET-03 GetAllocation: family-scoped (no IDOR)")]
    public async Task ALLOC_GET_03_GetAllocation_FamilyScoped_NoIDOR()
    {
        // Parent A with 2 children + allocation
        var (parentAId, parentAToken, childAId, _, walletAId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("AG03A");

        // Parent B with their own child + allocation
        var (parentBId, parentBToken) = await RegisterParentAsync("AG03B");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentBId, extraSeats: 0);
        var (childBId, _) = await AddChildAndSignInAsync(parentBToken, "AG03BC");

        var cycleStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentBId, 50, cycleStart, cycleEnd,
            $"ag03b:{parentBId}:{cycleStart:yyyyMM}");

        // Parent B calls GET Allocation — must not see Parent A's children
        var (resp, body, root) = await SendAsync(HttpMethod.Get, AllocationUrl, bearer: parentBToken);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "ALLOC-GET-03: Parent B must get 200; body={0}", body);

        TryProp(root, "data", out var data);
        TryProp(data, "children", out var childrenEl);

        var childIds = childrenEl.EnumerateArray()
            .Select(c =>
            {
                TryProp(c, "childId", out var cidEl);
                return cidEl.ValueKind == JsonValueKind.Number ? cidEl.GetInt32() : -1;
            })
            .ToList();

        childIds.Should().NotContain(childAId,
            "ALLOC-GET-03 DEFECT: Parent B must NOT see Parent A's child in GetAllocation; " +
            "childAId={0} leaked; body={1}", childAId, body);
    }

    /// <summary>
    /// ALLOC-GET-04: GetAllocation requires an authenticated parent JWT.
    /// - Anonymous → 401
    /// - Child JWT → 401 or 403
    /// </summary>
    [Fact(DisplayName = "ALLOC-GET-04 GetAllocation: anon → 401; child JWT → 401/403")]
    public async Task ALLOC_GET_04_GetAllocation_AnonymousAndChildJwt_Rejected()
    {
        // Anonymous
        var (anonResp, anonBody, _) = await SendAsync(HttpMethod.Get, AllocationUrl);
        anonResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "ALLOC-GET-04: anonymous must get 401; body={0}", anonBody);

        // Child JWT
        var (parentId, parentToken) = await RegisterParentAsync("AG04P");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId);
        var (_, childToken) = await AddChildAndSignInAsync(parentToken, "AG04C");

        var (childResp, childBody, _) = await SendAsync(HttpMethod.Get, AllocationUrl, bearer: childToken);
        // DEFECT P10-16-AUTH-01: The controller uses [Authorize] without a role restriction.
        // A child JWT (role=Student) reaches the handler and receives 404 ("wallet not found")
        // instead of 401/403 (wrong role). The spec requires parent-only access.
        // Fix: add [Authorize(Roles="Parent")] to FamilyEnergyController.
        ((int)childResp.StatusCode).Should().BeOneOf([401, 403],
            "ALLOC-GET-04 DEFECT P10-16-AUTH-01: child JWT (role=Student) must be rejected with 401 or 403 " +
            "(FamilyEnergyController is missing [Authorize(Roles=\"Parent\")]; currently returns 404); " +
            "actual={0}; body={1}", (int)childResp.StatusCode, childBody);
    }

    /// <summary>
    /// ALLOC-GET-05: A parent with no FamilyEnergyAccount yet → 404.
    /// </summary>
    [Fact(DisplayName = "ALLOC-GET-05 GetAllocation: no wallet yet → 404")]
    public async Task ALLOC_GET_05_GetAllocation_NoWallet_Returns404()
    {
        var (_, parentToken) = await RegisterParentAsync("AG05");
        // No children, no grant, no wallet

        var (resp, body, _) = await SendAsync(HttpMethod.Get, AllocationUrl, bearer: parentToken);
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "ALLOC-GET-05: parent with no wallet must get 404; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 2 — TRANSFER HAPPY PATH (TRANSFER-01, TRANSFER-02, TRANSFER-03)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-01: Transfer happy path — parent moves amount from child1 → child2.
    /// source.remaining decreases by amount; dest.remaining increases by amount.
    /// Family total (bucket A remaining) is unchanged — zero-sum.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-01 Transfer happy path: source.remaining−=amount; dest.remaining+=amount; zero-sum")]
    public async Task TRANSFER_01_TransferHappyPath_BalancesUpdatedZeroSum()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, cycleStart, totalGrant) =
            await SetupTwoChildFamilyWithAllocationsAsync("TH01");

        // Read starting remainders
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<BillingDbContext>();

        var alloc1Before = await dbBefore.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();
        var alloc2Before = await dbBefore.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child2Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();

        var remaining1Before = alloc1Before.Remaining;
        var remaining2Before = alloc2Before.Remaining;
        var familyTotalBefore = remaining1Before + remaining2Before;

        const int transferAmount = 30;

        var (resp, body, root) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = transferAmount,
            IdempotencyKey = $"TH01:{Guid.NewGuid():N}",
        }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "TRANSFER-01: transfer must succeed with 200; body={0}", body);

        // Envelope shape
        TryProp(root, "successed", out var succEl);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);

        // Verify DB state
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();

        var alloc1After = await dbAfter.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();
        var alloc2After = await dbAfter.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child2Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();

        alloc1After.Remaining.Should().Be(remaining1Before - transferAmount,
            "TRANSFER-01 DEFECT: source remaining must decrease by transfer amount; " +
            "before={0}, after={1}, expected={2}",
            remaining1Before, alloc1After.Remaining, remaining1Before - transferAmount);

        alloc2After.Remaining.Should().Be(remaining2Before + transferAmount,
            "TRANSFER-01 DEFECT: dest remaining must increase by transfer amount; " +
            "before={0}, after={1}, expected={2}",
            remaining2Before, alloc2After.Remaining, remaining2Before + transferAmount);

        var familyTotalAfter = alloc1After.Remaining + alloc2After.Remaining;
        familyTotalAfter.Should().Be(familyTotalBefore,
            "TRANSFER-01 DEFECT: family total remaining must be unchanged (zero-sum); " +
            "before={0}, after={1}", familyTotalBefore, familyTotalAfter);
    }

    /// <summary>
    /// TRANSFER-02: Transfer creates PAIRED ledger entries:
    /// - One TransferOut row on the source child's allocation (ReasonCode=TransferOut)
    /// - One TransferIn row on the destination child's allocation (ReasonCode=TransferIn)
    /// Both rows share the SAME CorrelationId.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-02 Transfer: paired TransferOut+TransferIn ledger entries with same CorrelationId")]
    public async Task TRANSFER_02_TransferHappyPath_PairedLedgerWithSharedCorrelationId()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("TH02");

        const int transferAmount = 20;
        var idempotencyKey = $"TH02:{Guid.NewGuid():N}";

        // Count credit transactions before
        int txCountBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            txCountBefore = await db.CreditTransactions.CountAsync();
        }

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = transferAmount,
            IdempotencyKey = idempotencyKey,
        }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "TRANSFER-02 prerequisite: transfer must succeed; body={0}", body);

        // Verify ledger entries
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Find the TransferOut row (type=Spend, reasonCode=TransferOut, linked to child1's allocation)
        var child1AllocId = (await dbAfter.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync()).Id;

        var child2AllocId = (await dbAfter.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child2Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync()).Id;

        var transferOutRow = await dbAfter.CreditTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.ChildEnergyAllocationId == child1AllocId
                && t.ReasonCode == CreditReasonCode.TransferOut);

        var transferInRow = await dbAfter.CreditTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.ChildEnergyAllocationId == child2AllocId
                && t.ReasonCode == CreditReasonCode.TransferIn);

        transferOutRow.Should().NotBeNull(
            "TRANSFER-02 DEFECT: a TransferOut ledger row must exist for the source child; " +
            "child1AllocId={0}, walletId={1}, body={2}", child1AllocId, walletId, body);

        transferInRow.Should().NotBeNull(
            "TRANSFER-02 DEFECT: a TransferIn ledger row must exist for the destination child; " +
            "child2AllocId={0}, walletId={1}, body={2}", child2AllocId, walletId, body);

        transferOutRow!.Amount.Should().Be(transferAmount,
            "TRANSFER-02 DEFECT: TransferOut amount must equal the transfer amount; " +
            "expected={0}, actual={1}", transferAmount, transferOutRow.Amount);

        transferInRow!.Amount.Should().Be(transferAmount,
            "TRANSFER-02 DEFECT: TransferIn amount must equal the transfer amount; " +
            "expected={0}, actual={1}", transferAmount, transferInRow.Amount);

        transferOutRow.CorrelationId.Should().NotBeNull(
            "TRANSFER-02 DEFECT: TransferOut must have a CorrelationId; row.IdempotencyKey={0}",
            transferOutRow.IdempotencyKey);

        transferInRow.CorrelationId.Should().NotBeNull(
            "TRANSFER-02 DEFECT: TransferIn must have a CorrelationId; row.IdempotencyKey={0}",
            transferInRow.IdempotencyKey);

        transferOutRow.CorrelationId.Should().Be(transferInRow.CorrelationId,
            "TRANSFER-02 DEFECT: TransferOut and TransferIn must share the SAME CorrelationId; " +
            "out={0}, in={1}", transferOutRow.CorrelationId, transferInRow.CorrelationId);

        // Exactly 2 new credit transaction rows written
        int txCountAfter;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            txCountAfter = await db.CreditTransactions.CountAsync();
        }
        (txCountAfter - txCountBefore).Should().Be(2,
            "TRANSFER-02 DEFECT: exactly 2 CreditTransaction rows must be written per transfer; " +
            "before={0}, after={1}", txCountBefore, txCountAfter);
    }

    /// <summary>
    /// TRANSFER-03: Transfer NEVER changes the shared purchased reserve (bucket B).
    /// FamilyEnergyAccount.PurchasedBalance must be identical before and after the transfer.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-03 Transfer: PurchasedBalance (bucket B) is UNCHANGED")]
    public async Task TRANSFER_03_Transfer_DoesNotTouchPurchasedBalance()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("TH03");

        // Seed a purchased balance to make the invariant detectable
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.FirstAsync(w => w.Id == walletId);
            wallet.PurchasedBalance = 500;
            await db.SaveChangesAsync(0);
        }

        int purchasedBefore;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            purchasedBefore = (await db.FamilyEnergyAccounts.AsNoTracking()
                .FirstAsync(w => w.Id == walletId)).PurchasedBalance;
        }

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = 10,
            IdempotencyKey = $"TH03:{Guid.NewGuid():N}",
        }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "TRANSFER-03 prerequisite: transfer must succeed; body={0}", body);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();
        var walletAfter = await dbAfter.FamilyEnergyAccounts.AsNoTracking()
            .FirstAsync(w => w.Id == walletId);

        walletAfter.PurchasedBalance.Should().Be(purchasedBefore,
            "TRANSFER-03 DEFECT: PurchasedBalance must NOT change after a transfer; " +
            "before={0}, after={1}. Transfer must touch bucket A only.",
            purchasedBefore, walletAfter.PurchasedBalance);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 3 — IDOR / CROSS-FAMILY GUARDS (TRANSFER-04, TRANSFER-05)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-04: IDOR — toChildId belongs to another parent's family → 403.
    /// Neither family's balances must be mutated.
    /// parentUserId is always JWT-resolved; a body-supplied parent id is never honoured.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-04 IDOR: toChildId from another family → 403; no mutation")]
    public async Task TRANSFER_04_IDOR_ToChildForeignFamily_Returns403_NoMutation()
    {
        // Parent A — source family
        var (parentAId, parentAToken, childA1Id, _, walletAId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("ID04A");

        // Parent B — owns childB1 (the IDOR target)
        var (parentBId, parentBToken) = await RegisterParentAsync("ID04B");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentBId, extraSeats: 0);
        var (childBId, _) = await AddChildAndSignInAsync(parentBToken, "ID04BC");

        var cycleStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentBId, 200, cycleStart, cycleEnd,
            $"id04b:{parentBId}:{cycleStart:yyyyMM}");

        // Read Parent B's child balance before the attempt
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<BillingDbContext>();

        var walletB = await dbBefore.FamilyEnergyAccounts.AsNoTracking()
            .FirstAsync(w => w.ParentUserId == parentBId);
        var allocBBefore = await dbBefore.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletB.Id && a.ChildId == childBId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync();

        // Parent A tries to transfer TO Parent B's child (cross-family)
        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = childA1Id,
            ToChildId      = childBId,    // IDOR target
            Amount         = 10,
            IdempotencyKey = $"ID04:{Guid.NewGuid():N}",
        }, parentAToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-04 DEFECT: cross-family transfer must be rejected; " +
            "actual status={0}; body={1}", (int)resp.StatusCode, body);

        ((int)resp.StatusCode).Should().BeOneOf([403, 400, 422, 424],
            "TRANSFER-04: cross-family transfer must return 403 (or 400/422/424); " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);

        // Verify no mutation on either family
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();

        // Parent A's child1 allocation must be unchanged
        var allocAAfter = await dbAfter.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletAId && a.ChildId == childA1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync();
        if (allocAAfter is not null && allocBBefore is not null)
        {
            // Parent B child must be unmodified
            var allocBAfter = await dbAfter.ChildEnergyAllocations.AsNoTracking()
                .Where(a => a.FamilyEnergyAccountId == walletB.Id && a.ChildId == childBId)
                .OrderByDescending(a => a.CycleStartUtc)
                .FirstOrDefaultAsync();

            allocBAfter!.Remaining.Should().Be(allocBBefore.Remaining,
                "TRANSFER-04 DEFECT: Parent B's child remaining must be unchanged after rejected IDOR; " +
                "before={0}, after={1}", allocBBefore.Remaining, allocBAfter.Remaining);
        }
    }

    /// <summary>
    /// TRANSFER-05: IDOR — fromChildId belongs to another parent's family → 403.
    /// Parent A tries to drain Parent B's child.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-05 IDOR: fromChildId from another family → 403; no mutation")]
    public async Task TRANSFER_05_IDOR_FromChildForeignFamily_Returns403()
    {
        // Parent A — has own children
        var (parentAId, parentAToken, childA1Id, childA2Id, _, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("ID05A");

        // Parent B — owns childB1
        var (parentBId, parentBToken) = await RegisterParentAsync("ID05B");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentBId, extraSeats: 0);
        var (childBId, _) = await AddChildAndSignInAsync(parentBToken, "ID05BC");

        var cycleStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd = cycleStart.AddMonths(1).AddSeconds(-1);
        await AllocateGrantAsync(parentBId, 200, cycleStart, cycleEnd,
            $"id05b:{parentBId}:{cycleStart:yyyyMM}");

        // Parent A tries to drain Parent B's child (childBId as fromChildId)
        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = childBId,    // IDOR: belongs to Parent B
            ToChildId      = childA1Id,
            Amount         = 10,
            IdempotencyKey = $"ID05:{Guid.NewGuid():N}",
        }, parentAToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-05 DEFECT: cross-family fromChildId must be rejected; " +
            "actual status={0}; body={1}", (int)resp.StatusCode, body);

        ((int)resp.StatusCode).Should().BeOneOf([403, 400, 422, 424],
            "TRANSFER-05: cross-family from must return 403/400/422/424; " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 4 — VALIDATION: AMOUNT GUARDS (TRANSFER-06..08)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-06: amount > source.remaining → rejected.
    /// Already-spent energy is not reclaimable; cap is the live remaining balance.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-06 Transfer: amount > source.remaining → rejected (422)")]
    public async Task TRANSFER_06_AmountExceedsRemaining_Rejected()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("VA06", grantPerChild: 50);

        // Read actual remaining for child1
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var alloc1 = await db.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();

        var overAmount = alloc1.Remaining + 1; // guaranteed to exceed remaining

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = overAmount,
            IdempotencyKey = $"VA06:{Guid.NewGuid():N}",
        }, parentToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-06 DEFECT: amount > remaining must be rejected; body={0}", body);
        ((int)resp.StatusCode).Should().BeOneOf([422, 400, 424],
            "TRANSFER-06: expected 422/400/424; actual={0}; body={1}",
            (int)resp.StatusCode, body);
    }

    /// <summary>
    /// TRANSFER-07: amount = 0 → validator rejects (422 from ValidationBehavior).
    /// </summary>
    [Fact(DisplayName = "TRANSFER-07 Transfer: amount=0 → validator 422")]
    public async Task TRANSFER_07_AmountZero_ValidatorRejects()
    {
        var (parentId, parentToken, child1Id, child2Id, _, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("VA07");

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = 0,
            IdempotencyKey = $"VA07:{Guid.NewGuid():N}",
        }, parentToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-07 DEFECT: amount=0 must be rejected; body={0}", body);
        ((int)resp.StatusCode).Should().BeOneOf([422, 400],
            "TRANSFER-07: validator must reject amount=0 with 422 or 400; " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);
    }

    /// <summary>
    /// TRANSFER-08: amount = -5 (negative) → validator rejects (422 from ValidationBehavior).
    /// </summary>
    [Fact(DisplayName = "TRANSFER-08 Transfer: amount<0 → validator 422")]
    public async Task TRANSFER_08_AmountNegative_ValidatorRejects()
    {
        var (parentId, parentToken, child1Id, child2Id, _, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("VA08");

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = -5,
            IdempotencyKey = $"VA08:{Guid.NewGuid():N}",
        }, parentToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-08 DEFECT: negative amount must be rejected; body={0}", body);
        ((int)resp.StatusCode).Should().BeOneOf([422, 400],
            "TRANSFER-08: validator must reject amount<0 with 422 or 400; " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);
    }

    /// <summary>
    /// TRANSFER-09: fromChildId == toChildId → validator rejects (422).
    /// </summary>
    [Fact(DisplayName = "TRANSFER-09 Transfer: fromChildId == toChildId → validator 422")]
    public async Task TRANSFER_09_SameSourceAndDest_ValidatorRejects()
    {
        var (parentId, parentToken, child1Id, _, _, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("VA09");

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child1Id,    // same
            Amount         = 10,
            IdempotencyKey = $"VA09:{Guid.NewGuid():N}",
        }, parentToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-09 DEFECT: same source and destination must be rejected; body={0}", body);
        ((int)resp.StatusCode).Should().BeOneOf([422, 400],
            "TRANSFER-09: validator must reject fromChildId==toChildId with 422 or 400; " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 5 — ONLY UNSPENT IS MOVABLE (TRANSFER-10)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-10: Already-spent energy cannot be moved.
    /// Set up a child who has spent some of their allocation; verify the transfer cap is
    /// source.remaining (not source.allocated) — trying to move more than remaining fails.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-10 Transfer: cap is source.remaining, not source.allocated; spent energy immovable")]
    public async Task TRANSFER_10_CapIsRemaining_SpentEnergyImmovable()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("UN10", grantPerChild: 60);

        // Simulate child1 spending some energy via the credit spend service
        const int spendAmount = 25;
        using var spendScope = _factory.Services.CreateScope();
        var spendSvc = spendScope.ServiceProvider.GetRequiredService<ICreditSpendService>();
        var debitResult = await spendSvc.TryDebitAsync(
            child1Id,
            spendAmount,
            "SpendAllocation",
            $"UN10-spend:{child1Id}:{Guid.NewGuid():N}");
        debitResult.Charged.Should().BeTrue(
            "TRANSFER-10 setup: spend must succeed; outcome={0}", debitResult.Outcome);

        // child1.Remaining is now (60 - 25) = 35.
        // Trying to move the full original allocated=60 must fail (amount=60 > remaining=35).
        var (respFail, bodyFail, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = 60,           // > remaining=35 — must be rejected
            IdempotencyKey = $"UN10-over:{Guid.NewGuid():N}",
        }, parentToken);

        respFail.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-10 DEFECT: moving more than remaining (allocated=60, spent=25, remaining=35) must fail; " +
            "body={0}", bodyFail);

        // Moving exactly remaining (35) must succeed
        var (respOk, bodyOk, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = 35,           // == remaining; must succeed
            IdempotencyKey = $"UN10-ok:{Guid.NewGuid():N}",
        }, parentToken);

        respOk.StatusCode.Should().Be(HttpStatusCode.OK,
            "TRANSFER-10 DEFECT: moving exactly remaining=35 must succeed; body={0}", bodyOk);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 6 — MID-CYCLE DESTINATION (TRANSFER-11)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-11: Transfer TO a mid-cycle destination (child with active seat but no allocation
    /// row for the current cycle) → service INSERTs a new allocation row; dest.remaining = amount.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-11 Transfer to mid-cycle destination: row INSERTed; dest.remaining = amount")]
    public async Task TRANSFER_11_Transfer_ToMidCycleDestination_InsertsRowAndSetsRemaining()
    {
        // Set up parent with child1 who has a grant (source), then add child2 mid-cycle (no grant row)
        var (parentId, parentToken) = await RegisterParentAsync("MC11");
        await SeatTestSupport.GrantSeatsAsync(_factory, parentId, extraSeats: 0);
        var (child1Id, _) = await AddChildAndSignInAsync(parentToken, "MC11C1");

        // Issue grant to child1 BEFORE adding child2
        var cycleStart = new DateTime(
            DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycleEnd = cycleStart.AddMonths(1).AddSeconds(-1);
        var grant = await AllocateGrantAsync(
            parentId, 100, cycleStart, cycleEnd, $"mc11:{parentId}:{cycleStart:yyyyMM}");
        grant.Succeeded.Should().BeTrue("setup grant must succeed");

        // Add child2 AFTER grant (mid-cycle, no allocation row)
        var (child2Id, _) = await AddChildAndSignInAsync(parentToken, "MC11C2");

        // Verify child2 has NO allocation row yet
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await dbBefore.FamilyEnergyAccounts.AsNoTracking()
            .FirstAsync(w => w.ParentUserId == parentId);

        var child2AllocBefore = await dbBefore.ChildEnergyAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.FamilyEnergyAccountId == wallet.Id && a.ChildId == child2Id);
        child2AllocBefore.Should().BeNull(
            "TRANSFER-11 setup: child2 must have NO allocation row before transfer");

        // Transfer 40 from child1 → child2 (mid-cycle)
        const int transferAmount = 40;
        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = transferAmount,
            IdempotencyKey = $"MC11:{Guid.NewGuid():N}",
        }, parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "TRANSFER-11 DEFECT: transfer to mid-cycle (zero-allocation) child must succeed with 200; body={0}", body);

        // Verify child2 now has an allocation row with remaining = transferAmount
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<BillingDbContext>();
        var child2AllocAfter = await dbAfter.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == wallet.Id && a.ChildId == child2Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync();

        child2AllocAfter.Should().NotBeNull(
            "TRANSFER-11 DEFECT: a ChildEnergyAllocation row must be INSERTed for the mid-cycle child; " +
            "child2Id={0}, walletId={1}", child2Id, wallet.Id);

        child2AllocAfter!.Remaining.Should().Be(transferAmount,
            "TRANSFER-11 DEFECT: mid-cycle dest remaining must equal transferAmount after transfer; " +
            "expected={0}, actual={1}", transferAmount, child2AllocAfter.Remaining);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 7 — IDEMPOTENCY (TRANSFER-12)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-12: Re-submitting the same request (same idempotencyKey) does NOT double-move.
    /// The second call returns the prior result, and balances are unchanged from the first call.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-12 Idempotency: same idempotencyKey twice → no double-move")]
    public async Task TRANSFER_12_Idempotency_SameKey_NoDoubleDMove()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("IK12", grantPerChild: 100);

        const int transferAmount = 30;
        var sharedKey = $"IK12:{Guid.NewGuid():N}";

        // First call
        var (resp1, body1, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = transferAmount,
            IdempotencyKey = sharedKey,
        }, parentToken);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK,
            "TRANSFER-12 setup: first call must succeed; body={0}", body1);

        // Capture balances after first call
        using var scope1 = _factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<BillingDbContext>();
        var alloc1After1 = await db1.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();
        var alloc2After1 = await db1.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child2Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();
        var remaining1AfterFirst = alloc1After1.Remaining;
        var remaining2AfterFirst = alloc2After1.Remaining;
        var txCountAfterFirst = await db1.CreditTransactions.CountAsync();

        // Second call — SAME idempotencyKey
        var (resp2, body2, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,
            Amount         = transferAmount,
            IdempotencyKey = sharedKey,   // same key!
        }, parentToken);

        resp2.IsSuccessStatusCode.Should().BeTrue(
            "TRANSFER-12: second call with same key must return 2xx (idempotent replay); body={0}", body2);

        // Balances must be identical to after the FIRST call
        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<BillingDbContext>();
        var alloc1After2 = await db2.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child1Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();
        var alloc2After2 = await db2.ChildEnergyAllocations.AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == walletId && a.ChildId == child2Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstAsync();
        var txCountAfterSecond = await db2.CreditTransactions.CountAsync();

        alloc1After2.Remaining.Should().Be(remaining1AfterFirst,
            "TRANSFER-12 DEFECT: source remaining must NOT change on idempotent replay; " +
            "after1st={0}, after2nd={1}", remaining1AfterFirst, alloc1After2.Remaining);

        alloc2After2.Remaining.Should().Be(remaining2AfterFirst,
            "TRANSFER-12 DEFECT: dest remaining must NOT change on idempotent replay; " +
            "after1st={0}, after2nd={1}", remaining2AfterFirst, alloc2After2.Remaining);

        txCountAfterSecond.Should().Be(txCountAfterFirst,
            "TRANSFER-12 DEFECT: no new CreditTransaction rows must be written on idempotent replay; " +
            "after1st={0}, after2nd={1}", txCountAfterFirst, txCountAfterSecond);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // AREA 8 — NOSEAT/LOCKED GUARD (TRANSFER-13, TRANSFER-14)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// TRANSFER-13: Transfer FROM a NoSeatLocked source child → rejected.
    /// The source child must hold an active seat.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-13 Transfer from NoSeatLocked source → rejected")]
    public async Task TRANSFER_13_SourceNoSeatLocked_Rejected()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("NS13");

        // Lock child1's seat state to NoSeatLocked directly in DB
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking()
                .FirstAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);
            var reservation = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == sub.Id && r.ChildId == child1Id);
            if (reservation is not null)
            {
                reservation.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,    // locked source
            ToChildId      = child2Id,
            Amount         = 10,
            IdempotencyKey = $"NS13:{Guid.NewGuid():N}",
        }, parentToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-13 DEFECT: transfer from NoSeatLocked child must be rejected; body={0}", body);
        ((int)resp.StatusCode).Should().BeOneOf([422, 400, 403, 424],
            "TRANSFER-13: expected 422/400/403/424; actual={0}; body={1}",
            (int)resp.StatusCode, body);
    }

    /// <summary>
    /// TRANSFER-14: Transfer TO a NoSeatLocked destination child → rejected.
    /// The destination child must hold an active seat.
    /// </summary>
    [Fact(DisplayName = "TRANSFER-14 Transfer to NoSeatLocked destination → rejected")]
    public async Task TRANSFER_14_DestinationNoSeatLocked_Rejected()
    {
        var (parentId, parentToken, child1Id, child2Id, walletId, _, _) =
            await SetupTwoChildFamilyWithAllocationsAsync("NS14");

        // Lock child2's seat state to NoSeatLocked
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var sub = await db.Subscriptions.AsNoTracking()
                .FirstAsync(s => s.ParentUserId == parentId && s.Status == SubscriptionStatus.Active);
            var reservation = await db.SeatReservations
                .FirstOrDefaultAsync(r => r.SubscriptionId == sub.Id && r.ChildId == child2Id);
            if (reservation is not null)
            {
                reservation.SeatState = SeatState.NoSeatLocked;
                await db.SaveChangesAsync(0);
            }
        }

        var (resp, body, _) = await SendAsync(HttpMethod.Post, TransferUrl, new
        {
            FromChildId    = child1Id,
            ToChildId      = child2Id,    // locked destination
            Amount         = 10,
            IdempotencyKey = $"NS14:{Guid.NewGuid():N}",
        }, parentToken);

        resp.IsSuccessStatusCode.Should().BeFalse(
            "TRANSFER-14 DEFECT: transfer to NoSeatLocked destination must be rejected; body={0}", body);
        ((int)resp.StatusCode).Should().BeOneOf([422, 400, 403, 424],
            "TRANSFER-14: expected 422/400/403/424; actual={0}; body={1}",
            (int)resp.StatusCode, body);
    }
}

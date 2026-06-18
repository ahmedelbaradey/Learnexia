// ReSharper disable InconsistentNaming
// P10-17 Refund Reconciliation — Integration Tests
//
// Verifies the Postgres DB round-trip for P10-17 acceptance criteria.
// Unit tests already cover the reconciliation MATH on SQLite; this file covers
// the real-ledger reconciliation, webhook-driven balance decrement, IDOR guards,
// bucket-A isolation, multi-pack FIFO, admin path, and idempotency/no-negative-balance
// invariants that require a live Postgres connection.
//
// Coverage map:
//   TC-RF-01  Quote (parent) — partial spend → refundable = PurchasedTotal − ConsumedPurchased
//   TC-RF-02  Quote (parent) — fully consumed → refundable = 0
//   TC-RF-03  Quote auth — anon → 401; child JWT not in Parent role → 403
//   TC-RF-04  Quote IDOR — parent quoting another family's payment → 404 (not their wallet)
//   TC-RF-05  Request → webhook settlement — happy path: idempotent Refund ledger row + PurchasedBalance decremented
//   TC-RF-06  Request + webhook idempotency — same refund.succeeded webhook replayed → no double-decrement
//   TC-RF-07  Bucket-A safety — subscription/granted rows EXCLUDED from refundable; PurchasedBalance ONLY decremented
//   TC-RF-08  Multi-pack FIFO — two packs, refund one → refundable = that payment's unused FIFO share
//   TC-RF-09  Admin path (BE-6) — admin JWT can quote + request refund for ANY family; Refund ledger row written
//   TC-RF-10  Admin IDOR guard — Parent JWT hitting admin route → 403
//   TC-RF-11  Refundable ≤ 0 guard — requesting refund when fully consumed → 422/424 rejected
//   TC-RF-12  Negative-balance clamp — if purchased was further spent between request and webhook,
//             webhook clamps: balance never goes negative; exact remainder debited

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Learnexia.IntegrationTests;

/// <summary>
/// P10-17 Refund Reconciliation integration tests.
///
/// Uses the shared <see cref="LearnexiaWebAppFactory"/> / [Collection("IntegrationTests")] container.
/// All setup is performed per-test via unique users/payments so tests remain independent within
/// the shared DB.
/// </summary>
[Collection("IntegrationTests")]
public sealed class P10_17_Refunds_IntegrationTests : IAsyncLifetime
{
    // ── Routes ────────────────────────────────────────────────────────────────────────────────
    private const string RegisterParentUrl  = "api/Users/Authentication/Register-Parent";
    private const string AddChildUrl        = "api/Parent/Add-Child";
    private const string SignInUrl          = "api/Users/Authentication/Sign-In";
    private const string WebhookProviderUrl = "api/Billing/Webhooks/Provider";

    // P10-17 routes (exact shapes from RefundController)
    private const string ParentQuoteRouteTemplate  = "api/Billing/Refunds/Quote/{0}";
    private const string ParentRequestUrl          = "api/Billing/Refunds/Request";
    private const string AdminQuoteRouteTemplate   = "api/Billing/Admin/Refunds/Quote/{0}";
    private const string AdminRequestUrl           = "api/Billing/Admin/Refunds/Request";

    private const string AdminUserName = "superadmin";
    private const string AdminPassword = "123Pa$$word!";
    private const string ValidChildPassword = "Child@Pass1";

    // Same signing secret as the other P10 test classes (wired in LearnexiaWebAppFactory)
    private const string TestSigningSecret = P10_W3_SubscriptionPayment_IntegrationTests.TestSigningSecret;

    // Default pack size seeded by GlobalSettingsSeeder
    private const int DefaultPackSize = 1000;

    private readonly LearnexiaWebAppFactory _factory;
    private readonly HttpClient _client;

    public P10_17_Refunds_IntegrationTests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ══════════════════════════════════════════════════════════════════════════════════════════

    private static string UniqueEmail(string tag = "")
        => $"rf17_{tag}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}@rf17.test";

    /// <summary>Case-insensitive JSON property lookup (handles camelCase and PascalCase).</summary>
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
        try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { /* non-JSON */ }
        return (resp, bodyStr, root);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var (resp, body, root) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = AdminUserName, Password = AdminPassword });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "admin sign-in; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        return tok.GetString()!;
    }

    private async Task<(int ParentId, string ParentToken)> RegisterParentAsync(string tag = "")
    {
        var email = UniqueEmail($"par_{tag}");
        var (resp, body, root) = await SendAsync(HttpMethod.Post, RegisterParentUrl, new
        {
            Email         = email,
            Password      = "Str0ng@Pass!",
            AcceptedTerms = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "parent register; body={0}", body);
        TryProp(root, "data", out var data);
        TryProp(data, "accessToken", out var tok);
        var token = tok.GetString()!;
        var id    = SeatTestSupport.DecodeUserId(token);
        return (id, token);
    }

    private async Task<(int ChildId, string ChildToken)> AddChildAndSignInAsync(
        string parentToken, string tag = "")
    {
        var email = UniqueEmail($"ch_{tag}");
        var (addResp, addBody, _) = await SendAsync(HttpMethod.Post, AddChildUrl, new
        {
            FullName         = $"Child {tag}",
            Email            = email,
            Password         = ValidChildPassword,
            Grade            = 4,
            Language         = "ar",
            Country          = "EG",
            LearningLanguage = "ar",
        }, parentToken);
        ((int)addResp.StatusCode).Should().BeOneOf([200, 201], "add-child; body={0}", addBody);

        var (signResp, signBody, signRoot) = await SendAsync(HttpMethod.Post, SignInUrl,
            new { UserName = email, Password = ValidChildPassword });
        signResp.StatusCode.Should().Be(HttpStatusCode.OK, "child sign-in; body={0}", signBody);
        TryProp(signRoot, "data", out var sd);
        TryProp(sd, "accessToken", out var t);
        var childToken = t.GetString()!;
        return (SeatTestSupport.DecodeUserId(childToken), childToken);
    }

    /// <summary>
    /// Seeds a Pack Payment row (bypasses checkout provider for speed).
    /// Returns (paymentId, providerRef).
    /// </summary>
    private async Task<(int PaymentId, string ProviderRef)> SeedPackPaymentAsync(
        int parentId, int? childId = null, decimal amount = 50m,
        PaymentStatus status = PaymentStatus.Initiated)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var providerRef = $"fake-pack-rf17-{Guid.NewGuid():N}";
        var payment = new Payment
        {
            ParentUserId       = parentId,
            TargetChildId      = childId,
            Amount             = amount,
            Currency           = PaymentCurrency.EGP,
            Status             = status,
            Kind               = PaymentKind.Pack,
            IdempotencyKey     = $"ik-rf17-{Guid.NewGuid():N}",
            ProviderPaymentRef = providerRef,
            CreatedAt          = DateTime.UtcNow,
            CreatedBy          = parentId,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(0);
        return (payment.Id, providerRef);
    }

    /// <summary>
    /// Posts a correctly-signed webhook and returns the response.
    /// </summary>
    private async Task<(HttpResponseMessage Resp, string Body, JsonElement Root)> PostWebhookAsync(
        string eventId, string eventType, string paymentRef, decimal amount,
        string? signingSecret = null)
    {
        var secret = signingSecret ?? TestSigningSecret;
        var (rawBody, sigHeader) = FakePaymentProvider.BuildSignedWebhookPayload(
            eventId, eventType, paymentRef, amount, secret);

        var req = new HttpRequestMessage(HttpMethod.Post, WebhookProviderUrl)
        {
            Content = new ByteArrayContent(rawBody),
        };
        req.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Hmac-Signature", sigHeader);

        var resp    = await _client.SendAsync(req);
        var bodyStr = await resp.Content.ReadAsStringAsync();
        JsonElement root = default;
        try { root = JsonDocument.Parse(bodyStr).RootElement; } catch { }
        return (resp, bodyStr, root);
    }

    /// <summary>
    /// Sends a pack purchase webhook (payment.succeeded) and asserts it returns 200.
    /// Returns the resulting webhook body.
    /// </summary>
    private async Task<string> SendPackPurchaseWebhookAsync(string providerRef)
    {
        var (resp, body, _) = await PostWebhookAsync(
            Guid.NewGuid().ToString("N"), "payment.succeeded", providerRef, 50m);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "pack purchase webhook; body={0}", body);
        return body;
    }

    /// <summary>
    /// Promotes a Payment row to Succeeded status directly in DB (simulates completed checkout).
    /// </summary>
    private async Task MarkPaymentSucceededAsync(int paymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var p  = await db.Payments.FirstAsync(x => x.Id == paymentId);
        p.Status = PaymentStatus.Succeeded;
        await db.SaveChangesAsync(0);
    }

    /// <summary>
    /// Performs a bucket-B spend directly on the FamilyEnergyAccount to simulate consumed
    /// purchased energy (bypasses the full spend path for targeted unit-level DB seeding).
    /// </summary>
    private async Task SeedPurchasedSpendAsync(
        int familyAccountId, int amount, string idempotencyKey)
    {
        using var scope = _factory.Services.CreateScope();
        var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var wallet = await db.FamilyEnergyAccounts.FirstAsync(w => w.Id == familyAccountId);

        var tx = wallet.DebitPurchasedFallback(amount, idempotencyKey);
        tx.FamilyEnergyAccountId = wallet.Id;
        db.CreditTransactions.Add(tx);
        await db.SaveChangesAsync(0);
    }

    /// <summary>
    /// Gets the FamilyEnergyAccount for a parent from DB. Returns null if not yet provisioned.
    /// </summary>
    private async Task<FamilyEnergyAccount?> GetWalletAsync(int parentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        return await db.FamilyEnergyAccounts.AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId);
    }

    /// <summary>
    /// Counts CreditTransaction rows of type Refund for the given familyAccountId.
    /// </summary>
    private async Task<int> CountRefundLedgerRowsAsync(int familyAccountId, string relatedPaymentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        return await db.CreditTransactions.AsNoTracking()
            .CountAsync(t =>
                t.FamilyEnergyAccountId == familyAccountId &&
                t.Type == CreditTransactionType.Refund &&
                t.RelatedPaymentId == relatedPaymentId);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-01: Quote (parent) — partial spend → refundable = PurchasedTotal − ConsumedPurchased
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-01 Quote: bought 10000, spent 3000 purchased → refundable=7000")]
    public async Task TC_RF_01_Quote_PartialSpend_RefundableIsUnused()
    {
        // ── Setup ─────────────────────────────────────────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("RF01");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF01C");

        // Seed a Pack Payment and send the pack purchase webhook to credit 1000 units (DefaultPackSize)
        // to FamilyEnergyAccount.PurchasedBalance and write the bucket-B Purchase ledger row.
        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);

        // The wallet now has PurchasedBalance = 1000 (DefaultPackSize).
        // Simulate 300 units of purchased spend (3000 energy = 300 units at the seeded pack size).
        // We use 300 so the fraction maths is clean.
        var wallet = await GetWalletAsync(parentId);
        wallet.Should().NotBeNull("wallet must exist after pack purchase webhook");
        int spendAmount = 300;
        await SeedPurchasedSpendAsync(wallet!.Id, spendAmount, $"rf01-spend:{parentId}:{Guid.NewGuid():N}");

        // ── GET Quote ────────────────────────────────────────────────────────────────────────
        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET Quote for parent with partial spend must return 200; body={0}", body);

        TryProp(root, "successed", out var succEl).Should().BeTrue("body={0}", body);
        succEl.GetBoolean().Should().BeTrue("successed must be true; body={0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body={0}", body);

        TryProp(data, "purchasedTotal", out var ptEl).Should().BeTrue("data.purchasedTotal; body={0}", body);
        ptEl.GetInt32().Should().Be(DefaultPackSize,
            "purchasedTotal must equal the PackSize={0}; actual={1}; body={2}",
            DefaultPackSize, ptEl.GetInt32(), body);

        TryProp(data, "consumedPurchased", out var cpEl).Should().BeTrue("data.consumedPurchased; body={0}", body);
        cpEl.GetInt32().Should().Be(spendAmount,
            "consumedPurchased must equal the spend={0}; actual={1}; body={2}",
            spendAmount, cpEl.GetInt32(), body);

        TryProp(data, "refundable", out var rfEl).Should().BeTrue("data.refundable; body={0}", body);
        var expectedRefundable = DefaultPackSize - spendAmount;  // 700
        rfEl.GetInt32().Should().Be(expectedRefundable,
            "refundable must be purchasedTotal({0}) − consumedPurchased({1}) = {2}; actual={3}; body={4}",
            DefaultPackSize, spendAmount, expectedRefundable, rfEl.GetInt32(), body);

        TryProp(data, "refundableAmount", out var raEl).Should().BeTrue("data.refundableAmount; body={0}", body);
        raEl.GetDecimal().Should().BeGreaterThan(0, "refundableAmount must be positive; body={0}", body);

        TryProp(data, "purchasePaymentId", out var ppIdEl).Should().BeTrue("data.purchasePaymentId; body={0}", body);
        ppIdEl.GetInt32().Should().Be(paymentId, "purchasePaymentId must match; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-02: Quote (parent) — fully consumed → refundable = 0
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-02 Quote: all purchased consumed → refundable=0")]
    public async Task TC_RF_02_Quote_FullyConsumed_RefundableIsZero()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF02");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF02C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);

        var wallet = await GetWalletAsync(parentId);
        wallet.Should().NotBeNull();

        // Spend the entire PurchasedBalance
        await SeedPurchasedSpendAsync(wallet!.Id, DefaultPackSize,
            $"rf02-spend:{parentId}:{Guid.NewGuid():N}");

        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "GET Quote for fully-consumed pack must still return 200; body={0}", body);

        TryProp(root, "data", out var data).Should().BeTrue("body={0}", body);

        TryProp(data, "refundable", out var rfEl).Should().BeTrue("data.refundable; body={0}", body);
        rfEl.GetInt32().Should().Be(0,
            "MONEY DEFECT: refundable must be 0 when all purchased is consumed; actual={0}; body={1}",
            rfEl.GetInt32(), body);

        TryProp(data, "refundableAmount", out var raEl).Should().BeTrue("data.refundableAmount; body={0}", body);
        raEl.GetDecimal().Should().Be(0m,
            "refundableAmount must be 0 when fully consumed; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-03: Auth guard — anon → 401; child JWT (not in Parent role) → 403
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-03 Auth: GET Quote anon → 401; POST Request anon → 401")]
    public async Task TC_RF_03_Auth_Anon_Returns401()
    {
        // No bearer token
        var (quoteResp, quoteBody, _) = await SendAsync(HttpMethod.Get,
            string.Format(ParentQuoteRouteTemplate, 999));

        quoteResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "GET Refunds/Quote requires [Authorize(Roles=Parent)] — anon must get 401; body={0}", quoteBody);

        var (requestResp, requestBody, _) = await SendAsync(HttpMethod.Post, ParentRequestUrl,
            new { PurchasePaymentId = 999, Reason = 1 });

        requestResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "POST Refunds/Request requires [Authorize(Roles=Parent)] — anon must get 401; body={0}", requestBody);
    }

    [Fact(DisplayName = "TC-RF-03b Auth: child JWT (Student role) hitting parent Quote route → 403")]
    public async Task TC_RF_03b_Auth_ChildJwt_Returns403()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF03B");
        var (_, childToken) = await AddChildAndSignInAsync(parentToken, "RF03BC");

        // Child JWT hitting the [Authorize(Roles="Parent")] route must be 403
        var (resp, body, _) = await SendAsync(HttpMethod.Get,
            string.Format(ParentQuoteRouteTemplate, 999), bearer: childToken);

        ((int)resp.StatusCode).Should().BeOneOf([401, 403],
            "child JWT must be rejected by [Authorize(Roles=Parent)] — expected 401 or 403; " +
            "actual={0}; body={1}", (int)resp.StatusCode, body);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-04: Quote IDOR — parent B quoting parent A's payment → 404 (not their wallet)
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-04 IDOR: parent B quoting parent A payment → 404")]
    public async Task TC_RF_04_Quote_IDOR_Returns404()
    {
        // Parent A: buy a pack
        var (parentAId, parentAToken) = await RegisterParentAsync("RF04A");
        var (childAId, _) = await AddChildAndSignInAsync(parentAToken, "RF04AC");
        var (paymentIdA, providerRefA) = await SeedPackPaymentAsync(parentAId, childAId);
        await SendPackPurchaseWebhookAsync(providerRefA);

        // Parent B: separate family, no pack
        var (_, parentBToken) = await RegisterParentAsync("RF04B");

        // Parent B trying to quote Parent A's payment ID → handler resolves wallet from JWT,
        // finds no purchase row in Parent B's wallet → 404 (payment not found in their scope).
        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentIdA);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentBToken);

        // The handler first tries to get Parent B's overview (wallet). Parent B has no wallet yet
        // (just registered, no pack) → 404 FamilyEnergyWalletNotFound.
        // Either way: must NOT return 200 with Parent A's refundable quote.
        resp.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "IDOR DEFECT: parent B must NOT receive a 200 quote for parent A's payment; " +
            "actual={0}; body={1}", resp.StatusCode, body);

        TryProp(root, "successed", out var succEl);
        succEl.GetBoolean().Should().BeFalse(
            "IDOR DEFECT: successed must be false when parent B queries parent A's payment; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-05: Request → webhook settlement — happy path
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-05 Request+Webhook: POST Request → provider accepted; refund.succeeded → Refund ledger + PurchasedBalance decremented")]
    public async Task TC_RF_05_Request_And_Webhook_Settlement_HappyPath()
    {
        // ── Setup: parent + child + pack purchase ──────────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("RF05");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF05C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        // Fire payment.succeeded webhook → PurchasedBalance becomes DefaultPackSize, Purchase ledger row written
        await SendPackPurchaseWebhookAsync(providerRef);
        // Mark payment as Succeeded so InitiateProviderRefundAsync passes its status check
        await MarkPaymentSucceededAsync(paymentId);

        var walletBefore = await GetWalletAsync(parentId);
        walletBefore.Should().NotBeNull();
        int purchasedBefore = walletBefore!.PurchasedBalance;
        purchasedBefore.Should().Be(DefaultPackSize, "PurchasedBalance must equal PackSize after purchase webhook");

        // ── POST /api/Billing/Refunds/Request — parent initiation ─────────────────────────────
        var (requestResp, requestBody, requestRoot) = await SendAsync(HttpMethod.Post, ParentRequestUrl,
            new { PurchasePaymentId = paymentId, Reason = (int)RefundReason.NoLongerNeeded }, parentToken);

        requestResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST Refunds/Request must return 200 when fully unspent; body={0}", requestBody);

        TryProp(requestRoot, "successed", out var succEl).Should().BeTrue("body={0}", requestBody);
        succEl.GetBoolean().Should().BeTrue("successed must be true on refund request; body={0}", requestBody);

        // ── Balance MUST NOT change yet (initiate-only; webhook drives the ledger change) ──────
        var walletAfterRequest = await GetWalletAsync(parentId);
        walletAfterRequest!.PurchasedBalance.Should().Be(purchasedBefore,
            "DEFECT: PurchasedBalance must NOT change on POST Request (initiate-only); " +
            "before={0}, after={1}", purchasedBefore, walletAfterRequest.PurchasedBalance);

        // ── No Refund ledger row yet ─────────────────────────────────────────────────────────
        int refundRowsBefore = await CountRefundLedgerRowsAsync(walletBefore.Id, paymentId.ToString());
        refundRowsBefore.Should().Be(0,
            "DEFECT: Refund ledger row must NOT be written before webhook settlement; found={0}", refundRowsBefore);

        // ── POST refund.succeeded webhook — settlement ─────────────────────────────────────────
        var refundEventId = $"rf-evt-{Guid.NewGuid():N}";
        var (webhookResp, webhookBody, webhookRoot) = await PostWebhookAsync(
            refundEventId, "refund.succeeded", providerRef, amount: 50m);

        webhookResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "refund.succeeded webhook must return 200; body={0}", webhookBody);

        // ── Ledger: exactly one Refund row written ─────────────────────────────────────────────
        int refundRowsAfter = await CountRefundLedgerRowsAsync(walletBefore.Id, paymentId.ToString());
        refundRowsAfter.Should().Be(1,
            "DEFECT: exactly one Refund ledger row must be written after refund.succeeded webhook; found={0}",
            refundRowsAfter);

        // Verify ledger row fields
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var refundTx = await db.CreditTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.FamilyEnergyAccountId == walletBefore.Id &&
                t.Type == CreditTransactionType.Refund &&
                t.RelatedPaymentId == paymentId.ToString());

        refundTx.Should().NotBeNull("Refund ledger row must exist after webhook settlement");
        refundTx!.SourceBucket.Should().Be(EnergyBucket.Purchased,
            "Refund row must be bucket B (Purchased)");
        refundTx.ReasonCode.Should().Be(CreditReasonCode.PurchasedRefund,
            "ReasonCode must be PurchasedRefund");
        refundTx.Amount.Should().BeGreaterThan(0,
            "Refund amount must be positive");

        // ── PurchasedBalance decremented by the refund amount ─────────────────────────────────
        var walletAfterWebhook = await GetWalletAsync(parentId);
        int expectedAfter = purchasedBefore - refundTx.Amount;
        walletAfterWebhook!.PurchasedBalance.Should().Be(expectedAfter,
            "MONEY DEFECT: PurchasedBalance must decrease by the refunded amount={0}; " +
            "before={1}, expected={2}, actual={3}",
            refundTx.Amount, purchasedBefore, expectedAfter, walletAfterWebhook.PurchasedBalance);

        walletAfterWebhook.PurchasedBalance.Should().BeGreaterThanOrEqualTo(0,
            "MONEY DEFECT: PurchasedBalance must never go negative");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-06: Webhook idempotency — same refund.succeeded replayed → no double-decrement
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-06 Idempotency: same refund.succeeded webhook replayed → no double-decrement")]
    public async Task TC_RF_06_Webhook_Idempotency_NoDoubleDecrement()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF06");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF06C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        var walletBefore = await GetWalletAsync(parentId);
        int purchasedBeforeRefund = walletBefore!.PurchasedBalance;

        // ── First refund.succeeded webhook ────────────────────────────────────────────────────
        var refundEventId = $"rf-dedup-{Guid.NewGuid():N}";
        var (resp1, body1, _) = await PostWebhookAsync(
            refundEventId, "refund.succeeded", providerRef, 50m);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first refund webhook; body={0}", body1);

        int balanceAfterFirst = (await GetWalletAsync(parentId))!.PurchasedBalance;

        // ── Second replay of the SAME refund event ────────────────────────────────────────────
        var (resp2, body2, _) = await PostWebhookAsync(
            refundEventId, "refund.succeeded", providerRef, 50m);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second (replay) refund webhook must return 200 no-op; body={0}", body2);

        int balanceAfterSecond = (await GetWalletAsync(parentId))!.PurchasedBalance;

        // ── Balance must NOT be decremented twice ─────────────────────────────────────────────
        balanceAfterSecond.Should().Be(balanceAfterFirst,
            "IDEMPOTENCY DEFECT: PurchasedBalance must NOT be decremented twice on duplicate refund webhook; " +
            "after first={0}, after second={1}", balanceAfterFirst, balanceAfterSecond);

        // ── Exactly ONE Refund ledger row for this payment ────────────────────────────────────
        int refundRowCount = await CountRefundLedgerRowsAsync(walletBefore.Id, paymentId.ToString());
        refundRowCount.Should().Be(1,
            "IDEMPOTENCY DEFECT: exactly ONE Refund ledger row must exist; found={0}", refundRowCount);

        // ── Exactly ONE WebhookEvent row ──────────────────────────────────────────────────────
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        int webhookEventCount = await db.WebhookEvents.AsNoTracking()
            .CountAsync(w => w.ProviderEventId == refundEventId);
        webhookEventCount.Should().Be(1,
            "IDEMPOTENCY DEFECT: exactly ONE WebhookEvent row per refundEventId; found={0}",
            webhookEventCount);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-07: Bucket-A safety — subscription/granted rows EXCLUDED; PurchasedBalance ONLY
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-07 Bucket-A safety: subscription balance untouched after refund webhook")]
    public async Task TC_RF_07_BucketA_NotTouched_By_PackRefund()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF07");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF07C");

        // Seed both buckets: pack purchase (bucket B) + subscription grant (bucket A).
        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        // Seed subscription balance directly (simulates monthly grant)
        int subscriptionGrantAmount = 200;
        {
            using var scope = _factory.Services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var wallet = await db.FamilyEnergyAccounts.FirstAsync(w => w.ParentUserId == parentId);
            wallet.SubscriptionBalance += subscriptionGrantAmount;
            await db.SaveChangesAsync(0);
        }

        var walletBefore = await GetWalletAsync(parentId);
        int subBalanceBefore = walletBefore!.SubscriptionBalance;
        int purchasedBefore  = walletBefore.PurchasedBalance;

        subBalanceBefore.Should().BeGreaterThanOrEqualTo(subscriptionGrantAmount,
            "SubscriptionBalance must include the seeded grant before refund");
        purchasedBefore.Should().Be(DefaultPackSize, "PurchasedBalance must be DefaultPackSize before refund");

        // ── Fire refund.succeeded webhook ─────────────────────────────────────────────────────
        var (resp, body, _) = await PostWebhookAsync(
            $"rf07-evt-{Guid.NewGuid():N}", "refund.succeeded", providerRef, 50m);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, "refund webhook; body={0}", body);

        var walletAfter = await GetWalletAsync(parentId);

        // ── Subscription balance MUST be UNCHANGED (bucket A must NEVER be touched) ──────────
        walletAfter!.SubscriptionBalance.Should().Be(subBalanceBefore,
            "BUCKET-A DEFECT: SubscriptionBalance must be UNCHANGED after pack refund webhook; " +
            "before={0}, after={1}", subBalanceBefore, walletAfter.SubscriptionBalance);

        // ── PurchasedBalance must have decreased (bucket B was refunded) ─────────────────────
        walletAfter.PurchasedBalance.Should().BeLessThan(purchasedBefore,
            "DEFECT: PurchasedBalance must decrease after refund webhook; " +
            "before={0}, after={1}", purchasedBefore, walletAfter.PurchasedBalance);

        // ── No bucket-A Refund ledger rows must exist ─────────────────────────────────────────
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        bool bucketARefundExists = await verifyDb.CreditTransactions.AsNoTracking()
            .AnyAsync(t =>
                t.FamilyEnergyAccountId == walletBefore.Id &&
                t.Type == CreditTransactionType.Refund &&
                t.SourceBucket == EnergyBucket.Subscription);

        bucketARefundExists.Should().BeFalse(
            "BUCKET-A DEFECT: a Refund ledger row must NEVER be written with SourceBucket=Subscription; " +
            "pack refunds are bucket B only");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-08: Multi-pack FIFO — two packs; refund one → refundable = FIFO-attributed unused share
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-08 Multi-pack FIFO: two packs, spend partially, refund pack-1 → FIFO-attributable refundable")]
    public async Task TC_RF_08_MultiPack_FIFO_RefundableIsPaymentSpecific()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF08");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF08C");

        // Pack 1: buy 1000 units (DefaultPackSize)
        var (paymentId1, providerRef1) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef1);

        // Pack 2: buy another 1000 units
        var (paymentId2, providerRef2) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef2);

        // Wallet now has PurchasedBalance = 2000.
        // Spend 300 units total (FIFO: 300 consumed from pack-1's attribution)
        var wallet = await GetWalletAsync(parentId);
        int spendAmount = 300;
        await SeedPurchasedSpendAsync(wallet!.Id, spendAmount,
            $"rf08-spend:{parentId}:{Guid.NewGuid():N}");

        // ── Quote for pack-1 ─────────────────────────────────────────────────────────────────
        var quoteUrl1 = string.Format(ParentQuoteRouteTemplate, paymentId1);
        var (resp1, body1, root1) = await SendAsync(HttpMethod.Get, quoteUrl1, bearer: parentToken);

        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "quote pack-1; body={0}", body1);
        TryProp(root1, "data", out var data1).Should().BeTrue("body={0}", body1);

        TryProp(data1, "purchasedTotal", out var pt1).Should().BeTrue("body={0}", body1);
        TryProp(data1, "consumedPurchased", out var cp1).Should().BeTrue("body={0}", body1);
        TryProp(data1, "refundable", out var rf1).Should().BeTrue("body={0}", body1);

        // FIFO: pack-1 is the older purchase. 300 spend is fully attributed to pack-1.
        pt1.GetInt32().Should().Be(DefaultPackSize,
            "pack-1 purchasedTotal must be DefaultPackSize={0}; actual={1}; body={2}",
            DefaultPackSize, pt1.GetInt32(), body1);
        cp1.GetInt32().Should().Be(spendAmount,
            "pack-1 consumedPurchased must equal 300 (FIFO attribution); actual={0}; body={1}",
            cp1.GetInt32(), body1);
        rf1.GetInt32().Should().Be(DefaultPackSize - spendAmount,   // 700
            "pack-1 refundable must be {0} (FIFO); actual={1}; body={2}",
            DefaultPackSize - spendAmount, rf1.GetInt32(), body1);

        // ── Quote for pack-2 ─────────────────────────────────────────────────────────────────
        var quoteUrl2 = string.Format(ParentQuoteRouteTemplate, paymentId2);
        var (resp2, body2, root2) = await SendAsync(HttpMethod.Get, quoteUrl2, bearer: parentToken);

        resp2.StatusCode.Should().Be(HttpStatusCode.OK, "quote pack-2; body={0}", body2);
        TryProp(root2, "data", out var data2).Should().BeTrue("body={0}", body2);

        TryProp(data2, "purchasedTotal", out var pt2).Should().BeTrue("body={0}", body2);
        TryProp(data2, "consumedPurchased", out var cp2).Should().BeTrue("body={0}", body2);
        TryProp(data2, "refundable", out var rf2).Should().BeTrue("body={0}", body2);

        // FIFO: spend of 300 < pack-1's 1000 → pack-2 has zero consumption
        pt2.GetInt32().Should().Be(DefaultPackSize,
            "pack-2 purchasedTotal must be DefaultPackSize={0}; actual={1}; body={2}",
            DefaultPackSize, pt2.GetInt32(), body2);
        cp2.GetInt32().Should().Be(0,
            "pack-2 consumedPurchased must be 0 (spend was within pack-1 FIFO attribution); " +
            "actual={0}; body={1}", cp2.GetInt32(), body2);
        rf2.GetInt32().Should().Be(DefaultPackSize,
            "pack-2 refundable must equal full DefaultPackSize={0}; actual={1}; body={2}",
            DefaultPackSize, rf2.GetInt32(), body2);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-09: Admin path — admin JWT can quote + request refund for ANY family
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-09 Admin path: admin quotes and requests refund for any family; Refund ledger row is written")]
    public async Task TC_RF_09_AdminPath_QuoteAndRequest_AnyFamily()
    {
        // ── Setup: a parent + pack purchase ──────────────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("RF09");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF09C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        var walletBefore = await GetWalletAsync(parentId);
        int purchasedBefore = walletBefore!.PurchasedBalance;

        var adminToken = await GetAdminTokenAsync();

        // ── Admin GET Quote ───────────────────────────────────────────────────────────────────
        var adminQuoteUrl = string.Format(AdminQuoteRouteTemplate, paymentId);
        var (quoteResp, quoteBody, quoteRoot) = await SendAsync(
            HttpMethod.Get, adminQuoteUrl, bearer: adminToken);

        quoteResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin GET Quote must return 200; body={0}", quoteBody);

        TryProp(quoteRoot, "successed", out var succEl).Should().BeTrue("body={0}", quoteBody);
        succEl.GetBoolean().Should().BeTrue("successed must be true for admin quote; body={0}", quoteBody);

        TryProp(quoteRoot, "data", out var quoteData).Should().BeTrue("body={0}", quoteBody);
        TryProp(quoteData, "refundable", out var rfEl).Should().BeTrue("body={0}", quoteBody);
        rfEl.GetInt32().Should().Be(DefaultPackSize,
            "admin quote refundable must equal full PackSize (nothing spent); body={0}", quoteBody);

        // ── Admin POST Request ────────────────────────────────────────────────────────────────
        var (requestResp, requestBody, requestRoot) = await SendAsync(HttpMethod.Post, AdminRequestUrl,
            new { PurchasePaymentId = paymentId, Reason = (int)RefundReason.AdminInitiated }, adminToken);

        requestResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "admin POST Request must return 200; body={0}", requestBody);

        TryProp(requestRoot, "successed", out var reqSucc).Should().BeTrue("body={0}", requestBody);
        reqSucc.GetBoolean().Should().BeTrue("admin request successed must be true; body={0}", requestBody);

        // ── Fire refund.succeeded webhook to settle ────────────────────────────────────────────
        var (whResp, whBody, _) = await PostWebhookAsync(
            $"rf09-evt-{Guid.NewGuid():N}", "refund.succeeded", providerRef, 50m);
        whResp.StatusCode.Should().Be(HttpStatusCode.OK, "refund webhook; body={0}", whBody);

        // ── Verify Refund ledger row was written with bucket-B SourceBucket ───────────────────
        int refundRowCount = await CountRefundLedgerRowsAsync(walletBefore.Id, paymentId.ToString());
        refundRowCount.Should().Be(1,
            "DEFECT: exactly one Refund ledger row must be written after webhook settlement; found={0}",
            refundRowCount);

        // ── Verify PurchasedBalance decremented ───────────────────────────────────────────────
        var walletAfter = await GetWalletAsync(parentId);
        walletAfter!.PurchasedBalance.Should().BeLessThan(purchasedBefore,
            "DEFECT: PurchasedBalance must decrease after admin-initiated refund webhook; " +
            "before={0}, after={1}", purchasedBefore, walletAfter.PurchasedBalance);

        walletAfter.PurchasedBalance.Should().BeGreaterThanOrEqualTo(0,
            "MONEY DEFECT: PurchasedBalance must never go negative after admin refund");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-10: Admin IDOR guard — Parent JWT hitting admin route → 403
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-10 Admin IDOR: Parent JWT on admin Quote/Request routes → 403")]
    public async Task TC_RF_10_Admin_Route_ParentJwt_Returns403()
    {
        var (_, parentToken) = await RegisterParentAsync("RF10");

        // Parent hitting the admin-only quote route
        var (quoteResp, quoteBody, _) = await SendAsync(HttpMethod.Get,
            string.Format(AdminQuoteRouteTemplate, 999), bearer: parentToken);

        ((int)quoteResp.StatusCode).Should().BeOneOf([401, 403],
            "AUTHZ DEFECT: Parent JWT on admin Quote route must be rejected (401/403); " +
            "actual={0}; body={1}", (int)quoteResp.StatusCode, quoteBody);

        // Parent hitting the admin-only request route
        var (requestResp, requestBody, _) = await SendAsync(HttpMethod.Post, AdminRequestUrl,
            new { PurchasePaymentId = 999, Reason = (int)RefundReason.AdminInitiated }, parentToken);

        ((int)requestResp.StatusCode).Should().BeOneOf([401, 403],
            "AUTHZ DEFECT: Parent JWT on admin Request route must be rejected (401/403); " +
            "actual={0}; body={1}", (int)requestResp.StatusCode, requestBody);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-11: Refundable ≤ 0 guard — requesting refund when fully consumed → 422
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-11 ZeroRefundable: POST Request when fully consumed → 422 rejected")]
    public async Task TC_RF_11_Request_WhenRefundableIsZero_Rejected()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF11");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF11C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        // Exhaust the entire PurchasedBalance
        var wallet = await GetWalletAsync(parentId);
        await SeedPurchasedSpendAsync(wallet!.Id, DefaultPackSize,
            $"rf11-spend:{parentId}:{Guid.NewGuid():N}");

        // Verify quote shows 0 refundable
        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentId);
        var (qResp, qBody, qRoot) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentToken);
        qResp.StatusCode.Should().Be(HttpStatusCode.OK, "quote; body={0}", qBody);
        TryProp(qRoot, "data", out var qData);
        TryProp(qData, "refundable", out var rfEl);
        rfEl.GetInt32().Should().Be(0, "test setup: refundable must be 0 for this test; body={0}", qBody);

        // ── POST Request — must be rejected because refundable is 0 ─────────────────────────
        var (requestResp, requestBody, requestRoot) = await SendAsync(HttpMethod.Post, ParentRequestUrl,
            new { PurchasePaymentId = paymentId, Reason = (int)RefundReason.NoLongerNeeded }, parentToken);

        // Handler returns 422 (UnprocessableEntity) for RefundableAmountIsZero
        ((int)requestResp.StatusCode).Should().BeOneOf([422, 424],
            "POST Request when refundable=0 must be rejected with 422/424; " +
            "actual={0}; body={1}", (int)requestResp.StatusCode, requestBody);

        TryProp(requestRoot, "successed", out var succEl).Should().BeTrue("body={0}", requestBody);
        succEl.GetBoolean().Should().BeFalse(
            "DEFECT: successed must be false when refundable=0; body={0}", requestBody);

        // ── Wallet must be unchanged (no double-decrement, nothing changed) ──────────────────
        var walletAfter = await GetWalletAsync(parentId);
        walletAfter!.PurchasedBalance.Should().Be(0,
            "PurchasedBalance must remain 0 (no negative write); actual={0}", walletAfter.PurchasedBalance);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-12: Negative-balance clamp — re-reconcile at webhook settlement
    // If purchased was further spent between request and webhook, balance is clamped (never negative)
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-12 NegativeClamp: further spend after request → webhook clamps; balance never negative")]
    public async Task TC_RF_12_WebhookReReconciles_ClampsToNeverNegative()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF12");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF12C");

        // Pack: 1000 units
        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        var walletAfterPurchase = await GetWalletAsync(parentId);
        walletAfterPurchase!.PurchasedBalance.Should().Be(DefaultPackSize,
            "wallet must have full purchased balance after pack purchase");

        // Spend 400 units (before request)
        await SeedPurchasedSpendAsync(walletAfterPurchase.Id, 400,
            $"rf12-spend1:{parentId}:{Guid.NewGuid():N}");

        // POST Request with 600 units still refundable (per-quote at request time: 1000-400=600)
        var (reqResp, reqBody, _) = await SendAsync(HttpMethod.Post, ParentRequestUrl,
            new { PurchasePaymentId = paymentId, Reason = (int)RefundReason.NoLongerNeeded }, parentToken);
        reqResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "POST Request must accept the refund initiation; body={0}", reqBody);

        // Simulate additional spend of 500 more units AFTER the request but BEFORE webhook
        var walletMid = await GetWalletAsync(parentId);
        int balAfterRequest = walletMid!.PurchasedBalance;
        // We can only spend up to the remaining balance; if it's >= 500 spend 500, else spend all of it
        int secondSpend = Math.Min(500, balAfterRequest);
        if (secondSpend > 0)
        {
            await SeedPurchasedSpendAsync(walletMid.Id, secondSpend,
                $"rf12-spend2:{parentId}:{Guid.NewGuid():N}");
        }

        var walletBeforeWebhook = await GetWalletAsync(parentId);
        int purchasedBeforeWebhook = walletBeforeWebhook!.PurchasedBalance;

        // ── refund.succeeded webhook — must re-reconcile (not trust the request-time figure) ─
        var (whResp, whBody, _) = await PostWebhookAsync(
            $"rf12-evt-{Guid.NewGuid():N}", "refund.succeeded", providerRef, 50m);
        whResp.StatusCode.Should().Be(HttpStatusCode.OK,
            "refund.succeeded webhook must return 200 even after further spend; body={0}", whBody);

        // ── Balance MUST NOT go negative ──────────────────────────────────────────────────────
        var walletAfterWebhook = await GetWalletAsync(parentId);
        walletAfterWebhook!.PurchasedBalance.Should().BeGreaterThanOrEqualTo(0,
            "MONEY DEFECT: PurchasedBalance must NEVER go negative; actual={0}",
            walletAfterWebhook.PurchasedBalance);

        // ── Refund row amount must be the RE-RECONCILED figure (clamped to available) ─────────
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var refundTx = await db.CreditTransactions.AsNoTracking()
            .Where(t =>
                t.FamilyEnergyAccountId == walletAfterPurchase.Id &&
                t.Type == CreditTransactionType.Refund &&
                t.RelatedPaymentId == paymentId.ToString())
            .OrderByDescending(t => t.OccurredAtUtc)
            .FirstOrDefaultAsync();

        refundTx.Should().NotBeNull("Refund ledger row must be written after webhook");
        // The webhook re-reconciles from ledger: refundable at webhook time =
        //   purchasedTotalForPayment - consumedForPayment (FIFO), clamped to PurchasedBalance at settlement time.
        // Clamp means: refundTx.Amount <= purchasedBeforeWebhook
        refundTx!.Amount.Should().BeLessOrEqualTo(purchasedBeforeWebhook,
            "MONEY DEFECT: Refund amount must be clamped to available PurchasedBalance at webhook time " +
            "(re-reconcile, never trust request-time figure); " +
            "refundedAmount={0}, availableAtWebhook={1}", refundTx.Amount, purchasedBeforeWebhook);

        // Final balance must match: purchasedBeforeWebhook - refundTx.Amount >= 0
        walletAfterWebhook.PurchasedBalance.Should().Be(purchasedBeforeWebhook - refundTx.Amount,
            "PurchasedBalance after webhook must be exactly purchasedBeforeWebhook({0}) - refundedAmount({1}) = {2}; " +
            "actual={3}",
            purchasedBeforeWebhook, refundTx.Amount,
            purchasedBeforeWebhook - refundTx.Amount,
            walletAfterWebhook.PurchasedBalance);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-13: Envelope shape — GET Quote success has all 5 BaseResponse keys
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-13 Envelope: GET Quote success response has all 5 BaseResponse keys")]
    public async Task TC_RF_13_Envelope_Shape_QuoteSuccess()
    {
        var (parentId, parentToken) = await RegisterParentAsync("RF13");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RF13C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);

        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentId);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentToken);

        resp.StatusCode.Should().Be(HttpStatusCode.OK, "body={0}", body);

        TryProp(root, "statusCode", out _).Should().BeTrue(
            "envelope must have 'statusCode'; body={0}", body);
        TryProp(root, "successed", out _).Should().BeTrue(
            "envelope must have 'successed' (sic — C# spelling); body={0}", body);
        TryProp(root, "message", out _).Should().BeTrue(
            "envelope must have 'message'; body={0}", body);
        TryProp(root, "data", out _).Should().BeTrue(
            "envelope must have 'data'; body={0}", body);
        TryProp(root, "errors", out _).Should().BeTrue(
            "envelope must have 'errors'; body={0}", body);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-S1 (Finding #1 CRITICAL): Distinct-event-id double-refund → no second decrement
    //
    // Scenario:
    //   1. Buy a pack → payment.succeeded webhook → PurchasedBalance = 1000.
    //   2. Settle refund with event id A → Refund ledger row written, balance decremented.
    //   3. POST second refund.succeeded with event id B (distinct) for THE SAME payment.
    //   4. Assert: balance NOT decremented again; exactly ONE Refund ledger row exists;
    //              payment.Status == Refunded.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-S1 DistinctEventId: second refund.succeeded with distinct event id → no second decrement")]
    public async Task TC_RF_S1_DistinctEventId_DoubleRefund_NoSecondDecrement()
    {
        // ── Setup ─────────────────────────────────────────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("RFS1");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RFS1C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        var walletBefore = await GetWalletAsync(parentId);
        walletBefore.Should().NotBeNull("wallet must exist after pack purchase");
        int purchasedBefore = walletBefore!.PurchasedBalance;

        // ── First refund.succeeded — event id A ───────────────────────────────────────────────
        var eventIdA = $"rfs1-evtA-{Guid.NewGuid():N}";
        var (resp1, body1, _) = await PostWebhookAsync(eventIdA, "refund.succeeded", providerRef, 50m);
        resp1.StatusCode.Should().Be(HttpStatusCode.OK, "first refund webhook (A); body={0}", body1);

        int balanceAfterFirst = (await GetWalletAsync(parentId))!.PurchasedBalance;
        int refundRowsAfterFirst = await CountRefundLedgerRowsAsync(walletBefore.Id, paymentId.ToString());

        // Verify first settlement produced exactly one Refund row.
        refundRowsAfterFirst.Should().Be(1,
            "DEFECT: exactly one Refund ledger row after first settlement; found={0}", refundRowsAfterFirst);
        balanceAfterFirst.Should().BeLessThan(purchasedBefore,
            "DEFECT: balance must decrease after first refund webhook");

        // ── Second refund.succeeded — event id B (DISTINCT from A) ───────────────────────────
        var eventIdB = $"rfs1-evtB-{Guid.NewGuid():N}";
        var (resp2, body2, _) = await PostWebhookAsync(eventIdB, "refund.succeeded", providerRef, 50m);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK,
            "second refund webhook (B, distinct event id) must return 200 no-op; body={0}", body2);

        int balanceAfterSecond = (await GetWalletAsync(parentId))!.PurchasedBalance;
        int refundRowsAfterSecond = await CountRefundLedgerRowsAsync(walletBefore.Id, paymentId.ToString());

        // ── Critical assertions: no double-refund ─────────────────────────────────────────────
        balanceAfterSecond.Should().Be(balanceAfterFirst,
            "CRITICAL MONEY DEFECT: balance MUST NOT be decremented a second time by a " +
            "distinct-event-id refund webhook for the same payment; " +
            "after first={0}, after second={1}", balanceAfterFirst, balanceAfterSecond);

        refundRowsAfterSecond.Should().Be(1,
            "CRITICAL MONEY DEFECT: exactly ONE Refund ledger row must exist even after " +
            "a distinct-event-id second webhook; found={0}", refundRowsAfterSecond);

        // ── Verify payment.Status == Refunded ─────────────────────────────────────────────────
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var payment = await db.Payments.AsNoTracking().FirstAsync(p => p.Id == paymentId);
        payment.Status.Should().Be(PaymentStatus.Refunded,
            "payment.Status must be Refunded after first settlement so the second webhook is rejected");
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-S2 (Finding #1 CRITICAL): Repurchase-in-between variant
    //
    // Scenario:
    //   1. Buy pack-1 → payment.succeeded → PurchasedBalance = 1000.
    //   2. Settle refund for pack-1 (event A) → balance decremented.
    //   3. Buy pack-2 → PurchasedBalance replenished to 1000.
    //   4. Post second refund.succeeded for pack-1 with distinct event id B.
    //   5. Assert: pack-1 refundable = 0 (already refunded); balance NOT decremented again.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-S2 Repurchase: new pack between refund-1 and distinct-event-id refund-2 → still no double-refund")]
    public async Task TC_RF_S2_Repurchase_Then_DistinctEventId_NoDoubleRefund()
    {
        // ── Setup: pack-1 ─────────────────────────────────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("RFS2");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RFS2C");

        var (paymentId1, providerRef1) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef1);
        await MarkPaymentSucceededAsync(paymentId1);

        int balanceAfterPack1 = (await GetWalletAsync(parentId))!.PurchasedBalance;
        balanceAfterPack1.Should().Be(DefaultPackSize, "pack-1 fully credited");

        // ── First refund.succeeded for pack-1 (event A) ───────────────────────────────────────
        var eventIdA = $"rfs2-evtA-{Guid.NewGuid():N}";
        var (r1Resp, r1Body, _) = await PostWebhookAsync(eventIdA, "refund.succeeded", providerRef1, 50m);
        r1Resp.StatusCode.Should().Be(HttpStatusCode.OK, "first refund webhook; body={0}", r1Body);

        int balanceAfterRefund1 = (await GetWalletAsync(parentId))!.PurchasedBalance;

        // ── Buy pack-2 — replenishes PurchasedBalance ─────────────────────────────────────────
        var (paymentId2, providerRef2) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef2);

        int balanceAfterPack2 = (await GetWalletAsync(parentId))!.PurchasedBalance;
        balanceAfterPack2.Should().BeGreaterThan(balanceAfterRefund1,
            "PurchasedBalance must increase after pack-2 purchase");

        // ── Quote for pack-1 (after refund settled + pack-2 bought) ──────────────────────────
        var walletAfterPack2 = await GetWalletAsync(parentId);
        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentId1);
        var (qResp, qBody, qRoot) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentToken);
        qResp.StatusCode.Should().Be(HttpStatusCode.OK, "quote after repurchase; body={0}", qBody);

        TryProp(qRoot, "data", out var qData).Should().BeTrue("body={0}", qBody);
        TryProp(qData, "refundable", out var rfEl).Should().BeTrue("body={0}", qBody);
        rfEl.GetInt32().Should().Be(0,
            "CRITICAL MONEY DEFECT: pack-1 refundable must be 0 after it was already refunded, " +
            "even though PurchasedBalance was replenished by pack-2; actual={0}; body={1}",
            rfEl.GetInt32(), qBody);

        // ── Second refund.succeeded for pack-1 — distinct event id B ─────────────────────────
        var eventIdB = $"rfs2-evtB-{Guid.NewGuid():N}";
        var (r2Resp, r2Body, _) = await PostWebhookAsync(eventIdB, "refund.succeeded", providerRef1, 50m);
        r2Resp.StatusCode.Should().Be(HttpStatusCode.OK,
            "second refund webhook (B, distinct event id) must return 200 no-op; body={0}", r2Body);

        int balanceAfterSecond = (await GetWalletAsync(parentId))!.PurchasedBalance;

        // ── No second decrement — balance must match post-pack2 balance ───────────────────────
        balanceAfterSecond.Should().Be(balanceAfterPack2,
            "CRITICAL MONEY DEFECT: PurchasedBalance MUST NOT be decremented by the second " +
            "distinct-event-id webhook even after PurchasedBalance was replenished; " +
            "afterPack2={0}, afterSecondWebhook={1}", balanceAfterPack2, balanceAfterSecond);

        // ── Exactly ONE Refund ledger row for pack-1 ─────────────────────────────────────────
        int refundRows = await CountRefundLedgerRowsAsync(walletAfterPack2!.Id, paymentId1.ToString());
        refundRows.Should().Be(1,
            "CRITICAL MONEY DEFECT: exactly ONE Refund ledger row for pack-1 even after repurchase + second webhook; " +
            "found={0}", refundRows);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-S3 (Finding #1 CRITICAL): Already-refunded payment → refundable = 0
    //
    // Verifies that requesting a quote or a refund for a payment that has been fully settled
    // returns refundable = 0 (not a positive amount), so a re-request is rejected at the
    // application layer before reaching the provider.
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-S3 AlreadyRefunded: quote after full refund settlement → refundable=0")]
    public async Task TC_RF_S3_AlreadyRefunded_Quote_Returns_Zero()
    {
        // ── Setup ─────────────────────────────────────────────────────────────────────────────
        var (parentId, parentToken) = await RegisterParentAsync("RFS3");
        var (childId, _) = await AddChildAndSignInAsync(parentToken, "RFS3C");

        var (paymentId, providerRef) = await SeedPackPaymentAsync(parentId, childId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRef);
        await MarkPaymentSucceededAsync(paymentId);

        // ── Settle refund ─────────────────────────────────────────────────────────────────────
        var eventId = $"rfs3-evt-{Guid.NewGuid():N}";
        var (wResp, wBody, _) = await PostWebhookAsync(eventId, "refund.succeeded", providerRef, 50m);
        wResp.StatusCode.Should().Be(HttpStatusCode.OK, "refund webhook; body={0}", wBody);

        // ── Get quote for the now-refunded payment ────────────────────────────────────────────
        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentId);
        var (qResp, qBody, qRoot) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentToken);
        qResp.StatusCode.Should().Be(HttpStatusCode.OK, "quote after refund; body={0}", qBody);

        TryProp(qRoot, "data", out var qData).Should().BeTrue("body={0}", qBody);
        TryProp(qData, "refundable", out var rfEl).Should().BeTrue("body={0}", qBody);
        rfEl.GetInt32().Should().Be(0,
            "CRITICAL MONEY DEFECT: refundable must be 0 after payment is already refunded; " +
            "actual={0}; body={1}", rfEl.GetInt32(), qBody);

        // ── Attempting to request a second refund must be rejected ────────────────────────────
        var (reqResp, reqBody, reqRoot) = await SendAsync(HttpMethod.Post, ParentRequestUrl,
            new { PurchasePaymentId = paymentId, Reason = (int)RefundReason.NoLongerNeeded }, parentToken);

        ((int)reqResp.StatusCode).Should().BeOneOf([422, 424],
            "POST Request for an already-refunded payment must be rejected with 422/424; " +
            "actual={0}; body={1}", (int)reqResp.StatusCode, reqBody);

        TryProp(reqRoot, "successed", out var succEl).Should().BeTrue("body={0}", reqBody);
        succEl.GetBoolean().Should().BeFalse(
            "successed must be false for an already-refunded payment request; body={0}", reqBody);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // TC-RF-S4 (Finding #3 Low): IDOR explicit ownership — cross-family quote rejected
    //
    // Verifies that ComputeRefundableAsync returns null (→ 404) when the payment's
    // ParentUserId does not match the requesting parent's family account, even if the
    // refundable amount would be zero (so the IDOR cannot be hidden behind a zero-quote).
    // ══════════════════════════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-RF-S4 IDOROwnership: payment owned by parent A — parent B's wallet resolves null (explicit ownership check)")]
    public async Task TC_RF_S4_IDOR_ExplicitOwnershipCheck_CrossFamilyQuoteRejected()
    {
        // ── Parent A: buy a pack ──────────────────────────────────────────────────────────────
        var (parentAId, parentAToken) = await RegisterParentAsync("RFS4A");
        var (childAId, _) = await AddChildAndSignInAsync(parentAToken, "RFS4AC");
        var (paymentIdA, providerRefA) = await SeedPackPaymentAsync(parentAId, childAId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRefA);

        // Fully consume parent A's pack (so refundable = 0 via normal FIFO — the OLD implicit IDOR
        // safety was emergent: zero refundable hid the violation. The new check is explicit.)
        var walletA = await GetWalletAsync(parentAId);
        walletA.Should().NotBeNull();
        await SeedPurchasedSpendAsync(walletA!.Id, DefaultPackSize,
            $"rfs4-spend:{parentAId}:{Guid.NewGuid():N}");

        // ── Parent B: buy own pack so they have a wallet ──────────────────────────────────────
        var (parentBId, parentBToken) = await RegisterParentAsync("RFS4B");
        var (childBId, _) = await AddChildAndSignInAsync(parentBToken, "RFS4BC");
        var (_, providerRefB) = await SeedPackPaymentAsync(parentBId, childBId, amount: 50m);
        await SendPackPurchaseWebhookAsync(providerRefB);
        // Parent B has a wallet now; but payment A belongs to parent A

        // ── Parent B tries to get a quote for parent A's payment ─────────────────────────────
        var quoteUrl = string.Format(ParentQuoteRouteTemplate, paymentIdA);
        var (resp, body, root) = await SendAsync(HttpMethod.Get, quoteUrl, bearer: parentBToken);

        // The handler resolves parent B's familyAccountId from JWT, then calls ComputeRefundableAsync.
        // The new ownership assertion returns null → NotFound response (not a successful zero-quote).
        resp.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "IDOR DEFECT (explicit ownership): parent B must NOT receive a 200 response " +
            "for parent A's payment, even when refundable=0; actual={0}; body={1}",
            resp.StatusCode, body);

        TryProp(root, "successed", out var succEl).Should().BeTrue("body={0}", body);
        succEl.GetBoolean().Should().BeFalse(
            "IDOR DEFECT: successed must be false when parent B queries parent A's payment " +
            "(explicit ownership check must reject before returning any quote); body={0}", body);
    }
}
